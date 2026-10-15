#!/usr/bin/env python3
"""Deploy only this patch's allowlisted files; never prune the destination."""

import argparse
import hashlib
import os
from pathlib import Path
import re
import shutil
import stat
import subprocess
import sys
import xml.etree.ElementTree as ET

PACKAGE_ID = "nanaloveyuki.radiusui.missioncontrol.fixes"
DESTINATION_NAME = "RadiusUI-MissionControl-Fixes"
FILES = (
    "1.6/Assemblies/RadiusUI.MissionControl.Fixes.dll",
    "About/About.xml",
    "LoadFolders.xml",
    "LICENSE",
    "README.md",
)
GAME_PROCESSES = {"rimworld", "rimworldwin", "rimworldwin64", "rimworldlinux", "rimworldmac"}
IS_WSL = sys.platform == "linux" and "microsoft" in os.uname().release.lower()


def fail(message):
    raise RuntimeError(message)


def absolute(path):
    return Path(os.path.abspath(os.path.expanduser(str(path))))


def powershell(script):
    executable = "powershell.exe" if IS_WSL else "powershell"
    if not shutil.which(executable):
        fail(f"{executable} is required to check Windows processes and unsafe paths.")
    result = subprocess.run(
        [executable, "-NoProfile", "-NonInteractive", "-Command", script],
        capture_output=True, text=True, check=False,
    )
    if result.returncode:
        fail(f"Windows safety check failed: {result.stderr.strip() or result.stdout.strip()}")
    return result.stdout.strip()


def windows_path(path):
    # Native Windows lstat exposes reparse attributes directly; only DrvFS needs a second check.
    if os.name == "nt":
        return None
    if IS_WSL and re.match(r"^/mnt/[a-zA-Z](?:/|$)", str(path)):
        result = subprocess.run(["wslpath", "-w", str(path)], capture_output=True, text=True, check=True)
        return result.stdout.strip()
    return None


def reject_links(path):
    # Check lexical ancestors before resolving, including dangling links and junctions.
    for part in reversed((path, *path.parents)):
        try:
            info = part.lstat()
        except FileNotFoundError:
            continue
        if stat.S_ISLNK(info.st_mode) or getattr(info, "st_file_attributes", 0) & 0x400:
            fail(f"Unsafe symlink/reparse path: {part}")
    native = windows_path(path)
    if native is not None:
        # DrvFS may expose a Windows junction as an ordinary directory. Check it natively too.
        escaped = native.replace("'", "''")
        powershell(
            "$ErrorActionPreference='Stop'; "
            f"$p=[IO.Path]::GetFullPath('{escaped}'); "
            "while ($p) { "
            "if (Test-Path -LiteralPath $p) { "
            "$item=Get-Item -LiteralPath $p -Force; "
            "if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) "
            "{ throw ('Unsafe reparse path: '+$p) } }; "
            "$parent=[IO.Directory]::GetParent($p); "
            "if ($null -eq $parent) { break }; $p=$parent.FullName }"
        )


def game_managed(game):
    reject_links(game)
    for folder in ("RimWorldWin64_Data", "RimWorldLinux_Data", "RimWorldMac_Data"):
        managed = game / folder / "Managed"
        if managed.is_dir() and (managed / "Assembly-CSharp.dll").is_file():
            reject_links(managed / "Assembly-CSharp.dll")
            return managed
    fail(f"Real RimWorld managed folder with Assembly-CSharp.dll not found: {game}")


def identity(about):
    reject_links(about)
    if not about.is_file():
        fail(f"Missing About/About.xml identity: {about}")
    try:
        metadata = ET.parse(about).getroot()
    except ET.ParseError as error:
        fail(f"Invalid mod identity XML: {about}: {error}")
    package_ids = metadata.findall("packageId")
    if metadata.tag != "ModMetaData" or len(package_ids) != 1 or (package_ids[0].text or "").strip().lower() != PACKAGE_ID:
        fail(f"Mod identity does not match {PACKAGE_ID}: {about}")


def guard_destination(root, game):
    target = game / "Mods" / DESTINATION_NAME
    reject_links(target)
    if target == root or root in target.parents or target in root.parents:
        fail(f"Source and deployment destination must be separate: {target}")
    if target.exists():
        if not target.is_dir():
            fail(f"Deployment destination is not a directory: {target}")
        identity(target / "About" / "About.xml")
    for relative in FILES:
        destination = target / relative
        reject_links(destination)
        if destination.exists() and not destination.is_file():
            fail(f"Deployment file path is not a regular file: {destination}")
    return target


def normalized_process(value):
    name = value.replace("\\", "/").rsplit("/", 1)[-1].lower()
    return name.removesuffix(".exe")


def reject_running_game():
    if os.name == "nt" or IS_WSL:
        names = ",".join(f"'{name}'" for name in sorted(GAME_PROCESSES))
        count = powershell(
            "$ErrorActionPreference='Stop'; "
            f"$names=@({names}); "
            "@(Get-Process | Where-Object { $names -contains $_.ProcessName.ToLowerInvariant() }).Count"
        )
        if count != "0":
            fail("Exit RimWorld before deploying (Windows game process detected).")
    if sys.platform == "linux":
        for process in Path("/proc").iterdir():
            if not process.name.isdigit():
                continue
            try:
                names = [process.joinpath("comm").read_text().strip()]
            except (FileNotFoundError, ProcessLookupError, PermissionError):
                continue
            try:
                names.append(os.readlink(process.joinpath("exe")))
            except OSError:
                pass
            try:
                command = process.joinpath("cmdline").read_bytes().split(b"\0", 1)[0]
                names.append(os.fsdecode(command))
            except (FileNotFoundError, ProcessLookupError, PermissionError):
                pass
            if any(normalized_process(name) in GAME_PROCESSES for name in names):
                fail(f"Exit RimWorld before deploying (Linux game process {process.name} detected).")


def sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for block in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--rimworld-dir", default=os.environ.get("RIMWORLD_DIR") or os.environ.get("RimWorldDir") or (
        r"E:\Apps\Steam\steamapps\common\RimWorld" if os.name == "nt" else "/mnt/e/Apps/Steam/steamapps/common/RimWorld"))
    parser.add_argument("--preflight", action="store_true", help="Validate safety before the entrypoint builds.")
    parser.add_argument("--build-only", action="store_true", help="Validate game/source paths without deploying or checking game processes.")
    args = parser.parse_args()
    root = absolute(Path(__file__).parent.parent)
    game = absolute(args.rimworld_dir)
    reject_links(root)
    managed = game_managed(game)
    if args.build_only:
        print(f"Build prerequisites verified: {managed}")
        return
    identity(root / "About" / "About.xml")
    target = guard_destination(root, game)
    reject_running_game()
    if args.preflight:
        print(f"Deployment safety verified: {target}")
        return
    sources = []
    for relative in FILES:
        source = root / relative
        reject_links(source)
        if not source.is_file():
            fail(f"Required package file is missing: {source}")
        sources.append((relative, source, sha256(source)))
    # Recheck immediately before writing. Only allowlisted files may be overwritten.
    guard_destination(root, game)
    reject_running_game()
    for relative, source, expected in sources:
        destination = target / relative
        reject_links(destination)
        destination.parent.mkdir(parents=True, exist_ok=True)
        reject_links(destination)
        shutil.copy2(source, destination)
        actual = sha256(destination)
        if actual != expected:
            fail(f"SHA-256 mismatch: {relative}")
        print(f"{actual}  {relative}")
    print(f"Deployed and SHA-256 verified {len(FILES)} files: {target}")


if __name__ == "__main__":
    try:
        main()
    except (RuntimeError, OSError, subprocess.SubprocessError) as error:
        print(f"Deployment refused: {error}", file=sys.stderr)
        sys.exit(1)
