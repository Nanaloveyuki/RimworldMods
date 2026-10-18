import hashlib
import json
from pathlib import Path
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from rhah_logs import bounded_json, read_increment, redact, summarize


class LogFilteringTests(unittest.TestCase):
    def test_other_mod_failure_is_counted_and_project_stack_is_preserved(self):
        text = ('OtherMod: routine\nOtherMod Error: unrelated\n'
                '  at OtherMod.Tick()\n'
                'Exception: failure\n  at HungerAndHavoc.Pawn.Tick()\n'
                'Exception: failure\n  at HungerAndHavoc.Pawn.Tick()\n')
        result = summarize(text)
        self.assertEqual(result['filtered']['otherDiagnostics'], 1)
        self.assertEqual(result['items'], [{
            'count': 2, 'message': 'Exception: failure\n  at HungerAndHavoc.Pawn.Tick()', 'omittedChars': 0,
        }])

    def test_filter_does_not_hide_owned_normal_messages(self):
        result = summarize('RHAH_ visitor arrived\nUnrelated ordinary event\n')
        self.assertEqual(result['items'][0]['message'], 'RHAH_ visitor arrived')
        self.assertEqual(result['filtered']['otherRecords'], 1)

    def test_startup_mod_list_does_not_flood_project_output(self):
        text = 'Initializing new game with mods:\n  - nanaloveyuki.ratkin.hungerandhavoc\nRHAH_ ready\n'
        result = summarize(text)
        self.assertEqual(result['items'][0]['message'], 'RHAH_ ready')
        self.assertEqual(result['filtered']['otherRecords'], 1)

    def test_unfinished_appended_line_is_read_after_it_completes(self):
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / 'Player.log'
            path.write_bytes(b'RHAH_ complete\nRHAH_ par')
            data, cursor, span = read_increment(path, {})
            self.assertEqual(data, b'RHAH_ complete\n')
            with path.open('ab') as stream:
                stream.write(b'tial\n')
            data, _, _ = read_increment(path, cursor)
            self.assertEqual(data, b'RHAH_ partial\n')

    def test_single_partial_line_waits_until_newline(self):
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / 'Player.log'
            path.write_bytes(b'RHAH_ par')
            data, cursor, _ = read_increment(path, {})
            self.assertEqual(data, b'')
            self.assertEqual(cursor['offset'], 0)
            with path.open('ab') as stream:
                stream.write(b'tial\n')
            data, _, _ = read_increment(path, cursor)
            self.assertEqual(data, b'RHAH_ partial\n')

    def test_explicit_regex_can_include_external_failure(self):
        result = summarize('OtherMod failed\nRHAH_ ordinary\n', match='OtherMod')
        self.assertEqual(result['items'][0]['message'], 'OtherMod failed')
        self.assertEqual(result['filtered']['otherRecords'], 1)

    def test_long_messages_and_aggregate_output_are_bounded(self):
        result = summarize('\n'.join(f'RHAH_ Error {i} ' + 'x' * 30000 for i in range(100)), limit=100)
        result['evidence'] = 'tmp/automation/logs/example.log'
        rendered = bounded_json(result, 3000)
        self.assertLessEqual(len(rendered), 3000)
        selected = json.loads(rendered)
        self.assertEqual(selected['omittedGroups'] + len(selected['items']), 100)
        self.assertEqual(selected['items'][0]['omittedChars'], len('RHAH_ Error 0 ') + 30000 - 700)

    def test_secrets_are_removed_from_evidence_text(self):
        text = 'RHAH_ Error Authorization: Bearer abc123 token="secret" password=pass'
        value = redact(text)
        for secret in ('abc123', 'secret', '=pass'):
            self.assertNotIn(secret, value)
        self.assertIn('[redacted]', value)

    def test_cursor_reads_only_new_bytes_and_detects_same_file_rewrite(self):
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / 'Player.log'
            path.write_bytes(b'RHAH_ first\n')
            data, cursor, span = read_increment(path, {})
            self.assertEqual(data, b'RHAH_ first\n')
            with path.open('ab') as stream:
                stream.write(b'RHAH_ second\n')
            data, cursor, span = read_increment(path, cursor)
            self.assertEqual(data, b'RHAH_ second\n')
            self.assertEqual(span['mode'], 'incremental')
            path.write_bytes(b'RHAH_ replaced with longer message\n')
            data, cursor, span = read_increment(path, cursor)
            self.assertEqual(data, b'RHAH_ replaced with longer message\n')
            self.assertEqual(span['mode'], 'rotated')
            data, _, _ = read_increment(path, cursor)
            self.assertEqual(data, b'')

    def test_scan_chunk_preserves_next_line_and_reports_unscanned_bytes(self):
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / 'Player.log'
            path.write_bytes(b'RHAH_ first\nRHAH_ second\nRHAH_ third\n')
            stat = path.stat()
            cursor = {'identity': f'{stat.st_dev}:{stat.st_ino}', 'offset': 0,
                      'anchor': hashlib.sha256(b'').hexdigest()}
            data, cursor, span = read_increment(path, cursor, scan_bytes=20)
            self.assertEqual(data, b'RHAH_ first\n')
            self.assertGreater(span['remainingBytes'], 0)
            data, _, _ = read_increment(path, cursor, scan_bytes=20)
            self.assertEqual(data, b'RHAH_ second\n')

    def test_fresh_tail_does_not_claim_to_inspect_whole_log(self):
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / 'Player.log'
            path.write_bytes(b'old history\n' * 100 + b'RHAH_ final\n')
            data, _, span = read_increment(path, {}, scan_bytes=64)
            self.assertTrue(data.endswith(b'RHAH_ final\n'))
            self.assertGreater(span['earlierBytesNotScanned'], 0)


if __name__ == '__main__':
    unittest.main()
