'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const { parseDocument, relativeTarget, digest, exportMatches, locateBlock } = require('../src/blocks/document');

const root = () => ({ schemaVersion: 1, generatorVersion: 1, target: { path: 'Oxygen/rules.oxy', mode: 'script' }, workspace: { blocks: { languageVersion: 0, blocks: [] } } });

test('rejects unsupported versions and malformed workspace instead of replacing data', () => {
  assert.throws(() => parseDocument('{'), /JSON/);
  assert.throws(() => parseDocument(JSON.stringify({ ...root(), schemaVersion: 2 })), /版本/);
  assert.throws(() => parseDocument(JSON.stringify({ ...root(), generatorVersion: 2 })), /版本/);
  assert.throws(() => parseDocument(JSON.stringify({ ...root(), workspace: { blocks: { languageVersion: 0, blocks: {} } } })), /数组/);
});

test('normal target cannot escape its directory or overwrite other file kinds', () => {
  for (const path of ['../rules.oxy', 'Oxygen/../../rules.oxy', '/tmp/rules.oxy', 'C:\\Mods\\rules.oxy', 'rule.oxyblocks', 'Oxygen//rules.oxy', 'Oxygen/./rules.oxy', 'rules.oxy\n']) {
    assert.throws(() => relativeTarget(path), path);
  }
  assert.equal(relativeTarget('Oxygen\\rules.oxy'), 'Oxygen/rules.oxy');
});

test('only the exact previous export content and URI can be replaced without confirmation', () => {
  const text = Buffer.from('fn rule() { return true }\n');
  const record = { path: 'file:///mods/rules.oxy', sha256: digest(text) };
  assert.equal(exportMatches(record, record.path, text), true);
  assert.equal(exportMatches(record, record.path, Buffer.from('fn rule() { return false }\n')), false);
  assert.equal(exportMatches(record, 'file:///mods/copy.oxy', text), false);
  assert.equal(exportMatches(undefined, record.path, text), false);
});

test('diagnostics map to the smallest containing block span with end-exclusive boundaries', () => {
  const ranges = [{ blockId: 'function', start: 0, length: 80 }, { blockId: 'return', start: 20, length: 40 }, { blockId: 'name', start: 27, length: 7 }];
  assert.equal(locateBlock(ranges, 28), 'name');
  assert.equal(locateBlock(ranges, 34), 'return');
  assert.equal(locateBlock(ranges, 80), undefined);
});
