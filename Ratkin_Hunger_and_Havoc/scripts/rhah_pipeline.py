#!/usr/bin/env python3
"""Bounded-console, local-evidence build and release pipeline (standard library only)."""
import argparse
import datetime
import hashlib
import json
import os
from pathlib import Path
import platform
import re
import shutil
import stat
import subprocess
import sys
import tempfile
import time
import traceback
import uuid
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).resolve().parents[1]
PACKAGE_ID = "nanaloveyuki.ratkin.hungerandhavoc"
MOD_NAME = "RatkinHungerAndHavoc"
FORBIDDEN_TARGET = "RatkinGreatFamineYearContinued"
FOLDERS = {
    "About": {".xml", ".png", ".jpg", ".jpeg", ".txt", ".md"},
    "Languages": {".xml", ".txt"},
    "Guard/Languages": {".xml"},
    "1.6/Defs": {".xml"},
    "1.6/Patches": {".xml"},
    "Biotech": {".xml"},
    "Textures": {".png", ".jpg", ".jpeg", ".dds"},
}
DLLS = (
    "1.6/Assemblies/HungerAndHavoc.Api.dll",
    "1.6/Assemblies/HungerAndHavoc.dll",
    "Guard/Assemblies/HungerAndHavocGuard.dll",
)
FILES = DLLS + ("LoadFolders.xml", "LICENSE", "NOTICE", "README.md")
EXCLUDED_DIRS = {"bin", "obj", "tmp", "source", "scripts", ".git", ".github", "secrets", "credentials"}
SENSITIVE_NAME = re.compile(r"(?:^|[._-])(?:token|secret|credential|password|private[._-]?key)(?:[._-]|$)", re.I)
GAME_PROCESS = re.compile(r"^rimworld(?:linux|win)?(?:32|64)?(?:\.exe)?$", re.I)


class PipelineError(Exception):
    pass


def sha256(path):
    digest = hashlib.sha256()
    with Path(path).open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def is_link(path):
    info = Path(path).lstat()
    return stat.S_ISLNK(info.st_mode) or bool(getattr(info, "st_file_attributes", 0) & 0x400)


def assert_plain_path(path):
    path = Path(os.path.abspath(path))
    for item in (path, *path.parents):
        if os.path.lexists(item) and is_link(item):
            raise PipelineError("Symlink/reparse path refused: " + str(item))
    return path


def assert_plain_tree(path):
    path = assert_plain_path(path)
    if not path.exists():
        return
    if not path.is_dir():
        raise PipelineError("Expected directory: " + str(path))
    for current, dirs, files in os.walk(path, followlinks=False):
        for name in dirs + files:
            item = Path(current) / name
            if is_link(item):
                raise PipelineError("Symlink/reparse entry refused: " + str(item))
            if not item.is_dir() and not item.is_file():
                raise PipelineError("Nonregular entry refused: " + str(item))


def package_identity(root):
    try:
        metadata = ET.parse(Path(root) / "About/About.xml").getroot()
    except (OSError, ET.ParseError) as exc:
        raise PipelineError("Missing/invalid About/About.xml identity: " + str(root)) from exc
    if metadata.tag != "ModMetaData" or metadata.findtext("packageId", "").strip() != PACKAGE_ID:
        raise PipelineError("Target/source belongs to another mod: " + str(root))
    return metadata.findtext("modVersion", "").strip()


def release_version(root, tag=None):
    version = package_identity(root)
    try:
        project = ET.parse(Path(root) / "Source/HungerAndHavoc.csproj").getroot()
        implementation = project.findtext("./PropertyGroup/Version", "").strip()
    except (OSError, ET.ParseError) as exc:
        raise PipelineError("Cannot read implementation release version") from exc
    if not re.fullmatch(r"\d+\.\d+\.\d+(?:[-+][A-Za-z0-9.-]+)?", version):
        raise PipelineError("Invalid About release version: " + version)
    if implementation != version:
        raise PipelineError("Release version mismatch: About=" + version + "; Source/HungerAndHavoc.csproj=" + implementation + "; versions were not modified")
    if tag is not None and tag != "v" + version:
        raise PipelineError("Release tag must match About and implementation version: v" + version)
    return version


def publishable(relative):
    path = Path(relative)
    if any(part.lower() in EXCLUDED_DIRS or part.startswith(".") for part in path.parts):
        return False
    if path.parts[0] == "Languages" and path.suffix.lower() == ".txt" and (len(path.parts) < 4 or path.parts[2] != "Strings"):
        return False
    if any(SENSITIVE_NAME.search(part) for part in path.parts) or path.suffix.lower() in {".pdb", ".cs", ".ps1", ".py", ".dll", ".rws", ".pem", ".key", ".pfx"}:
        return str(path.as_posix()) in DLLS
    return True


def release_files(root):
    root = assert_plain_path(root)
    package_identity(root)
    found = {}
    for folder, extensions in FOLDERS.items():
        base = root / folder
        if not base.is_dir():
            raise PipelineError("Missing release content directory: " + folder)
        assert_plain_tree(base)
        for item in sorted(base.rglob("*")):
            relative = item.relative_to(root).as_posix()
            if item.is_file() and item.suffix.lower() in extensions and publishable(relative):
                found[relative] = item
    for relative in FILES:
        item = assert_plain_path(root / relative)
        if not item.is_file():
            raise PipelineError("Missing required release file: " + relative)
        if relative in DLLS and item.stat().st_size == 0:
            raise PipelineError("Empty required mod assembly: " + relative)
        found[relative] = item
    return dict(sorted(found.items()))


def probe_processes(system=None, wsl=None, runner=subprocess.run):
    system = system or platform.system()
    if wsl is None:
        wsl = system == "Linux" and "microsoft" in platform.release().lower()
    if system == "Windows" or wsl:
        executable = "powershell.exe"
        command = [executable, "-NoProfile", "-NonInteractive", "-Command", "$ErrorActionPreference='Stop'; try { $p=@(Get-Process -ErrorAction Stop | Where-Object { $_.ProcessName -match '^RimWorld(?:Linux|Win)?(?:32|64)?$' }); [Console]::Write($p.Count) } catch { [Console]::Error.Write($_); exit 1 }"]
        try:
            result = runner(command, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True, timeout=30, check=False)
        except (OSError, subprocess.SubprocessError) as exc:
            raise PipelineError("Cannot inspect Windows game processes; deployment refused (PowerShell is required on WSL)") from exc
        count = result.stdout.strip()
        if result.returncode or not re.fullmatch(r"\d+", count):
            raise PipelineError("Windows process inspection failed; deployment refused")
        if int(count):
            raise PipelineError("Exit RimWorld before deploying; no game process will be terminated")
    if system == "Linux":
        try:
            result = runner(["ps", "-A", "-o", "comm="], stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True, timeout=30, check=False)
        except (OSError, subprocess.SubprocessError) as exc:
            raise PipelineError("Cannot inspect Linux game processes; deployment refused") from exc
        if result.returncode or not result.stdout.strip():
            raise PipelineError("Linux process inspection failed; deployment refused")
        if any(GAME_PROCESS.fullmatch(name.strip()) for name in result.stdout.splitlines()):
            raise PipelineError("Exit RimWorld before deploying; no game process will be terminated")
    elif system != "Windows":
        raise PipelineError("Unsupported process inspection platform; deployment refused: " + system)


def validate_target(rimworld, target):
    mods = assert_plain_path(Path(rimworld) / "Mods")
    target = assert_plain_path(target)
    if target.parent != mods or target.name.casefold() == FORBIDDEN_TARGET.casefold():
        raise PipelineError("Deployment target must be a direct, non-legacy mod directory under " + str(mods))
    if os.path.lexists(target):
        assert_plain_tree(target)
        package_identity(target)
        for folder, owned in (("1.6/Assemblies", set(DLLS[:2])), ("Guard/Assemblies", {DLLS[2]})):
            base = target / folder
            if base.is_dir():
                unexpected = [item.relative_to(target).as_posix() for item in base.rglob("*") if item.is_file() and item.relative_to(target).as_posix() not in owned]
                if unexpected:
                    raise PipelineError("Unowned assembly content in deployment target; review manually: " + ", ".join(unexpected))
    elif target.name != MOD_NAME:
        raise PipelineError("Unknown new deployment target; only " + MOD_NAME + " may be created")
    return target


def deployment_ffmpeg():
    executable = shutil.which("ffmpeg")
    if not executable:
        raise PipelineError("FFmpeg is required for KingdomArc deployment compression; install ffmpeg and add it to PATH")
    return executable


def compress_kingdom_textures(sources, staging):
    # PNG has no lossy quality knob: retain full color, reduce dimensions only if needed.
    maximum = 750 * 1024
    target_size = 700 * 1024
    images = {relative: source for relative, source in sources.items()
              if Path(relative).is_relative_to("Textures/KingdomArc") and Path(relative).suffix.lower() == ".png"}
    oversized = {relative: source for relative, source in images.items() if source.stat().st_size > maximum}
    executable = deployment_ffmpeg() if oversized else None
    before = sum(source.stat().st_size for source in images.values())
    for relative, source in oversized.items():
        destination = Path(staging) / relative
        destination.parent.mkdir(parents=True, exist_ok=True)
        with source.open("rb") as stream:
            header = stream.read(24)
        original_width = int.from_bytes(header[16:20], "big")
        original_height = int.from_bytes(header[20:24], "big")
        width, height = original_width, original_height
        while True:
            command = [executable, "-hide_banner", "-loglevel", "error", "-nostdin", "-y", "-i", str(source)]
            if (width, height) != (original_width, original_height):
                command += ["-vf", f"scale={width}:{height}:flags=lanczos"]
            command += ["-frames:v", "1", "-c:v", "png", "-compression_level", "9", "-pred", "mixed", "-threads", "1", "-update", "1", str(destination)]
            try:
                result = subprocess.run(command, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True, timeout=120, check=False)
            except (OSError, subprocess.SubprocessError) as exc:
                raise PipelineError("KingdomArc PNG compression failed: " + relative) from exc
            if result.returncode:
                raise PipelineError("KingdomArc PNG compression failed: " + relative + "\n" + result.stderr[-2000:])
            size = destination.stat().st_size
            if size <= maximum:
                break
            if width <= 1:
                raise PipelineError("Cannot compress KingdomArc PNG below 750 KiB: " + relative)
            width = max(1, min(width - 1, int(width * (target_size / size) ** 0.5)))
            height = max(1, round(original_height * width / original_width))
        sources[relative] = destination
    after = sum(sources[relative].stat().st_size for relative in images)
    return {"images": len(images), "compressed": len(oversized), "source_bytes": before, "deployed_bytes": after}


def synchronize(root, rimworld, target, process_check=probe_processes):
    process_check()
    sources = release_files(root)
    target = validate_target(rimworld, target)
    with tempfile.TemporaryDirectory(prefix="rhah-deploy-") as staging:
        compression = compress_kingdom_textures(sources, staging)
        manifest = {relative: sha256(source) for relative, source in sources.items()}
        process_check()
        target = validate_target(rimworld, target)
        target.mkdir(parents=True, exist_ok=True)
        for relative, source in sources.items():
            destination = assert_plain_path(target / relative)
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(source, destination)
            if sha256(destination) != manifest[relative]:
                raise PipelineError("Deployment SHA-256 mismatch: " + relative)
        removed = []
        for folder in FOLDERS:
            base = target / folder
            if not base.is_dir():
                continue
            for destination in sorted(base.rglob("*"), reverse=True):
                if destination.is_file():
                    relative = destination.relative_to(target).as_posix()
                    if relative not in sources:
                        destination.unlink()
                        removed.append(relative)
                elif destination.is_dir() and not any(destination.iterdir()):
                    destination.rmdir()
        return {"files": manifest, "removed": removed, "count": len(manifest), "kingdom_textures": compression}


def make_package(root, output, tag=None):
    version = release_version(root, tag)
    sources = release_files(root)
    output = assert_plain_path(output)
    if any(output.is_relative_to(Path(root) / folder) for folder in FOLDERS):
        raise PipelineError("Package output cannot be inside release input directories")
    output.mkdir(parents=True, exist_ok=True)
    archive = output / (MOD_NAME + "-" + version + ".zip")
    manifest_path = output / (MOD_NAME + "-" + version + ".sha256.json")
    assert_plain_path(archive)
    assert_plain_path(manifest_path)
    files = {relative: sha256(source) for relative, source in sources.items()}
    with zipfile.ZipFile(archive, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=9) as bundle:
        for relative, source in sources.items():
            bundle.write(source, MOD_NAME + "/" + relative)
    with zipfile.ZipFile(archive) as bundle:
        for relative, expected in files.items():
            digest = hashlib.sha256()
            with bundle.open(MOD_NAME + "/" + relative) as stream:
                for chunk in iter(lambda: stream.read(1024 * 1024), b""):
                    digest.update(chunk)
            if digest.hexdigest() != expected:
                raise PipelineError("Package input changed while archiving: " + relative)
    manifest = {"package_id": PACKAGE_ID, "version": version, "archive": archive.name, "archive_sha256": sha256(archive), "files": files}
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return archive, manifest_path, manifest


class Evidence:
    def __init__(self, root, command):
        self.root = Path(root)
        base = assert_plain_path(self.root / "tmp/automation")
        base.mkdir(parents=True, exist_ok=True)
        identifier = datetime.datetime.now(datetime.timezone.utc).strftime("%Y%m%dT%H%M%SZ") + "-" + command + "-" + uuid.uuid4().hex[:8]
        self.directory = base / identifier
        self.directory.mkdir()
        self.path = self.directory / "run.json"
        self.budget = 6000
        self.report = {"schema_version": 1, "command": command, "status": "running", "full_gate": False, "started_at": datetime.datetime.now(datetime.timezone.utc).isoformat(), "stages": [], "artifacts": []}
        self.save()
        latest = assert_plain_path(base / "latest.json")
        latest.write_text(json.dumps({"report": self.path.relative_to(self.root).as_posix()}) + "\n", encoding="utf-8")

    def say(self, text):
        data = (text + "\n").encode("utf-8", errors="replace")[:max(0, self.budget)]
        self.budget -= len(data)
        if data:
            sys.stdout.write(data.decode("utf-8", errors="ignore"))
            sys.stdout.flush()

    def save(self):
        self.path.write_text(json.dumps(self.report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    def stage(self, name, action):
        log = self.directory / (str(len(self.report["stages"]) + 1).zfill(2) + "-" + name + ".log")
        record = {"name": name, "status": "running", "log": log.relative_to(self.root).as_posix(), "exit_code": None}
        self.report["stages"].append(record)
        self.save()
        started = time.monotonic()
        try:
            with log.open("wb") as stream:
                action(stream)
            record.update(status="passed", exit_code=0)
        except Exception as exc:
            with log.open("ab") as stream:
                stream.write(("\n" + traceback.format_exc()).encode("utf-8", errors="replace"))
            record.update(status="failed", exit_code=getattr(exc, "returncode", 1) or 1)
            record["duration_seconds"] = round(time.monotonic() - started, 3)
            self.save()
            self.say("FAIL " + name + " (" + record["log"] + ")")
            with log.open("rb") as stream:
                stream.seek(max(0, log.stat().st_size - 2800))
                excerpt = stream.read(2800).decode("utf-8", errors="replace")
            self.say(excerpt)
            raise
        record["duration_seconds"] = round(time.monotonic() - started, 3)
        self.save()
        self.say("PASS " + name + " (" + str(record["duration_seconds"]) + "s)")

    def run(self, name, command, cwd=None):
        def action(log):
            log.write((json.dumps(command) + "\n").encode("utf-8"))
            log.flush()
            result = subprocess.run(command, cwd=cwd or self.root, stdout=log, stderr=subprocess.STDOUT, check=False)
            if result.returncode:
                raise subprocess.CalledProcessError(result.returncode, command)
        self.stage(name, action)

    def finish(self, successful):
        self.report["status"] = "passed" if successful else "failed"
        self.report["finished_at"] = datetime.datetime.now(datetime.timezone.utc).isoformat()
        self.save()
        self.say("Evidence: " + self.path.relative_to(self.root).as_posix())
        safe = {"schema_version": 1, "command": self.report["command"], "status": self.report["status"], "full_gate": self.report["full_gate"], "stages": [{"name": item["name"], "status": item["status"], "exit_code": item["exit_code"]} for item in self.report["stages"]]}
        (self.directory / "safe-report.json").write_text(json.dumps(safe, indent=2) + "\n", encoding="utf-8")


class Pipeline:
    def __init__(self, args, root=ROOT, evidence=None):
        self.args = args
        self.root = Path(root)
        self.rimworld = Path(args.rimworld_dir).expanduser().absolute()
        self.oxygen = Path(args.oxygen_dsl_dir).expanduser().absolute()
        self.configuration = args.configuration
        self.evidence = evidence or Evidence(root, args.command)
        self.managed = self.rimworld / "RimWorldWin64_Data/Managed"
        self.harmony = Path(args.harmony_path).expanduser().absolute() if args.harmony_path else self.rimworld / "../../workshop/content/294100/2009463077/1.6/Assemblies/0Harmony.dll"
        self.built = False

    def operation(self, name, function):
        def action(stream):
            result = function()
            if result is not None:
                stream.write((json.dumps(result, ensure_ascii=False, indent=2, default=str) + "\n").encode("utf-8"))
        self.evidence.stage(name, action)

    def dependencies(self):
        def inspect():
            if not shutil.which("dotnet"):
                raise PipelineError("dotnet SDK is required (see global.json)")
            if platform.system() == "Linux" and not shutil.which("mono"):
                raise PipelineError("Mono is required for existing net48 Linux tests")
            required = [self.managed / name for name in ("Assembly-CSharp.dll", "UnityEngine.CoreModule.dll", "UnityEngine.IMGUIModule.dll", "UnityEngine.SharedInternalsModule.dll", "UnityEngine.TextRenderingModule.dll", "UnityEngine.InputLegacyModule.dll", "UnityEngine.dll")]
            required.extend([self.oxygen / "Source/Mod/Oxygendsl.Mod.csproj", self.harmony])
            missing = [str(path) for path in required if not path.is_file()]
            if missing:
                raise PipelineError("Missing local dependencies (no automatic downloads): " + "; ".join(missing))
            return {"dotnet": shutil.which("dotnet"), "mono": shutil.which("mono"), "rimworld": str(self.rimworld), "oxygen_dsl": str(self.oxygen), "harmony": str(self.harmony), "configuration": self.configuration}
        self.operation("dependencies", inspect)

    def check(self):
        for folder in ("1.6/Assemblies", "Guard/Assemblies"):
            assert_plain_path(self.root / folder).mkdir(parents=True, exist_ok=True)
        self.evidence.run("structure", [sys.executable, str(self.root / "scripts/verify-scaffold.py")])

    def tools(self):
        self.evidence.run("tool-tests", [sys.executable, "-m", "unittest", "discover", "-s", "scripts/tests", "-p", "test_*.py"])

    def dotnet(self, name, verb, project, extra=()):
        command = ["dotnet", verb, str(project), "-c", self.configuration, "--nologo", "-v", "q", "-p:RimWorldDir=" + str(self.rimworld), "-p:RimWorldManagedDir=" + str(self.managed), "-p:OxygenDslDir=" + str(self.oxygen), "-p:HarmonyPath=" + str(self.harmony)]
        self.evidence.run(name, command + list(extra))

    def build(self):
        self.dependencies()
        self.dotnet("build-odf", "build", self.oxygen / "Source/Mod/Oxygendsl.Mod.csproj")
        self.operation("odf-outputs", lambda: self.require_odf_outputs())
        self.dotnet("build-rhah-api-and-main", "build", self.root / "Source/HungerAndHavoc.csproj")
        self.dotnet("build-guard", "build", self.root / "Guard/Source/HungerAndHavocGuard.csproj")
        self.built = True

    def require_odf_outputs(self):
        for name in ("Oxygendsl.Core.dll", "Oxygendsl.Mod.dll"):
            if not (self.oxygen / "tmp/build" / name).is_file():
                raise PipelineError("ODF build did not produce " + name)
        return {"required_outputs": ["Oxygendsl.Core.dll", "Oxygendsl.Mod.dll"]}

    def tests(self):
        if not self.built:
            self.build()
        self.dotnet("net48-tests", "test", self.root / "Source/Tests/HungerAndHavoc.Tests.csproj", ["-p:BuildProjectReferences=false", "--logger", "trx;LogFileName=net48-tests.trx", "--results-directory", str(self.evidence.directory / "test-results")])

    def ci(self):
        self.tools()
        self.check()
        self.build()
        self.tests()
        self.evidence.report["full_gate"] = True
        self.evidence.save()

    def package(self):
        if self.configuration != "Release":
            raise PipelineError("Packages require --configuration Release")
        self.ci()
        def bundle():
            output = Path(self.args.output_dir).expanduser().absolute() if self.args.output_dir else self.evidence.directory / "package"
            archive, manifest, details = make_package(self.root, output, self.args.release_tag)
            self.evidence.report["artifacts"].extend([str(archive), str(manifest)])
            self.evidence.report["version"] = details["version"]
            self.evidence.save()
            return {"archive": str(archive), "manifest": str(manifest), "files": len(details["files"]), "sha256": details["archive_sha256"]}
        self.operation("package", bundle)

    def deploy(self):
        target = Path(self.args.target).expanduser().absolute() if self.args.target else self.rimworld / "Mods" / MOD_NAME
        self.operation("deployment-preflight", lambda: (probe_processes(), validate_target(self.rimworld, target), deployment_ffmpeg()))
        self.ci()
        self.operation("deploy", lambda: synchronize(self.root, self.rimworld, target))

    def doctor(self):
        problems = []
        for name, callback in (("dependencies", self.dependencies), ("versions", lambda: self.operation("versions", lambda: {"release": release_version(self.root)})), ("game-processes", lambda: self.operation("game-processes", probe_processes)), ("deployment-ffmpeg", lambda: self.operation("deployment-ffmpeg", deployment_ffmpeg))):
            try:
                callback()
            except Exception:
                problems.append(name)
        if problems:
            raise PipelineError("Doctor found failed prerequisites: " + ", ".join(problems))


def parser():
    def options(target, defaults):
        absent = None if defaults else argparse.SUPPRESS
        target.add_argument("--rimworld-dir", default=(os.environ.get("RIMWORLD_DIR") or os.environ.get("RimWorldDir") or (r"E:\Apps\Steam\steamapps\common\RimWorld" if os.name == "nt" else "/mnt/e/Apps/Steam/steamapps/common/RimWorld")) if defaults else absent)
        target.add_argument("--oxygen-dsl-dir", default=(os.environ.get("OXYGEN_DSL_DIR") or str(ROOT.parent / "Oxygen-Dsl-Framework")) if defaults else absent)
        target.add_argument("--configuration", choices=("Release", "Debug"), default=os.environ.get("CONFIGURATION", "Release") if defaults else absent)
        target.add_argument("--harmony-path", default=os.environ.get("HARMONY_PATH") if defaults else absent)
    result = argparse.ArgumentParser(description=__doc__)
    options(result, True)
    subcommands = result.add_subparsers(dest="command", required=True)
    for name in ("check", "build", "test", "ci", "package", "deploy", "doctor"):
        sub = subcommands.add_parser(name)
        options(sub, False)
        if name == "test":
            sub.add_argument("--tools-only", action="store_true", help="Offline Python tools only; explicitly not the full CI gate")
        if name == "package":
            sub.add_argument("--output-dir", help="Default: current evidence run/package")
            sub.add_argument("--release-tag", help="Reviewed release tag, must equal vVERSION")
        if name == "deploy":
            sub.add_argument("--target", help="Direct child of RimWorld/Mods; existing target must have exact mod identity")
    return result


def main(argv=None):
    args = parser().parse_args(argv)
    evidence = None
    successful = False
    try:
        evidence = Evidence(ROOT, args.command)
        pipeline = Pipeline(args, evidence=evidence)
        if args.command == "test":
            pipeline.tools() if args.tools_only else pipeline.tests()
        else:
            getattr(pipeline, args.command)()
        successful = True
        return 0
    except Exception as exc:
        if evidence:
            if not any(stage["status"] == "failed" for stage in evidence.report["stages"]):
                try:
                    evidence.stage("failure", lambda stream: (_ for _ in ()).throw(exc))
                except Exception:
                    pass
            evidence.report["error_type"] = type(exc).__name__
        else:
            sys.stderr.write(("Pipeline failed before evidence creation: " + str(exc))[:1000] + "\n")
        return 1
    finally:
        if evidence:
            evidence.finish(successful)


if __name__ == "__main__":
    sys.exit(main())
