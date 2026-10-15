#!/usr/bin/env python3
"""Bounded, evidence-preserving CLI and MCP adapter for the installed Carbon Agent."""
import argparse
import collections
import datetime
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import uuid
import urllib.error
import urllib.parse
import urllib.request
import xml.etree.ElementTree as ET

REPO = Path(__file__).resolve().parents[1]
DEFAULT_ROOT = r'E:\Apps\Steam\steamapps\common\RimWorld' if os.name == 'nt' else '/mnt/e/Apps/Steam/steamapps/common/RimWorld'
DEFAULT_PROFILE = r'E:\RHAHAutomation\RatkinHungerAndHavoc\profile' if os.name == 'nt' else '/mnt/e/RHAHAutomation/RatkinHungerAndHavoc/profile'
PROJECT_TERMS = ('rhah', 'hungerandhavoc', 'hunger and havoc', 'oxygendsl', 'oxygen dsl', 'odf')
REQUIRED_MODS = ('brrainz.harmony', 'ludeon.rimworld', 'erdelf.humanoidalienraces',
                 'solaris.ratkinracemod', 'nanaloveyuki.oxygendsl',
                 'nanaloveyuki.ratkin.hungerandhavoc', 'nanaloveyuki.rimcarbon14')
SECRET_KEYS = {'token', 'authorization', 'password', 'access_token', 'bearer', 'secret'}
CONTROL_KEYS = {'state', 'error', 'errors', 'bridgeError', 'operationId', 'requestId', 'id',
                'stamp', 'instanceId', 'session', 'cursor', 'nextCursor', 'lost', 'passed',
                'output', 'evidence', 'evidenceDirectory', 'screenshots', 'artifactId', 'artifactPath'}
READ_LOCAL = {'bridge_status', 'process_status', 'bridge_events'}
WRITE_LOCAL = {'process_start', 'process_stop', 'process_restart', 'profile_prepare', 'desktop_input'}


class CarbonError(Exception):
    def __init__(self, code, message, **details):
        super().__init__(message)
        self.data = {'error': code, 'message': message, **details}


def dumps(value):
    return json.dumps(value, ensure_ascii=False, separators=(',', ':'))


def is_wsl():
    if os.name == 'nt':
        return False
    if os.environ.get('WSL_INTEROP') or os.environ.get('WSL_DISTRO_NAME'):
        return True
    try:
        return 'microsoft' in Path('/proc/sys/kernel/osrelease').read_text().lower()
    except OSError:
        return False


def local_path(value):
    value = str(value)
    match = re.match(r'^([A-Za-z]):[\\/](.*)$', value)
    if match and os.name != 'nt':
        return Path('/mnt/' + match[1].lower() + '/' + match[2].replace('\\', '/'))
    return Path(value).expanduser().absolute()


def windows_path(value):
    value = str(value)
    if os.name == 'nt' or re.match(r'^[A-Za-z]:[\\/]', value) or value.startswith('\\\\'):
        return value
    path = str(local_path(value))
    match = re.match(r'^/mnt/([a-zA-Z])(?:/(.*))?$', path)
    if match:
        return match[1].upper() + ':\\' + (match[2] or '').replace('/', '\\')
    try:
        result = subprocess.run(['wslpath', '-w', path], capture_output=True, text=True, check=True)
    except (OSError, subprocess.CalledProcessError) as exc:
        raise CarbonError('path_not_windows_accessible', 'Use a mounted Windows drive for this path: ' + path) from exc
    return result.stdout.strip()


def redact(value, secrets=()):
    if isinstance(value, dict):
        return {k: ('[REDACTED]' if k.lower() in SECRET_KEYS else redact(v, secrets)) for k, v in value.items()}
    if isinstance(value, list):
        return [redact(v, secrets) for v in value]
    if isinstance(value, str):
        for secret in secrets:
            if secret:
                value = value.replace(secret, '[REDACTED]')
        value = re.sub(r'(?i)Bearer\s+[A-Za-z0-9._~+/=-]+', 'Bearer [REDACTED]', value)
        value = re.sub(r'(?i)("(?:token|access_token|authorization|password)"\s*:\s*")[^"]*(")', r'\1[REDACTED]\2', value)
    return value


def json_path(value, path):
    if path in ('', '$'):
        return value
    text = path.removeprefix('$.')
    tokens = re.findall(r'([^\.\[\]]+)|\[(\d+)\]', text)
    rebuilt = ''.join(('[' + index + ']') if index else ('.' if i else '') + key
                      for i, (key, index) in enumerate(tokens))
    if rebuilt != text:
        raise CarbonError('invalid_selection', 'Use dotted keys and numeric array indexes: ' + path)
    try:
        for key, index in tokens:
            value = value[int(index)] if index else value[key]
        return value
    except (KeyError, IndexError, TypeError) as exc:
        raise CarbonError('selection_not_found', path) from exc


def has_failure(value):
    if isinstance(value, dict):
        if value.get('state') in ('failed', 'cancelled') or value.get('passed') is False or value.get('isError') is True:
            return True
        if value.get('error') not in (None, '', False, {}, []):
            return True
        if value.get('bridgeReachable') is False:
            return True
        if isinstance(value.get('items'), list) and 'nextCursor' in value:
            return False
        return any(has_failure(v) for v in value.values())
    return isinstance(value, list) and any(has_failure(v) for v in value)


def diagnostics(value):
    important = []
    ordinary = []
    total = 0
    def walk(item, path):
        nonlocal total
        if isinstance(item, dict):
            control = {k: v for k, v in item.items() if k in CONTROL_KEYS}
            if control:
                total += 1
                target = important if any(k in control for k in ('error', 'errors', 'operationId', 'requestId', 'stamp', 'cursor', 'nextCursor', 'lost')) else ordinary
                if len(target) < 24:
                    target.append({'path': path, **control})
            for key, child in item.items():
                if isinstance(child, (dict, list)):
                    walk(child, path + '.' + key)
        elif isinstance(item, list):
            for index, child in enumerate(item):
                walk(child, path + '[' + str(index) + ']')
    walk(value, '$')
    root = [group for group in ordinary if group['path'] == '$']
    found = (root + important + [group for group in ordinary if group['path'] != '$'])[:24]
    return found, total - len(found)


def bounded(value, evidence, select=None, fields=None, limit=6144, depth=6, array_limit=12, string_limit=600):
    if not 1024 <= limit <= 1048576:
        raise CarbonError('invalid_limit', 'Output byte limit must be 1024..1048576.')
    original = redact(value)
    selected = json_path(original, select) if select else original
    if fields:
        selected = {field: json_path(selected, field) for field in fields}
    controls, omitted_controls = diagnostics(original)
    def project(item, remaining, array_cap, string_cap, counts):
        if isinstance(item, str):
            if len(item) > string_cap:
                counts['stringCharacters'] += len(item) - string_cap
                return item[:string_cap] + '…'
            return item
        if isinstance(item, (dict, list)) and remaining <= 0:
            counts['containers'] += 1
            return {'omitted': len(item), 'type': type(item).__name__}
        if isinstance(item, list):
            counts['arrayItems'] += max(0, len(item) - array_cap)
            return [project(v, remaining - 1, array_cap, string_cap, counts) for v in item[:array_cap]]
        if isinstance(item, dict):
            keys = sorted(item, key=lambda k: k not in CONTROL_KEYS)
            cap = max(16, array_cap)
            counts['objectFields'] += max(0, len(keys) - cap)
            return {k: project(item[k], remaining - 1, array_cap, string_cap, counts) for k in keys[:cap]}
        return item
    while True:
        counts = collections.Counter()
        result = {'result': project(selected, depth, array_limit, string_limit, counts),
                  'evidence': str(evidence)}
        if select or fields or counts:
            result['diagnostics'] = project(controls, 4, 24, min(256, string_limit), counts)
            counts['diagnosticGroups'] += omitted_controls
        result['omitted'] = dict(counts)
        if len(dumps(result).encode('utf-8')) <= limit:
            return result
        if array_limit > 1:
            array_limit = max(1, array_limit // 2)
        elif string_limit > 64:
            string_limit = max(64, string_limit // 2)
        elif depth > 1:
            depth -= 1
        else:
            minimal = []
            for group in controls[:8]:
                count = collections.Counter()
                minimal.append(project(group, 3, 2, 80, count))
            essential = {}
            for group in controls:
                for key in ('state', 'error', 'operationId', 'requestId', 'instanceId', 'stamp', 'cursor', 'nextCursor', 'lost'):
                    if key in group and key not in essential:
                        essential[key] = project(group[key], 2, 2, 80, collections.Counter())
            result = {'result': {'omitted': 'payload exceeds summary budget', **essential}, 'diagnostics': minimal,
                      'evidence': str(evidence), 'omitted': {'payload': True, 'diagnosticGroups': max(0, len(controls) - 8) + omitted_controls}}
            while result['diagnostics'] and len(dumps(result).encode('utf-8')) > limit:
                result['diagnostics'].pop()
                result['omitted']['diagnosticGroups'] += 1
            if len(dumps(result).encode('utf-8')) > limit:
                result['evidence'] = str(evidence)[-min(300, limit // 3):]
                result['omitted']['evidencePathCharacters'] = max(0, len(str(evidence)) - len(result['evidence']))
            return result


def filter_events(page, terms=PROJECT_TERMS, all_events=False):
    grouped = collections.OrderedDict()
    unrelated = collections.Counter()
    excluded = 0
    for item in page.get('items', []):
        text = dumps(item).lower()
        kind = item.get('kind', '')
        relevant = all_events or any(term.lower() in text for term in terms)
        data = item.get('data', {})
        if (not all_events and terms == PROJECT_TERMS and kind == 'game.log' and isinstance(data, dict) and
                str(data.get('level', '')).lower() == 'log' and
                str(data.get('message', '')).startswith(('Initializing new game with mods:', 'Loading game from file'))):
            relevant = False
        if not relevant:
            excluded += 1
            level = str(item.get('data', {}).get('level', '')).lower()
            if level in ('error', 'exception', 'assert') or 'error' in kind or 'failed' in text:
                unrelated[level or kind] += 1
            continue
        key = dumps({'kind': kind, 'data': {k: v for k, v in data.items() if k != 'thread'}}) if isinstance(data, dict) else dumps(data)
        if key not in grouped:
            grouped[key] = {**item, 'count': 0, 'firstSequence': item.get('sequence')}
        group = grouped[key]
        group['count'] += 1
        group['lastSequence'] = item.get('sequence')
    return {'items': list(grouped.values()), 'nextCursor': page.get('nextCursor'), 'lost': page.get('lost', 0),
            'received': len(page.get('items', [])), 'excluded': excluded,
            'otherModErrors': dict(unrelated), 'duplicates': sum(g['count'] - 1 for g in grouped.values())}


def event_page_failed(page):
    return not isinstance(page, dict) or not isinstance(page.get('items'), list) or not isinstance(page.get('nextCursor'), int)


class Adapter:
    def __init__(self, options):
        self.options = options
        self.root = local_path(options.game_root)
        self.profile = local_path(options.profile)
        self.agent = local_path(options.agent or self.root / 'Mods/RimCarbon14/Tools/win-x64/RimCarbon14.Agent.exe')
        self.evidence = local_path(options.evidence_dir)
        self.secrets = []
        discovery = self.profile / 'RimCarbon14/discovery.json'
        if discovery.is_file():
            try:
                token = json.loads(discovery.read_text(encoding='utf-8-sig')).get('token')
                if token:
                    self.secrets.append(token)
            except (OSError, ValueError):
                pass

    def save(self, value, label='response'):
        discovery = self.profile / 'RimCarbon14/discovery.json'
        if discovery.is_file():
            try:
                token = json.loads(discovery.read_text(encoding='utf-8-sig')).get('token')
                if token and token not in self.secrets:
                    self.secrets.append(token)
            except (OSError, ValueError):
                pass
        self.evidence.mkdir(parents=True, exist_ok=True)
        path = self.evidence / (datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%S%fZ') + '-' + uuid.uuid4().hex[:8] + '-' + label + '.json')
        path.write_text(json.dumps(redact(value, self.secrets), ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
        return path

    def read_tools(self):
        try:
            discovery_path = self.profile / 'RimCarbon14/discovery.json'
            if not discovery_path.is_file():
                raise CarbonError('bridge_offline', 'No discovery file in the isolated profile.')
            discovery = json.loads(discovery_path.read_text(encoding='utf-8-sig'))
            if type(discovery.get('protocolVersion')) is not int or discovery['protocolVersion'] != 1:
                raise CarbonError('protocol_mismatch', 'Expected Carbon bridge protocol 1.')
            endpoint = urllib.parse.urlsplit(discovery.get('endpoint', ''))
            try:
                port = endpoint.port
            except ValueError as exc:
                raise CarbonError('invalid_endpoint', 'Discovery endpoint has an invalid port.') from exc
            if (endpoint.scheme != 'http' or endpoint.hostname != '127.0.0.1' or not port or
                    endpoint.username is not None or endpoint.password is not None or
                    endpoint.path not in ('', '/') or endpoint.query or endpoint.fragment):
                raise CarbonError('invalid_endpoint', 'Discovery must be loopback HTTP with an explicit valid port and no credentials or extra URL parts.')
            token = discovery.get('token')
            if type(token) is not str or not token or any(char.isspace() for char in token):
                raise CarbonError('invalid_discovery', 'Discovery authentication token is missing or malformed.')
            if token not in self.secrets:
                self.secrets.append(token)
            if is_wsl():
                helper = REPO / 'scripts/carbon/read-tools.ps1'
                command = ['powershell.exe', '-NoLogo', '-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass',
                           '-File', windows_path(helper), '-DiscoveryFile', windows_path(discovery_path),
                           '-TimeoutMs', str(min(60000, max(1000, self.options.timeout_ms)))]
                process = subprocess.run(command, capture_output=True, text=True, encoding='utf-8', errors='replace',
                                         timeout=min(60, max(1, self.options.timeout_ms / 1000)) + 15)
                if process.returncode:
                    try:
                        error = json.loads(process.stderr.strip())
                    except ValueError:
                        error = {'error': 'windows_tools_discovery_failed', 'message': process.stderr.strip()}
                    raise CarbonError('windows_tools_discovery_failed', 'Windows loopback tool discovery failed.',
                                      response=redact(error, self.secrets), exitCode=process.returncode)
                payload = process.stdout.strip()
                transport = 'windows-authenticated-loopback-http'
            else:
                class NoRedirect(urllib.request.HTTPRedirectHandler):
                    def redirect_request(self, request, response, code, message, headers, new_url):
                        raise CarbonError('redirect_rejected', 'Carbon tool discovery must not redirect authentication.')
                opener = urllib.request.build_opener(urllib.request.ProxyHandler({}), NoRedirect())
                request = urllib.request.Request('http://127.0.0.1:' + str(port) + '/v1/tools',
                                                 headers={'Authorization': 'Bearer ' + token, 'Accept': 'application/json'})
                with opener.open(request, timeout=min(60, max(1, self.options.timeout_ms / 1000))) as response:
                    payload = response.read().decode('utf-8-sig')
                transport = 'authenticated-loopback-http'
            result = json.loads(payload)
            if not isinstance(result, list):
                raise CarbonError('invalid_tool_list', 'Bridge tools must return an array.')
            result = redact(result, self.secrets)
            return result, self.save({'transport': transport, 'response': result}, 'tools')
        except Exception as exc:
            result = exc.data if isinstance(exc, CarbonError) else {'error': type(exc).__name__, 'message': str(exc)}
            result = redact(result, self.secrets)
            return result, self.save(result, 'tools-error')

    def isolated(self):
        normalized = str(self.profile).replace('\\', '/').lower()
        if '/appdata/locallow/' in normalized or not (self.profile / '.rhah-carbon-profile.json').is_file():
            raise CarbonError('unsafe_profile', 'Mutations require a project-owned profile created by carbon prepare.', profile=str(self.profile))
        marker = json.loads((self.profile / '.rhah-carbon-profile.json').read_text(encoding='utf-8'))
        if marker.get('project') != 'nanaloveyuki.ratkin.hungerandhavoc' or marker.get('profile') != str(self.profile.resolve()):
            raise CarbonError('unsafe_profile', 'Profile marker does not match this directory.')

    def invoke(self, command, *positionals, arguments=None, extras=(), timeout_ms=None):
        timeout_ms = timeout_ms or self.options.timeout_ms
        argv = [str(self.agent), command, *positionals, '--profile', windows_path(self.profile),
                '--game-root', windows_path(self.root), '--timeout-ms', str(timeout_ms), *extras]
        if arguments is not None:
            argv += ['--args', dumps(arguments)]
        try:
            process = subprocess.run(argv, capture_output=True, text=True, encoding='utf-8', errors='replace',
                                     timeout=timeout_ms / 1000 + 30)
            stdout = redact(process.stdout.strip(), self.secrets)
            stderr = redact(process.stderr.strip(), self.secrets)
            try:
                result = json.loads(stdout) if stdout else json.loads(stderr)
            except ValueError:
                result = {'error': 'agent_invalid_json', 'message': 'Agent did not return one JSON document.', 'stdout': stdout, 'stderr': stderr}
            if process.returncode and not has_failure(result):
                result = {'error': 'agent_exit', 'exitCode': process.returncode, 'response': result, 'stderr': stderr}
            if isinstance(result, dict) and result.get('error') == 'operation_uncertain':
                match = re.search(r'Instance=([^,\s]+), operationId=([^\.\s]+)', result.get('message', ''))
                if match:
                    result['instanceId'], result['operationId'] = match.groups()
            discovery = self.profile / 'RimCarbon14/discovery.json'
            if discovery.is_file():
                try:
                    token = json.loads(discovery.read_text(encoding='utf-8-sig')).get('token')
                    if token and token not in self.secrets:
                        self.secrets.append(token)
                except (OSError, ValueError):
                    pass
            result = redact(result, self.secrets)
            evidence = self.save({'command': command, 'response': result, 'stderr': stderr, 'exitCode': process.returncode})
            return result, evidence
        except subprocess.TimeoutExpired as exc:
            result = {'error': 'operation_uncertain', 'message': 'Agent timed out; query operation before any mutation replay.',
                      'stdout': redact((exc.stdout or b'').decode('utf-8', 'replace') if isinstance(exc.stdout, bytes) else (exc.stdout or ''), self.secrets)}
            if command in ('call', 'run'):
                result['instanceId'] = None
                result['operationId'] = None
                result['recovery'] = 'No operation acknowledgement was captured. Inspect profile events; do not replay an uncertain write.'
        except OSError as exc:
            result = {'error': 'agent_unavailable', 'message': str(exc), 'agent': str(self.agent)}
        return result, self.save(result)

    def call(self, tool, arguments, timeout_ms=None):
        arguments = dict(arguments)
        if tool == 'bridge_call':
            return self.call(arguments.get('tool', ''), arguments.get('arguments', {}), arguments.get('timeoutMs', timeout_ms))
        if tool == 'profile_prepare':
            return self.prepare(arguments.get('source'), arguments.get('minimal', False))
        if tool in WRITE_LOCAL:
            self.isolated()
        elif tool not in READ_LOCAL and tool != 'bridge_operation':
            specs, _ = self.read_tools()
            if not isinstance(specs, list):
                if has_failure(specs):
                    return specs, self.save(specs, 'schema-error')
                raise CarbonError('invalid_tool_list', 'Agent tools must return an array.')
            if has_failure(specs):
                return specs, self.save(specs, 'schema-error')
            spec = next((s for s in specs if s.get('name') == tool), None)
            if spec is None:
                raise CarbonError('unknown_tool', tool)
            if spec.get('effect') != 'read':
                self.isolated()
        if tool == 'bridge_operation' and arguments.get('cancel'):
            self.isolated()
        result, evidence = self.invoke('call', tool, arguments=arguments, timeout_ms=timeout_ms)
        if isinstance(result, dict):
            data = result.get('result', {}).get('data', {})
            if isinstance(data, dict) and data.get('artifactId'):
                artifact = data['artifactId']
                instance = result.get('result', {}).get('stamp', {}).get('instanceId') or data.get('capturedAt', {}).get('instanceId')
                if isinstance(artifact, str) and Path(artifact).name == artifact and instance and re.fullmatch(r'[a-zA-Z0-9-]+', instance):
                    source = self.profile / 'RimCarbon14/artifacts' / instance / artifact
                    if source.is_file():
                        target = self.evidence / (uuid.uuid4().hex + '-' + artifact)
                        target.write_bytes(source.read_bytes())
                        data['artifactPath'] = str(target)
                        evidence = self.save(result, 'capture')
                    else:
                        data['artifactPath'] = str(source)
                        data['artifactUnavailable'] = True
                        evidence = self.save(result, 'capture')
        return result, evidence

    def tools(self, query=None, name=None):
        specs, evidence = self.read_tools()
        if not isinstance(specs, list):
            if has_failure(specs):
                return specs, evidence
            raise CarbonError('invalid_tool_list', 'Agent tools must return an array.')
        if has_failure(specs):
            return specs, evidence
        if name:
            spec = next((s for s in specs if s.get('name') == name), None)
            if spec is None:
                raise CarbonError('unknown_tool', name)
            return spec, evidence
        items = [{'name': s['name'], 'effect': s.get('effect')} for s in specs
                 if not query or query.lower() in dumps(s).lower()]
        return {'tools': items, 'count': len(items), 'query': query}, evidence

    def events(self, after=None, limit=100, pages=1, terms=PROJECT_TERMS, all_events=False):
        if not 1 <= limit <= 500 or not 1 <= pages <= 20:
            raise CarbonError('invalid_pagination', 'Event limit is 1..500, pages 1..20.')
        status, status_evidence = self.invoke('status')
        if has_failure(status):
            return status, status_evidence
        instance = status.get('instanceId')
        if not instance:
            raise CarbonError('missing_instance', 'Status did not identify the connected instance.')
        key = hashlib.sha256((str(self.profile) + ':' + instance).encode()).hexdigest()[:24]
        state_path = self.evidence / ('cursor-' + key + '.json')
        cursor = after if after is not None else (json.loads(state_path.read_text()).get('cursor', 0) if state_path.is_file() else 0)
        if not isinstance(cursor, int) or cursor < 0:
            raise CarbonError('invalid_cursor', 'Event cursor must be a nonnegative integer.')
        raw_pages = []
        for _ in range(pages):
            page, evidence = self.call('bridge_events', {'after': cursor, 'limit': limit})
            if event_page_failed(page):
                if has_failure(page):
                    return page, evidence
                return {'error': 'invalid_event_page', 'response': page}, evidence
            raw_pages.append(page)
            next_cursor = page.get('nextCursor', cursor)
            if next_cursor <= cursor or not page.get('items'):
                cursor = next_cursor
                break
            cursor = next_cursor
        final_status, final_evidence = self.invoke('status')
        if has_failure(final_status) or final_status.get('instanceId') != instance:
            return {'error': 'instance_changed', 'instanceId': instance, 'message': 'Events crossed a process instance; cursor was not committed.'}, final_evidence
        self.evidence.mkdir(parents=True, exist_ok=True)
        pending = state_path.with_suffix('.tmp')
        pending.write_text(dumps({'instanceId': instance, 'cursor': cursor}), encoding='utf-8')
        pending.replace(state_path)
        combined = {'items': [item for page in raw_pages for item in page.get('items', [])],
                    'nextCursor': cursor, 'lost': sum(page.get('lost', 0) for page in raw_pages)}
        evidence = self.save({'instanceId': instance, 'pages': raw_pages}, 'events')
        return {'instanceId': instance, **filter_events(combined, terms, all_events), 'pages': len(raw_pages)}, evidence

    def installed(self):
        mods = {}
        errors = []
        directories = [self.root / 'Data', self.root / 'Mods', self.root.parent.parent / 'workshop/content/294100']
        for directory in directories:
            if not directory.is_dir():
                continue
            for child in sorted(directory.iterdir()):
                about = next((child / case / 'About.xml' for case in ('About', 'about') if (child / case / 'About.xml').is_file()), None)
                if not about:
                    continue
                try:
                    document = ET.parse(about).getroot()
                    package = (document.findtext('packageId') or '').lower()
                    if package and package not in mods:
                        mods[package] = {'root': str(child), 'dependencies': [(e.findtext('packageId') or '').lower() for e in document.findall('modDependencies/li')],
                                         'after': [(e.text or '').lower() for e in document.findall('loadAfter/li')]}
                except (OSError, ET.ParseError) as exc:
                    errors.append({'file': str(about), 'message': str(exc)})
        return mods, errors

    def prepare(self, source=None, minimal=False):
        normalized = str(self.profile).replace('\\', '/').lower()
        if '/appdata/locallow/' in normalized or str(self.profile.resolve()) == str(self.root.resolve()):
            raise CarbonError('unsafe_profile', 'Choose a new isolated Windows-accessible test directory.')
        if self.profile.exists() and any(self.profile.iterdir()):
            raise CarbonError('profile_exists', 'Existing profiles are never changed. Choose a new --profile directory.')
        if source and local_path(source).resolve() == self.profile.resolve():
            raise CarbonError('unsafe_source', 'Source and test profile must differ.')
        converted_profile = windows_path(self.profile)
        if not converted_profile:
            raise CarbonError('unsafe_profile', 'Profile must be Windows-accessible.')
        if not source and not minimal:
            minimal = True
        installed, scan_errors = self.installed()
        wanted = set(REQUIRED_MODS)
        if source and not minimal:
            source_config = ET.parse(local_path(source) / 'Config/ModsConfig.xml').getroot()
            wanted.update((node.text or '').lower() for node in source_config.findall('activeMods/li'))
        queue = list(wanted)
        while queue:
            package = queue.pop()
            if package not in installed:
                raise CarbonError('missing_dependency', 'Required installed package not found: ' + package, scanErrors=scan_errors)
            for dep in installed[package]['dependencies']:
                if dep and dep not in wanted:
                    wanted.add(dep)
                    queue.append(dep)
        ordered = []
        remaining = set(wanted)
        while remaining:
            ready = [p for p in remaining if not ((set(installed[p]['dependencies']) | set(installed[p]['after'])) & remaining)]
            if not ready:
                raise CarbonError('dependency_cycle', 'Installed dependency/loadAfter graph has a cycle.', packages=sorted(remaining))
            for package in sorted(ready, key=lambda p: (p != 'brrainz.harmony', not p.startswith('ludeon.'), p)):
                ordered.append(package)
                remaining.remove(package)
        extras = ['--minimal'] if minimal else []
        if source:
            extras += ['--source', windows_path(source)]
        result, evidence = self.invoke('prepare', extras=extras)
        if has_failure(result):
            return result, evidence
        config_path = self.profile / 'Config/ModsConfig.xml'
        document = ET.parse(config_path)
        active = document.getroot().find('activeMods')
        if active is None:
            raise CarbonError('invalid_mod_config', 'Agent-created ModsConfig lacks activeMods.')
        active.clear()
        for package in ordered:
            ET.SubElement(active, 'li').text = package
        document.write(config_path, encoding='utf-8', xml_declaration=True)
        marker = {'project': 'nanaloveyuki.ratkin.hungerandhavoc', 'profile': str(self.profile.resolve()), 'mods': ordered}
        (self.profile / '.rhah-carbon-profile.json').write_text(dumps(marker) + '\n', encoding='utf-8')
        result = {**result, 'mods': ordered, 'installed': {p: installed[p]['root'] for p in ordered}, 'scanErrors': scan_errors}
        return result, self.save(result, 'prepare')

    def run(self, scenario):
        self.isolated()
        scenario_path = local_path(scenario)
        document = json.loads(scenario_path.read_text(encoding='utf-8'))
        if document.get('recovery', {}).get('maxCrashRestarts', 0):
            raise CarbonError('replay_disabled', 'Adapter does not automatically replay scenario mutations.')
        result, evidence = self.invoke('run', windows_path(scenario_path), timeout_ms=max(self.options.timeout_ms, 3600000))
        if isinstance(result, dict) and result.get('output'):
            output = local_path(result['output'])
            allowed = (self.profile / 'RimCarbon14/runs').resolve()
            if not output.resolve().is_relative_to(allowed) or not output.is_dir():
                raise CarbonError('invalid_run_evidence', 'Agent output must be an existing run below the isolated profile.', output=str(output))
            archive = self.evidence / ('scenario-' + uuid.uuid4().hex)
            archive.mkdir(parents=True)
            files = []
            for path in sorted(output.rglob('*')):
                if not path.is_file():
                    continue
                relative = path.relative_to(output)
                target = archive / relative
                target.parent.mkdir(parents=True, exist_ok=True)
                if path.suffix.lower() == '.png':
                    target.write_bytes(path.read_bytes())
                elif path.suffix.lower() in ('.json', '.xml', '.log'):
                    target.write_text(redact(path.read_text(encoding='utf-8-sig', errors='replace'), self.secrets), encoding='utf-8')
                else:
                    continue
                files.append(str(target))
            result = {**result, 'scenario': document.get('name', scenario_path.name), 'evidenceDirectory': str(archive),
                      'files': files, 'screenshots': [f for f in files if f.endswith('.png')]}
            evidence = self.save(result, 'scenario-summary')
        return result, evidence

    def doctor(self):
        mods, scan_errors = self.installed()
        missing = [p for p in REQUIRED_MODS if p not in mods]
        result = {'agent': str(self.agent), 'agentExists': self.agent.is_file(), 'profile': str(self.profile),
                  'profilePrepared': (self.profile / '.rhah-carbon-profile.json').is_file(), 'gameRoot': str(self.root),
                  'gameExists': (self.root / 'RimWorldWin64.exe').is_file(),
                  'sourceDirectory': self.options.source_dir, 'sourceExists': local_path(self.options.source_dir).is_dir(),
                  'missingDependencies': missing, 'installed': {p: mods[p]['root'] for p in REQUIRED_MODS if p in mods},
                  'scanErrors': scan_errors}
        result['passed'] = result['agentExists'] and result['gameExists'] and not missing
        return result, self.save(result, 'doctor')


def tool_schema(name, description, properties=None, required=(), readonly=True):
    return {'name': name, 'description': description,
            'inputSchema': {'type': 'object', 'properties': properties or {}, 'required': list(required), 'additionalProperties': False},
            'annotations': {'readOnlyHint': readonly, 'destructiveHint': not readonly, 'openWorldHint': False}}


PROJECTION = {'select': {'type': 'string'}, 'fields': {'type': 'array', 'items': {'type': 'string'}},
              'limit': {'type': 'integer', 'minimum': 1024, 'maximum': 1048576}}
MCP_TOOLS = [
    tool_schema('carbon_status', 'Read owned process, bridge reachability and heartbeat.'),
    tool_schema('carbon_tools', 'List names/effects with optional query, or retrieve ONE exact game tool schema.',
                {'query': {'type': 'string'}, 'name': {'type': 'string'}, **PROJECTION}),
    tool_schema('carbon_call', 'Call an exact Carbon tool once; never retry writes. Lifecycle tools require prepared project profile.',
                {'tool': {'type': 'string'}, 'arguments': {'type': 'object'}, 'timeoutMs': {'type': 'integer'}, **PROJECTION}, ('tool',), False),
    tool_schema('carbon_events', 'Read bounded incremental project events, aggregate duplicates, report other-mod errors and lost events.',
                {'after': {'type': 'integer'}, 'limit': {'type': 'integer', 'minimum': 1, 'maximum': 500},
                 'pages': {'type': 'integer', 'minimum': 1, 'maximum': 20}, 'all': {'type': 'boolean'}, 'filter': {'type': 'string'}}),
    tool_schema('carbon_operation', 'Look up an uncertain operation; specify original instanceId. Cancellation is explicit.',
                {'id': {'type': 'string'}, 'instanceId': {'type': 'string'}, 'cancel': {'type': 'boolean'}, **PROJECTION}, ('id', 'instanceId'), False),
    tool_schema('carbon_run', 'Execute real Agent scenario assertions and retain unique evidence/PNG paths. No recovery replay.',
                {'scenario': {'type': 'string'}, **PROJECTION}, (), False),
    tool_schema('carbon_doctor', 'Inspect installed executable, dependency packages and isolated profile without launching the game.')
]


def validate_mcp_arguments(spec, args):
    schema = spec['inputSchema']
    if not isinstance(args, dict) or set(args) - set(schema['properties']) or any(k not in args for k in schema['required']):
        raise CarbonError('invalid_arguments', 'Unexpected or missing MCP arguments.')
    types = {'string': str, 'integer': int, 'boolean': bool, 'object': dict, 'array': list}
    for key, value in args.items():
        prop = schema['properties'][key]
        expected = types[prop['type']]
        if type(value) is not expected:
            raise CarbonError('invalid_arguments', key + ' must be ' + prop['type'])
        if expected is int and (value < prop.get('minimum', 0) or value > prop.get('maximum', 3600000)):
            raise CarbonError('invalid_arguments', key + ' is outside its supported range.')
        if expected is list and any(type(item) is not str for item in value):
            raise CarbonError('invalid_arguments', key + ' must contain only strings.')


def operation(adapter, identifier, instance=None, cancel=False):
    status, evidence = adapter.invoke('status')
    if has_failure(status):
        return status, evidence
    if instance and status.get('instanceId') != instance:
        return {'error': 'instance_mismatch', 'instanceId': status.get('instanceId'), 'operationId': identifier,
                'message': 'Old-instance operations cannot be recovered in a new game process.'}, evidence
    return adapter.call('bridge_operation', {'id': identifier, 'cancel': cancel})


def serve_mcp(adapter, reader=None, writer=None):
    reader = reader or sys.stdin
    writer = writer or sys.stdout
    for line in reader:
        request = None
        try:
            request = json.loads(line)
            if not isinstance(request, dict) or request.get('jsonrpc') != '2.0' or not isinstance(request.get('method'), str):
                raise CarbonError('invalid_request', 'Expected a JSON-RPC 2.0 request object.')
            if 'id' not in request:
                continue
            method = request['method']
            params = request.get('params') or {}
            if not isinstance(params, dict):
                raise CarbonError('invalid_request', 'params must be an object.')
            if method == 'initialize':
                supported = ('2024-11-05', '2025-03-26', '2025-06-18')
                requested = params.get('protocolVersion')
                version = requested if requested in supported else supported[-1]
                result = {'protocolVersion': version, 'capabilities': {'tools': {}},
                          'serverInfo': {'name': 'rhah-carbon', 'version': '1.0.0'}}
            elif method == 'ping':
                result = {}
            elif method == 'tools/list':
                result = {'tools': MCP_TOOLS}
            elif method == 'tools/call':
                name = params.get('name')
                args = params.get('arguments') or {}
                spec = next((t for t in MCP_TOOLS if t['name'] == name), None)
                if spec is None:
                    raise CarbonError('unknown_tool', str(name))
                try:
                    validate_mcp_arguments(spec, args)
                    if name == 'carbon_status':
                        value, evidence = adapter.call('process_status', {})
                    elif name == 'carbon_doctor':
                        value, evidence = adapter.doctor()
                    elif name == 'carbon_tools':
                        value, evidence = adapter.tools(args.get('query'), args.get('name'))
                    elif name == 'carbon_call':
                        value, evidence = adapter.call(args['tool'], args.get('arguments', {}), args.get('timeoutMs'))
                    elif name == 'carbon_events':
                        value, evidence = adapter.events(args.get('after'), args.get('limit', 100), args.get('pages', 1),
                                                         (args['filter'],) if args.get('filter') else PROJECT_TERMS, args.get('all', False))
                    elif name == 'carbon_operation':
                        value, evidence = operation(adapter, args['id'], args['instanceId'], args.get('cancel', False))
                    else:
                        value, evidence = adapter.run(args.get('scenario', REPO / 'scripts/carbon/rhah-smoke.json'))
                    summary = bounded(value, evidence, args.get('select'), args.get('fields'),
                                      args.get('limit', 6144) if name != 'carbon_events' else 6144)
                except Exception as exc:
                    value = exc.data if isinstance(exc, CarbonError) else {'error': type(exc).__name__, 'message': str(exc)}
                    value = redact(value, adapter.secrets)
                    evidence = adapter.save(value, 'mcp-error')
                    summary = bounded(value, evidence)
                result = {'content': [{'type': 'text', 'text': dumps(summary)}], 'isError': has_failure(value)}
            else:
                response = {'jsonrpc': '2.0', 'id': request['id'], 'error': {'code': -32601, 'message': 'Method not found'}}
                writer.write(dumps(response) + '\n')
                writer.flush()
                continue
            response = {'jsonrpc': '2.0', 'id': request['id'], 'result': result}
        except Exception as exc:
            code = -32700 if isinstance(exc, json.JSONDecodeError) else -32600
            response = {'jsonrpc': '2.0', 'id': request.get('id') if isinstance(request, dict) else None,
                        'error': {'code': code, 'message': str(redact(str(exc), adapter.secrets))[:400]}}
        writer.write(dumps(response) + '\n')
        writer.flush()


def parser():
    common = argparse.ArgumentParser(add_help=False)
    common.add_argument('--profile', default=os.environ.get('CARBON_PROFILE', DEFAULT_PROFILE))
    common.add_argument('--game-root', default=os.environ.get('RIMWORLD_DIR', os.environ.get('RimWorldDir', DEFAULT_ROOT)))
    common.add_argument('--agent', default=os.environ.get('CARBON_AGENT'))
    common.add_argument('--source-dir', default=os.environ.get('CARBON_SOURCE_DIR', '/root/repos/csharp/RimCarbon14'))
    common.add_argument('--evidence-dir', default=str(REPO / 'tmp/automation/carbon'))
    common.add_argument('--timeout-ms', type=int, default=10000)
    common.add_argument('--select')
    common.add_argument('--fields', help='Comma-separated dotted paths relative to selected result')
    common.add_argument('--output-limit', type=int, default=6144)
    common.add_argument('--depth', type=int, default=6)
    common.add_argument('--array-limit', type=int, default=12)
    common.add_argument('--string-limit', type=int, default=600)
    root = argparse.ArgumentParser(description=__doc__)
    sub = root.add_subparsers(dest='command', required=True)
    for name in ('doctor', 'status', 'start', 'mcp', 'config'):
        sub.add_parser(name, parents=[common])
    stop = sub.add_parser('stop', parents=[common])
    stop.add_argument('--force', action='store_true')
    tools = sub.add_parser('tools', parents=[common])
    tools.add_argument('--query')
    tools.add_argument('--name')
    call = sub.add_parser('call', parents=[common])
    call.add_argument('tool')
    arguments = call.add_mutually_exclusive_group()
    arguments.add_argument('--args', default='{}')
    arguments.add_argument('--args-file')
    events = sub.add_parser('events', parents=[common])
    events.add_argument('--after', type=int)
    events.add_argument('--limit', type=int, default=100)
    events.add_argument('--pages', type=int, default=1)
    events.add_argument('--all', action='store_true')
    events.add_argument('--filter')
    op = sub.add_parser('operation', parents=[common])
    op.add_argument('id')
    op.add_argument('--instance')
    op.add_argument('--cancel', action='store_true')
    prepare = sub.add_parser('prepare', parents=[common])
    prepare.add_argument('--source')
    prepare.add_argument('--minimal', action='store_true')
    run = sub.add_parser('run', parents=[common])
    run.add_argument('scenario', nargs='?', default=str(REPO / 'scripts/carbon/rhah-smoke.json'))
    return root


def main(argv=None):
    options = parser().parse_args(argv)
    adapter = Adapter(options)
    if options.command == 'mcp':
        serve_mcp(adapter)
        return 0
    if options.command == 'config':
        print(dumps({'mcpServers': {'rhah-carbon': {'command': sys.executable,
              'args': [str(REPO / 'scripts/rhah.py'), 'carbon', 'mcp', '--profile', str(adapter.profile),
                       '--game-root', str(adapter.root), '--evidence-dir', str(adapter.evidence),
                       '--agent', str(adapter.agent)]}}}))
        return 0
    try:
        if options.timeout_ms <= 0 or options.depth < 1 or options.array_limit < 1 or options.string_limit < 1:
            raise CarbonError('invalid_limit', 'Timeout, depth, array and string limits must be positive.')
        command = options.command
        if command == 'doctor':
            value, evidence = adapter.doctor()
        elif command == 'status':
            value, evidence = adapter.call('process_status', {})
        elif command == 'tools':
            value, evidence = adapter.tools(options.query, options.name)
        elif command == 'call':
            arguments = json.loads(local_path(options.args_file).read_text(encoding='utf-8-sig') if options.args_file else options.args)
            if not isinstance(arguments, dict):
                raise CarbonError('invalid_arguments', 'Tool arguments must be a JSON object.')
            value, evidence = adapter.call(options.tool, arguments, options.timeout_ms)
        elif command == 'events':
            value, evidence = adapter.events(options.after, options.limit, options.pages,
                                             (options.filter,) if options.filter else PROJECT_TERMS, options.all)
        elif command == 'operation':
            value, evidence = operation(adapter, options.id, options.instance, options.cancel)
        elif command == 'prepare':
            value, evidence = adapter.prepare(options.source, options.minimal)
        elif command == 'run':
            value, evidence = adapter.run(options.scenario)
        elif command == 'start':
            value, evidence = adapter.call('process_start', {'timeoutMs': max(options.timeout_ms, 600000)}, max(options.timeout_ms, 600000))
        else:
            value, evidence = adapter.call('process_stop', {'force': options.force}, max(options.timeout_ms, 30000))
        summary = bounded(value, evidence, options.select, options.fields.split(',') if options.fields else None,
                          options.output_limit, options.depth, options.array_limit, options.string_limit)
    except Exception as exc:
        value = exc.data if isinstance(exc, CarbonError) else {'error': type(exc).__name__, 'message': str(exc)}
        value = redact(value, adapter.secrets)
        evidence = adapter.save(value, 'error')
        summary = bounded(value, evidence)
    print(dumps(summary))
    return 1 if has_failure(value) else 0


if __name__ == '__main__':
    sys.exit(main())
