import contextlib
import importlib.util
import io
import json
import os
from pathlib import Path
import random
import shutil
import struct
import subprocess
import sys
import tempfile
import unittest
from unittest import mock
import zipfile
import zlib

SCRIPT = Path(__file__).resolve().parents[1] / "rhah_pipeline.py"
spec = importlib.util.spec_from_file_location("rhah_pipeline", SCRIPT)
pipeline = importlib.util.module_from_spec(spec)
spec.loader.exec_module(pipeline)


def write(root, relative, content="fixture"):
    path = root / relative
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content, encoding="utf-8")
    return path


def fixture(root, version="1.2.3"):
    for folder in pipeline.FOLDERS:
        (root / folder).mkdir(parents=True, exist_ok=True)
    for relative in pipeline.FILES:
        write(root, relative)
    write(root, "About/About.xml", "<ModMetaData><packageId>" + pipeline.PACKAGE_ID + "</packageId><modVersion>" + version + "</modVersion></ModMetaData>")
    write(root, "Source/HungerAndHavoc.csproj", "<Project><PropertyGroup><Version>" + version + "</Version></PropertyGroup></Project>")
    write(root, "Languages/English/Keyed/Content.xml", "<LanguageData />")
    write(root, "Textures/image.png")


class ReleaseTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.base = Path(self.temp.name)
        self.root = self.base / "repo"
        fixture(self.root)

    def test_package_is_strict_whitelist_and_hashes(self):
        flowers = "Languages/ChineseSimplified/Strings/Names/Noun/RHAH_FlowerNames.txt"
        write(self.root, flowers, "牡丹\n芍药\n")
        excluded = ("1.6/Assemblies/0Harmony.dll", "Guard/Assemblies/ThirdParty.dll", "About/secret.txt", "About/token.xml", "About/.env", "Textures/Source/code.cs", "Textures/bin/fake.png", "Textures/image.pdb", "Languages/English/credentials/key.xml", "Languages/English/notes.txt", "Languages/English/Strings/secret.txt", "tmp/automation/private.json", "Source/private.cs")
        for path in excluded:
            write(self.root, path)
        archive, manifest_path, manifest = pipeline.make_package(self.root, self.base / "package", "v1.2.3")
        with zipfile.ZipFile(archive) as bundle:
            names = set(bundle.namelist())
            self.assertEqual(names, {pipeline.MOD_NAME + "/" + path for path in pipeline.release_files(self.root)})
            for path in excluded:
                self.assertNotIn(pipeline.MOD_NAME + "/" + path, names)
            self.assertEqual(bundle.read(pipeline.MOD_NAME + "/" + flowers).decode("utf-8"), "牡丹\n芍药\n")
        self.assertEqual(json.loads(manifest_path.read_text()), manifest)
        self.assertEqual(manifest["archive_sha256"], pipeline.sha256(archive))
        for relative, digest in manifest["files"].items():
            self.assertEqual(digest, pipeline.sha256(self.root / relative))

    def test_each_runtime_dll_is_mandatory(self):
        for relative in pipeline.DLLS:
            with self.subTest(relative=relative):
                path = self.root / relative
                path.unlink()
                with self.assertRaisesRegex(pipeline.PipelineError, "Missing required"):
                    pipeline.release_files(self.root)
                write(self.root, relative)

    def test_empty_runtime_dll_fails(self):
        write(self.root, pipeline.DLLS[0], "")
        with self.assertRaisesRegex(pipeline.PipelineError, "Empty required"):
            pipeline.release_files(self.root)

    def test_version_mismatch_does_not_modify_source(self):
        project = write(self.root, "Source/HungerAndHavoc.csproj", "<Project><PropertyGroup><Version>9.9.9</Version></PropertyGroup></Project>")
        original = project.read_bytes()
        with self.assertRaisesRegex(pipeline.PipelineError, "version mismatch"):
            pipeline.make_package(self.root, self.base / "package")
        self.assertEqual(original, project.read_bytes())
        self.assertFalse((self.base / "package").exists())

    def test_release_tag_must_match(self):
        with self.assertRaisesRegex(pipeline.PipelineError, "Release tag"):
            pipeline.make_package(self.root, self.base / "package", "v8.0.0")
        with self.assertRaisesRegex(pipeline.PipelineError, "Release tag"):
            pipeline.make_package(self.root, self.base / "package", "1.2.3")

    def test_package_output_cannot_pollute_inputs(self):
        with self.assertRaisesRegex(pipeline.PipelineError, "inside release"):
            pipeline.make_package(self.root, self.root / "Textures/package")

    @unittest.skipUnless(hasattr(os, "symlink"), "symlink unavailable")
    def test_source_symlink_is_refused(self):
        external = write(self.base, "external.xml")
        try:
            (self.root / "About/external.xml").symlink_to(external)
        except OSError:
            self.skipTest("symlink permission unavailable")
        with self.assertRaisesRegex(pipeline.PipelineError, "Symlink/reparse"):
            pipeline.release_files(self.root)


class DeploymentTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.base = Path(self.temp.name)
        self.root = self.base / "repo"
        fixture(self.root)
        self.game = self.base / "game"
        (self.game / "Mods").mkdir(parents=True)
        self.target = self.game / "Mods" / pipeline.MOD_NAME

    def test_safe_deploy_hashes_and_cleans_only_owned_roots(self):
        fixture(self.target)
        stale = write(self.target, "Languages/English/Keyed/old.xml")
        preserved = write(self.target, "user-notes.txt", "keep")
        unrelated = write(self.game, "Mods/Other/About/About.xml", "other")
        process_check = mock.Mock()
        result = pipeline.synchronize(self.root, self.game, self.target, process_check)
        self.assertEqual(process_check.call_count, 2)
        self.assertFalse(stale.exists())
        self.assertEqual(preserved.read_text(), "keep")
        self.assertEqual(unrelated.read_text(), "other")
        self.assertIn("Languages/English/Keyed/old.xml", result["removed"])
        for relative, digest in result["files"].items():
            self.assertEqual(pipeline.sha256(self.target / relative), digest)

    def test_game_running_refuses_without_any_writes(self):
        process_check = mock.Mock(side_effect=pipeline.PipelineError("Exit RimWorld"))
        with self.assertRaisesRegex(pipeline.PipelineError, "Exit RimWorld"):
            pipeline.synchronize(self.root, self.game, self.target, process_check)
        self.assertFalse(self.target.exists())

    def test_game_started_between_checks_refuses_before_writes(self):
        process_check = mock.Mock(side_effect=[None, pipeline.PipelineError("Exit RimWorld")])
        with self.assertRaisesRegex(pipeline.PipelineError, "Exit RimWorld"):
            pipeline.synchronize(self.root, self.game, self.target, process_check)
        self.assertFalse(self.target.exists())

    def test_existing_unknown_or_wrong_identity_refused(self):
        for metadata in (None, "<ModMetaData><packageId>another.mod</packageId></ModMetaData>", "<invalid>", "<wrong><packageId>" + pipeline.PACKAGE_ID + "</packageId></wrong>"):
            with self.subTest(metadata=metadata):
                self.target.mkdir(exist_ok=True)
                about = self.target / "About/About.xml"
                if about.exists():
                    about.unlink()
                if metadata is not None:
                    write(self.target, "About/About.xml", metadata)
                marker = write(self.target, "marker.txt", "untouched")
                with self.assertRaises(pipeline.PipelineError):
                    pipeline.synchronize(self.root, self.game, self.target, mock.Mock())
                self.assertEqual(marker.read_text(), "untouched")
                self.assertFalse((self.target / pipeline.DLLS[0]).exists())

    def test_unknown_new_and_legacy_targets_refused(self):
        for target in (self.game / "Mods/Unrecognized", self.game / "Mods" / pipeline.FORBIDDEN_TARGET, self.base / "outside"):
            with self.subTest(target=target):
                with self.assertRaises(pipeline.PipelineError):
                    pipeline.validate_target(self.game, target)
                self.assertFalse(target.exists())

    def test_unowned_assembly_refused_before_copy(self):
        fixture(self.target)
        write(self.target, "1.6/Assemblies/ThirdParty.dll")
        marker = write(self.target, "README.md", "untouched")
        with self.assertRaisesRegex(pipeline.PipelineError, "Unowned assembly"):
            pipeline.synchronize(self.root, self.game, self.target, mock.Mock())
        self.assertEqual(marker.read_text(), "untouched")

    @unittest.skipUnless(hasattr(os, "symlink"), "symlink unavailable")
    def test_target_ancestor_and_nested_symlinks_refused(self):
        fixture(self.target)
        external = write(self.base, "external.txt", "untouched")
        try:
            (self.target / "Languages/link.xml").symlink_to(external)
        except OSError:
            self.skipTest("symlink permission unavailable")
        with self.assertRaisesRegex(pipeline.PipelineError, "Symlink/reparse"):
            pipeline.synchronize(self.root, self.game, self.target, mock.Mock())
        self.assertEqual(external.read_text(), "untouched")
        (self.target / "Languages/link.xml").unlink()
        linked = self.base / "linked-game"
        linked.symlink_to(self.game, target_is_directory=True)
        with self.assertRaisesRegex(pipeline.PipelineError, "Symlink/reparse"):
            pipeline.validate_target(linked, linked / "Mods" / pipeline.MOD_NAME)

    def test_windows_reparse_attribute_refused(self):
        info = mock.Mock(st_mode=0, st_file_attributes=0x400)
        with mock.patch.object(Path, "lstat", return_value=info):
            self.assertTrue(pipeline.is_link(self.target))

    def test_copy_hash_mismatch_is_failure(self):
        with mock.patch.object(pipeline.shutil, "copy2", side_effect=lambda src, dst: Path(dst).write_bytes(b"corrupted")):
            with self.assertRaisesRegex(pipeline.PipelineError, "SHA-256 mismatch"):
                pipeline.synchronize(self.root, self.game, self.target, mock.Mock())

    @unittest.skipUnless(shutil.which("ffmpeg"), "FFmpeg unavailable")
    def test_kingdom_png_is_compressed_only_in_deployment(self):
        width, height = 1024, 768
        pixels = random.Random(42).randbytes(width * height * 3)
        def chunk(kind, data):
            return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data))
        rows = b"".join(b"\x00" + pixels[y * width * 3:(y + 1) * width * 3] for y in range(height))
        image = (b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 2, 0, 0, 0))
                 + chunk(b"IDAT", zlib.compress(rows)) + chunk(b"IEND", b""))
        relative = "Textures/KingdomArc/nested/scene.PNG"
        source = self.root / relative
        source.parent.mkdir(parents=True)
        source.write_bytes(image)
        small = write(self.root, "Textures/KingdomArc/small.png")
        small.write_bytes(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", 1, 1, 8, 2, 0, 0, 0))
                          + chunk(b"IDAT", zlib.compress(b"\x00\x11\x22\x33")) + chunk(b"IEND", b""))
        original_hash = pipeline.sha256(source)
        untouched_hash = pipeline.sha256(self.root / "Textures/image.png")
        result = pipeline.synchronize(self.root, self.game, self.target, mock.Mock())
        deployed = self.target / relative
        self.assertGreaterEqual(deployed.stat().st_size, 400 * 1024)
        self.assertLessEqual(deployed.stat().st_size, 750 * 1024)
        self.assertEqual(pipeline.sha256(source), original_hash)
        self.assertNotEqual(pipeline.sha256(deployed), original_hash)
        self.assertEqual(pipeline.sha256(deployed), result["files"][relative])
        deployed_width, deployed_height = struct.unpack(">II", deployed.read_bytes()[16:24])
        self.assertLess(deployed_width, width)
        self.assertAlmostEqual(deployed_width / deployed_height, width / height, delta=0.005)
        self.assertEqual(pipeline.sha256(self.target / "Textures/image.png"), untouched_hash)
        self.assertEqual((self.target / "Textures/KingdomArc/small.png").read_bytes(), small.read_bytes())
        archive, _, _ = pipeline.make_package(self.root, self.base / "package")
        with zipfile.ZipFile(archive) as bundle:
            self.assertEqual(bundle.read(pipeline.MOD_NAME + "/" + relative), image)

    def test_missing_ffmpeg_refuses_before_target_changes(self):
        fixture(self.target)
        marker = write(self.target, "README.md", "untouched")
        source = write(self.root, "Textures/KingdomArc/large.png", "x" * (750 * 1024 + 1))
        with mock.patch.object(pipeline.shutil, "which", return_value=None):
            with self.assertRaisesRegex(pipeline.PipelineError, "FFmpeg is required"):
                pipeline.synchronize(self.root, self.game, self.target, mock.Mock())
        self.assertEqual(marker.read_text(), "untouched")
        self.assertFalse((self.target / source.relative_to(self.root)).exists())

    @unittest.skipUnless(shutil.which("ffmpeg"), "FFmpeg unavailable")
    def test_invalid_kingdom_png_refuses_before_target_changes(self):
        fixture(self.target)
        marker = write(self.target, "README.md", "untouched")
        source = write(self.root, "Textures/KingdomArc/invalid.png", "x" * (750 * 1024 + 1))
        with self.assertRaisesRegex(pipeline.PipelineError, "compression failed"):
            pipeline.synchronize(self.root, self.game, self.target, mock.Mock())
        self.assertEqual(marker.read_text(), "untouched")
        self.assertFalse((self.target / source.relative_to(self.root)).exists())


class ProcessTests(unittest.TestCase):
    def test_linux_running_and_inspection_failure(self):
        for result in (subprocess.CompletedProcess([], 0, "python3\nRimWorldLinux64\n", ""), subprocess.CompletedProcess([], 1, "", "denied"), subprocess.CompletedProcess([], 0, "", "")):
            with self.subTest(result=result):
                with self.assertRaises(pipeline.PipelineError):
                    pipeline.probe_processes("Linux", False, mock.Mock(return_value=result))
        with self.assertRaises(pipeline.PipelineError):
            pipeline.probe_processes("Linux", False, mock.Mock(side_effect=FileNotFoundError("ps")))

    def test_windows_fail_closed_and_running(self):
        for result in (subprocess.CompletedProcess([], 0, "1", ""), subprocess.CompletedProcess([], 0, "", ""), subprocess.CompletedProcess([], 0, "garbage", ""), subprocess.CompletedProcess([], 1, "0", "denied")):
            with self.subTest(result=result):
                with self.assertRaises(pipeline.PipelineError):
                    pipeline.probe_processes("Windows", False, mock.Mock(return_value=result))
        with self.assertRaises(pipeline.PipelineError):
            pipeline.probe_processes("Windows", False, mock.Mock(side_effect=FileNotFoundError("PowerShell")))

    def test_wsl_checks_both_process_namespaces(self):
        runner = mock.Mock(side_effect=[subprocess.CompletedProcess([], 0, "0\r\n", ""), subprocess.CompletedProcess([], 0, "python3\nbash\n", "")])
        pipeline.probe_processes("Linux", True, runner)
        self.assertEqual(runner.call_count, 2)
        self.assertEqual(runner.call_args_list[0].args[0][0], "powershell.exe")
        self.assertEqual(runner.call_args_list[1].args[0][0], "ps")

    def test_unsupported_platform_refused(self):
        with self.assertRaises(pipeline.PipelineError):
            pipeline.probe_processes("Unknown", False, mock.Mock())


class EvidenceTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)

    def test_long_single_line_failure_is_bounded_but_log_complete(self):
        console = io.StringIO()
        with contextlib.redirect_stdout(console):
            evidence = pipeline.Evidence(self.root, "ci")
            def failure(log):
                log.write(b"X" * 100000)
                raise pipeline.PipelineError("failure")
            with self.assertRaises(pipeline.PipelineError):
                evidence.stage("large-failure", failure)
            evidence.finish(False)
        self.assertLessEqual(len(console.getvalue().encode("utf-8")), 6000)
        report = json.loads(evidence.path.read_text())
        self.assertEqual(report["status"], "failed")
        self.assertFalse(report["full_gate"])
        self.assertGreater((self.root / report["stages"][0]["log"]).stat().st_size, 100000)
        self.assertEqual(json.loads((self.root / "tmp/automation/latest.json").read_text())["report"], evidence.path.relative_to(self.root).as_posix())
        safe = json.loads((evidence.directory / "safe-report.json").read_text())
        self.assertNotIn("log", safe["stages"][0])
        self.assertNotIn("artifacts", safe)

    def test_full_command_output_is_logged_not_streamed(self):
        console = io.StringIO()
        with contextlib.redirect_stdout(console):
            evidence = pipeline.Evidence(self.root, "check")
            evidence.run("command", [sys.executable, "-c", "print('evidence-' + 'x' * 50000)"])
            evidence.finish(True)
        self.assertNotIn("evidence-", console.getvalue())
        log = self.root / evidence.report["stages"][0]["log"]
        self.assertIn("evidence-" + "x" * 50000, log.read_text())

    def test_failed_external_command_preserves_exit_code(self):
        with contextlib.redirect_stdout(io.StringIO()):
            evidence = pipeline.Evidence(self.root, "check")
            with self.assertRaises(subprocess.CalledProcessError):
                evidence.run("failure", [sys.executable, "-c", "raise SystemExit(7)"])
            evidence.finish(False)
        self.assertEqual(evidence.report["stages"][0]["exit_code"], 7)


class CliTests(unittest.TestCase):
    def test_arguments_work_before_and_after_command(self):
        for argv in (["--configuration", "Debug", "build"], ["build", "--configuration", "Debug"]):
            self.assertEqual(pipeline.parser().parse_args(argv).configuration, "Debug")

    def test_no_skip_gate_option(self):
        with contextlib.redirect_stderr(io.StringIO()), self.assertRaises(SystemExit):
            pipeline.parser().parse_args(["deploy", "--skip-checks"])

    def test_environment_and_cli_precedence(self):
        values = {"RIMWORLD_DIR": "/primary", "RimWorldDir": "/secondary", "OXYGEN_DSL_DIR": "/oxygen", "CONFIGURATION": "Debug"}
        with mock.patch.dict(os.environ, values):
            args = pipeline.parser().parse_args(["build"])
            self.assertEqual(args.rimworld_dir, "/primary")
            self.assertEqual(args.oxygen_dsl_dir, "/oxygen")
            self.assertEqual(args.configuration, "Debug")
            self.assertEqual(pipeline.parser().parse_args(["build", "--rimworld-dir", "/cli"]).rimworld_dir, "/cli")

    def test_check_creates_missing_clean_checkout_output_directories(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            evidence = mock.Mock()
            args = pipeline.parser().parse_args(["check"])
            instance = pipeline.Pipeline(args, root, evidence)
            instance.check()
            self.assertTrue((root / "1.6/Assemblies").is_dir())
            self.assertTrue((root / "Guard/Assemblies").is_dir())
            self.assertIn("verify-scaffold.py", evidence.run.call_args.args[1][1])


    def test_ci_failure_does_not_claim_full_gate(self):
        evidence = mock.Mock(report={"full_gate": False})
        instance = pipeline.Pipeline(pipeline.parser().parse_args(["ci"]), evidence=evidence)
        instance.tools = mock.Mock(side_effect=pipeline.PipelineError("failed"))
        with self.assertRaises(pipeline.PipelineError):
            instance.ci()
        self.assertFalse(evidence.report["full_gate"])


    def test_package_cannot_bypass_ci(self):
        evidence = mock.Mock()
        instance = pipeline.Pipeline(pipeline.parser().parse_args(["package"]), evidence=evidence)
        instance.ci = mock.Mock(side_effect=pipeline.PipelineError("full gate failed"))
        with mock.patch.object(pipeline, "make_package") as bundle:
            with self.assertRaises(pipeline.PipelineError):
                instance.package()
            bundle.assert_not_called()

    def test_deploy_cannot_bypass_ci(self):
        evidence = mock.Mock()
        instance = pipeline.Pipeline(pipeline.parser().parse_args(["deploy"]), evidence=evidence)
        instance.operation = lambda name, function: function()
        instance.ci = mock.Mock(side_effect=pipeline.PipelineError("full gate failed"))
        with mock.patch.object(pipeline, "probe_processes"), mock.patch.object(pipeline, "validate_target"), mock.patch.object(pipeline, "synchronize") as sync:
            with self.assertRaises(pipeline.PipelineError):
                instance.deploy()
            sync.assert_not_called()

    def test_missing_dotnet_is_explicit_failure(self):
        with tempfile.TemporaryDirectory() as temporary, contextlib.redirect_stdout(io.StringIO()):
            evidence = pipeline.Evidence(Path(temporary), "build")
            instance = pipeline.Pipeline(pipeline.parser().parse_args(["build"]), Path(temporary), evidence)
            with mock.patch.object(pipeline.shutil, "which", return_value=None):
                with self.assertRaisesRegex(pipeline.PipelineError, "dotnet SDK is required"):
                    instance.dependencies()

    def test_missing_game_or_odf_dependency_is_explicit_failure(self):
        with tempfile.TemporaryDirectory() as temporary, contextlib.redirect_stdout(io.StringIO()):
            root = Path(temporary)
            evidence = pipeline.Evidence(root, "build")
            args = pipeline.parser().parse_args(["build", "--rimworld-dir", str(root / "missing-game"), "--oxygen-dsl-dir", str(root / "missing-odf")])
            instance = pipeline.Pipeline(args, root, evidence)
            with mock.patch.object(pipeline.shutil, "which", return_value="available"):
                with self.assertRaisesRegex(pipeline.PipelineError, "Missing local dependencies"):
                    instance.dependencies()



if __name__ == "__main__":
    unittest.main()
