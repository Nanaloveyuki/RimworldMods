import argparse
import hashlib
from pathlib import Path
import shutil
import xml.etree.ElementTree as ET

parser = argparse.ArgumentParser(description="Package one ODF Mod without bundling shared dependencies.")
parser.add_argument("mod")
parser.add_argument("--target", type=Path)
args = parser.parse_args()
root = Path(__file__).resolve().parent.parent
if args.mod not in {p.name for p in (root / "Mods").iterdir() if p.is_dir()}:
    parser.error("Unknown Mod directory")
source = root / "Mods" / args.mod
package = ET.parse(source / "About/About.xml").getroot().findtext("packageId")
if not package or not package.startswith("odf.nanaloveyuki."):
    raise SystemExit("All Mod packageIds must start with odf.nanaloveyuki.")
target = args.target or root / "artifacts" / args.mod
if target.is_symlink() or target.resolve() == source.resolve() or source.resolve() in target.resolve().parents:
    raise SystemExit("Refusing source-directory or symlink deployment")
if target.exists():
    about = target / "About/About.xml"
    if about.is_file():
        if ET.parse(about).getroot().findtext("packageId") != package:
            raise SystemExit("Destination belongs to a different Mod")
    elif any(target.iterdir()):
        raise SystemExit("Nonempty destination has no Mod identity")
manifest = []
for folder in ("About", "Languages", "Oxygen", "1.6/Defs"):
    directory = source / folder
    if directory.is_dir():
        manifest.extend((p, p.relative_to(source)) for p in sorted(directory.rglob("*")) if p.is_file())
for name in ("LoadFolders.xml", "README.md"):
    if (source / name).is_file():
        manifest.append((source / name, Path(name)))
manifest.append((root / "LICENSE", Path("LICENSE")))
assembly = root / "tmp/build" / args.mod / f"Odf.{args.mod}.dll"
manifest.append((assembly, Path("1.6/Assemblies") / assembly.name))
for item, relative in manifest:
    if not item.is_file():
        raise SystemExit(f"Missing release input: {item}")
    destination = target / relative
    if destination.resolve() != target.resolve() / relative:
        raise SystemExit(f"Refusing deployment through symlink: {destination}")
for item, relative in manifest:
    destination = target / relative
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(item, destination)
    if hashlib.sha256(item.read_bytes()).digest() != hashlib.sha256(destination.read_bytes()).digest():
        raise SystemExit(f"SHA-256 mismatch: {relative}")
print(f"Packaged {package}: {len(manifest)} files, SHA-256 verified, {target}")
