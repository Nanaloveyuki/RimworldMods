#!/usr/bin/env python3
"""Copy the Washing Machine release manifest without changing unrelated files."""
import hashlib
import os
from pathlib import Path
import shutil
import stat
import sys
import xml.etree.ElementTree as ET

PACKAGE_ID = "nanaloveyuki.washingmachine"
DATA_FOLDERS = ("About", "Common")


def fail(message):
    raise SystemExit(message)


def reject_links(path):
    # lstat every existing ancestor: resolving first would hide a link.
    for part in (path, *path.parents):
        try:
            info = part.lstat()
        except FileNotFoundError:
            continue
        if stat.S_ISLNK(info.st_mode) or getattr(info, "st_file_attributes", 0) & 0x400:
            fail(f"Refusing filesystem link/reparse point: {part}")


def identity(about):
    reject_links(about)
    try:
        value = ET.parse(about).getroot().findtext("packageId", "").strip().lower()
    except (OSError, ET.ParseError) as error:
        fail(f"Cannot read mod identity {about}: {error}")
    if value != PACKAGE_ID:
        fail(f"Unexpected mod identity: {about}")


def data_files(folder):
    reject_links(folder)
    if not folder.is_dir():
        fail(f"Required data directory not found: {folder}")
    for item in sorted(folder.iterdir()):
        reject_links(item)
        if item.is_dir():
            yield from data_files(item)
        elif item.is_file():
            yield item
        else:
            fail(f"Unsupported filesystem entry: {item}")


def sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.digest()


def main():
    if len(sys.argv) != 3:
        fail("Usage: deploy.py SOURCE_ROOT RIMWORLD_DIR")
    root = Path(os.path.abspath(sys.argv[1]))
    game = Path(os.path.abspath(sys.argv[2]))
    target = game / "Mods" / "WashingMachine"
    reject_links(root)
    reject_links(game)
    reject_links(target)
    if not (game / "Mods").is_dir():
        fail(f"RimWorld Mods directory not found: {game / 'Mods'}")
    if target == root or root in target.parents or target in root.parents:
        fail("Deployment target and source must not contain each other.")
    identity(root / "About" / "About.xml")
    if target.exists():
        if not target.is_dir():
            fail(f"Deployment target is not a directory: {target}")
        # Reject links even in untouched files; never traverse them.
        list(data_files(target))
        about = target / "About" / "About.xml"
        if about.is_file():
            identity(about)
        elif any(target.iterdir()):
            fail(f"Non-empty target has no matching About.xml: {target}")

    manifest = [(root / "1.7/Assemblies/WashingMachine.dll", Path("1.7/Assemblies/WashingMachine.dll"))]
    for folder in DATA_FOLDERS:
        manifest.extend((item, item.relative_to(root)) for item in data_files(root / folder))
    manifest.extend((root / name, Path(name)) for name in ("LoadFolders.xml", "README.md", "LICENSE"))
    # Validate the complete manifest and all destination ancestors before writing.
    for source, relative in manifest:
        reject_links(source)
        if not source.is_file():
            fail(f"Required source file not found: {source}")
        destination = target / relative
        reject_links(destination)
        if destination.exists() and not destination.is_file():
            fail(f"Destination is not a regular file: {destination}")
        for parent in destination.parents:
            if parent.exists() and not parent.is_dir():
                fail(f"Destination ancestor is not a directory: {parent}")
    hashes = [(source, relative, sha256(source)) for source, relative in manifest]
    for source, relative, expected in hashes:
        destination = target / relative
        reject_links(destination)
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, destination)
        if sha256(destination) != expected:
            fail(f"SHA-256 mismatch: {relative}")
    print(f"Deployed and SHA-256 verified {len(manifest)} files: {target}")
    print("Unrelated files and ModsConfig.xml were not changed.")


if __name__ == "__main__":
    main()
