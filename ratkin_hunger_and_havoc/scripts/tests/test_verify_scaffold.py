import importlib.util
from pathlib import Path
import tempfile
import unittest
from unittest import mock


SCRIPT = Path(__file__).resolve().parents[1] / "verify-scaffold.py"
spec = importlib.util.spec_from_file_location("verify_scaffold", SCRIPT)
scaffold = importlib.util.module_from_spec(spec)
spec.loader.exec_module(scaffold)


class KeyedLanguageTests(unittest.TestCase):
    def test_multiline_xml_text_matches_single_line_translation(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            for language, text in (
                ("English", "{1} speaks.\n\nThey listen &amp; wait."),
                ("ChineseSimplified", "{1}开口了。"),
            ):
                folder = root / language
                folder.mkdir()
                (folder / "Letters.xml").write_text(
                    "<LanguageData><!-- ignored --><RHAH_Letter>" + text
                    + "</RHAH_Letter></LanguageData>", encoding="utf-8")
            with mock.patch.object(scaffold, "ROOT", root), mock.patch.object(scaffold, "errors", []):
                scaffold.compare_keyed("ChineseSimplified", "English", "letters")

                self.assertEqual([], scaffold.errors)
                self.assertEqual("{1} speaks.\n\nThey listen & wait.",
                                 scaffold.keyed_entries("English")["RHAH_Letter"])

    def test_multiline_placeholder_mismatch_is_reported(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            for language, text in (("English", "First paragraph.\n\n{1}"),
                                   ("ChineseSimplified", "只有正文。")):
                folder = root / language
                folder.mkdir()
                (folder / "Letters.xml").write_text(
                    "<LanguageData><RHAH_Letter>" + text + "</RHAH_Letter></LanguageData>", encoding="utf-8")
            with mock.patch.object(scaffold, "ROOT", root), mock.patch.object(scaffold, "errors", []):
                scaffold.compare_keyed("ChineseSimplified", "English", "letters")

                self.assertEqual(["letters placeholder count mismatch RHAH_Letter zh=0 en=1"], scaffold.errors)
