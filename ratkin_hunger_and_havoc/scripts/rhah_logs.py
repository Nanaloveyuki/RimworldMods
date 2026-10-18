#!/usr/bin/env python3
"""Incremental, bounded Player.log inspection with local evidence."""
import argparse
from collections import Counter
import hashlib
import json
import os
from pathlib import Path
import re
import sys
import time

ROOT = Path(__file__).resolve().parents[1]
DEFAULT_LOG = (Path(os.environ.get('USERPROFILE', str(Path.home()))) / 'AppData/LocalLow/Ludeon Studios/RimWorld by Ludeon Studios/Player.log'
               if os.name == 'nt' else Path('/mnt/c/Users/miaom/AppData/LocalLow/Ludeon Studios/RimWorld by Ludeon Studios/Player.log'))
PROJECT = re.compile(r'RHAH_|HungerAndHavoc|ratkin\.hungerandhavoc|Oxygendsl|Oxygen DSL', re.I)
SEVERITY = re.compile(r'exception|\berror\b|\bfailed\b|\bwarning\b|\bwarn\b', re.I)
STACK = re.compile(r'^\s|^at |^\[0x|^\(Filename:|^UnityEngine\.|^Verse\.Log|^\[Ref ')
SECRET = re.compile(r'(?i)([\"\']?(?:token|authorization|password)[\"\']?\s*[:=]\s*[\"\']?)([^\s\"\',}]+)')


def redact(text):
    text = re.sub(r'(?i)(bearer\s+)([^\s\"\',}]+)', r'\1[redacted]', text)
    return SECRET.sub(lambda match: match[1] + '[redacted]', text)


def records(text):
    current = []
    for line in text.splitlines():
        if current and line and not STACK.search(line):
            yield '\n'.join(current)
            current = []
        if line:
            current.append(line)
    if current:
        yield '\n'.join(current)


def summarize(text, *, all_records=False, match=None, limit=12):
    groups = Counter()
    hidden = Counter()
    totals = Counter()
    pattern = re.compile(match, re.I) if match else None
    for record in records(text):
        serious = bool(SEVERITY.search(record))
        owned = bool(PROJECT.search(record))
        totals['records'] += 1
        totals['diagnostics'] += int(serious)
        selected = all_records or (bool(pattern.search(record)) if pattern else owned)
        if not all_records and pattern is None and not serious and record.startswith(('Initializing new game with mods:', 'Loading game from file')):
            selected = False
        if not selected:
            hidden['otherDiagnostics' if serious else 'otherRecords'] += 1
            continue
        # 相同堆栈保留一份，引用号不参与分组
        clean = redact(record)
        clean = re.sub(r'\[Ref [0-9A-Fa-f]+\]', '[Ref ...]', clean)
        groups[clean] += 1
    ordered = sorted(groups.items(), key=lambda item: (not bool(SEVERITY.search(item[0])), -item[1]))
    return {
        'counts': dict(totals), 'filtered': dict(hidden),
        'uniqueSelected': len(groups), 'omittedGroups': max(0, len(groups) - limit),
        'items': [{'count': count, 'message': message[:700], 'omittedChars': max(0, len(message) - 700)}
                  for message, count in ordered[:limit]],
    }


def read_increment(path, cursor, *, reset=False, scan_bytes=262144):
    with path.open('rb') as stream:
        stat = os.fstat(stream.fileno())
        identity = f'{stat.st_dev}:{stat.st_ino}'
        previous = cursor.get('offset', 0)
        start = previous
        reason = 'incremental'
        anchor_start = max(0, previous - 64)
        stream.seek(anchor_start)
        anchor = hashlib.sha256(stream.read(max(0, previous - anchor_start))).hexdigest()
        if reset or cursor.get('identity') != identity or previous > stat.st_size or cursor.get('anchor') != anchor:
            reason = 'reset' if reset else ('initial' if not cursor else 'rotated')
            start = max(0, stat.st_size - scan_bytes)
        stream.seek(start)
        data = stream.read(scan_bytes)
        if start and reason != 'incremental':
            # 首次从日志尾部取样时舍弃不完整的第一行
            boundary = data.find(b'\n')
            if boundary >= 0:
                start += boundary + 1
                data = data[boundary + 1:]
        consumed = len(data)
        end = start + consumed
        if data and not data.endswith(b'\n') and b'\n' in data:
            consumed = data.rfind(b'\n') + 1
            data = data[:consumed]
            end = start + consumed
        elif data and not data.endswith(b'\n') and end == stat.st_size:
            data = b''
            end = start
        stream.seek(max(0, end - 64))
        next_anchor = hashlib.sha256(stream.read(end - max(0, end - 64))).hexdigest()
    return data, {
        'identity': identity, 'offset': end, 'anchor': next_anchor,
    }, {
        'mode': reason, 'startByte': start, 'endByte': end, 'snapshotBytes': stat.st_size,
        'remainingBytes': max(0, stat.st_size - end),
        'earlierBytesNotScanned': start if reason != 'incremental' else 0,
    }


def bounded_json(result, max_chars):
    while len(json.dumps(result, ensure_ascii=False)) > max_chars and result.get('items'):
        result['items'].pop()
        result['omittedGroups'] += 1
    rendered = json.dumps(result, ensure_ascii=False, separators=(',', ':'))
    if len(rendered) > max_chars:
        raise ValueError('Output metadata exceeds --max-chars; choose a larger budget')
    return rendered


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('path', nargs='?', type=Path, default=Path(os.environ.get('RHAH_PLAYER_LOG', DEFAULT_LOG)))
    parser.add_argument('--reset', action='store_true', help='Read a fresh bounded tail instead of the cursor')
    parser.add_argument('--all', action='store_true', dest='all_records', help='Include other mods and routine records')
    parser.add_argument('--match', help='Case-insensitive regex; overrides the project filter')
    parser.add_argument('--limit', type=int, default=12)
    parser.add_argument('--max-chars', type=int, default=6000)
    parser.add_argument('--scan-bytes', type=int, default=262144)
    args = parser.parse_args(argv)
    if not 1 <= args.limit <= 100 or not 1024 <= args.max_chars <= 12000 or not 1024 <= args.scan_bytes <= 1048576:
        parser.error('limit must be 1..100, max-chars 1024..12000, scan-bytes 1024..1048576')
    try:
        path = args.path.resolve(strict=True)
        evidence = ROOT / 'tmp/automation/logs'
        evidence.mkdir(parents=True, exist_ok=True)
        key = hashlib.sha256(str(path).encode()).hexdigest()[:16]
        state_path = evidence / f'{key}-cursor.json'
        cursor = json.loads(state_path.read_text()) if state_path.exists() else {}
        data, next_cursor, span = read_increment(path, cursor, reset=args.reset, scan_bytes=args.scan_bytes)
        text = data.decode('utf-8', errors='replace')
        result = summarize(text, all_records=args.all_records, match=args.match, limit=args.limit)
        receipt = evidence / f'{time.time_ns()}-{key}.log'
        receipt.write_text(redact(text), encoding='utf-8')
        result.update({'source': str(path), 'evidence': str(receipt.relative_to(ROOT)), 'scan': span})
        rendered = bounded_json(result, args.max_chars)
        state_path.write_text(json.dumps(next_cursor), encoding='utf-8')
        print(rendered)
        return 0
    except (OSError, ValueError, re.error) as error:
        print(json.dumps({'error': redact(str(error))[:500]}, ensure_ascii=False), file=sys.stderr)
        return 1


if __name__ == '__main__':
    raise SystemExit(main())
