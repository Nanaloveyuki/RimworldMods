'use strict';

const vscode = require('vscode');
const { spawn } = require('node:child_process');
const path = require('node:path');
const readline = require('node:readline');
const { registerBlockEditor } = require('./blocks/editor');

class Analyzer {
  constructor(serverPath, output) {
    this.serverPath = serverPath;
    this.output = output;
    this.child = undefined;
    this.nextId = 1;
    this.pending = new Map();
  }

  start(dotnetPath) {
    if (this.child) return;
    const child = spawn(dotnetPath, [this.serverPath], {
      windowsHide: true,
      stdio: ['pipe', 'pipe', 'pipe'],
      env: { ...process.env, DOTNET_ROLL_FORWARD: 'Major' }
    });
    this.child = child;
    const lines = readline.createInterface({ input: child.stdout });
    lines.on('line', line => {
      if (this.child !== child) return;
      let response;
      try { response = JSON.parse(line); }
      catch { this.fail(child, new Error('Analyzer emitted invalid JSON.')); return; }
      const pending = this.pending.get(response.id);
      if (!pending) return;
      this.pending.delete(response.id);
      clearTimeout(pending.timer);
      if (response.error) pending.reject(new Error(response.error));
      else pending.resolve(response);
    });
    child.stderr.on('data', data => this.output.append(data.toString()));
    child.stdin.on('error', error => this.fail(child, error));
    child.on('error', error => this.fail(child, error));
    child.on('exit', (code, signal) => {
      lines.close();
      this.fail(child, new Error(`Analyzer exited (${signal || code}). Check .NET 9+ on the extension host.`));
    });
  }

  request(source, options) {
    this.start(options.dotnetPath);
    const child = this.child;
    const id = this.nextId++;
    return new Promise((resolve, reject) => {
      const timer = setTimeout(() => this.fail(child, new Error('Analyzer did not respond within 15 seconds.')), 15000);
      this.pending.set(id, { resolve, reject, timer });
      child.stdin.write(JSON.stringify({ id, source, isModule: options.isModule, autoMode: options.autoMode, language: options.language }) + '\n');
    });
  }

  fail(child, error) {
    if (child !== this.child) return;
    this.child = undefined;
    child.kill();
    for (const pending of this.pending.values()) {
      clearTimeout(pending.timer);
      pending.reject(error);
    }
    this.pending.clear();
  }

  dispose() {
    if (this.child) this.fail(this.child, new Error('Analyzer stopped.'));
  }
}

function optionsFor(document) {
  const config = vscode.workspace.getConfiguration('oxygen', document.uri);
  const mode = config.get('analysis.mode', 'auto');
  const isModule = mode === 'module';
  const language = config.get('diagnostics.language', 'auto');
  return {
    enabled: config.get('diagnostics.enabled', true),
    dotnetPath: config.get('dotnetPath', 'dotnet'),
    isModule,
    autoMode: mode === 'auto',
    language: language === 'auto' ? (vscode.env.language.startsWith('zh') ? 'zh-CN' : 'en') : language
  };
}

function activate(context) {
  const output = vscode.window.createOutputChannel('Oxygen');
  const diagnostics = vscode.languages.createDiagnosticCollection('oxygen');
  const analyzer = new Analyzer(context.asAbsolutePath('server/Oxygendsl.Editor.dll'), output);
  registerBlockEditor(context, analyzer, output, optionsFor);
  const cache = new Map();
  const inflight = new Map();
  const scheduled = new Map();
  let disposed = false;
  let failureNotified = false;
  const isOxygen = document => document.languageId === 'oxygen';
  const keyFor = document => document.uri.toString();

  function range(document, start, length) {
    return new vscode.Range(document.positionAt(start), document.positionAt(start + length));
  }

  function applyDiagnostics(document, result, force) {
    if (!force && !optionsFor(document).enabled) {
      diagnostics.delete(document.uri);
      return;
    }
    const levels = { error: vscode.DiagnosticSeverity.Error, warning: vscode.DiagnosticSeverity.Warning, info: vscode.DiagnosticSeverity.Information };
    diagnostics.set(document.uri, result.diagnostics.map(item => {
      const diagnostic = new vscode.Diagnostic(range(document, item.start, item.length), item.message, levels[item.severity] ?? levels.error);
      diagnostic.code = item.code;
      diagnostic.source = 'Oxygen';
      return diagnostic;
    }));
  }

  async function analyze(document, force = false) {
    if (!vscode.workspace.isTrusted) return undefined;
    const key = keyFor(document);
    clearTimeout(scheduled.get(key));
    scheduled.delete(key);
    const options = optionsFor(document);
    const signature = JSON.stringify(options);
    const version = document.version;
    const existing = cache.get(key);
    if (existing && existing.version === version && existing.signature === signature) {
      applyDiagnostics(document, existing.result, force);
      return existing.result;
    }
    const active = inflight.get(key);
    if (active && active.version === version && active.signature === signature) {
      const result = await active.promise;
      if (result && !disposed && !document.isClosed && document.version === version && JSON.stringify(optionsFor(document)) === signature) applyDiagnostics(document, result, force);
      return result;
    }
    const promise = analyzer.request(document.getText(), options).then(result => {
      if (disposed || document.isClosed || document.version !== version || JSON.stringify(optionsFor(document)) !== signature) return undefined;
      cache.set(key, { version, signature, result });
      applyDiagnostics(document, result, force);
      failureNotified = false;
      return result;
    }).catch(error => {
      if (disposed || document.isClosed || document.version !== version || JSON.stringify(optionsFor(document)) !== signature) return undefined;
      diagnostics.delete(document.uri);
      output.appendLine(`${document.uri.toString()}: ${error.message}`);
      if (!failureNotified) {
        failureNotified = true;
        vscode.window.showWarningMessage('Oxygen analysis unavailable. Install .NET 9+ or configure oxygen.dotnetPath. See Oxygen output.', 'Show Output')
          .then(choice => { if (choice === 'Show Output' && !disposed) output.show(); });
      }
      return undefined;
    }).finally(() => {
      if (inflight.get(key)?.promise === promise) inflight.delete(key);
    });
    inflight.set(key, { version, signature, promise });
    return promise;
  }

  function schedule(document, immediate = false) {
    if (!isOxygen(document)) return;
    const key = keyFor(document);
    cache.delete(key);
    clearTimeout(scheduled.get(key));
    if (!vscode.workspace.isTrusted || !optionsFor(document).enabled) {
      diagnostics.delete(document.uri);
      scheduled.delete(key);
      return;
    }
    scheduled.set(key, setTimeout(() => {
      scheduled.delete(key);
      if (!disposed && !document.isClosed) void analyze(document);
    }, immediate ? 0 : 300));
  }

  const kinds = {
    function: vscode.SymbolKind.Function, method: vscode.SymbolKind.Method,
    struct: vscode.SymbolKind.Struct, trait: vscode.SymbolKind.Interface,
    namespace: vscode.SymbolKind.Namespace, variable: vscode.SymbolKind.Variable,
    constant: vscode.SymbolKind.Constant, field: vscode.SymbolKind.Field
  };
  function symbol(document, item) {
    const result = new vscode.DocumentSymbol(item.name || 'block', item.detail || '', kinds[item.kind] ?? kinds.namespace,
      range(document, item.start, item.length), range(document, item.selectionStart, item.selectionLength));
    result.children = (item.children || []).map(child => symbol(document, child));
    return result;
  }

  context.subscriptions.push(output, diagnostics,
    vscode.languages.registerDocumentSymbolProvider({ language: 'oxygen' }, {
      async provideDocumentSymbols(document, token) {
        const version = document.version;
        const result = await analyze(document);
        if (!result || token.isCancellationRequested || document.version !== version) return [];
        return result.symbols.map(item => symbol(document, item));
      }
    }),
    vscode.workspace.onDidOpenTextDocument(document => schedule(document, true)),
    vscode.workspace.onDidChangeTextDocument(event => { if (event.contentChanges.length) schedule(event.document); }),
    vscode.workspace.onDidSaveTextDocument(document => schedule(document, true)),
    vscode.workspace.onDidCloseTextDocument(document => {
      const key = keyFor(document);
      clearTimeout(scheduled.get(key));
      scheduled.delete(key);
      cache.delete(key);
      diagnostics.delete(document.uri);
    }),
    vscode.workspace.onDidGrantWorkspaceTrust(() => {
      for (const document of vscode.workspace.textDocuments) schedule(document, true);
    }),
    vscode.workspace.onDidChangeConfiguration(event => {
      if (!event.affectsConfiguration('oxygen')) return;
      if (event.affectsConfiguration('oxygen.dotnetPath')) analyzer.dispose();
      cache.clear();
      failureNotified = false;
      for (const document of vscode.workspace.textDocuments) schedule(document, true);
    }),
    vscode.commands.registerCommand('oxygen.checkDocument', async () => {
      const document = vscode.window.activeTextEditor?.document;
      if (!document || !isOxygen(document)) {
        vscode.window.showInformationMessage('Open an Oxygen (.oxy) document first.');
        return;
      }
      if (!vscode.workspace.isTrusted) {
        vscode.window.showInformationMessage('Oxygen analysis requires a trusted workspace. Highlighting and snippets remain available.');
        return;
      }
      const result = await analyze(document, true);
      if (!result) { output.show(); return; }
      const errors = result.diagnostics.filter(item => item.severity === 'error').length;
      vscode.window.showInformationMessage(`Oxygen: ${errors} errors, ${result.diagnostics.length - errors} other diagnostics. Parser/analyzer only; modules and game Defs are not resolved.`);
    }),
    vscode.commands.registerCommand('oxygen.restartAnalyzer', () => {
      analyzer.dispose();
      cache.clear();
      inflight.clear();
      failureNotified = false;
      for (const document of vscode.workspace.textDocuments) schedule(document, true);
    }),
    vscode.commands.registerCommand('oxygen.showOutput', () => output.show()),
    { dispose() {
      disposed = true;
      for (const timer of scheduled.values()) clearTimeout(timer);
      scheduled.clear();
      cache.clear();
      analyzer.dispose();
    } }
  );
  for (const document of vscode.workspace.textDocuments) schedule(document, true);
}

module.exports = { activate };
