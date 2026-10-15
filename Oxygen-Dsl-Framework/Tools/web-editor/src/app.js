import { EditorView, basicSetup } from 'codemirror';
import { EditorState } from '@codemirror/state';
import { StreamLanguage } from '@codemirror/language';
import { oneDark } from '@codemirror/theme-one-dark';

const $ = id => document.getElementById(id);
const blocks = globalThis.OxygenBlocks;
const fragment = new URLSearchParams(location.hash.slice(1));
const token = fragment.get('token');
const requestedPackage = fragment.get('package');
const requestedDocument = fragment.get('document');
let documents = [], current, dirty = false, loading = false, busy = false, connected = false, analysisTimer;
let conflictId;
let language = localStorage.getItem('oxygen.language') || 'zh';
let generation = 0;
const words = new Set('fn struct trait impl static pub use optional as let const embed if else for in while return break continue Self'.split(' '));
const types = new Set('any int float bool string list array object'.split(' '));
const oxygen = StreamLanguage.define({
  startState: () => ({ comment: false, quote: null }),
  token(stream, state) {
    if (state.comment) { if (stream.skipTo('*/')) { stream.match('*/'); state.comment = false; } else stream.skipToEnd(); return 'comment'; }
    if (state.quote) { if (stream.skipTo(state.quote)) { stream.match(state.quote); state.quote = null; } else stream.skipToEnd(); return 'string'; }
    if (stream.eatSpace()) return null;
    if (stream.match('//')) { stream.skipToEnd(); return 'comment'; }
    if (stream.match('/*')) { state.comment = true; return 'comment'; }
    if (stream.match(/f?(?:"""|''')/)) { state.quote = stream.current().replace(/^f/, ''); return 'string'; }
    if (stream.match(/f?["']/)) {
      const quote = stream.current().slice(-1); let escaped = false, ch;
      while ((ch = stream.next()) != null) { if (ch === quote && !escaped) break; escaped = ch === '\\' && !escaped; }
      return 'string';
    }
    if (stream.match(/\d+(?:\.\d+)?(?:[eE][+-]?\d+)?/)) return 'number';
    if (stream.match(/[\p{L}_][\p{L}\p{N}_]*/u)) {
      const word = stream.current();
      if (words.has(word)) return 'keyword';
      if (types.has(word)) return 'typeName';
      if (['true', 'false', 'null'].includes(word)) return 'atom';
      return null;
    }
    stream.next(); return null;
  }
});
const view = new EditorView({ parent: $('text-host'), state: EditorState.create({ extensions: [basicSetup, oneDark, oxygen,
  EditorView.theme({ '&': { height: '100%' }, '.cm-scroller': { overflow: 'auto', minHeight: '320px' }, '.cm-content': { fontFamily: 'ui-monospace, monospace' } }),
  EditorView.updateListener.of(update => { if (update.docChanged && !loading) changed(); })] }) });

const copy = {
  zh: { online: '本地服务已连接', offline: '服务不可用；本地修改仍保留', dirty: '未保存', saved: '已保存，未应用', applied: '已应用到当前存档', status: '保存与应用是两个独立动作。', switch: '放弃当前文档未保存的修改？', reload: '放弃本地修改并载入最新版本？', apply: '应用已保存的规则到当前存档？错误规则不会替换上一份有效规则。', noPreview: '当前模组没有提供命名试算接口。', preview: '仅执行显式 JSON 事实，不读取或修改游戏状态。', conflict: '本地修改已保留。请先对照磁盘版本；重新载入才会放弃本地内容。', missing: '此文档已不在当前目录中；可能已切换存档。请保留本地内容并重新选择文档。', noToken: '缺少会话令牌。请从 IrisMenus 重新打开编辑器。', export: '覆盖选中的源码文件？保存积木本身不会覆盖源码。', source: '从新画布生成；不支持已有源码反解。输出路径相对于当前 Mod，必须在 Oxygen/ 下。', empty: '选择左侧文档，或在选中的 Mod 下创建 .oxy / .oxyblocks。', reconnect: '从 IrisMenus 打开编辑器后再连接。关闭浏览器不会回收服务。', check: '检查通过', create: '已创建', readonly: '只读' },
  en: { online: 'Local service connected', offline: 'Service unavailable; local edits retained', dirty: 'Unsaved', saved: 'Saved, not applied', applied: 'Applied to the current save', status: 'Save and Apply are separate actions.', switch: 'Discard unsaved changes to this document?', reload: 'Discard local edits and load the latest version?', apply: 'Apply the saved rules to this game? Invalid rules retain the previous valid rules.', noPreview: 'This Mod has no naming preview provider.', preview: 'Runs explicit JSON facts only; does not read or change the game.', conflict: 'Local edits retained. Compare with the remote version; reloading discards local content.', missing: 'The document is no longer in the catalog, possibly because the game changed. Retain local edits and select another document.', noToken: 'Missing session token. Reopen the editor from IrisMenus.', export: 'Overwrite the selected source file? Saving blocks does not overwrite source.', source: 'Generates from a new canvas, not from existing source. Targets must be under this Mod’s Oxygen/ directory.', empty: 'Select a document or create .oxy / .oxyblocks under the selected Mod.', reconnect: 'Open the editor in IrisMenus, then reconnect. Closing the browser leaves the service running.', check: 'Check passed', create: 'Created', readonly: 'Read only' }
};
const t = key => copy[language][key];
function message(text, error = false) { $('status').textContent = text; $('status').classList.toggle('error', error); }
function controls() {
  for (const id of ['save', 'analyze', 'reload']) $(id).disabled = busy || !connected || !current || (id === 'save' && !current.writable);
  $('apply').disabled = busy || !connected || !current?.canApply || dirty;
  $('preview-button').disabled = busy || !connected || !previewHost();
  $('create-button').disabled = busy || !connected || !$('package').value;
  for (const id of ['check', 'export', 'export-copy']) $(id).disabled = busy || !connected || current?.kind !== 'blocks';
}
function state() {
  const text = current ? (!current.writable ? t('readonly') : dirty ? t('dirty') : t('saved')) : '';
  $('text-state').textContent = $('blocks-state').textContent = $('save-state').textContent = text;
  $('save-state').className = 'state ' + (dirty ? 'dirty' : 'saved'); controls();
}
function changed() {
  dirty = true; generation++; $('apply-state').textContent = ''; state();
  clearTimeout(analysisTimer); analysisTimer = setTimeout(() => execute(analyze), 500);
}
function source() { if (!current) throw new Error('No document selected'); return current.kind === 'blocks' ? blocks.blockDocumentText() : view.state.doc.toString(); }
function prepared() {
  if (current?.kind === 'blocks') { const result = blocks.prepareCheck(); if (!result.ok) throw new Error(result.message); return result; }
  return { source: source(), isModule: !!current?.isModule, ranges: [] };
}
function offline() {
  const first = $('offline').hidden;
  connected = false; $('service').textContent = t('offline'); $('service').classList.add('down'); $('offline').hidden = false; controls();
  if (first) $('retry').focus();
}
async function api(path, body) {
  if (!token) throw new Error(t('noToken'));
  let response;
  try { response = await fetch(path, { method: body === undefined ? 'GET' : 'POST', headers: { 'X-Oxygen-Token': token, ...(body === undefined ? {} : { 'Content-Type': 'application/json' }) }, ...(body === undefined ? {} : { body: JSON.stringify(body) }), signal: AbortSignal.timeout(15000) }); }
  catch (error) { offline(); throw error; }
  const payload = await response.json();
  if (!response.ok) {
    if (response.status === 409) { conflictId = body?.id; $('conflict').hidden = false; $('conflict-copy').textContent = t('conflict'); $('keep-local').focus(); }
    const error = new Error(payload.message || payload.error || `HTTP ${response.status}`); error.status = response.status; throw error;
  }
  connected = true; $('service').textContent = t('online'); $('service').classList.remove('down'); $('offline').hidden = true;
  return payload;
}
async function execute(action) {
  if (busy) return; busy = true; controls();
  try { await action(); } catch (error) { message(error.message, true); } finally { busy = false; controls(); }
}
function catalog() {
  const query = $('filter').value.trim().toLowerCase(); $('documents').replaceChildren();
  for (const doc of documents.filter(doc => `${doc.title} ${doc.path} ${doc.packageId}`.toLowerCase().includes(query))) {
    const li = document.createElement('li'), button = document.createElement('button'), detail = document.createElement('small');
    button.type = 'button'; button.textContent = doc.title || doc.path; button.setAttribute('aria-current', String(doc.id === current?.id));
    detail.textContent = doc.path; button.append(detail); button.addEventListener('click', () => execute(() => open(doc.id))); li.append(button); $('documents').append(li);
  }
}
function exportTargets() {
  const selected = $('export-target').value; $('export-target').replaceChildren();
  const outputPath = document.createElement('option'); outputPath.value = ''; outputPath.textContent = '输出路径 / Output path'; $('export-target').append(outputPath);
  for (const doc of documents.filter(doc => doc.packageId === current?.packageId && doc.kind !== 'blocks' && doc.writable)) {
    const option = document.createElement('option'); option.value = doc.id; option.textContent = doc.path; $('export-target').append(option);
  }
  $('export-target').value = selected;
}
async function refresh() {
  const result = await api('/api/documents'); documents = Array.isArray(result) ? result : result.documents;
  const selected = $('package').value || requestedPackage; $('package').replaceChildren();
  const packages = [...new Set(documents.map(doc => doc.packageId))];
  if (requestedPackage && !packages.includes(requestedPackage)) packages.push(requestedPackage);
  for (const id of packages) { const option = document.createElement('option'); option.value = option.textContent = id; $('package').append(option); }
  if (packages.includes(selected)) $('package').value = selected;
  catalog();
  exportTargets();
  if (current && !documents.some(doc => doc.id === current.id)) { current = { ...current, writable: false, canApply: false, canPreview: false }; message(t('missing'), true); state(); }
}
async function open(id, force = false) {
  if (dirty && !force && !confirm(t('switch'))) return;
  clearTimeout(analysisTimer);
  const descriptor = documents.find(doc => doc.id === id); if (!descriptor) throw new Error(t('missing'));
  const data = await api('/api/document?id=' + encodeURIComponent(id));
  loading = true;
  try {
    current = { ...descriptor, ...data }; generation++;
    const isBlocks = current.kind === 'blocks';
    $('empty').hidden = true; $('text-editor').hidden = isBlocks; $('blocks-editor').hidden = !isBlocks;
    if (isBlocks) { if (!blocks.loadBlockText(data.text)) throw new Error('Invalid block document; original file unchanged'); blocks.refreshBlockly(); }
    else view.dispatch({ changes: { from: 0, to: view.state.doc.length, insert: data.text } });
    for (const kind of ['text', 'blocks']) { $(kind + '-title').textContent = current.title; $(kind + '-path').textContent = current.packageId + ' / ' + current.path; }
    dirty = false; $('apply-state').textContent = ''; $('diagnostics').replaceChildren(); $('preview-result').textContent = ''; catalog(); state();
    exportTargets();
    message(t('status'));
  } finally { loading = false; }
}
async function analyze() {
  if (!current) return;
  const captured = generation, id = current.id, plan = prepared();
  const result = await api('/api/analyze', { id, source: plan.source, isModule: plan.isModule, language });
  if (generation !== captured || current.id !== id) return;
  const diagnostics = Array.isArray(result) ? result : result.diagnostics || [];
  $('diagnostics').replaceChildren();
  for (const diagnostic of diagnostics) {
    const li = document.createElement('li'), button = document.createElement('button'); button.type = 'button';
    const start = diagnostic.start ?? 0, preceding = plan.source.slice(0, start).split('\n');
    button.textContent = `${preceding.length}:${preceding.at(-1).length + 1} ${diagnostic.code || ''} ${diagnostic.message}`;
    button.addEventListener('click', () => { if (current.kind === 'text') { const offset = Math.min(diagnostic.start ?? 0, view.state.doc.length); view.dispatch({ selection: { anchor: offset }, scrollIntoView: true }); view.focus(); } });
    li.className = diagnostic.severity === 'warning' ? 'warning' : 'error'; li.append(button); $('diagnostics').append(li);
  }
  if (current.kind === 'blocks') blocks.showAnalysis(diagnostics.map(diagnostic => ({ ...diagnostic, blockId: plan.ranges.filter(range => range.start <= diagnostic.start && diagnostic.start < range.start + range.length).sort((a, b) => a.length - b.length)[0]?.blockId })));
  message(diagnostics.length ? `${diagnostics.length} diagnostics` : t('check'));
}
async function save() {
  const text = source(), version = generation;
  const result = await api('/api/save', { id: current.id, text, expectedHash: current.hash });
  current.hash = result.hash; dirty = generation !== version; state(); message(dirty ? t('dirty') : t('saved'));
}
function previewHost() { return current?.canPreview ? current : documents.find(doc => doc.packageId === current?.packageId && doc.canPreview); }
async function preview() {
  const host = previewHost(); if (!host) throw new Error(t('noPreview'));
  const plan = prepared(); JSON.parse($('facts').value);
  const result = await api('/api/preview', { id: host.id, source: plan.source, facts: $('facts').value });
  $('preview-result').textContent = typeof result === 'string' ? result : JSON.stringify(result, null, 2); message(t('preview'));
}
async function create(event) {
  event.preventDefault(); await execute(async () => {
    if (dirty && !confirm(t('switch'))) return;
    const text = $('new-kind').value === 'blocks' ? blocks.templateText($('template').value) : 'fn codename(pawn): string? {\n    return pawn.original_name\n}\n';
    const result = await api('/api/create', { packageId: $('package').value, path: $('new-path').value, text });
    await refresh(); await open(result.id, true); message(t('create'));
  });
}
async function exportSource(copyOnly = false) {
  const plan = prepared();
  const checked = await api('/api/analyze', { source: plan.source, isModule: plan.isModule, language });
  const errors = (Array.isArray(checked) ? checked : checked.diagnostics || []).filter(diagnostic => diagnostic.severity === 'error');
  if (errors.length) { blocks.showAnalysis(errors); throw new Error(errors[0].message); }
  const selected = !copyOnly && documents.find(doc => doc.id === $('export-target').value && doc.packageId === current.packageId);
  const filePlan = selected ? plan : blocks.exportPlan(); if (!filePlan.ok) throw new Error(filePlan.message);
  let path = selected ? selected.path : filePlan.path;
  if (copyOnly) { path = prompt('Oxygen/… .oxy', path.replace(/\.oxy$/i, '-copy.oxy')); if (!path) return; }
  const target = selected || documents.find(doc => doc.packageId === current.packageId && doc.path === path);
  if (target && !target.writable) throw new Error(t('readonly'));
  if (target) {
    const latest = await api('/api/document?id=' + encodeURIComponent(target.id));
    if (!confirm(t('export') + '\n' + target.path)) return;
    await api('/api/save', { id: target.id, text: plan.source, expectedHash: latest.hash });
  } else await api('/api/create', { packageId: current.packageId, path, text: plan.source });
  await refresh(); message(t('saved') + ': ' + path);
}
function localize() {
  document.documentElement.lang = language === 'zh' ? 'zh-CN' : 'en'; $('language').value = language;
  $('empty-copy').textContent = t('empty'); $('facts-help').textContent = t('preview'); $('path-help').textContent = t('source'); $('offline-copy').textContent = t('reconnect');
  const labels = language === 'zh' ? ['检查', '保存', '重新载入', '应用到本局', '刷新', '创建', '用这些事实预览'] : ['Check', 'Save', 'Reload', 'Apply to game', 'Refresh', 'Create', 'Preview these facts'];
  ['analyze','save','reload','apply','refresh','create-button','preview-button'].forEach((id,i) => $(id).textContent = labels[i]);
  state();
}
blocks.onWorkspaceChange(() => { if (!loading && current?.kind === 'blocks') changed(); });
$('filter').addEventListener('input', catalog);
$('create').addEventListener('submit', create);
$('new-kind').addEventListener('change', () => { $('new-path').value = $('new-path').value.replace(/\.(oxy|oxyblocks)$/i, $('new-kind').value === 'blocks' ? '.oxyblocks' : '.oxy'); });
$('language').addEventListener('change', () => { language = $('language').value; localStorage.setItem('oxygen.language', language); localize(); });
for (const id of ['analyze','check']) $(id).addEventListener('click', () => execute(analyze));
$('save').addEventListener('click', () => execute(save));
$('preview-button').addEventListener('click', () => execute(preview));
$('reload').addEventListener('click', () => execute(() => { if (current && (!dirty || confirm(t('reload')))) return open(current.id, true); }));
$('apply').addEventListener('click', () => execute(async () => {
  if (dirty || !confirm(t('apply'))) return;
  const result = await api('/api/apply', { id: current.id, expectedHash: current.hash });
  if (!result.success) throw new Error(result.message);
  $('apply-state').textContent = t('applied'); message(result.message || t('applied'));
}));
$('export').addEventListener('click', () => execute(() => exportSource(false)));
$('export-copy').addEventListener('click', () => execute(() => exportSource(true)));
$('refresh').addEventListener('click', () => execute(refresh));
$('retry').addEventListener('click', () => execute(refresh));
$('keep-local').addEventListener('click', () => { $('conflict').hidden = true; $('save').focus(); });
$('take-remote').addEventListener('click', () => execute(async () => { await open(conflictId || current.id, true); $('conflict').hidden = true; if (current.kind === 'text') view.focus(); }));
window.addEventListener('beforeunload', event => { if (dirty) { event.preventDefault(); event.returnValue = ''; } });
setInterval(() => { if (!busy && connected) execute(refresh); }, 5000);
localize();
execute(async () => { await refresh(); const doc = documents.find(doc => doc.id === requestedDocument) || documents.find(doc => doc.packageId === requestedPackage && doc.path === 'current/naming') || documents.find(doc => doc.packageId === requestedPackage) || documents[0]; if (doc) await open(doc.id); });
