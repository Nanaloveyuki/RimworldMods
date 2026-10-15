#!/usr/bin/env bash
# Release 构建并部署到 RimWorld Mods。游戏进程存在时拒绝覆盖。
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
configuration="${CONFIGURATION:-Release}"
rimworld="${RIMWORLD_DIR:-${RimWorldDir:-/mnt/e/Apps/Steam/steamapps/common/RimWorld}}"
target="$rimworld/Mods/LeadYourPetsContinued"
package_id="nanaloveyuki.leadyourpet.continued"

if [[ ! -d "$rimworld/RimWorldWin64_Data/Managed" ]]; then
  echo "RimWorld managed assemblies not found: $rimworld" >&2
  exit 1
fi

if command -v powershell.exe >/dev/null 2>&1; then
  count="$(powershell.exe -NoProfile -Command "@(Get-Process RimWorldWin64 -ErrorAction SilentlyContinue).Count" | tr -d '\r')"
  if [[ "$count" != "0" ]]; then
    echo "Exit RimWorld before deploying." >&2
    exit 1
  fi
fi

for project in \
  Source/LeadYourPet.csproj \
  Guard/Source/LeadYourPetContinuedGuard.csproj
do
  dotnet build "$root/$project" -c "$configuration" -p:RimWorldDir="$rimworld" --nologo -v q
done

if [[ -L "$target" ]]; then
  echo "Target is a symlink: $target" >&2
  exit 1
fi
if [[ -e "$target/About/About.xml" ]] && ! grep -q "$package_id" "$target/About/About.xml"; then
  echo "Target belongs to another mod: $target" >&2
  exit 1
fi

python3 - "$root" "$target" <<'PY'
import hashlib
import shutil
import sys
from pathlib import Path

root = Path(sys.argv[1])
target = Path(sys.argv[2])
folders = (
    "About",
    "Defs",
    "Languages",
    "Guard/Languages",
)
files = (
    "Assemblies/LeadYourPet.dll",
    "Guard/Assemblies/LeadYourPetContinuedGuard.dll",
    "LoadFolders.xml",
    "NOTICE",
    "README.md",
    "LICENSE",
)
sources = []
for folder in folders:
    path = root / folder
    if path.is_dir():
        sources.extend(item for item in path.rglob("*") if item.is_file())
for relative in files:
    path = root / relative
    if path.is_file():
        sources.append(path)

copied = []
for source in sources:
    relative = source.relative_to(root)
    destination = target / relative
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, destination)
    if hashlib.sha256(source.read_bytes()).digest() != hashlib.sha256(destination.read_bytes()).digest():
        raise SystemExit(f"hash mismatch: {relative}")
    copied.append(relative)

for folder in folders:
    live = target / folder
    if not live.is_dir():
        continue
    for destination in live.rglob("*"):
        if destination.is_file() and not (root / destination.relative_to(target)).is_file():
            destination.unlink()

print(f"Deployed and SHA-256 verified {len(copied)} files: {target}")
PY
