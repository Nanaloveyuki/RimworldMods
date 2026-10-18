"""Offline behavior contracts; transport substitutions never stand in for game smoke."""
import contextlib
import importlib.util
import io
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest import mock

MODULE_PATH = Path(__file__).resolve().parents[1] / 'rhah_carbon.py'
spec = importlib.util.spec_from_file_location('rhah_carbon', MODULE_PATH)
carbon = importlib.util.module_from_spec(spec)
spec.loader.exec_module(carbon)


class CarbonBehaviorTests(unittest.TestCase):
    def adapter(self, directory):
        args = carbon.parser().parse_args(['doctor', '--profile', str(Path(directory) / 'profile'),
                                           '--game-root', str(Path(directory) / 'game'),
                                           '--agent', str(Path(directory) / 'Agent.exe'),
                                           '--evidence-dir', str(Path(directory) / 'evidence')])
        return carbon.Adapter(args)

    def own(self, adapter):
        adapter.profile.mkdir(parents=True, exist_ok=True)
        (adapter.profile / '.rhah-carbon-profile.json').write_text(carbon.dumps({
            'project': 'nanaloveyuki.ratkin.hungerandhavoc', 'profile': str(adapter.profile.resolve())}))

    def test_windows_drive_conversion_spaces_and_unicode(self):
        self.assertEqual(carbon.windows_path('/mnt/e/Apps/Rim World/鼠族.json'), 'E:\\Apps\\Rim World\\鼠族.json')
        self.assertEqual(carbon.windows_path('E:\\Apps\\RimWorld'), 'E:\\Apps\\RimWorld')
        self.assertEqual(carbon.local_path('E:\\Apps\\Rim World'), Path('/mnt/e/Apps/Rim World'))

    def test_wsl_home_conversion_uses_wslpath_without_shell(self):
        response = subprocess.CompletedProcess([], 0, '\\\\wsl.localhost\\Ubuntu\\root\\scenario.json\n', '')
        with mock.patch.object(carbon.subprocess, 'run', return_value=response) as command:
            self.assertEqual(carbon.windows_path('/root/scenario.json'), response.stdout.strip())
        self.assertEqual(command.call_args.args[0], ['wslpath', '-w', '/root/scenario.json'])

    def test_default_paths_environment_overrides(self):
        with mock.patch.dict(os.environ, {'CARBON_PROFILE': 'E:\\Tests\\RHAH', 'CARBON_AGENT': 'E:\\Tools\\Agent.exe',
                                         'CARBON_SOURCE_DIR': '/source/carbon', 'RIMWORLD_DIR': 'E:\\Games\\RimWorld'}):
            args = carbon.parser().parse_args(['status'])
        self.assertEqual(args.profile, 'E:\\Tests\\RHAH')
        self.assertEqual(args.source_dir, '/source/carbon')
        self.assertEqual(args.agent, 'E:\\Tools\\Agent.exe')
        self.assertEqual(args.game_root, 'E:\\Games\\RimWorld')

    def test_recursive_summary_budget_keeps_nested_error_and_operation(self):
        response = {'state': 'completed', 'operationId': 'op-7', 'result': {'stamp': {'instanceId': 'instance-A', 'tick': 123},
                    'data': {'items': [{'large': '鼠' * 10000} for _ in range(100)],
                             'problem': {'error': {'code': 'real_error', 'message': 'failure'}}}}}
        summary = carbon.bounded(response, '/evidence/raw.json')
        self.assertLessEqual(len(carbon.dumps(summary).encode()), 6144)
        self.assertIn('real_error', carbon.dumps(summary))
        self.assertIn('op-7', carbon.dumps(summary))
        self.assertIn('instance-A', carbon.dumps(summary))
        self.assertTrue(summary['omitted'])
        self.assertTrue(carbon.has_failure(response))

    def test_selection_keeps_control_plane_and_reports_omission_counts(self):
        response = {'state': 'failed', 'operationId': 'op-x', 'error': {'code': 'broken'},
                    'result': {'data': {'items': ['abc'] * 25}}}
        summary = carbon.bounded(response, '/raw', 'result.data', ['items'])
        self.assertIn('broken', carbon.dumps(summary))
        self.assertIn('op-x', carbon.dumps(summary))
        self.assertEqual(summary['omitted']['arrayItems'], 13)
        self.assertEqual(carbon.json_path(response, 'result.data.items[0]'), 'abc')
        with self.assertRaises(carbon.CarbonError):
            carbon.json_path(response, 'result.data.items[-1]')

    def test_token_redaction_nested_and_embedded_log_json(self):
        result = carbon.redact({'token': 'secret', 'nested': {'message': 'Bearer secret-token',
                                  'line': '{"token":"top-secret","state":"failed"}'}}, ['secret-token'])
        self.assertNotIn('top-secret', carbon.dumps(result))
        self.assertNotIn('secret-token', carbon.dumps(result))
        self.assertEqual(result['token'], '[REDACTED]')
        self.assertIn('failed', carbon.dumps(result))

    def test_filters_project_stack_other_errors_and_duplicate_aggregation(self):
        events = [
            {'sequence': 1, 'kind': 'game.log', 'data': {'message': 'RHAH issue', 'level': 'Error', 'thread': 1}},
            {'sequence': 2, 'kind': 'game.log', 'data': {'message': 'RHAH issue', 'level': 'Error', 'thread': 2}},
            {'sequence': 3, 'kind': 'game.log', 'data': {'message': 'Other mod broken', 'level': 'Exception'}},
            {'sequence': 4, 'kind': 'game.log', 'data': {'message': 'Normal noise', 'level': 'Log'}},
            {'sequence': 5, 'kind': 'game.log', 'data': {'message': 'Failure', 'stack': 'Oxygendsl.Mod.Call', 'level': 'Error'}}]
        page = {'items': events, 'nextCursor': 5, 'lost': 8}
        result = carbon.filter_events(page)
        self.assertEqual(len(result['items']), 2)
        self.assertEqual(result['items'][0]['count'], 2)
        self.assertEqual(result['duplicates'], 1)
        self.assertEqual(result['otherModErrors'], {'exception': 1})
        self.assertEqual(result['excluded'], 2)
        self.assertEqual(result['lost'], 8)
        self.assertEqual(len(carbon.filter_events(page, all_events=True)['items']), 4)
        self.assertEqual(len(carbon.filter_events(page, terms=('normal noise',))['items']), 1)

    def test_startup_inventory_is_filtered_without_hiding_diagnostic_or_explicit_request(self):
        message = 'Initializing new game with mods:\n  - nanaloveyuki.ratkin.hungerandhavoc'
        page = {'items': [{'sequence': 1, 'kind': 'game.log', 'data': {'level': 'Log', 'message': message}},
                          {'sequence': 2, 'kind': 'game.log', 'data': {'level': 'Error', 'message': message}}],
                'nextCursor': 2, 'lost': 0}
        filtered = carbon.filter_events(page)
        self.assertEqual([item['sequence'] for item in filtered['items']], [2])
        self.assertEqual(filtered['excluded'], 1)
        self.assertEqual(len(carbon.filter_events(page, all_events=True)['items']), 2)
        self.assertEqual(len(carbon.filter_events(page, terms=('initializing',))['items']), 2)

    def test_incremental_event_cursors_are_isolated_by_instance(self):
        with tempfile.TemporaryDirectory() as directory:
            adapter = self.adapter(directory)
            seen = []
            instance = ['first']
            def invoke(command, *args, **kwargs):
                return {'instanceId': instance[0]}, adapter.save({'instanceId': instance[0]})
            def call(tool, args, **kwargs):
                seen.append(args['after'])
                cursor = args['after'] + 10
                return {'items': [{'sequence': cursor, 'kind': 'game.log', 'data': {'message': 'RHAH'}}],
                        'nextCursor': cursor, 'lost': 0}, adapter.save({})
            with mock.patch.object(adapter, 'invoke', side_effect=invoke), mock.patch.object(adapter, 'call', side_effect=call):
                adapter.events()
                adapter.events()
                instance[0] = 'second'
                adapter.events()
            self.assertEqual(seen, [0, 10, 0])
            self.assertEqual(len(list(adapter.evidence.glob('cursor-*.json'))), 2)

    def test_event_cursor_not_committed_when_instance_changes(self):
        with tempfile.TemporaryDirectory() as directory:
            adapter = self.adapter(directory)
            with mock.patch.object(adapter, 'invoke', side_effect=[({'instanceId': 'one'}, '/a'), ({'instanceId': 'two'}, '/b')]), \
                 mock.patch.object(adapter, 'call', return_value=({'items': [], 'nextCursor': 4, 'lost': 0}, '/events')):
                result, _ = adapter.events()
            self.assertEqual(result['error'], 'instance_changed')
            self.assertFalse(list(adapter.evidence.glob('cursor-*.json')))

    def test_large_agent_json_is_parsed_not_line_truncated(self):
        with tempfile.TemporaryDirectory() as directory:
            adapter = self.adapter(directory)
            response = {'state': 'failed', 'id': 'operation-123', 'result': {'data': {'text': 'x' * 100000}},
                        'error': {'code': 'failure-at-end'}}
            completed = subprocess.CompletedProcess([], 1, carbon.dumps(response), '')
            with mock.patch.object(carbon, 'windows_path', side_effect=str), \
                 mock.patch.object(carbon.subprocess, 'run', return_value=completed):
                result, evidence = adapter.invoke('call', 'game_state', arguments={})
            self.assertEqual(result['error']['code'], 'failure-at-end')
            self.assertEqual(json.loads(evidence.read_text())['response'], response)
            summary = carbon.bounded(result, evidence)
            self.assertIn('failure-at-end', carbon.dumps(summary))
            self.assertLessEqual(len(carbon.dumps(summary).encode()), 6144)

    def test_uncertain_agent_operation_extracts_recovery_ids(self):
        with tempfile.TemporaryDirectory() as directory:
            adapter = self.adapter(directory)
            response = {'error': 'operation_uncertain', 'message': 'Timeout. Instance=abc123, operationId=123abc. Query this operation; do not replay the mutation.'}
            completed = subprocess.CompletedProcess([], 1, '', carbon.dumps(response))
            with mock.patch.object(carbon, 'windows_path', side_effect=str), \
                 mock.patch.object(carbon.subprocess, 'run', return_value=completed) as invoke:
                result, _ = adapter.invoke('call', 'write_tool', arguments={})
            self.assertEqual(result['instanceId'], 'abc123')
            self.assertEqual(result['operationId'], '123abc')
            self.assertEqual(invoke.call_count, 1)

    def test_unowned_profile_refuses_lifecycle_and_generic_write(self):
        with tempfile.TemporaryDirectory() as directory:
            adapter = self.adapter(directory)
            with self.assertRaises(carbon.CarbonError):
                adapter.call('process_start', {})
            with mock.patch.object(adapter, 'read_tools', return_value=([{'name': 'game_step', 'effect': 'write'}], '/schema')):
                with self.assertRaises(carbon.CarbonError):
                    adapter.call('game_step', {'ticks': 1})
            self.own(adapter)
            adapter.isolated()

    def test_prepare_never_changes_existing_profile(self):
        with tempfile.TemporaryDirectory() as directory:
            adapter = self.adapter(directory)
            adapter.profile.mkdir()
            sentinel = adapter.profile / 'user-setting.xml'
            sentinel.write_text('untouched')
            with mock.patch.object(adapter, 'invoke') as invoke:
                with self.assertRaises(carbon.CarbonError):
                    adapter.prepare(minimal=True)
            self.assertEqual(sentinel.read_text(), 'untouched')
            invoke.assert_not_called()

    def test_exact_tools_schema_and_default_compact_names(self):
        with tempfile.TemporaryDirectory() as directory:
            adapter = self.adapter(directory)
            specs = [{'name': 'game_state', 'effect': 'read', 'inputSchema': {'type': 'object'}, 'description': 'RHAH types'},
                     {'name': 'game_step', 'effect': 'write', 'inputSchema': {'type': 'object'}}]
            with mock.patch.object(adapter, 'read_tools', return_value=(specs, '/schema')):
                compact, _ = adapter.tools()
                exact, _ = adapter.tools(name='game_state')
                queried, _ = adapter.tools(query='RHAH')
            self.assertEqual(set(compact['tools'][0]), {'name', 'effect'})
            self.assertEqual(exact, specs[0])
            self.assertEqual(queried['count'], 1)

    def test_operation_instance_mismatch_never_queries_new_instance(self):
        with tempfile.TemporaryDirectory() as directory:
            adapter = self.adapter(directory)
            with mock.patch.object(adapter, 'invoke', return_value=({'instanceId': 'new'}, '/status')), \
                 mock.patch.object(adapter, 'call') as call:
                result, _ = carbon.operation(adapter, 'old-op', 'old')
            self.assertEqual(result['error'], 'instance_mismatch')
            call.assert_not_called()

    def test_stdio_initialize_notifications_list_ping_and_unknown_method(self):
        with tempfile.TemporaryDirectory() as directory:
            adapter = self.adapter(directory)
            requests = [
                {'jsonrpc': '2.0', 'id': 1, 'method': 'initialize', 'params': {'protocolVersion': '2025-03-26'}},
                {'jsonrpc': '2.0', 'method': 'notifications/initialized'},
                {'jsonrpc': '2.0', 'id': 2, 'method': 'tools/list'},
                {'jsonrpc': '2.0', 'id': 3, 'method': 'ping'},
                {'jsonrpc': '2.0', 'id': 4, 'method': 'nonexistent'}]
            output = io.StringIO()
            with mock.patch.object(adapter, 'invoke') as invoke:
                carbon.serve_mcp(adapter, io.StringIO('\n'.join(map(carbon.dumps, requests)) + '\n'), output)
            messages = [json.loads(line) for line in output.getvalue().splitlines()]
            self.assertEqual([m['id'] for m in messages], [1, 2, 3, 4])
            self.assertEqual(messages[0]['result']['protocolVersion'], '2025-03-26')
            self.assertEqual(len(messages[1]['result']['tools']), 7)
            self.assertLess(len(carbon.dumps(messages[1]).encode()), 7000)
            self.assertEqual(messages[3]['error']['code'], -32601)
            invoke.assert_not_called()

    def test_stdio_large_tool_response_is_bounded_error_not_protocol_failure(self):
        with tempfile.TemporaryDirectory() as directory:
            adapter = self.adapter(directory)
            request = {'jsonrpc': '2.0', 'id': 7, 'method': 'tools/call',
                       'params': {'name': 'carbon_call', 'arguments': {'tool': 'game_state'}}}
            response = {'state': 'failed', 'id': 'op', 'error': {'message': 'observed failure'}, 'items': ['x' * 20000] * 100}
            output = io.StringIO()
            with mock.patch.object(adapter, 'call', return_value=(response, '/raw')):
                carbon.serve_mcp(adapter, io.StringIO(carbon.dumps(request) + '\n'), output)
            message = json.loads(output.getvalue())
            self.assertTrue(message['result']['isError'])
            summary_text = message['result']['content'][0]['text']
            self.assertLessEqual(len(summary_text.encode()), 6144)
            self.assertIn('observed failure', summary_text)
            self.assertNotIn('image', message['result']['content'][0])

    def test_stdio_invalid_json_returns_parse_error_without_traceback(self):
        with tempfile.TemporaryDirectory() as directory:
            output = io.StringIO()
            carbon.serve_mcp(self.adapter(directory), io.StringIO('not-json\n'), output)
            message = json.loads(output.getvalue())
            self.assertEqual(message['error']['code'], -32700)
            self.assertNotIn('Traceback', output.getvalue())


    def test_small_budget_keeps_uncertainty_ids_even_when_payload_is_omitted(self):
        response = {'error': 'operation_uncertain', 'state': 'running', 'operationId': 'op-uncertain',
                    'instanceId': 'instance-uncertain', 'payload': [{'id': str(i), 'text': '鼠' * 9000} for i in range(100)]}
        summary = carbon.bounded(response, '/raw', limit=1024)
        self.assertLessEqual(len(carbon.dumps(summary).encode()), 1024)
        self.assertIn('operation_uncertain', carbon.dumps(summary))
        self.assertIn('op-uncertain', carbon.dumps(summary))
        self.assertIn('instance-uncertain', carbon.dumps(summary))

    def test_mcp_negotiates_unknown_protocol_and_rejects_wrong_argument_types(self):
        with tempfile.TemporaryDirectory() as directory:
            adapter = self.adapter(directory)
            requests = [
                {'jsonrpc': '2.0', 'id': 1, 'method': 'initialize', 'params': {'protocolVersion': 'future-invalid'}},
                {'jsonrpc': '2.0', 'id': 2, 'method': 'tools/call', 'params': {'name': 'carbon_run', 'arguments': {'scenario': 3}}},
                {'jsonrpc': '2.0', 'id': 3, 'method': 'tools/call', 'params': {'name': 'carbon_events', 'arguments': {'limit': True}}}]
            output = io.StringIO()
            with mock.patch.object(adapter, 'run') as run, mock.patch.object(adapter, 'events') as events:
                carbon.serve_mcp(adapter, io.StringIO('\n'.join(map(carbon.dumps, requests)) + '\n'), output)
            results = [json.loads(line)['result'] for line in output.getvalue().splitlines()]
            self.assertEqual(results[0]['protocolVersion'], '2025-06-18')
            self.assertTrue(results[1]['isError'])
            self.assertTrue(results[2]['isError'])
            run.assert_not_called()
            events.assert_not_called()

    def test_historical_failed_event_is_data_not_a_failed_page(self):
        page = {'items': [{'sequence': 1, 'kind': 'operation.failed', 'data': {'error': {'code': 'old-failure'}, 'tool': 'RHAH'}}],
                'nextCursor': 1, 'lost': 0}
        self.assertFalse(carbon.event_page_failed(page))
        self.assertEqual(carbon.filter_events(page)['nextCursor'], 1)

    def test_prepare_installed_dependency_closure_and_owned_marker(self):
        with tempfile.TemporaryDirectory() as directory:
            adapter = self.adapter(directory)
            installed = {package: {'root': '/installed/' + package, 'dependencies': [], 'after': []}
                         for package in carbon.REQUIRED_MODS}
            installed['nanaloveyuki.ratkin.hungerandhavoc']['dependencies'] = ['ludeon.rimworld.biotech', 'nanaloveyuki.irismenus']
            installed['ludeon.rimworld.biotech'] = {'root': '/biotech', 'dependencies': ['ludeon.rimworld'], 'after': []}
            installed['nanaloveyuki.irismenus'] = {'root': '/iris', 'dependencies': ['brrainz.harmony'], 'after': []}
            def invoke(command, *args, **kwargs):
                config = adapter.profile / 'Config'
                config.mkdir(parents=True)
                (config / 'ModsConfig.xml').write_text('<ModsConfigData><activeMods><li>ludeon.rimworld</li></activeMods></ModsConfigData>')
                return {'profile': str(adapter.profile)}, adapter.save({})
            with mock.patch.object(adapter, 'installed', return_value=(installed, [])), \
                 mock.patch.object(adapter, 'invoke', side_effect=invoke), mock.patch.object(carbon, 'windows_path', side_effect=str):
                result, _ = adapter.prepare(minimal=True)
            self.assertIn('ludeon.rimworld.biotech', result['mods'])
            self.assertIn('nanaloveyuki.irismenus', result['mods'])
            self.assertLess(result['mods'].index('brrainz.harmony'), result['mods'].index('nanaloveyuki.irismenus'))
            adapter.isolated()


    def test_run_preserves_old_failures_and_archives_real_agent_files(self):
        with tempfile.TemporaryDirectory() as directory:
            adapter = self.adapter(directory)
            self.own(adapter)
            scenario = Path(directory) / 'scenario.json'
            scenario.write_text('{"name":"real-agent-test","steps":[]}')
            upstream = adapter.profile / 'RimCarbon14/runs/first-run'
            upstream.mkdir(parents=True)
            (upstream / 'results.json').write_text('{"passed":false}')
            (upstream / 'failure.png').write_bytes(b'PNG')
            response = {'passed': False, 'output': str(upstream)}
            with mock.patch.object(adapter, 'invoke', return_value=(response, '/upstream')), \
                 mock.patch.object(carbon, 'windows_path', side_effect=str):
                first, _ = adapter.run(scenario)
                second, _ = adapter.run(scenario)
            self.assertNotEqual(first['evidenceDirectory'], second['evidenceDirectory'])
            self.assertTrue(Path(first['screenshots'][0]).is_file())
            self.assertTrue((upstream / 'results.json').is_file())
            self.assertFalse(first['passed'])

    def test_tools_http_discovery_uses_auth_without_agent_cli_or_token_evidence(self):
        with tempfile.TemporaryDirectory() as directory:
            adapter = self.adapter(directory)
            discovery = adapter.profile / 'RimCarbon14/discovery.json'
            discovery.parent.mkdir(parents=True)
            discovery.write_text(carbon.dumps({'protocolVersion': 1, 'endpoint': 'http://127.0.0.1:43125/', 'token': 'private-token'}))
            specs = [{'name': 'game_state', 'effect': 'read', 'inputSchema': {'type': 'object'}}]
            response = mock.MagicMock()
            response.__enter__.return_value.read.return_value = carbon.dumps(specs).encode()
            opener = mock.Mock()
            opener.open.return_value = response
            with mock.patch.object(carbon, 'is_wsl', return_value=False), \
                 mock.patch.object(carbon.urllib.request, 'build_opener', return_value=opener), \
                 mock.patch.object(carbon.subprocess, 'run') as agent:
                result, evidence = adapter.read_tools()
            self.assertEqual(result, specs)
            request = opener.open.call_args.args[0]
            self.assertEqual(request.full_url, 'http://127.0.0.1:43125/v1/tools')
            self.assertEqual(request.get_header('Authorization'), 'Bearer private-token')
            self.assertNotIn('private-token', evidence.read_text())
            agent.assert_not_called()

    def test_tools_discovery_rejects_remote_bad_protocol_and_redirects(self):
        with tempfile.TemporaryDirectory() as directory:
            adapter = self.adapter(directory)
            discovery = adapter.profile / 'RimCarbon14/discovery.json'
            discovery.parent.mkdir(parents=True)
            unsafe = ['https://127.0.0.1:1234/', 'http://example.com:1234/', 'http://127.0.0.1/',
                      'http://user@127.0.0.1:1234/', 'http://127.0.0.1:1234/other', 'http://127.0.0.1:1234/?token=x']
            with mock.patch.object(carbon.urllib.request, 'build_opener') as transport:
                for endpoint in unsafe:
                    discovery.write_text(carbon.dumps({'protocolVersion': 1, 'endpoint': endpoint, 'token': 'private-token'}))
                    result, _ = adapter.read_tools()
                    self.assertEqual(result['error'], 'invalid_endpoint')
                discovery.write_text(carbon.dumps({'protocolVersion': 2, 'endpoint': 'http://127.0.0.1:1234/', 'token': 'private-token'}))
                result, _ = adapter.read_tools()
                self.assertEqual(result['error'], 'protocol_mismatch')
                transport.assert_not_called()
            discovery.write_text(carbon.dumps({'protocolVersion': 1, 'endpoint': 'http://127.0.0.1:1234/', 'token': 'private-token'}))
            def build(*handlers):
                redirect = next(handler for handler in handlers if isinstance(handler, carbon.urllib.request.HTTPRedirectHandler))
                opener = mock.Mock()
                opener.open.side_effect = lambda *args, **kwargs: redirect.redirect_request(None, None, 302, '', {}, 'http://example.com/')
                return opener
            with mock.patch.object(carbon, 'is_wsl', return_value=False), \
                 mock.patch.object(carbon.urllib.request, 'build_opener', side_effect=build):
                result, evidence = adapter.read_tools()
            self.assertEqual(result['error'], 'redirect_rejected')
            self.assertNotIn('private-token', evidence.read_text())

    def test_wsl_tools_http_runs_windows_helper_without_token_in_command(self):
        with tempfile.TemporaryDirectory() as directory:
            adapter = self.adapter(directory)
            discovery = adapter.profile / 'RimCarbon14/discovery.json'
            discovery.parent.mkdir(parents=True)
            discovery.write_text(carbon.dumps({'protocolVersion': 1, 'endpoint': 'http://127.0.0.1:43125/', 'token': 'private-token'}))
            specs = [{'name': 'objects_types', 'effect': 'read'}]
            completed = subprocess.CompletedProcess([], 0, carbon.dumps(specs), '')
            with mock.patch.object(carbon, 'is_wsl', return_value=True), \
                 mock.patch.object(carbon, 'windows_path', side_effect=lambda path: 'WINDOWS:' + str(path)), \
                 mock.patch.object(carbon.subprocess, 'run', return_value=completed) as process, \
                 mock.patch.object(carbon.urllib.request, 'build_opener') as linux_http:
                result, evidence = adapter.read_tools()
            self.assertEqual(result, specs)
            argv = process.call_args.args[0]
            self.assertEqual(argv[0], 'powershell.exe')
            self.assertIn('-DiscoveryFile', argv)
            self.assertNotIn('private-token', carbon.dumps(argv))
            self.assertNotIn('private-token', evidence.read_text())
            self.assertIn('windows-authenticated-loopback-http', evidence.read_text())
            linux_http.assert_not_called()

    def test_wsl_tools_helper_nonzero_failure_is_not_ignored(self):
        with tempfile.TemporaryDirectory() as directory:
            adapter = self.adapter(directory)
            discovery = adapter.profile / 'RimCarbon14/discovery.json'
            discovery.parent.mkdir(parents=True)
            discovery.write_text(carbon.dumps({'protocolVersion': 1, 'endpoint': 'http://127.0.0.1:43125/', 'token': 'private-token'}))
            completed = subprocess.CompletedProcess([], 1, '[{"name":"must-not-pass"}]', '{"error":"failure","message":"private-token"}')
            with mock.patch.object(carbon, 'is_wsl', return_value=True), \
                 mock.patch.object(carbon, 'windows_path', side_effect=str), \
                 mock.patch.object(carbon.subprocess, 'run', return_value=completed):
                result, evidence = adapter.read_tools()
            self.assertEqual(result['error'], 'windows_tools_discovery_failed')
            self.assertEqual(result['exitCode'], 1)
            self.assertNotIn('private-token', evidence.read_text())



if __name__ == '__main__':
    unittest.main()
