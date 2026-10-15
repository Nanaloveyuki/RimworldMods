import * as Blockly from 'blockly/core';
import * as zh from 'blockly/msg/zh-hans';
import { defineBlocks, toolbox, blockCatalog, generate, createDocument, templates } from '../../vscode/src/blocks/library.js';

Blockly.setLocale(zh);
defineBlocks(Blockly);

const $ = id => document.getElementById(id);
const workspace = Blockly.inject($('blockly'), {
  toolbox, media: '/blockly/', renderer: 'zelos', sounds: false,
  theme: Blockly.Theme.defineTheme('oxygenLocal', {
    base: Blockly.Themes.Classic,
    componentStyles: { workspaceBackgroundColour: '#172331', toolboxBackgroundColour: '#1e2c3c',
      toolboxForegroundColour: '#e7eef6', flyoutBackgroundColour: '#24384c', flyoutForegroundColour: '#e7eef6',
      flyoutOpacity: 1, scrollbarColour: '#7eb6d6', insertionMarkerColour: '#e08a4f', insertionMarkerOpacity: 0.4 },
    fontStyle: { family: 'Segoe UI, Microsoft YaHei, sans-serif', size: 14 }
  }),
  trashcan: true, scrollbars: true, move: { scrollbars: true, drag: true, wheel: true },
  zoom: { controls: true, wheel: true, startScale: 0.85, maxScale: 1.6, minScale: 0.3 },
  grid: { spacing: 24, length: 2, colour: '#6a7280', snap: false }
});

let root;
let loading = false;
let selected;
let generated = { source: '', ranges: [], errors: [] };
let analysis = [];
const onChange = [];

export function onWorkspaceChange(listener) { onChange.push(listener); }
export function generatedSource() { return generated; }
export function blockDocument() { return root; }
export function blockDocumentText() { return JSON.stringify(root, null, 2) + '\n'; }

function serialize() {
  return { ...root, workspace: Blockly.serialization.workspaces.save(workspace), target: { path: $('target-path').value, mode: $('target-mode').value } };
}
function changed() { if (!loading) for (const listener of onChange) listener(); }

function status(message, error = false) {
  $('block-status').textContent = message;
  $('block-status').classList.toggle('error', error);
}

function controls() {
  const broken = !root || generated.errors.length > 0;
  for (const id of ['check', 'export', 'export-copy']) $(id).disabled = broken;
  $('load-template').disabled = !root;
  $('add-block').disabled = !root || !$('add-type').value;
}

function renderErrors() {
  $('errors').replaceChildren();
  for (const error of [...generated.errors, ...analysis]) {
    const li = document.createElement('li');
    const label = (error.code ? error.code + ' · ' : '') + error.message;
    if (error.blockId && workspace.getBlockById(error.blockId)) {
      const button = document.createElement('button');
      button.type = 'button';
      button.textContent = label;
      button.addEventListener('click', () => focusBlock(error.blockId));
      li.append(button);
    } else li.textContent = label;
    li.className = error.severity === 'warning' ? 'warning' : 'error';
    $('errors').append(li);
  }
  if (!generated.errors.length && !analysis.length) {
    const li = document.createElement('li');
    li.className = 'muted';
    li.textContent = '积木连接完整。检查会把生成源码交给 Oxygen 分析器；这里不会把已有源码反解成积木。';
    $('errors').append(li);
  }
}

function renderPreview() {
  const range = selected && generated.ranges.filter(item => item.blockId === selected.id).sort((a, b) => a.length - b.length)[0];
  $('preview').replaceChildren();
  if (!range) { $('preview').textContent = generated.source; return; }
  $('preview').append(document.createTextNode(generated.source.slice(0, range.start)));
  const mark = document.createElement('mark');
  mark.textContent = generated.source.slice(range.start, range.start + range.length);
  $('preview').append(mark);
  $('preview').append(document.createTextNode(generated.source.slice(range.start + range.length)));
}

function blockChoices() {
  const select = $('select-block');
  select.replaceChildren();
  const empty = document.createElement('option');
  empty.value = '';
  empty.textContent = '选择积木';
  select.append(empty);
  for (const block of workspace.getAllBlocks(false)) {
    const option = document.createElement('option');
    option.value = block.id;
    option.textContent = block.toString(65);
    select.append(option);
  }
  select.value = selected?.id || '';
}

function renderSource() {
  generated = root ? generate(root.workspace) : { source: '', ranges: [], errors: [] };
  blockChoices();
  renderPreview();
  renderErrors();
  controls();
}

function labeled(text, control) {
  const label = document.createElement('label');
  const span = document.createElement('span');
  span.textContent = text;
  label.append(span, control);
  return label;
}

function withEventGroup(fn) {
  Blockly.Events.setGroup(true);
  try { fn(); } finally { Blockly.Events.setGroup(false); }
}

function focusBlock(id) {
  const block = workspace.getBlockById(id);
  if (!block) return;
  block.select();
  workspace.centerOnBlock(id);
  selected = block;
  inspector();
  renderPreview();
}

function reorder(down) {
  if (!selected?.previousConnection || !selected.nextConnection) return;
  const upper = down ? selected : selected.getPreviousBlock();
  const lower = down ? selected.getNextBlock() : selected;
  if (!upper || !lower?.nextConnection || !upper.previousConnection) return;
  const before = upper.previousConnection.targetConnection;
  const after = lower.nextConnection.targetConnection;
  const position = upper.getRelativeToSurfaceXY();
  withEventGroup(() => {
    upper.previousConnection.disconnect();
    upper.nextConnection.disconnect();
    lower.nextConnection.disconnect();
    if (before) before.connect(lower.previousConnection);
    lower.nextConnection.connect(upper.previousConnection);
    if (after) upper.nextConnection.connect(after);
    if (!before) lower.moveTo(position);
  });
  changed();
  inspector();
}

function inspector() {
  if (selected && workspace.getBlockById(selected.id) !== selected) selected = undefined;
  $('select-block').value = selected?.id || '';
  $('selected-fields').replaceChildren();
  $('connections').replaceChildren();
  $('selected-title').textContent = selected ? blockCatalog.find(item => item.type === selected.type)?.label || selected.type : '选择积木';
  for (const id of ['move-up', 'move-down', 'delete-block']) $(id).disabled = !selected;
  if (!selected) {
    $('selected-fields').textContent = '点击画布上的积木，或使用“添加积木”。字段和连接也可通过键盘编辑。';
    return;
  }
  const block = selected;
  for (const input of block.inputList) for (const field of input.fieldRow) {
    if (!field.name || !field.isCurrentlyEditable()) continue;
    let control;
    if (field instanceof Blockly.FieldDropdown) {
      control = document.createElement('select');
      for (const [name, value] of field.getOptions(false)) {
        const option = document.createElement('option');
        option.value = value;
        option.textContent = typeof name === 'string' ? name : value;
        control.append(option);
      }
      control.value = field.getValue();
    } else {
      control = document.createElement('input');
      control.type = field instanceof Blockly.FieldNumber ? 'number' : 'text';
      if (control.type === 'number') control.step = 'any';
      control.value = field.getValue();
    }
    control.addEventListener('change', () => { block.setFieldValue(control.value, field.name); changed(); inspector(); });
    const names = { VALUE: '值', NAME: '名称', PARAMS: '参数（逗号分隔）', RETURN: '返回类型', MEMBER: '成员', PAWN: 'Pawn 变量', KEY: '字段 / 设置键', TYPE: 'Def 类型', ID: '稳定 ID', LABEL: '标签', DESCRIPTION: '描述', OFFSET: '心情点数', TARGET: '目标', DURATION: '持续 ticks', COOLDOWN: '冷却 ticks', MODULE: '模块路径', PACKAGE: '包名', ALIAS: '别名', OPTIONAL: '依赖方式', KIND: '控件类型', SCOPE: '保存范围', TITLE: '标题', EXTENDS: '继承模板', OP: '运算符', GATE: '闸门' };
    $('selected-fields').append(labeled(names[field.name] || field.name, control));
  }
  const enabled = document.createElement('input');
  enabled.type = 'checkbox';
  enabled.checked = block.isEnabled();
  enabled.addEventListener('change', () => { block.setDisabledReason(!enabled.checked, 'MANUALLY_DISABLED'); changed(); });
  $('selected-fields').append(labeled('参与生成', enabled));
  const connector = document.createElement('select');
  const empty = document.createElement('option');
  empty.value = '';
  empty.textContent = '选择连接位置';
  connector.append(empty);
  const options = [];
  const connection = block.outputConnection || block.previousConnection;
  if (connection) for (const parent of workspace.getAllBlocks(false)) {
    if (parent === block || block.getDescendants(false).includes(parent)) continue;
    for (const input of parent.inputList) {
      const target = input.connection;
      if (!target || target.isConnected() || !workspace.connectionChecker.canConnect(connection, target, false)) continue;
      options.push({ target, label: `${parent.toString(35)} → ${input.name}` });
    }
    if (parent.nextConnection && !parent.nextConnection.isConnected() && workspace.connectionChecker.canConnect(connection, parent.nextConnection, false)) {
      options.push({ target: parent.nextConnection, label: `${parent.toString(35)} → 下一条` });
    }
  }
  options.forEach((option, index) => {
    const element = document.createElement('option');
    element.value = String(index);
    element.textContent = option.label;
    connector.append(element);
  });
  const connect = document.createElement('button');
  connect.type = 'button';
  connect.textContent = '连接到此处';
  connect.disabled = !options.length;
  connect.addEventListener('click', () => {
    const option = options[Number(connector.value)];
    if (connector.value === '' || !option) return;
    withEventGroup(() => { if (connection.isConnected()) connection.disconnect(); option.target.connect(connection); });
    changed();
    inspector();
  });
  const disconnect = document.createElement('button');
  disconnect.type = 'button';
  disconnect.textContent = '断开父连接';
  disconnect.disabled = !connection?.isConnected();
  disconnect.addEventListener('click', () => { connection.disconnect(); changed(); inspector(); });
  $('connections').append(labeled('键盘连接', connector), connect, disconnect);
}

function addChoices() {
  const query = $('block-search').value.toLowerCase().trim();
  $('add-type').replaceChildren();
  for (const item of blockCatalog.filter(item => `${item.label} ${item.category} ${item.type}`.toLowerCase().includes(query))) {
    const option = document.createElement('option');
    option.value = item.type;
    option.textContent = item.category + ' · ' + item.label;
    $('add-type').append(option);
  }
  controls();
}

export function showAnalysis(diagnostics) {
  analysis = diagnostics || [];
  renderErrors();
}

export function prepareCheck() {
  if (!root) return { ok: false, message: '还没有积木文档。' };
  root = serialize();
  renderSource();
  if (generated.errors.length) return { ok: false, message: '积木还不能生成完整源码。' };
  return { ok: true, source: generated.source, isModule: root.target.mode === 'module', ranges: generated.ranges };
}

export function exportPlan() {
  const prepared = prepareCheck();
  if (!prepared.ok) return prepared;
  const target = root.target.path.trim().replace(/\\/g, '/');
  if (!/^Oxygen\/(?:[^/]+\/)*[^/]+\.oxy$/i.test(target) || target.split('/').some(part => part === '.' || part === '..')) {
    return { ok: false, message: '输出路径必须是当前模组 Oxygen/ 下的相对 .oxy 路径。' };
  }
  return { ...prepared, path: target };
}

function loadWorkspace(document, message) {
  Blockly.Events.disable();
  loading = true;
  try {
    Blockly.serialization.workspaces.load(document.workspace, workspace);
    workspace.clearUndo();
    root = document;
    selected = undefined;
    analysis = [];
    $('target-path').value = root.target.path;
    $('target-mode').value = root.target.mode;
    inspector();
    renderSource();
    status(message);
  } catch (error) {
    root = undefined;
    controls();
    status('无法载入积木：' + error.message, true);
  } finally {
    loading = false;
    Blockly.Events.enable();
    Blockly.svgResize(workspace);
  }
}

export function loadBlockText(text) {
  let incoming;
  try { incoming = JSON.parse(text); }
  catch (error) { status('积木文件不是有效 JSON：' + error.message, true); return false; }
  if (incoming.schemaVersion !== 1 || incoming.generatorVersion !== 1 || !incoming.workspace || !incoming.target || !['script', 'module'].includes(incoming.target.mode)) {
    status('积木文件版本或结构不受支持。不会把文本反解成积木。', true);
    return false;
  }
  loadWorkspace(incoming, '积木已载入。保存写入 .oxyblocks；导出才写入 .oxy。');
  return true;
}

export function loadBlockTemplate(id) {
  loadWorkspace(createDocument(id), '已载入模板。可编辑积木后保存，再单独导出源码；不会从已有源码还原画布。');
}

export function refreshBlockly() { Blockly.svgResize(workspace); }

workspace.addChangeListener(event => {
  if (loading) return;
  if (event.type === Blockly.Events.SELECTED) {
    const block = event.newElementId && workspace.getBlockById(event.newElementId);
    if (block) selected = block;
    inspector();
    renderPreview();
  } else if (event.type === Blockly.Events.CLICK && event.blockId) focusBlock(event.blockId);
  else if (!event.isUiEvent) {
    if (root) { root = serialize(); analysis = []; renderSource(); }
    changed();
  }
});

for (const template of templates) {
  const option = document.createElement('option');
  option.value = template.id;
  option.textContent = template.label;
  option.title = template.description;
  $('template').append(option);
}
$('template').value = 'prisoner';
$('load-template').addEventListener('click', () => {
  if (workspace.getAllBlocks(false).length && !confirm('替换当前画布？未保存的积木修改将被丢弃。')) return;
  loadBlockTemplate($('template').value);
  changed();
});
$('block-search').addEventListener('input', addChoices);
export function templateText(id = 'prisoner') {
  return JSON.stringify(createDocument(id), null, 2) + '\n';
}
$('select-block').addEventListener('change', () => { if ($('select-block').value) focusBlock($('select-block').value); });
$('add-block').addEventListener('click', () => {
  if (!$('add-type').value || !root) return;
  const block = workspace.newBlock($('add-type').value);
  block.initSvg();
  block.render();
  const metrics = workspace.getMetrics();
  block.moveBy((metrics.viewLeft + 64) / workspace.scale, (metrics.viewTop + 64) / workspace.scale);
  block.select();
  selected = block;
  inspector();
  changed();
});
$('target-path').addEventListener('change', () => { if (root) { root = serialize(); changed(); } });
$('target-mode').addEventListener('change', () => { if (root) { root = serialize(); analysis = []; renderSource(); changed(); } });
for (const [id, redo] of [['undo', false], ['redo', true]]) $(id).addEventListener('click', () => { workspace.undo(redo); if (root) root = serialize(); inspector(); changed(); });
$('zoom-fit').addEventListener('click', () => workspace.zoomToFit());
$('move-up').addEventListener('click', () => reorder(false));
$('move-down').addEventListener('click', () => reorder(true));
$('delete-block').addEventListener('click', () => { if (selected) { selected.dispose(true); selected = undefined; inspector(); changed(); } });
new ResizeObserver(() => Blockly.svgResize(workspace)).observe($('blockly'));
addChoices();
controls();
globalThis.OxygenBlocks = { onWorkspaceChange, generatedSource, blockDocument, blockDocumentText, showAnalysis, prepareCheck, exportPlan, loadBlockText, loadBlockTemplate, refreshBlockly, templateText };
