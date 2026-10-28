#!/usr/bin/env python3
"""Build, stage and SHA-256 verify one Mod without pruning unrelated installed files."""
import argparse
import copy
import hashlib
import importlib.util
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
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
CONFIGS = ROOT / "scripts/deploy"
IS_WSL = sys.platform == "linux" and "microsoft" in platform.release().lower()
GAME_NAMES = {"rimworld", "rimworldwin", "rimworldwin64", "rimworldwin64steam", "rimworldlinux", "rimworldmac"}


def run(command, **kwargs):
    print("+ " + subprocess.list2cmdline([str(value) for value in command]), flush=True)
    subprocess.run([str(value) for value in command], check=True, **kwargs)


def local_path(value):
    value = os.path.expanduser(str(value))
    if IS_WSL and (re.match(r"^[A-Za-z]:", value) or value.startswith("\\\\")):
        value = subprocess.check_output(["wslpath", "-u", value], text=True).strip()
    return Path(os.path.abspath(value))


def windows_path(value):
    return subprocess.check_output(["wslpath", "-w", str(value)], text=True).strip() if IS_WSL else str(value)


def powershell(script):
    candidates = ("powershell.exe",) if IS_WSL else ("pwsh", "powershell.exe", "powershell")
    executable = next((shutil.which(name) for name in candidates if shutil.which(name)), None)
    if not executable:
        raise RuntimeError("PowerShell is required for Windows safety checks (also from WSL).")
    return subprocess.check_output([executable, "-NoProfile", "-NonInteractive", "-Command", script], text=True).strip()


def reject_links(path, native=True):
    for part in (path, *path.parents):
        try:
            info = part.lstat()
        except FileNotFoundError:
            continue
        if stat.S_ISLNK(info.st_mode) or getattr(info, "st_file_attributes", 0) & 0x400:
            raise RuntimeError(f"Refusing symlink/reparse path: {part}")
        if part != path and not stat.S_ISDIR(info.st_mode):
            raise RuntimeError(f"Ancestor is not a directory: {part}")
    if native and IS_WSL and re.match(r"^/mnt/[a-zA-Z](?:/|$)", str(path)):
        escaped = windows_path(path).replace("'", "''")
        powershell("$ErrorActionPreference='Stop'; $p='" + escaped + "'; while ($p) { "
                   "if (Test-Path -LiteralPath $p) { $i=Get-Item -LiteralPath $p -Force; "
                   "if ($i.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw ('Unsafe reparse path: '+$p) } }; "
                   "$parent=[IO.Directory]::GetParent($p); if ($null -eq $parent) { break }; $p=$parent.FullName }")


def tree_files(directory, native=True):
    reject_links(directory, native=native)
    if not directory.is_dir():
        raise RuntimeError(f"Required directory missing: {directory}")
    if native and IS_WSL and re.match(r"^/mnt/[a-zA-Z](?:/|$)", str(directory)):
        escaped = windows_path(directory).replace("'", "''")
        powershell("$ErrorActionPreference='Stop'; Get-ChildItem -LiteralPath '" + escaped +
                   "' -Recurse -Force | ForEach-Object { if ($_.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw ('Unsafe reparse entry: '+$_.FullName) } }")
    for item in sorted(directory.iterdir()):
        reject_links(item, native=False)
        if item.is_dir():
            yield from tree_files(item, native=False)
        elif item.is_file():
            yield item
        else:
            raise RuntimeError(f"Not a regular file: {item}")


def identity(directory, expected):
    about = directory / "About/About.xml"
    reject_links(about)
    metadata = ET.parse(about).getroot()
    ids = metadata.findall("packageId")
    if metadata.tag != "ModMetaData" or len(ids) != 1 or (ids[0].text or "").strip().casefold() != expected.casefold():
        raise RuntimeError(f"Mod identity does not match {expected}: {about}")


def sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def reject_running_game():
    if os.name == "nt" or IS_WSL:
        names = ",".join("'" + name + "'" for name in sorted(GAME_NAMES))
        count = powershell("$ErrorActionPreference='Stop'; $names=@(" + names + "); "
                           "@(Get-Process -ErrorAction Stop | Where-Object { $names -contains $_.ProcessName.ToLowerInvariant() }).Count")
        if count != "0":
            raise RuntimeError("Exit RimWorld before deploying (Windows game process detected).")
    if os.name != "nt":
        output = subprocess.check_output(["ps", "-A", "-o", "comm="], text=True)
        if not output.strip():
            raise RuntimeError("Cannot inspect game processes; deployment refused.")
        if any(Path(name.strip()).name.lower().removesuffix(".exe") in GAME_NAMES for name in output.splitlines()):
            raise RuntimeError("Exit RimWorld before deploying (game process detected).")


def merge(base, override):
    for key, value in override.items():
        if isinstance(value, dict) and isinstance(base.get(key), dict):
            merge(base[key], value)
        else:
            base[key] = value
    return base


def expand(value, context):
    return str(value).format_map(context)


def ps_command(script, parameters):
    executable = next((shutil.which(name) for name in ("pwsh", "powershell.exe", "powershell") if shutil.which(name)), None)
    if not executable:
        raise RuntimeError("PowerShell 7 (pwsh) is required for this project's build entrypoint.")
    if Path(executable).name.lower() == "powershell.exe" and "-AsHashtable" in script.read_text(encoding="utf-8"):
        raise RuntimeError("This build entrypoint requires PowerShell 7; install pwsh.")
    native = IS_WSL and executable.lower().endswith(".exe")
    command = [executable, "-NoProfile", "-NonInteractive", "-File", windows_path(script) if native else str(script)]
    for key, value in parameters.items():
        if value is False or value is None:
            continue
        command.append("-" + key)
        if value is not True:
            text = str(value)
            if native and text.startswith("/"):
                text = windows_path(text)
            command.append(text)
    return command


def module_pipeline(source):
    spec = importlib.util.spec_from_file_location("rhah_release_pipeline", source / "scripts/rhah_pipeline.py")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def build(config, args, context):
    for step in config.get("build", []):
        if step[0] == "powershell":
            parameters = {key: expand(value, context) if isinstance(value, str) else value for key, value in step[2].items()}
            overrides = dict(args.property)
            for property_name, parameter_name in config.get("powershell_properties", {}).items():
                if property_name in overrides:
                    parameters[parameter_name] = overrides[property_name]
            run(ps_command(ROOT / expand(step[1], context), parameters), cwd=ROOT)
        elif step[0] == "dotnet":
            properties = {"RimWorldDir": context["game"], "RimWorldManagedDir": context["managed"],
                          "RimWorldVersion": context["version"], "GameVersion": context["version"]}
            properties.update({key: expand(value, context) for key, value in step[2].items()})
            properties.update(dict(args.property))
            run([args.dotnet, "build", ROOT / expand(step[1], context), "-c", args.configuration, "--nologo"] +
                [f"-p:{key}={value}" for key, value in properties.items()], cwd=ROOT)
        else:
            command = [expand(value, context) for value in step]
            if config.get("rhah"):
                overrides = dict(args.property)
                for property_name, option in (("OxygenDslDir", "--oxygen-dsl-dir"), ("HarmonyPath", "--harmony-path")):
                    if property_name in overrides:
                        command.extend([option, overrides[property_name]])
            run(command, cwd=ROOT)


def manifest(config, source, context):
    found = {}
    for entry in config.get("files", []):
        if isinstance(entry, str):
            entry = {"source": entry}
        pattern = expand(entry["source"], context)
        matches = sorted(source.glob(pattern)) if any(char in pattern for char in "*?[") else [source / pattern]
        matches = [path for path in matches if path.exists()]
        if not matches and not entry.get("optional", False):
            raise RuntimeError(f"Missing release input: {source / pattern}")
        for path in matches:
            reject_links(path)
            destination = Path(expand(entry.get("target", pattern), context)) if "target" in entry else path.relative_to(source)
            if destination.is_absolute() or ".." in destination.parts:
                raise RuntimeError(f"Release path escapes staging: {destination}")
            if path.is_dir():
                for item in tree_files(path):
                    relative = destination / item.relative_to(path)
                    found[relative.as_posix()] = item
            elif path.is_file():
                found[destination.as_posix()] = path
            else:
                raise RuntimeError(f"Not a regular release input: {path}")
    for required in config.get("required", []):
        path = expand(required, context)
        if path not in found or not found[path].stat().st_size:
            raise RuntimeError(f"Required nonempty release input missing: {path}")
    return found


def guard_target(target, game, source, config, files):
    reject_links(target)
    if target.parent != game / "Mods":
        raise RuntimeError(f"Target must be a direct child of {game / 'Mods'}")
    if not target.parent.is_dir():
        raise RuntimeError(f"RimWorld Mods directory missing: {target.parent}")
    if target == ROOT or ROOT in target.parents or target in ROOT.parents or target == source:
        raise RuntimeError("Deployment target must be separate from the repository.")
    if target.name.casefold() in {value.casefold() for value in config.get("forbidden_targets", [])}:
        raise RuntimeError(f"Forbidden deployment target: {target}")
    if target.exists():
        if not target.is_dir():
            raise RuntimeError(f"Target is not a directory: {target}")
        list(tree_files(target))
        if (target / "About/About.xml").is_file():
            identity(target, config["package_id"])
        elif any(target.iterdir()):
            raise RuntimeError(f"Nonempty target has no About.xml: {target}")
        elif config.get("require_existing"):
            raise RuntimeError(f"Target must already have the correct Mod identity: {target}")
    elif config.get("require_existing"):
        raise RuntimeError(f"This Mod requires an existing installed target: {target}")
    elif config.get("new_target_name") and target.name != config["target"]:
        raise RuntimeError(f"New target must be named {config['target']}")
    for folder, owned in config.get("owned_assemblies", {}).items():
        base = target / folder
        if base.is_dir():
            unexpected = [path.name for path in tree_files(base) if path.name not in owned]
            if unexpected:
                raise RuntimeError(f"Unowned assembly content in {base}: {', '.join(unexpected)}")
    for relative in files:
        destination = target / relative
        reject_links(destination, native=False)
        if destination.exists() and not destination.is_file():
            raise RuntimeError(f"Destination is not a file: {destination}")


def copy_verified(files, target, preserve_ids=False, preserve_legacy=False):
    for relative, source in files.items():
        destination = target / relative
        reject_links(destination, native=False)
        if preserve_ids and Path(relative).name.lower() == "publishedfileid.txt" and destination.exists():
            continue
        if preserve_legacy and Path(relative).parts[0] == "1.6" and destination.exists():
            continue
        expected = sha256(source)
        destination.parent.mkdir(parents=True, exist_ok=True)
        reject_links(destination, native=False)
        shutil.copy2(source, destination)
        if sha256(destination) != expected:
            raise RuntimeError(f"SHA-256 mismatch: {destination}")


def stage_release(config, source, context, output):
    pipeline = module_pipeline(source) if config.get("rhah") else None
    files = pipeline.release_files(source) if pipeline else manifest(config, source, context)
    reject_links(output)
    if output.exists():
        identity(output, config["package_id"])
        list(tree_files(output))
    output.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix=".release-", dir=output.parent) as temporary:
        staging = Path(temporary) / "release"
        staging.mkdir()
        copy_verified(files, staging)
        identity(staging, config["package_id"])
        if output.exists():
            shutil.rmtree(output)
        shutil.move(staging, output)
    return {relative: output / relative for relative in files}


def selected_configs(name, mod, override):
    path = CONFIGS / (name + ".json")
    if not path.is_file():
        raise RuntimeError(f"Unknown project: {name}; use --list")
    config = json.loads(path.read_text(encoding="utf-8"))
    if override:
        merge(config, json.loads(local_path(override).read_text(encoding="utf-8")))
    if "refuse" in config:
        raise RuntimeError(config["refuse"])
    if "mods" not in config:
        if mod:
            raise RuntimeError("--mod is only supported by mod containers.")
        return [(name, config)]
    choices = config["mods"]
    if not mod:
        raise RuntimeError("Select --mod " + "|".join([*choices, "all"]))
    if mod != "all" and mod not in choices:
        raise RuntimeError(f"Unknown internal Mod: {mod}")
    result = []
    for child in choices if mod == "all" else [mod]:
        merged = copy.deepcopy(config)
        del merged["mods"]
        merge(merged, choices[child])
        result.append((child, merged))
    return result


def property_pair(text):
    name, separator, value = text.partition("=")
    if not separator or not re.fullmatch(r"[A-Za-z_][A-Za-z0-9_]*", name):
        raise argparse.ArgumentTypeError("Expected MSBuild property NAME=VALUE")
    return name, value


def parser():
    result = argparse.ArgumentParser(description=__doc__)
    result.add_argument("project", nargs="?", help="Project directory name; use --list")
    result.add_argument("--list", action="store_true", help="List every release and refusal")
    result.add_argument("--mod", help="Independent Mod in a container, or all")
    result.add_argument("--build-only", action="store_true", help="Build and stage without inspecting game processes or writing to Mods")
    result.add_argument("--skip-build", action="store_true", help="Stage existing build outputs")
    result.add_argument("--rimworld-dir", default=os.environ.get("RIMWORLD_DIR") or os.environ.get("RimWorldDir"), help="Installed game root (required for deployment)")
    result.add_argument("--target", help="Explicit target: direct child of the selected game's Mods directory")
    result.add_argument("--configuration", choices=("Debug", "Release"), default=os.environ.get("CONFIGURATION", "Release"))
    result.add_argument("--version", choices=("1.6", "1.7"), default=os.environ.get("RIMWORLD_VERSION"), help="Defaults to the project's supported current version")
    result.add_argument("--config", help="JSON object overriding the selected project's configuration")
    result.add_argument("--property", type=property_pair, action="append", default=[], metavar="NAME=VALUE", help="Override an MSBuild property; repeat as needed")
    result.add_argument("--dotnet", default=os.environ.get("DOTNET", "dotnet"))
    result.add_argument("--preflight", action="store_true", help="Inspect prerequisites and target only; do not build, stage or deploy")
    return result


def main(argv=None):
    command = parser()
    args = command.parse_args(argv)
    if args.list:
        for path in sorted(CONFIGS.glob("*.json")):
            config = json.loads(path.read_text(encoding="utf-8"))
            detail = config.get("refuse") or ("--mod " + "|".join(config["mods"]) if "mods" in config else config["target"])
            print(f"{path.stem}: {detail}")
        return 0
    if not args.project:
        command.error("project is required; use --list")
    selected = selected_configs(args.project, args.mod, args.config)
    if args.target and len(selected) != 1:
        command.error("--target requires exactly one Mod")
    if not args.rimworld_dir:
        command.error("--rimworld-dir or RIMWORLD_DIR must explicitly select the installed game")
    game = local_path(args.rimworld_dir)
    for child, config in selected:
        version = args.version or config.get("default_version", "1.7")
        if version not in config.get("versions", ["1.7"]):
            raise RuntimeError(f"{args.project} supports deployment versions {config.get('versions', ['1.7'])}; preserved binaries are not rebuilt")
        if "variants" in config:
            merge(config, config["variants"][version])
        source = ROOT / config.get("source", args.project)
        reject_links(source)
        identity(source, config["package_id"])
        managed = game if version == "1.7" else next((game / folder / "Managed" for folder in ("RimWorldWin64_Data", "RimWorldLinux_Data", "RimWorldMac_Data") if (game / folder / "Managed/Assembly-CSharp.dll").is_file()), game / "RimWorldWin64_Data/Managed")
        for name in ("Assembly-CSharp.dll", "UnityStubs.dll" if version == "1.7" else "UnityEngine.CoreModule.dll"):
            reject_links(managed / name)
            if not (managed / name).is_file():
                raise RuntimeError(f"Missing installed RimWorld {version} assembly: {managed / name}")
        context = {"root": str(ROOT), "project": args.project, "source": str(source), "game": str(game),
                   "managed": str(managed), "version": version, "configuration": args.configuration,
                   "dotnet": args.dotnet, "python": sys.executable, "mod": child,
                   "runtime": "1.7/" if version == "1.7" else "",
                   "pipeline_command": "build" if args.build_only else "ci"}
        for key, value in config.get("context", {}).items():
            context[key] = expand(value, context)
        target = local_path(args.target) if args.target else game / "Mods" / config["target"]
        if not args.build_only:
            reject_running_game()
            guard_target(target, game, source, config, {})
        if args.preflight:
            print(f"Prerequisites accepted: {args.project}/{child} ({version}) -> {target}")
            continue
        if not args.skip_build:
            build(config, args, context)
        staging = ROOT / "artifacts" / args.project
        if "mods" in json.loads((CONFIGS / (args.project + ".json")).read_text(encoding="utf-8")):
            staging /= child
        staging /= "release"
        files = stage_release(config, source, context, staging)
        print(f"Staged and SHA-256 verified {len(files)} files: {staging}")
        if args.build_only:
            continue
        excluded = config.get("legacy_paths", []).copy() if version == "1.7" else ["1.7"]
        if version == "1.7" and not config.get("install_missing_legacy"):
            excluded.append("1.6")
        deployed = {relative: path for relative, path in files.items()
                    if not any(relative == prefix or relative.startswith(prefix + "/") for prefix in excluded)}
        guard_target(target, game, source, config, deployed)
        reject_running_game()
        copy_verified(deployed, target, preserve_ids=True, preserve_legacy=version == "1.7")
        print(f"Deployed and SHA-256 verified: {target}; installed legacy files and PublishedFileId preserved")
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (RuntimeError, OSError, ValueError, ET.ParseError, subprocess.SubprocessError) as error:
        print(f"Deployment refused: {error}", file=sys.stderr)
        sys.exit(1)
