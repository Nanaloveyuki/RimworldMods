import * as Blockly from 'blockly/core';
import * as zh from 'blockly/msg/zh-hans';
import { defineBlocks, toolbox, blockCatalog, generate, createDocument, templates } from './library';

const api = acquireVsCodeApi();
Blockly.setLocale(zh);
defineBlocks(Blockly);
const $ = id => document.getElementById(id);
const workspace = Blockly.inject($('blockly'), {
  toolbox, media: document.body.dataset.media, renderer: 'zelos', sounds: false,
  trashcan: true, scrollbars: true, move: { scrollbars: true, drag: true, wheel: true },
  zoom: { controls: true, wheel: true, startScale: 0.85, maxScale: 1.6, minScale: 0.3 },
  grid: { spacing: 24, length: 2, colour: '#6a7280', snap: false }
});
let root;
let version = -1;
let trusted = false;
let loading = false;
let inFlight;
let dirty = false;
let timer;
let pendingAction;
let busy;
let selected;
let generated = { source: '', ranges: [], errors: [] };
let analysis = [];
let valid = false;
const signature = value => JSON.stringify({ workspace: value.workspace, target: value.target });
function status(message, error = false) { $('status').textContent = message; $('status').classList.toggle('error', error); }
function controls() {
  for (const id of ['check', 'export', 'export-copy']) $(id).disabled = !valid || !!busy || generated.errors.length > 0;
  $('undo').disabled = !valid;
  $('redo').disabled = !valid;
  $('load-template').disabled = !valid;
  $('add-block').disabled = !valid || !$('add-type').value;
}
function serialize() {
  return { ...root, workspace: Blockly.serialization.workspaces.save(workspace), target: { path: $('target-path').value, mode: $('target-mode').value } };
}
function change() {
  if (loading || !valid) return;
  const next = serialize();
  if (signature(next) === signature(root)) return;
  root = next; dirty = true; analysis = []; renderSource();
  clearTimeout(timer); timer = setTimeout(flush, 250);
}
function flush() {
  clearTimeout(timer);
  if (!valid || inFlight || !dirty) return;
  root = serialize();
  inFlight = { document: structuredClone(root), baseVersion: version };
  dirty = false;
  api.postMessage({ type: 'edit', baseVersion: version, document: inFlight.document });
  status('积木修改已提交；Ctrl+S 保存文件。');
}
function act(type, copy = false) {
  pendingAction = { type, copy }; flush(); dispatchAction();
}
function dispatchAction() {
  if (!pendingAction || inFlight || dirty || !valid) return;
  const action = pendingAction; pendingAction = undefined; busy = action.type;
  api.postMessage({ ...action, version });
  status(action.type === 'check' ? '正在检查生成的 Oxygen…' : '正在检查并导出源码…'); controls();
}
function focusBlock(id) {
  const block = workspace.getBlockById(id);
  if (!block) return;
  block.select(); workspace.centerOnBlock(id); selected = block; inspector(); renderPreview();
}
function renderErrors() {
  $('errors').replaceChildren();
  for (const error of [...generated.errors, ...analysis]) {
    const li = document.createElement('li');
    const label = (error.code ? error.code + ' · ' : '') + error.message;
    if (error.blockId && workspace.getBlockById(error.blockId)) {
      const button = document.createElement('button'); button.textContent = label;
      button.addEventListener('click', () => focusBlock(error.blockId)); li.append(button);
    } else li.textContent = label;
    li.className = error.severity === 'warning' ? 'warning' : 'error';
    $('errors').append(li);
  }
  if (!generated.errors.length && !analysis.length) {
    const li = document.createElement('li'); li.className = 'muted'; li.textContent = '积木连接完整。点击“检查积木”分析生成源码；不执行游戏逻辑。'; $('errors').append(li);
  }
}
function renderPreview() {
  const range = selected && generated.ranges.filter(item => item.blockId === selected.id).sort((a, b) => a.length - b.length)[0];
  $('preview').replaceChildren();
  if (!range) { $('preview').textContent = generated.source; return; }
  $('preview').append(document.createTextNode(generated.source.slice(0, range.start)));
  const mark = document.createElement('mark'); mark.textContent = generated.source.slice(range.start, range.start + range.length); $('preview').append(mark);
  $('preview').append(document.createTextNode(generated.source.slice(range.start + range.length)));
}
function blockChoices() {
  const select = $('select-block');
  select.replaceChildren();
  const empty = document.createElement('option'); empty.value = ''; empty.textContent = '选择积木'; select.append(empty);
  for (const block of workspace.getAllBlocks(false)) {
    const option = document.createElement('option'); option.value = block.id; option.textContent = block.toString(65); select.append(option);
  }
  select.value = selected?.id || '';
}
function renderSource() { generated = generate(root.workspace); blockChoices(); renderPreview(); renderErrors(); controls(); }
function labeled(text, control) {
  const label = document.createElement('label'); const span = document.createElement('span'); span.textContent = text; label.append(span, control); return label;
}
function withEventGroup(fn) { Blockly.Events.setGroup(true); try { fn(); } finally { Blockly.Events.setGroup(false); } }
function reorder(down) {
  if (!selected?.previousConnection || !selected.nextConnection) return;
  const upper = down ? selected : selected.getPreviousBlock();
  const lower = down ? selected.getNextBlock() : selected;
  if (!upper || !lower?.nextConnection || !upper.previousConnection) return;
  const before = upper.previousConnection.targetConnection;
  const after = lower.nextConnection.targetConnection;
  const position = upper.getRelativeToSurfaceXY();
  withEventGroup(() => {
    upper.previousConnection.disconnect(); upper.nextConnection.disconnect(); lower.nextConnection.disconnect();
    if (before) before.connect(lower.previousConnection);
    lower.nextConnection.connect(upper.previousConnection);
    if (after) upper.nextConnection.connect(after);
    if (!before) lower.moveTo(position);
  });
  change(); inspector();
}
function inspector() {
  if (selected && workspace.getBlockById(selected.id) !== selected) selected = undefined;
  $('select-block').value = selected?.id || '';
  $('selected-fields').replaceChildren(); $('connections').replaceChildren();
  $('selected-title').textContent = selected ? blockCatalog.find(item => item.type === selected.type)?.label || selected.type : '选择积木';
  for (const id of ['move-up', 'move-down', 'delete-block']) $(id).disabled = !selected;
  if (!selected) { $('selected-fields').textContent = '点击画布上的积木，或使用“添加积木”。字段和连接也可通过键盘编辑。'; return; }
  const block = selected;
  for (const input of block.inputList) for (const field of input.fieldRow) {
    if (!field.name || !field.isCurrentlyEditable()) continue;
    let control;
    if (field instanceof Blockly.FieldDropdown) {
      control = document.createElement('select');
      for (const [name, value] of field.getOptions(false)) { const option = document.createElement('option'); option.value = value; option.textContent = typeof name === 'string' ? name : value; control.append(option); }
      control.value = field.getValue();
    } else {
      control = document.createElement('input'); control.type = field instanceof Blockly.FieldNumber ? 'number' : 'text';
      if (control.type === 'number') control.step = 'any'; control.value = field.getValue();
    }
    control.addEventListener('change', () => { block.setFieldValue(control.value, field.name); change(); inspector(); });
    const names = { VALUE: '值', NAME: '名称', PARAMS: '参数（逗号分隔）', RETURN: '返回类型', MEMBER: '成员', PAWN: 'Pawn 变量', KEY: '字段 / 设置键', TYPE: 'Def 类型', ID: '稳定 ID', LABEL: '标签', DESCRIPTION: '描述', OFFSET: '心情点数', TARGET: '目标', DURATION: '持续 ticks', COOLDOWN: '冷却 ticks', MODULE: '模块路径', PACKAGE: '包名', ALIAS: '别名', OPTIONAL: '依赖方式', KIND: '控件类型', SCOPE: '保存范围', TITLE: '标题', EXTENDS: '继承模板', OP: '运算符', GATE: '闸门' };
    $('selected-fields').append(labeled(names[field.name] || field.name, control));
  }
  const enabled = document.createElement('input'); enabled.type = 'checkbox'; enabled.checked = block.isEnabled();
  enabled.addEventListener('change', () => { block.setDisabledReason(!enabled.checked, 'MANUALLY_DISABLED'); change(); });
  $('selected-fields').append(labeled('参与生成', enabled));
  const connector = document.createElement('select');
  const empty = document.createElement('option'); empty.value = ''; empty.textContent = '选择连接位置'; connector.append(empty);
  const options = [];
  const connection = block.outputConnection || block.previousConnection;
  if (connection) for (const parent of workspace.getAllBlocks(false)) {
    if (parent === block || block.getDescendants(false).includes(parent)) continue;
    for (const input of parent.inputList) {
      const target = input.connection;
      if (!target || target.isConnected() || !workspace.connectionChecker.canConnect(connection, target, false)) continue;
      options.push({ target, label: `${parent.toString(35)} → ${input.name}` });
    }
    if (parent.nextConnection && !parent.nextConnection.isConnected() && workspace.connectionChecker.canConnect(connection, parent.nextConnection, false)) options.push({ target: parent.nextConnection, label: `${parent.toString(35)} → 下一条` });
  }
  options.forEach((option, index) => { const element = document.createElement('option'); element.value = String(index); element.textContent = option.label; connector.append(element); });
  const connect = document.createElement('button'); connect.textContent = '连接到此处'; connect.disabled = !options.length;
  connect.addEventListener('click', () => {
    const option = options[Number(connector.value)]; if (connector.value === '' || !option) return;
    withEventGroup(() => { if (connection.isConnected()) connection.disconnect(); option.target.connect(connection); }); change(); inspector();
  });
  const disconnect = document.createElement('button'); disconnect.textContent = '断开父连接'; disconnect.disabled = !connection?.isConnected();
  disconnect.addEventListener('click', () => { connection.disconnect(); change(); inspector(); });
  $('connections').append(labeled('键盘连接', connector), connect, disconnect);
}
function addChoices() {
  const query = $('block-search').value.toLowerCase().trim();
  $('add-type').replaceChildren();
  for (const item of blockCatalog.filter(item => `${item.label} ${item.category} ${item.type}`.toLowerCase().includes(query))) {
    const option = document.createElement('option'); option.value = item.type; option.textContent = item.category + ' · ' + item.label; $('add-type').append(option);
  }
  controls();
}
function theme() {
  const style = getComputedStyle(document.body);
  const css = name => style.getPropertyValue('--vscode-' + name).trim();
  workspace.setTheme(Blockly.Theme.defineTheme('oxygen', {
    base: Blockly.Themes.Classic,
    componentStyles: { workspaceBackgroundColour: css('editor-background') || '#1e1e1e', toolboxBackgroundColour: css('sideBar-background') || '#252526', toolboxForegroundColour: css('foreground') || '#cccccc', flyoutBackgroundColour: css('sideBar-background') || '#252526', flyoutForegroundColour: css('foreground') || '#cccccc', flyoutOpacity: 1, scrollbarColour: css('scrollbarSlider-background') || '#777777', insertionMarkerColour: css('focusBorder') || '#007acc', insertionMarkerOpacity: 0.4 },
    fontStyle: { family: css('font-family') || 'sans-serif', weight: 'normal', size: 12 }
  }));
}
function loadDocument(message) {
  if (message.version < version && !message.conflict) return;
  if (message.version === version && valid && !message.conflict) {
    if (trusted !== message.trusted) status(message.trusted ? '积木已载入。Ctrl+S 保存；导出前将检查源码。' : '编辑和保存可用；检查与导出需要受信任工作区。');
    trusted = message.trusted;
    return;
  }
  let incoming;
  try {
    incoming = JSON.parse(message.text);
    if (incoming.schemaVersion !== 1 || incoming.generatorVersion !== 1 || !incoming.workspace || !incoming.target || !['script', 'module'].includes(incoming.target.mode)) throw new Error('文件版本或结构不受支持。');
  } catch (error) {
    valid = false; inFlight = undefined; dirty = false; busy = undefined; controls();
    status('积木文件无效：' + error.message + ' 请通过 VSCode“重新打开编辑器方式 → 文本编辑器”修复。', true); return;
  }
  const ownAck = inFlight && signature(incoming) === signature(inFlight.document) && !message.conflict;
  const unchanged = root && signature(incoming) === signature(root);
  version = message.version; trusted = message.trusted;
  if (ownAck) {
    inFlight = undefined;
    root = { ...incoming, ...(dirty ? { workspace: root.workspace, target: root.target } : {}) };
    flush(); dispatchAction(); return;
  }
  if (message.version === inFlight?.baseVersion && !message.conflict) return;
  if (unchanged && !message.conflict) { root = incoming; dispatchAction(); return; }
  clearTimeout(timer); inFlight = undefined; dirty = false; pendingAction = undefined; busy = undefined;
  loading = true;
  try {
    Blockly.serialization.workspaces.load(incoming.workspace, workspace);
    workspace.clearUndo(); root = incoming; valid = true;
    $('target-path').value = root.target.path; $('target-mode').value = root.target.mode;
    analysis = []; inspector(); renderSource();
    status(message.conflict ? '文件已被其他编辑操作更新，已载入最新版本；请重新进行修改。' : trusted ? '积木已载入。Ctrl+S 保存；导出前将检查源码。' : '积木已载入。编辑和保存可用；检查与导出需要受信任工作区。', message.conflict);
  } catch (error) { valid = false; controls(); status('无法载入积木：' + error.message + '。文件未被覆盖，请用文本编辑器修复。', true); }
  finally { loading = false; }
}
window.addEventListener('message', event => {
  const message = event.data;
  if (message.type === 'document') loadDocument(message);
  else if (message.type === 'analysis') {
    if (message.version !== version || dirty || inFlight) return;
    analysis = message.diagnostics || [];
    if (message.error) analysis.push({ message: message.error, severity: 'error' });
    renderErrors();
    if (busy === 'check' || message.error || analysis.some(item => item.severity === 'error')) busy = undefined;
    status(message.error || (analysis.length ? '检查发现问题；点击错误可定位积木。' : '当前文件语法与静态语义检查通过；仍需对应内容宿主或游戏验收。'), !!message.error); controls();
  } else if (message.type === 'status') { busy = undefined; status(message.message, message.error); controls(); }
});
workspace.addChangeListener(event => {
  if (loading) return;
  if (event.type === Blockly.Events.SELECTED) {
    const block = event.newElementId && workspace.getBlockById(event.newElementId);
    if (block) selected = block;
    inspector(); renderPreview();
  }
  else if (event.type === Blockly.Events.CLICK && event.blockId) { focusBlock(event.blockId); }
  else if (!event.isUiEvent) change();
});
for (const template of templates) { const option = document.createElement('option'); option.value = template.id; option.textContent = template.label; $('template').append(option); }
$('load-template').addEventListener('click', () => {
  if (workspace.getAllBlocks(false).length && !confirm('替换当前画布？可使用 VSCode 撤销恢复已提交的修改。')) return;
  const next = createDocument($('template').value);
  loading = true; Blockly.serialization.workspaces.load(next.workspace, workspace); loading = false;
  $('target-path').value = next.target.path; $('target-mode').value = next.target.mode; inspector(); change();
});
$('block-search').addEventListener('input', addChoices);
$('select-block').addEventListener('change', () => { if ($('select-block').value) focusBlock($('select-block').value); });
$('add-block').addEventListener('click', () => {
  if (!$('add-type').value) return;
  const block = workspace.newBlock($('add-type').value); block.initSvg(); block.render();
  const metrics = workspace.getMetrics(); block.moveBy((metrics.viewLeft + 64) / workspace.scale, (metrics.viewTop + 64) / workspace.scale); block.select(); selected = block; inspector(); change();
});
$('target-path').addEventListener('change', change); $('target-mode').addEventListener('change', change);
$('check').addEventListener('click', () => act('check')); $('export').addEventListener('click', () => act('export')); $('export-copy').addEventListener('click', () => act('export', true));
for (const [id, redo] of [['undo', false], ['redo', true]]) $(id).addEventListener('click', () => { workspace.undo(redo); change(); inspector(); });
$('zoom-fit').addEventListener('click', () => workspace.zoomToFit());
$('move-up').addEventListener('click', () => reorder(false)); $('move-down').addEventListener('click', () => reorder(true));
$('delete-block').addEventListener('click', () => { if (selected) { selected.dispose(true); selected = undefined; inspector(); change(); } });
new ResizeObserver(() => Blockly.svgResize(workspace)).observe($('blockly'));
new MutationObserver(theme).observe(document.body, { attributes: true, attributeFilter: ['class', 'style'] });
window.addEventListener('pagehide', flush);
addChoices(); theme(); controls(); api.postMessage({ type: 'ready' });
