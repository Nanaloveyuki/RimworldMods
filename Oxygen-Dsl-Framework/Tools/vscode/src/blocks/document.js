'use strict';

const path = require('node:path');
const { createHash } = require('node:crypto');

function parseDocument(text) {
  let value;
  try { value = JSON.parse(text); }
  catch (error) { throw new Error(`积木文件不是有效 JSON：${error.message}。请使用“重新打开编辑器方式 → 文本编辑器”修复。`); }
  if (!value || typeof value !== 'object' || Array.isArray(value)) throw new Error('积木文件必须是 JSON 对象。');
  if (value.schemaVersion !== 1 || value.generatorVersion !== 1) throw new Error('不支持此积木文件版本；当前 schemaVersion 和 generatorVersion 必须为 1。');
  if (!value.target || !['script', 'module'].includes(value.target.mode)) throw new Error('target.mode 必须为 script 或 module。');
  relativeTarget(value.target.path);
  if (!value.workspace || typeof value.workspace !== 'object' || Array.isArray(value.workspace)) throw new Error('workspace 必须是 Blockly 序列化对象。');
  if (value.workspace.blocks && (!Array.isArray(value.workspace.blocks.blocks) || value.workspace.blocks.languageVersion !== 0)) throw new Error('workspace.blocks 必须包含 languageVersion: 0 和 blocks 数组。');
  return value;
}

function relativeTarget(value) {
  if (typeof value !== 'string' || !value.trim() || value !== value.trim()) throw new Error('请填写相对于积木文件的 .oxy 输出路径。');
  const normalized = value.replace(/\\/g, '/');
  if (normalized.startsWith('/') || /^[a-zA-Z]:/.test(normalized) || normalized.includes(':') || /[\u0000-\u001f]/.test(normalized)) throw new Error('输出路径必须是相对路径；另存副本可选择其他目录。');
  const segments = normalized.split('/');
  if (segments.some(part => !part || part === '.' || part === '..') || path.posix.extname(normalized).toLowerCase() !== '.oxy') throw new Error('输出路径必须以 .oxy 结尾，不能包含空目录、. 或 ..。');
  return normalized;
}

function digest(bytes) { return createHash('sha256').update(bytes).digest('hex'); }

function exportMatches(record, uri, bytes) {
  return !!record && record.path === uri && typeof record.sha256 === 'string' && record.sha256 === digest(bytes);
}

function locateBlock(ranges, start) {
  return ranges.filter(range => range.start <= start && start < range.start + range.length)
    .sort((a, b) => a.length - b.length)[0]?.blockId;
}

module.exports = { parseDocument, relativeTarget, digest, exportMatches, locateBlock };
