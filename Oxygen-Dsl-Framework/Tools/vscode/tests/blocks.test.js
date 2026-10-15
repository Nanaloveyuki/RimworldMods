'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const { generate, createDocument } = require('../src/blocks/library');
const b = (id, type, fields = {}, inputs = {}) => ({ id, type, fields, inputs: Object.fromEntries(Object.entries(inputs).map(([key, block]) => [key, { block }])) });
const workspace = (...blocks) => ({ blocks: { languageVersion: 0, blocks } });

test('nested operations retain their semantics regardless of operator precedence', () => {
  const number = (id, value) => b(id, 'oxy_number', { VALUE: value });
  const sum = b('sum', 'oxy_binary', { OP: '+' }, { LEFT: number('a', 1), RIGHT: number('b', 2) });
  const product = b('product', 'oxy_binary', { OP: '*' }, { LEFT: sum, RIGHT: number('c', 3) });
  const result = generate(workspace(b('return', 'oxy_return', {}, { VALUE: product })));
  assert.equal(result.source, 'return ((1 + 2) * 3)\n');
  assert.deepEqual(result.errors, []);
});

test('string data cannot break out of a literal and control escapes are Oxygen compatible', () => {
  const text = '\"\\\n\r\t\b\f\0中文😀';
  const result = generate(workspace(b('let', 'oxy_let', { NAME: 'text' }, { VALUE: b('value', 'oxy_text', { VALUE: text }) })));
  assert.equal(result.source, 'let text = "\\\"\\\\\\n\\r\\t\\u0008\\u000c\\u0000中文😀"\n');
  const span = result.ranges.find(item => item.blockId === 'value');
  assert.equal(result.source.slice(span.start, span.start + span.length), result.source.slice(11, -1));
});

test('literal backslash b and f sequences remain data rather than control characters', () => {
  const result = generate(workspace(b('let', 'oxy_let', { NAME: 'text' }, { VALUE: b('value', 'oxy_text', { VALUE: '\\bar\\foo' }) })));
  assert.equal(result.source, 'let text = "\\\\bar\\\\foo"\n');
});

test('missing sockets and detached expressions are rejected instead of receiving invented values', () => {
  assert.match(generate(workspace(b('return', 'oxy_return'))).errors[0].message, /连接/);
  assert.match(generate(workspace(b('number', 'oxy_number', { VALUE: 12 }))).errors[0].message, /不能放在/);
  assert.match(generate(workspace(b('unknown', 'raw_oxygen'))).errors[0].message, /不支持/);
});

test('duplicate IDs and injected identifiers do not silently generate source', () => {
  const first = b('same', 'oxy_let', { NAME: 'a' }, { VALUE: b('number', 'oxy_number', { VALUE: 1 }) });
  const second = b('same', 'oxy_return', {}, { VALUE: b('ref', 'oxy_name', { NAME: 'a' }) });
  assert.match(generate(workspace(first, second)).errors[0].message, /重复/);
  assert.match(generate(workspace(b('let', 'oxy_let', { NAME: 'x; evil()' }, { VALUE: b('n', 'oxy_number', { VALUE: 1 }) }))).errors[0].message, /标识符/);
});

test('visual declaration order is deterministic and disabled chains retain enabled siblings', () => {
  const a = b('a', 'oxy_return', {}, { VALUE: b('n1', 'oxy_number', { VALUE: 1 }) }); a.y = 20;
  const c = b('c', 'oxy_return', {}, { VALUE: b('n3', 'oxy_number', { VALUE: 3 }) }); c.y = 40;
  const disabled = b('b', 'oxy_return'); disabled.disabledReasons = ['MANUALLY_DISABLED']; disabled.next = { block: c };
  const result = generate(workspace(disabled, a));
  assert.equal(result.source, 'return 3\nreturn 1\n');
  assert.deepEqual(result.errors, []);
});

test('settings select requires options and prevents duplicate reserved setting fields', () => {
  const setting = b('setting', 'oxy_setting', { KIND: 'select', ID: 's', KEY: 's', LABEL: '选项' }, { DEFAULT: b('default', 'oxy_text', { VALUE: 'a' }) });
  const page = b('page', 'oxy_page', { ID: 'p', TITLE: '设置', SCOPE: 'global' }, { ITEMS: setting });
  const registry = b('registry', 'oxy_registry', {}, { PAGES: page });
  assert.match(generate(workspace(registry)).errors[0].message, /options/);
  setting.inputs.EXTRA = { block: b('extra', 'oxy_field', { KEY: 'id' }, { VALUE: b('text', 'oxy_text', { VALUE: 'override' }) }) };
  assert.ok(generate(workspace(registry)).errors.some(item => /字段重复/.test(item.message)));
});
