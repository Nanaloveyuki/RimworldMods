'use strict';

const vscode = require('vscode');
const { randomBytes } = require('node:crypto');
const { createDocument, generate, templates } = require('./library');
const { parseDocument, relativeTarget, digest, exportMatches, locateBlock } = require('./document');

const viewType = 'oxygen.blocks';
const encode = text => text.replace(/[&<>"']/g, char => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[char]));
const sameUri = (a, b) => a.toString() === b.toString();

function editorHtml(webview, extensionUri) {
  const media = vscode.Uri.joinPath(extensionUri, 'media');
  const script = webview.asWebviewUri(vscode.Uri.joinPath(media, 'blocks.js'));
  const style = webview.asWebviewUri(vscode.Uri.joinPath(media, 'blocks.css'));
  const blockMedia = webview.asWebviewUri(vscode.Uri.joinPath(media, 'blockly')).toString() + '/';
  const nonce = randomBytes(18).toString('base64');
  return `<!doctype html><html lang="zh-CN"><head><meta charset="UTF-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<meta http-equiv="Content-Security-Policy" content="default-src 'none'; script-src 'nonce-${nonce}'; style-src ${webview.cspSource} 'unsafe-inline'; img-src ${webview.cspSource} data:; font-src ${webview.cspSource};">
<link rel="stylesheet" href="${style}"><title>Oxygen 积木编辑器</title></head>
<body data-media="${encode(blockMedia)}"><header class="editor-header"><div><span class="brand">OXYGEN</span><h1>积木编辑器</h1></div><p>拼接规则 · 预览源码 · 导出 Mod</p></header>
<section class="toolbar" aria-label="文件与生成"><label>输出路径<input id="target-path" spellcheck="false" aria-describedby="path-help"></label><label>检查模式<select id="target-mode"><option value="script">入口脚本</option><option value="module">库模块</option></select></label><button id="check">检查积木</button><button id="export" class="primary">导出 .oxy</button><button id="export-copy">另存源码副本</button></section>
<div id="path-help" class="path-help">路径相对于当前 .oxyblocks 文件。保存积木不会自动覆盖源码；检查只分析语法与当前文件语义。</div>
<main class="editor-layout"><section class="canvas-pane" aria-label="积木画布"><div class="canvas-toolbar"><label>模板<select id="template"></select></label><button id="load-template">使用模板</button><span class="toolbar-spacer"></span><button id="undo">撤销</button><button id="redo">重做</button><button id="zoom-fit">适配画布</button></div>
<div class="add-toolbar"><label>搜索积木<input id="block-search" type="search" placeholder="条件、心情、设置…"></label><label>添加<select id="add-type"></select></label><button id="add-block">添加积木</button></div><div id="blockly"></div></section>
<aside class="inspector-pane"><section class="inspector-section"><label>定位画布积木<select id="select-block"><option value="">选择积木</option></select></label><h2 id="selected-title">选择积木</h2><div id="selected-fields"></div><div id="connections"></div><div class="block-actions"><button id="move-up">上移</button><button id="move-down">下移</button><button id="delete-block">删除积木</button></div></section>
<section class="preview-section"><h2>生成的 Oxygen <span class="read-only">只读预览</span></h2><pre id="preview" tabindex="0" aria-label="生成的 Oxygen 源码"></pre></section><section class="errors-section"><h2>检查结果</h2><ul id="errors"></ul></section></aside></main><footer id="status" role="status" aria-live="polite">正在加载积木编辑器…</footer>
<script nonce="${nonce}" src="${script}"></script></body></html>`;
}

function registerBlockEditor(context, analyzer, output, optionsFor) {
  const diagnostics = vscode.languages.createDiagnosticCollection('oxygen-blocks');
  context.subscriptions.push(diagnostics);

  async function replace(document, root) {
    const edit = new vscode.WorkspaceEdit();
    edit.replace(document.uri, new vscode.Range(document.positionAt(0), document.positionAt(document.getText().length)), JSON.stringify(root, null, 2) + '\n');
    if (!await vscode.workspace.applyEdit(edit)) throw new Error('无法更新积木文件；请确认文件可写。');
  }

  function dirtyTarget(uri) {
    return vscode.workspace.textDocuments.some(doc => sameUri(doc.uri, uri) && doc.isDirty);
  }

  async function existingBytes(uri) {
    try { return await vscode.workspace.fs.readFile(uri); }
    catch (error) { if (error.code === 'FileNotFound') return undefined; throw error; }
  }

  const provider = {
    async resolveCustomTextEditor(document, panel) {
      const webview = panel.webview;
      webview.options = { enableScripts: true, localResourceRoots: [vscode.Uri.joinPath(context.extensionUri, 'media')] };
      webview.html = editorHtml(webview, context.extensionUri);
      let disposed = false;
      let exporting = false;
      let queue = Promise.resolve();
      const send = message => { if (!disposed) void webview.postMessage(message); };
      const status = (message, error = false) => send({ type: 'status', message, error });
      const sendDocument = (conflict = false) => send({ type: 'document', text: document.getText(), version: document.version, trusted: vscode.workspace.isTrusted, conflict });
      const current = version => !disposed && !document.isClosed && document.version === version;
      const requireTrust = () => {
        if (!vscode.workspace.isTrusted) throw new Error('检查与导出需要受信任的工作区；积木编辑和保存仍可使用。');
      };

      async function check(version) {
        requireTrust();
        if (!current(version)) { sendDocument(true); return; }
        const root = parseDocument(document.getText());
        const generated = generate(root.workspace);
        if (generated.errors.length) {
          send({ type: 'analysis', version, diagnostics: generated.errors.map(item => ({ ...item, severity: 'error', code: 'BLOCK', start: 0, length: 0 })) });
          return;
        }
        const options = { ...optionsFor(document), isModule: root.target.mode === 'module', autoMode: false };
        const result = await analyzer.request(generated.source, options);
        if (!current(version)) return;
        const items = result.diagnostics.map(item => ({ ...item, blockId: locateBlock(generated.ranges, item.start) }));
        send({ type: 'analysis', version, diagnostics: items });
        diagnostics.set(document.uri, items.map(item => {
          const diagnostic = new vscode.Diagnostic(new vscode.Range(0, 0, 0, 0), `${item.blockId ? `积木 ${item.blockId}：` : ''}${item.message}`, item.severity === 'error' ? vscode.DiagnosticSeverity.Error : vscode.DiagnosticSeverity.Warning);
          diagnostic.code = item.code;
          diagnostic.source = 'Oxygen Blocks';
          return diagnostic;
        }));
        return { root, generated, diagnostics: items };
      }

      async function chooseCopy() {
        return vscode.window.showSaveDialog({ defaultUri: vscode.Uri.joinPath(document.uri, '..', 'rules.oxy'), filters: { 'Oxygen 源码': ['oxy'] }, title: '另存 Oxygen 源码' });
      }

      async function exportSource(version, copy) {
        if (exporting) return;
        exporting = true;
        try {
          const checked = await check(version);
          if (!checked || !current(version)) return;
          if (checked.diagnostics.some(item => item.severity === 'error')) throw new Error('源码仍有检查错误；请先修复对应积木。');
          const { root, generated } = checked;
          let uri = copy ? await chooseCopy() : vscode.Uri.joinPath(document.uri, '..', relativeTarget(root.target.path));
          if (!uri) { status('已取消导出。'); return; }
          if (!current(version)) { sendDocument(true); return; }
          if (!/\.oxy$/i.test(uri.path) || sameUri(uri, document.uri)) throw new Error('请选择 .oxy 源码文件，不能覆盖积木文件。');
          let before = await existingBytes(uri);
          if (dirtyTarget(uri)) throw new Error('输出文件在 VSCode 中有未保存的修改；请先保存，或选择另存副本。');
          if (before !== undefined && (copy || !exportMatches(root.lastExport, uri.toString(), before))) {
            const choice = await vscode.window.showWarningMessage('目标源码已存在或在上次导出后被修改。覆盖会丢失手工修改。', { modal: true }, '覆盖此文件', '选择其他文件');
            if (!current(version)) { sendDocument(true); return; }
            if (choice === '选择其他文件') {
              uri = await chooseCopy();
              if (!uri) { status('已取消导出。'); return; }
              if (!/\.oxy$/i.test(uri.path) || sameUri(uri, document.uri)) throw new Error('请选择 .oxy 文件。');
              before = await existingBytes(uri);
              if (before !== undefined) throw new Error('另选的文件已存在；请使用另存源码副本并明确确认覆盖。');
            } else if (choice !== '覆盖此文件') { status('已取消导出，目标文件未改变。'); return; }
          }
          requireTrust();
          if (!current(version)) { sendDocument(true); return; }
          if (dirtyTarget(uri)) throw new Error('输出文件现在有未保存修改，已取消导出。');
          const latest = await existingBytes(uri);
          if ((before === undefined) !== (latest === undefined) || (before !== undefined && digest(before) !== digest(latest))) throw new Error('确认期间目标文件发生变化，已取消导出；请重新导出。');
          if (!current(version)) { sendDocument(true); return; }
          const bytes = Buffer.from(generated.source, 'utf8');
          await vscode.workspace.fs.createDirectory(vscode.Uri.joinPath(uri, '..'));
          if (!current(version)) { sendDocument(true); return; }
          await vscode.workspace.fs.writeFile(uri, bytes);
          if (!disposed && !document.isClosed) {
            const latestRoot = parseDocument(document.getText());
            latestRoot.lastExport = { path: uri.toString(), sha256: digest(bytes) };
            await replace(document, latestRoot);
          }
          status(`已导出 ${uri.path}；积木文件中的导出记录也已更新，请保存积木。`);
        } finally { exporting = false; }
      }

      async function handle(message) {
        if (!message || disposed) return;
        if (message.type === 'ready') { sendDocument(); return; }
        if (message.type === 'edit') {
          if (!current(message.baseVersion)) { sendDocument(true); return; }
          const oldRoot = parseDocument(document.getText());
          const root = parseDocument(JSON.stringify(message.document));
          if (oldRoot.lastExport) root.lastExport = oldRoot.lastExport;
          else delete root.lastExport;
          await replace(document, root);
          sendDocument();
          return;
        }
        if (!['check', 'export'].includes(message.type)) return;
        try {
          if (message.type === 'check') await check(message.version);
          else await exportSource(message.version, message.copy === true);
        } catch (error) {
          output.appendLine(`${document.uri}: ${error.message}`);
          send({ type: 'analysis', version: document.version, diagnostics: [], error: error.message });
          status(error.message, true);
        }
      }

      const subscriptions = [
        webview.onDidReceiveMessage(message => {
          queue = queue.then(() => handle(message)).catch(error => { status(error.message, true); sendDocument(true); });
        }),
        vscode.workspace.onDidChangeTextDocument(event => {
          if (sameUri(event.document.uri, document.uri)) { diagnostics.delete(document.uri); sendDocument(); }
        }),
        vscode.workspace.onDidGrantWorkspaceTrust(() => sendDocument()),
        panel.onDidDispose(() => {
          disposed = true;
          diagnostics.delete(document.uri);
          for (const subscription of subscriptions) subscription.dispose();
        })
      ];
    }
  };

  context.subscriptions.push(
    vscode.window.registerCustomEditorProvider(viewType, provider, { webviewOptions: { retainContextWhenHidden: true }, supportsMultipleEditorsPerDocument: false }),
    vscode.commands.registerCommand('oxygen.newBlocks', async () => {
      try {
      const template = await vscode.window.showQuickPick(templates.map(item => ({ label: item.label, description: item.description, id: item.id })), { title: '选择 Oxygen 积木模板' });
      if (!template) return;
      const uri = await vscode.window.showSaveDialog({ filters: { 'Oxygen 积木': ['oxyblocks'] }, title: '创建 Oxygen 积木文件' });
      if (!uri) return;
      if (!/\.oxyblocks$/i.test(uri.path)) throw new Error('请选择以 .oxyblocks 结尾的积木文件。');
      if (await existingBytes(uri) !== undefined) {
        const choice = await vscode.window.showWarningMessage('积木文件已存在，覆盖会丢失已有内容。', { modal: true }, '覆盖积木文件');
        if (choice !== '覆盖积木文件') return;
        if (dirtyTarget(uri)) throw new Error('积木文件有未保存修改，不能覆盖。');
      }
      await vscode.workspace.fs.writeFile(uri, Buffer.from(JSON.stringify(createDocument(template.id), null, 2) + '\n'));
      await vscode.commands.executeCommand('vscode.openWith', uri, viewType);
      } catch (error) {
        output.appendLine(error.message);
        await vscode.window.showErrorMessage(`Oxygen：${error.message}`);
      }
    })
  );
}

module.exports = { registerBlockEditor, editorHtml };
