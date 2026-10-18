#!/usr/bin/env python3
"""Project automation entrypoint; no dependencies beyond Python's standard library."""
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
PIPELINE_COMMANDS = {'doctor', 'check', 'build', 'test', 'ci', 'package', 'deploy'}


def main(argv=None):
    args = list(sys.argv[1:] if argv is None else argv)
    if not args or args[0] in {'help', '-h', '--help'}:
        print('RHAH automation: python3 scripts/rhah.py <command> [options]\n'
              '  doctor | check | build | test | ci | package | deploy\n'
              '  carbon <command>   RimCarbon14 status, tools, calls, events, scenarios\n'
              '  mcp                Compact project MCP server over stdio\n'
              '  logs [path]        Incremental project-filtered Player.log\n'
              '  report [run.json]   Last pipeline status and evidence paths\n'
              'Use <command> --help for its exact arguments. Full evidence: tmp/automation/')
        return 0
    command, *rest = args
    if command in PIPELINE_COMMANDS:
        from rhah_pipeline import main as run
        return run([command, *rest])
    if command == 'pipeline':
        from rhah_pipeline import main as run
        return run(rest)
    if command in {'carbon', 'mcp'}:
        from rhah_carbon import main as run
        return run(rest if command == 'carbon' else ['mcp', *rest])
    if command == 'logs':
        from rhah_logs import main as run
        return run(rest)
    if command == 'report':
        from rhah_report import main as run
        return run(rest)
    print(f'Unknown command: {command}. Use --help.', file=sys.stderr)
    return 2


if __name__ == '__main__':
    raise SystemExit(main())
