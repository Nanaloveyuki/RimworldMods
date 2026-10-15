import hashlib
from pathlib import Path
import shutil
import sys
import xml.etree.ElementTree as ET

root = Path(sys.argv[1]).resolve()
target = Path(sys.argv[2])
package_id = "nanaloveyuki.oxygendsl"


def identity(about):
    if ET.parse(about).getroot().findtext("packageId") != package_id:
        raise SystemExit(f"Unexpected mod identity: {about}")


identity(root / "About/About.xml")
if target.is_symlink() or target.resolve() == root:
    raise SystemExit("Refusing symlink or source-directory deployment")
if target.exists():
    about = target / "About/About.xml"
    if about.is_file():
        identity(about)
    elif any(target.iterdir()):
        raise SystemExit("Non-empty target has no mod identity")

manifest = []
for folder in ("About", "Languages", "docs", "Examples", "Oxygen", "WebEditor", "1.6/Defs"):
    source = root / folder
    if not source.is_dir():
        raise SystemExit(f"Missing release directory: {source}")
    manifest.extend((item, item.relative_to(root)) for item in sorted(source.rglob("*")) if item.is_file())
for name in ("LoadFolders.xml", "LICENSE", "THIRD_PARTY_NOTICES.md"):
    manifest.append((root / name, Path(name)))
if (root / "README.md").is_file():
    manifest.append((root / "README.md", Path("README.md")))
for name in ("Oxygendsl.Core", "Oxygendsl.Mod"):
    manifest.append((root / f"tmp/build/{name}.dll", Path(f"1.6/Assemblies/{name}.dll")))
    pdb = root / f"tmp/build/{name}.pdb"
    if pdb.is_file():
        manifest.append((pdb, Path(f"1.6/Assemblies/{name}.pdb")))
for name, folder in (("Oxygendsl.Rhah", "Rhah"), ("Oxygendsl.IrisMenus", "IrisMenus")):
    adapter = root / f"tmp/build/{name}.dll"
    if not adapter.is_file():
        raise SystemExit(f"Missing optional adapter build input: {adapter}")
    manifest.append((adapter, Path(f"{folder}/Assemblies/{name}.dll")))
for source, relative in manifest:
    if not source.is_file():
        raise SystemExit(f"Missing release input: {source}")
    destination = target / relative
    if destination.resolve() != target.resolve() / relative:
        raise SystemExit(f"Refusing deployment through symlink: {destination}")
for source, relative in manifest:
    destination = target / relative
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, destination)
    if hashlib.sha256(source.read_bytes()).digest() != hashlib.sha256(destination.read_bytes()).digest():
        raise SystemExit(f"SHA-256 mismatch: {relative}")
print(f"Deployed and SHA-256 verified {len(manifest)} files: {target}")
