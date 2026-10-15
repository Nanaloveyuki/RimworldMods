'use strict';

// One shared definition/generation contract for the Webview and extension host.
const field = (name, text = '') => ({ type: 'field_input', name, text });
const number = (name, value = 0) => ({ type: 'field_number', name, value });
const choice = (name, options) => ({ type: 'field_dropdown', name, options: options.map(value => Array.isArray(value) ? value : [value, value]) });
const input = (name, check) => ({ type: 'input_value', name, ...(check ? { check } : {}) });
const stack = (name, check) => ({ type: 'input_statement', name, ...(check ? { check } : {}) });
const definitions = [];
const blockCatalog = [];
function block(type, label, category, message, args, shape, colour) {
  if (!Object.hasOwn(shape, 'output') && args.filter(arg => arg.type.startsWith('field_')).length >= 3) {
    message = message.replace(/%(\d+)\s+/g, (match, index) => args[Number(index) - 1].type.startsWith('field_') ? '%' + index + '\n' : match);
  }
  definitions.push({ type, message0: message, args0: args, colour, tooltip: label, inputsInline: Object.hasOwn(shape, 'output'), ...shape });
  blockCatalog.push({ type, label, category });
}
const value = output => ({ output });
const statement = { previousStatement: 'Statement', nextStatement: 'Statement' };
const entry = kind => ({ previousStatement: kind, nextStatement: kind });
block('oxy_number', '数值', '值与集合', '数值 %1', [number('VALUE')], value('Number'), 210);
block('oxy_text', '文本', '值与集合', '文本 %1', [field('VALUE', '文字')], value('String'), 210);
block('oxy_bool', '布尔值', '值与集合', '布尔 %1', [choice('VALUE', [['是', 'true'], ['否', 'false']])], value('Boolean'), 210);
block('oxy_null', '空值 null', '值与集合', '空值 null', [], value(null), 210);
block('oxy_name', '变量或函数引用', '值与集合', '引用 %1', [field('NAME', 'pawn')], value(null), 210);
block('oxy_list', '列表', '值与集合', '列表 %1', [stack('ITEMS', 'ListItem')], value('List'), 210);
block('oxy_list_item', '列表成员', '值与集合', '成员 %1', [input('VALUE')], entry('ListItem'), 210);
block('oxy_object', '字段对象', '值与集合', '字段对象 %1', [stack('FIELDS', 'ObjectField')], value('Object'), 210);
block('oxy_field', '对象字段', '值与集合', '字段 %1 = %2', [field('KEY', 'label'), input('VALUE')], entry('ObjectField'), 210);
block('oxy_def_ref', '原生 Def 引用', '原生内容', 'Def 引用 类型 %1 名称 %2', [field('TYPE', 'ThoughtDef'), field('NAME', 'MyMod_Thought')], value('DefReference'), 35);
block('oxy_binary', '比较、算术与逻辑', '条件', '%1 %2 %3', [input('LEFT'), choice('OP', ['>', '>=', '<', '<=', '==', '!=', '&&', '||', '+', '-', '*', '/', '%']), input('RIGHT')], value(null), 160);
block('oxy_compare', '比较', '条件', '%1 %2 %3', [input('LEFT'), choice('OP', ['>', '>=', '<', '<=', '==', '!=']), input('RIGHT')], value('Boolean'), 160);
block('oxy_not', '逻辑取反', '条件', '非 %1', [input('VALUE', 'Boolean')], value('Boolean'), 160);
block('oxy_member', '对象成员查询', 'Pawn 查询', '%1 的 %2', [input('OBJECT'), field('MEMBER', 'food')], value(null), 285);
block('oxy_pawn', 'Pawn 常用字段', 'Pawn 查询', 'Pawn %1 字段 %2', [field('PAWN', 'pawn'), choice('MEMBER', ['food', 'rest', 'mood', 'age', 'drafted', 'downed', 'colonist', 'young_sick', 'own_illness', 'group_illness', 'origin', 'active_visitor', 'hunger'])], value(null), 285);
block('oxy_call', '调用函数或查询', 'Pawn 查询', '调用 %1 参数 %2', [field('NAME', 'pawn.has_hediff'), stack('ARGS', 'ListItem')], value(null), 285);
block('oxy_pawn_query', '囚犯代号查询', '囚犯代号', 'Pawn %1 查询 %2', [field('PAWN', 'pawn'), choice('MEMBER', ['number', 'number_text', 'grade', 'score', 'race', 'race_label', 'original_name', 'original_full_name', 'first_name', 'last_name', 'interaction_label', 'age', 'resistance', 'recruitable'])], value(null), 45);
block('oxy_concat', '拼接文本', '囚犯代号', '%1 接上 %2', [input('LEFT', 'String'), input('RIGHT', 'String')], value('String'), 45);
block('oxy_function', '函数', '函数与流程', '函数 %1 参数 %2 返回类型 %3 %4', [field('NAME', 'condition'), field('PARAMS', 'pawn'), field('RETURN', 'bool'), stack('BODY', 'Statement')], statement, 230);
block('oxy_return', '返回值', '函数与流程', '返回 %1', [input('VALUE')], statement, 230);
block('oxy_if', '条件分支', '函数与流程', '如果 %1 则 %2 否则 %3', [input('CONDITION', 'Boolean'), stack('THEN', 'Statement'), stack('ELSE', 'Statement')], statement, 230);
block('oxy_let', '定义变量', '函数与流程', '定义 %1 = %2', [field('NAME', 'value'), input('VALUE')], statement, 230);
block('oxy_assign', '更新变量', '函数与流程', '更新 %1 = %2', [field('NAME', 'value'), input('VALUE')], statement, 230);
block('oxy_do', '执行调用', '函数与流程', '执行 %1', [input('CALL')], statement, 230);
block('oxy_use', '导入模块', '模块', '%1 导入 %2 @ %3 别名 %4', [choice('OPTIONAL', [['普通', 'false'], ['可选', 'true']]), field('MODULE', 'helpers'), field('PACKAGE', 'nanaloveyuki.oxygendsl'), field('ALIAS', '')], statement, 250);
const targets = [['殖民者', 'colonists'], ['地图', 'map'], ['固定选择', 'selected']];
block('oxy_mood', '持续心情', '心情与规则', '持续心情 ID %1 名称 %2 描述 %3 点数 %4 目标 %5 条件函数 %6', [field('ID', 'fed_comfort'), field('LABEL', '吃饱了'), field('DESCRIPTION', '我吃饱了。现在感觉不错。'), number('OFFSET', 5), choice('TARGET', targets), input('WHEN')], statement, 20);
block('oxy_memory', '限时记忆', '心情与规则', '限时记忆 ID %1 名称 %2 描述 %3 点数 %4 目标 %5 持续 ticks %6 冷却 ticks %7 条件函数 %8', [field('ID', 'small_break'), field('LABEL', '稍作休息'), field('DESCRIPTION', '我歇了一会儿。'), number('OFFSET', 3), choice('TARGET', targets), number('DURATION', 2500), number('COOLDOWN', 2500), input('WHEN')], statement, 20);
block('oxy_thought_rule', '原生思想阶段规则', '心情与规则', '思想规则 ID %1 思想 Def %2 评估函数 %3', [field('ID', 'thought_state'), input('THOUGHT', 'DefReference'), input('EVALUATE')], statement, 20);
block('oxy_gate_rule', 'RHAH 行为规则', '心情与规则', '行为规则 ID %1 闸门 %2 评估函数 %3', [field('ID', 'begging_policy'), choice('GATE', ['Beg', 'Steal', 'Fight', 'LeaveAfterFed', 'EatOutsideRelief', 'FeedFromRelief', 'Gnaw', 'TailBite', 'Leash', 'Carry', 'JoinColony', 'Hire', 'Transfer', 'Imprison', 'DropOffChild', 'ExitMap']), input('EVALUATE')], statement, 20);
block('oxy_template', '原生内容模板', '原生内容', '模板 ID %1 类型 %2 字段 %3', [field('ID', 'rare'), field('TYPE', 'TraitDef'), input('FIELDS', 'Object')], statement, 35);
block('oxy_define', '声明原生 Def', '原生内容', '声明 类型 %1 名称 %2 继承模板（可空）%3 字段 %4', [field('TYPE', 'TraitDef'), field('NAME', 'MyMod_Trait'), field('EXTENDS', ''), input('FIELDS', 'Object')], statement, 35);
block('oxy_patch', '修改原生 Def 字段', '原生内容', '修改 类型 %1 名称 %2 设置字段 %3', [field('TYPE', 'TraitDef'), field('NAME', 'Industriousness'), input('FIELDS', 'Object')], statement, 35);
block('oxy_registry', 'IrisMenus 设置页组', '玩家设置', '可选 IrisMenus 设置页 %1', [stack('PAGES', 'Page')], statement, 300);
block('oxy_page', '设置分页', '玩家设置', '分页 ID %1 标题 %2 保存范围 %3 控件 %4', [field('ID', 'general'), field('TITLE', '常规设置'), choice('SCOPE', [['全局', 'global'], ['存档', 'game']]), stack('ITEMS', 'SettingItem')], entry('Page'), 300);
block('oxy_setting_label', '设置说明或分节', '玩家设置', '%1 ID %2 标题 %3', [choice('KIND', [['说明', 'label'], ['分节', 'section']]), field('ID', 'intro'), field('LABEL', '设置说明')], entry('SettingItem'), 300);
block('oxy_setting', '设置值控件', '玩家设置', '%1 ID %2 标签 %3 设置键 %4 默认值 %5 额外字段 %6', [choice('KIND', [['开关', 'checkbox'], ['数值', 'number'], ['滑条', 'slider'], ['文本', 'text'], ['下拉', 'select']]), field('ID', 'enabled'), field('LABEL', '启用'), field('KEY', 'enabled'), input('DEFAULT'), stack('EXTRA', 'ObjectField')], entry('SettingItem'), 300);
block('oxy_setting_button', '设置按钮', '玩家设置', '按钮 ID %1 标签 %2 动作函数 %3', [field('ID', 'editor'), field('LABEL', '打开编辑器'), input('ACTION')], entry('SettingItem'), 300);

function defineBlocks(Blockly) { Blockly.defineBlocksWithJsonArray(definitions.filter(def => !Blockly.Blocks[def.type])); }
const categories = [...new Set(blockCatalog.map(item => item.category))];
const toolbox = { kind: 'categoryToolbox', contents: categories.map(name => ({ kind: 'category', name, colour: String(definitions.find(def => blockCatalog.find(item => item.type === def.type)?.category === name).colour), contents: blockCatalog.filter(item => item.category === name).map(item => ({ kind: 'block', type: item.type })) })) };
const definitionMap = new Map(definitions.map(def => [def.type, def]));
const reserved = new Set(['true', 'false', 'null', 'optional', 'use', 'as', 'const', 'let', 'fn', 'struct', 'trait', 'impl', 'static', 'embed', 'pub', 'if', 'else', 'for', 'in', 'while', 'return', 'break', 'continue', 'Self']);
const identPattern = /^[\p{L}_][\p{L}\p{N}_]*$/u;
function isIdentifier(text) { return identPattern.test(text) && !reserved.has(text); }
function isPath(text) { return text.split('.').every(isIdentifier); }
function literal(text) {
  const escapes = { '"': '\\"', '\\': '\\\\', '\n': '\\n', '\r': '\\r', '\t': '\\t' };
  return '"' + String(text).replace(/["\\\u0000-\u001f]/g, char => escapes[char] || '\\u' + char.charCodeAt(0).toString(16).padStart(4, '0')) + '"';
}
function fragment(...parts) {
  let source = '';
  const ranges = [];
  for (const part of parts.flat()) {
    if (typeof part === 'string') source += part;
    else if (part) { for (const range of part.ranges) ranges.push({ ...range, start: range.start + source.length }); source += part.source; }
  }
  return { source, ranges };
}
function marked(node, frag) { frag.ranges.push({ blockId: node.id, start: 0, length: frag.source.length }); return frag; }

function generate(workspaceState) {
  const errors = [];
  const seen = new Set();
  const empty = () => fragment('');
  function fail(node, message) { errors.push({ blockId: node?.id, message }); return empty(); }
  function text(node, key) { return String(node.fields?.[key] ?? definitionMap.get(node.type)?.args0.find(arg => arg.name === key)?.text ?? ''); }
  function safe(node, key, kind = 'identifier') {
    const name = text(node, key).trim();
    if (!(kind === 'path' ? isPath(name) : isIdentifier(name))) fail(node, `${key} 不是有效${kind === 'path' ? '成员路径' : '标识符'}：${name}`);
    return name;
  }
  function numeric(node, key) {
    const raw = node.fields?.[key] ?? definitionMap.get(node.type)?.args0.find(arg => arg.name === key)?.value;
    if (raw === '' || raw === null || raw === undefined || !Number.isFinite(Number(raw))) { fail(node, `${key} 必须为有限数值。`); return '0'; }
    return String(Number(raw));
  }
  function child(node, name, depth) {
    const connection = node.inputs?.[name];
    const child = connection?.block || connection?.shadow;
    if (!child) return fail(node, `请连接“${name}”输入。`);
    return emit(child, 'value', depth + 1);
  }
  function entries(node, name, kind, depth) { return chain(node.inputs?.[name]?.block || node.inputs?.[name]?.shadow, kind, depth + 1); }
  function chain(first, kind, depth) {
    const result = [];
    let node = first;
    let count = 0;
    while (node) {
      if (++count > 10000) { fail(node, '积木链过长。'); break; }
      const next = node.next?.block;
      if (!node.disabled && !node.disabledReasons?.length) result.push(emit(node, kind, depth));
      node = next;
    }
    return result;
  }
  const comma = parts => fragment(...parts.flatMap((part, i) => i ? [', ', part] : [part]));
  function emit(node, context, depth = 0) {
    if (!node || typeof node !== 'object') return fail(node, '无效的积木对象。');
    if (depth > 160) return fail(node, '积木嵌套超过 160 层。');
    if (typeof node.id !== 'string' || !node.id || seen.has(node.id)) return fail(node, '积木 ID 缺失或重复。');
    seen.add(node.id);
    const def = definitionMap.get(node.type);
    if (!def) return fail(node, `不支持的积木类型：${node.type}`);
    if (node.disabled || node.disabledReasons?.length) return fail(node, '输入连接到了停用积木。');
    const actual = Object.hasOwn(def, 'output') ? 'value' : def.previousStatement;
    if (actual !== context) return fail(node, `“${blockCatalog.find(item => item.type === node.type).label}”不能放在此处；请连接到对应输入。`);
    if (context === 'value' && node.next) fail(node, '值积木不能包含后续语句。');
    const f = key => text(node, key);
    const c = key => child(node, key, depth);
    const pad = '    '.repeat(depth);
    let result;
    switch (node.type) {
      case 'oxy_number': result = fragment(numeric(node, 'VALUE')); break;
      case 'oxy_text': result = fragment(literal(f('VALUE'))); break;
      case 'oxy_bool': if (!['true', 'false'].includes(f('VALUE'))) fail(node, '布尔值必须为 true 或 false。'); result = fragment(f('VALUE')); break;
      case 'oxy_null': result = fragment('null'); break;
      case 'oxy_name': result = fragment(safe(node, 'NAME', 'path')); break;
      case 'oxy_list': result = fragment('[', comma(entries(node, 'ITEMS', 'ListItem', depth)), ']'); break;
      case 'oxy_list_item': result = c('VALUE'); break;
      case 'oxy_object': result = fragment('{ ', comma(entries(node, 'FIELDS', 'ObjectField', depth)), ' }'); break;
      case 'oxy_field': result = fragment(literal(f('KEY')), ': ', c('VALUE')); break;
      case 'oxy_def_ref': safe(node, 'TYPE'); if (!f('NAME').trim()) fail(node, 'Def 名称不能为空。'); result = fragment('def_ref(', literal(f('TYPE')), ', ', literal(f('NAME')), ')'); break;
      case 'oxy_binary': case 'oxy_compare': case 'oxy_logic': case 'oxy_math': {
        const sets = { oxy_compare: ['>', '>=', '<', '<=', '==', '!='], oxy_logic: ['&&', '||'], oxy_math: ['+', '-', '*', '/', '%'] };
        const allowed = sets[node.type] || ['>', '>=', '<', '<=', '==', '!=', '&&', '||', '+', '-', '*', '/', '%'];
        const op = f('OP');
        if (!allowed.includes(op)) fail(node, '未知运算符。');
        result = fragment('(', c('LEFT'), ' ', op, ' ', c('RIGHT'), ')'); break;
      }
      case 'oxy_not': result = fragment('(!', c('VALUE'), ')'); break;
      case 'oxy_member': result = fragment('(', c('OBJECT'), ').', safe(node, 'MEMBER')); break;
      case 'oxy_pawn': result = fragment(safe(node, 'PAWN'), '.', safe(node, 'MEMBER')); break;
      case 'oxy_pawn_query': result = fragment(safe(node, 'PAWN'), '.', safe(node, 'MEMBER')); break;
      case 'oxy_concat': result = fragment('(', c('LEFT'), ' + ', c('RIGHT'), ')'); break;
      case 'oxy_call': result = fragment(safe(node, 'NAME', 'path'), '(', comma(entries(node, 'ARGS', 'ListItem', depth)), ')'); break;
      case 'oxy_function': {
        const params = f('PARAMS').trim();
        const types = /^(?:int|float|number|bool|string|list|array|void|any|[\p{L}_][\p{L}\p{N}_]*)(?:\?)?$/u;
        for (const param of params ? params.split(',') : []) {
          const pieces = param.trim().split(':').map(item => item.trim());
          if (!isIdentifier(pieces[0]) || pieces.length > 2 || (pieces.length === 2 && !types.test(pieces[1]))) fail(node, '参数须为 name 或 name: type，以逗号分隔。');
        }
        const returnType = f('RETURN').trim();
        if (returnType && !types.test(returnType)) fail(node, '返回类型无效；可留空使用动态返回值。');
        const body = entries(node, 'BODY', 'Statement', depth);
        result = fragment(pad, 'fn ', safe(node, 'NAME'), '(', params, ')', returnType ? ': ' + returnType : '', ' {\n', ...body, pad, '}\n'); break;
      }
      case 'oxy_return': result = fragment(pad, 'return ', c('VALUE'), '\n'); break;
      case 'oxy_if': {
        const then = entries(node, 'THEN', 'Statement', depth);
        if (!then.length) fail(node, '条件分支需要至少一条语句。');
        const otherwise = entries(node, 'ELSE', 'Statement', depth);
        result = fragment(pad, 'if ', c('CONDITION'), ' {\n', ...then, pad, '}', ...(otherwise.length ? [' else {\n', ...otherwise, pad, '}'] : []), '\n'); break;
      }
      case 'oxy_let': case 'oxy_assign': result = fragment(pad, node.type === 'oxy_let' ? 'let ' : '', safe(node, 'NAME'), ' = ', c('VALUE'), '\n'); break;
      case 'oxy_do': if (node.inputs?.CALL?.block?.type !== 'oxy_call') fail(node, '执行积木需要连接函数调用。'); result = fragment(pad, c('CALL'), '\n'); break;
      case 'oxy_use': {
        const module = f('MODULE').trim(); const pkg = f('PACKAGE').trim(); const alias = f('ALIAS').trim();
        if (!isPath(module) || !/^[\p{L}\p{N}_-]+(?:\.[\p{L}\p{N}_-]+)*$/u.test(pkg)) fail(node, '模块路径或包名无效。');
        if (alias && !isIdentifier(alias)) fail(node, '导入别名无效。');
        result = fragment(pad, f('OPTIONAL') === 'true' ? 'optional ' : '', 'use ', module, '@', pkg, alias ? ' as ' + alias : '', '\n'); break;
      }
      case 'oxy_mood': case 'oxy_memory': {
        if (!f('ID').trim()) fail(node, '规则 ID 不能为空。');
        if (!['colonists', 'map', 'selected'].includes(f('TARGET'))) fail(node, '未知规则目标。');
        const memory = node.type === 'oxy_memory';
        const extra = [];
        if (memory) {
          const duration = numeric(node, 'DURATION'); const cooldown = numeric(node, 'COOLDOWN');
          if (!Number.isInteger(Number(duration)) || Number(duration) <= 0 || !Number.isInteger(Number(cooldown)) || Number(cooldown) < 0) fail(node, '持续 tick 必须为正整数，冷却 tick 必须为非负整数。');
          extra.push(', duration: ', duration, ', cooldown: ', cooldown, ', mode: "refresh"');
        }
        result = fragment(pad, memory ? 'memory(' : 'mood(', literal(f('ID')), ', label: ', literal(f('LABEL')), ', description: ', literal(f('DESCRIPTION')), ', offset: ', numeric(node, 'OFFSET'), ', target: ', literal(f('TARGET')), ...extra, ', when: ', c('WHEN'), ')\n'); break;
      }
      case 'oxy_thought_rule': result = fragment(pad, 'thought_rule(', literal(f('ID')), ', thought: ', c('THOUGHT'), ', evaluate: ', c('EVALUATE'), ')\n'); break;
      case 'oxy_gate_rule': result = fragment(pad, 'gate_rule(', literal(f('ID')), ', gate: ', literal(f('GATE')), ', evaluate: ', c('EVALUATE'), ')\n'); break;
      case 'oxy_template': result = fragment(pad, 'template(', literal(f('ID')), ', type: ', literal(safe(node, 'TYPE')), ', fields: ', c('FIELDS'), ')\n'); break;
      case 'oxy_define': case 'oxy_patch': result = fragment(pad, node.type === 'oxy_define' ? 'define(' : 'patch(', literal(safe(node, 'TYPE')), ', ', literal(f('NAME')), node.type === 'oxy_define' && f('EXTENDS') ? ', extends: ' + literal(f('EXTENDS')) : '', node.type === 'oxy_define' ? ', fields: ' : ', set: ', c('FIELDS'), ')\n'); break;
      case 'oxy_registry': result = fragment(pad, 'optional use irismenus@std\n', pad, 'if irismenus.available {\n', pad, '    irismenus.registry(pages: [', comma(entries(node, 'PAGES', 'Page', depth)), '])\n', pad, '}\n'); break;
      case 'oxy_page': result = fragment('{ id: ', literal(f('ID')), ', title: ', literal(f('TITLE')), ', scope: ', literal(f('SCOPE')), ', items: [', comma(entries(node, 'ITEMS', 'SettingItem', depth)), '] }'); break;
      case 'oxy_setting_label': result = fragment('{ id: ', literal(f('ID')), ', kind: ', literal(f('KIND')), ', label: ', literal(f('LABEL')), ' }'); break;
      case 'oxy_setting': {
        const extra = entries(node, 'EXTRA', 'ObjectField', depth);
        const used = new Set(['id', 'kind', 'label', 'key', 'default']);
        for (let item = node.inputs?.EXTRA?.block; item; item = item.next?.block) {
          const key = text(item, 'KEY'); if (used.has(key)) fail(item, '设置字段重复：' + key); used.add(key);
        }
        if (f('KIND') === 'select' && !used.has('options')) fail(node, '下拉控件必须添加 options 字段（包含 value、label 的对象列表）。');
        result = fragment('{ id: ', literal(f('ID')), ', kind: ', literal(f('KIND')), ', label: ', literal(f('LABEL')), ', key: ', literal(f('KEY')), ', default: ', c('DEFAULT'), ...(extra.length ? [', ', comma(extra)] : []), ' }'); break;
      }
      case 'oxy_setting_button': result = fragment('{ id: ', literal(f('ID')), ', kind: "button", label: ', literal(f('LABEL')), ', action: ', c('ACTION'), ' }'); break;
      default: return fail(node, '积木生成器未定义此类型。');
    }
    return marked(node, result);
  }
  try {
    if (!workspaceState || typeof workspaceState !== 'object' || Array.isArray(workspaceState)) return { source: '', ranges: [], errors: [{ message: 'workspace 必须是对象。' }] };
    const top = workspaceState.blocks?.blocks ?? [];
    if (!Array.isArray(top)) return { source: '', ranges: [], errors: [{ message: '积木列表必须是数组。' }] };
    const ordered = [...top].sort((a, b) => (Number(a.y) || 0) - (Number(b.y) || 0) || (Number(a.x) || 0) - (Number(b.x) || 0) || String(a.id).localeCompare(String(b.id), 'en'));
    const result = fragment(...ordered.flatMap(node => chain(node, 'Statement', 0)));
    return { ...result, errors };
  } catch (error) { return { source: '', ranges: [], errors: [{ message: '无法生成积木：' + error.message }] }; }
}

let templateId = 0;
function node(type, fields = {}, inputs = {}) { return { type, id: 'template_' + ++templateId, fields, inputs: Object.fromEntries(Object.entries(inputs).map(([name, block]) => [name, { block }])) }; }
function link(nodes) { for (let i = 0; i < nodes.length - 1; i++) nodes[i].next = { block: nodes[i + 1] }; return nodes[0]; }
const ref = name => node('oxy_name', { NAME: name });
const num = value => node('oxy_number', { VALUE: value });
const str = value => node('oxy_text', { VALUE: value });
const fields = entries => node('oxy_object', {}, { FIELDS: link(entries.map(([key, value]) => node('oxy_field', { KEY: key }, { VALUE: value }))) });
const list = values => node('oxy_list', {}, { ITEMS: link(values.map(value => node('oxy_list_item', {}, { VALUE: value }))) });
const templates = [
  { id: 'mood', label: '饱食心情规则', description: '带空值保护的 Pawn 食物条件与持续心情' },
  { id: 'native', label: '原生特质与思想', description: '声明 TraitDef、ThoughtDef 并修改已有字段' },
  { id: 'settings', label: 'IrisMenus 设置分页', description: '可选适配、存档开关和说明条目' },
  { id: 'prisoner', label: '囚犯动态命名规则', description: 'fn codename(pawn): string?，按种族、等级和编号拼接；这是新画布，不会把已有源码反解成积木' },
  { id: 'blank', label: '空白画布', description: '从积木工具箱开始' },
];
function createDocument(template = 'mood') {
  templateId = 0;
  let blocks = [];
  let path = 'rules.oxy';
  if (template === 'mood') {
    const pawnFood = () => node('oxy_pawn', { PAWN: 'pawn', MEMBER: 'food' });
    const condition = node('oxy_binary', { OP: '&&' }, {
      LEFT: node('oxy_binary', { OP: '!=' }, { LEFT: pawnFood(), RIGHT: node('oxy_null') }),
      RIGHT: node('oxy_binary', { OP: '>' }, { LEFT: pawnFood(), RIGHT: num(0.25) })
    });
    blocks = [link([
      node('oxy_function', { NAME: 'comfortable', PARAMS: 'pawn', RETURN: 'bool' }, { BODY: node('oxy_return', {}, { VALUE: condition }) }),
      node('oxy_mood', { ID: 'fed_comfort', LABEL: '吃饱了', DESCRIPTION: '我吃饱了。现在感觉不错。', OFFSET: 5, TARGET: 'colonists' }, { WHEN: ref('comfortable') })
    ])];
    path = 'mood.oxy';
  } else if (template === 'native') {
    blocks = [link([
      node('oxy_define', { TYPE: 'TraitDef', NAME: 'MyMod_Worker', EXTENDS: '' }, { FIELDS: fields([['commonality', num(0)], ['degreeDatas', list([fields([['degree', num(0)], ['label', str('勤快')], ['description', str('{PAWN_nameDef} 工作更快。')], ['statFactors', list([fields([['stat', node('oxy_def_ref', { TYPE: 'StatDef', NAME: 'WorkSpeedGlobal' })], ['value', num(1.15)]])])]])])]]) }),
      node('oxy_define', { TYPE: 'ThoughtDef', NAME: 'MyMod_Comfort', EXTENDS: '' }, { FIELDS: fields([['stages', list([fields([['label', str('舒适')], ['description', str('现在感觉不错。')], ['baseMoodEffect', num(5)]])])]]) }),
      node('oxy_patch', { TYPE: 'TraitDef', NAME: 'Industriousness' }, { FIELDS: fields([['commonality', num(1.5)]]) })
    ])];
    path = 'content.oxy';
  } else if (template === 'prisoner') {
    const pawn = member => node('oxy_pawn_query', { PAWN: 'pawn', MEMBER: member });
    const prefix = node('oxy_if', {}, {
      CONDITION: node('oxy_compare', { OP: '==' }, { LEFT: pawn('race'), RIGHT: str('Ratkin') }),
      THEN: node('oxy_assign', { NAME: 'kind' }, { VALUE: str('鼠') })
    });
    const alias = node('oxy_let', { NAME: 'alias' }, { VALUE: node('oxy_concat', {}, { LEFT: ref('kind'), RIGHT: pawn('number_text') }) });
    const grade = node('oxy_if', {}, {
      CONDITION: node('oxy_compare', { OP: '!=' }, { LEFT: pawn('grade'), RIGHT: str('') }),
      THEN: node('oxy_assign', { NAME: 'alias' }, { VALUE: node('oxy_concat', {}, { LEFT: node('oxy_concat', {}, { LEFT: ref('alias'), RIGHT: str('·') }), RIGHT: pawn('grade') }) })
    });
    const named = node('oxy_return', {}, { VALUE: node('oxy_concat', {}, { LEFT: node('oxy_concat', {}, { LEFT: ref('alias'), RIGHT: str('·') }), RIGHT: pawn('original_name') }) });
    blocks = [node('oxy_function', { NAME: 'codename', PARAMS: 'pawn', RETURN: 'string?' }, {
      BODY: link([
        node('oxy_let', { NAME: 'kind' }, { VALUE: str('囚') }),
        prefix,
        alias,
        grade,
        named
      ])
    })];
    path = 'Oxygen/naming.oxy';
  } else if (template === 'settings') {
    const checkbox = node('oxy_setting', { KIND: 'checkbox', ID: 'enabled', LABEL: '启用功能', KEY: 'enabled' }, { DEFAULT: node('oxy_bool', { VALUE: 'true' }) });
    blocks = [node('oxy_registry', {}, { PAGES: node('oxy_page', { ID: 'general', TITLE: '常规设置', SCOPE: 'game' }, { ITEMS: link([node('oxy_setting_label', { KIND: 'label', ID: 'intro', LABEL: '这些设置随当前存档保存。' }), checkbox]) }) })];
    path = 'settings.oxy';
  } else if (template !== 'blank') throw new Error('未知积木模板：' + template);
  for (const [i, block] of blocks.entries()) { block.x = 48; block.y = 48 + i * 240; }
  return { schemaVersion: 1, generatorVersion: 1, target: { path, mode: 'script' }, workspace: { blocks: { languageVersion: 0, blocks } } };
}

module.exports = { defineBlocks, toolbox, blockCatalog, generate, createDocument, templates };
