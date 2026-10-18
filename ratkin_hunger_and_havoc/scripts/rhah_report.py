#!/usr/bin/env python3
"""Read the last pipeline outcome without reopening its full logs."""
import argparse
import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('path', nargs='?', type=Path, help='A pipeline run.json; defaults to latest.json pointer')
    args = parser.parse_args(argv)
    try:
        path = args.path
        if path is None:
            latest = json.loads((ROOT / 'tmp/automation/latest.json').read_text(encoding='utf-8'))
            path = ROOT / latest['report']
        report = json.loads(path.read_text(encoding='utf-8'))
        summary = {key: report.get(key) for key in ('command', 'status', 'full_gate')}
        summary['report'] = str(path)
        summary['stages'] = [
            {key: stage.get(key) for key in ('name', 'status', 'exit_code', 'log')}
            for stage in report.get('stages', [])[:20]
        ]
        summary['artifacts'] = report.get('artifacts', [])
        print(json.dumps(summary, ensure_ascii=False, separators=(',', ':')))
        return 0
    except (OSError, KeyError, ValueError) as error:
        print(json.dumps({'error': str(error)[:500]}, ensure_ascii=False), file=sys.stderr)
        return 1


if __name__ == '__main__':
    raise SystemExit(main())
