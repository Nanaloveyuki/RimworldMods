#!/usr/bin/env bash
# Release 构建并部署所选版本；1.7 部署不复制或删除旧版运行时。
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
configuration="${CONFIGURATION:-Release}"
version="${RIMWORLD_VERSION:-${RimWorldVersion:-1.7}}"
rimworld="${RIMWORLD_DIR:-${RimWorldDir:-/mnt/e/Apps/Steam/steamapps/common/RimWorld}}"
target="$rimworld/Mods/LeadYourPetsContinued"
build_only=false
if [[ "${1:-}" == "--build-only" && "$#" == 1 ]]; then
  build_only=true
elif [[ "$#" != 0 ]]; then
  echo "Usage: deploy.sh [--build-only] (RIMWORLD_VERSION=1.7 or 1.6, RIMWORLD_DIR=game root)" >&2
  exit 1
fi
case "$version" in
  1.7) managed="$rimworld"; unity="UnityStubs.dll" ;;
  1.6) managed="$rimworld/RimWorldWin64_Data/Managed"; unity="UnityEngine.CoreModule.dll" ;;
  *) echo "RIMWORLD_VERSION must be 1.6 or 1.7." >&2; exit 1 ;;
esac
if [[ ! -f "$managed/Assembly-CSharp.dll" || ! -f "$managed/$unity" ]]; then
  echo "RimWorld $version assemblies not found: $managed (1.7 needs the .NET 10 SDK)." >&2
  exit 1
fi

assert_game_stopped() {
  if command -v powershell.exe >/dev/null 2>&1; then
    local count
    count="$(powershell.exe -NoProfile -Command '@(Get-Process RimWorld, RimWorldWin64 -ErrorAction SilentlyContinue).Count' | tr -d '\r')"
    if [[ "$count" != "0" ]]; then
      echo "Exit RimWorld (RimWorld.exe or RimWorldWin64.exe) before deploying." >&2
      exit 1
    fi
  elif [[ -r /proc/sys/kernel/osrelease ]] && [[ "$(</proc/sys/kernel/osrelease)" == *[Mm]icrosoft* ]]; then
    echo "powershell.exe is required to check Windows game processes from WSL before deployment." >&2
    exit 1
  fi
  if command -v pgrep >/dev/null 2>&1; then
    if pgrep -f '(^|/)(RimWorld|RimWorldWin64|RimWorldLinux)(\.exe)?([[:space:]]|$)' >/dev/null; then
      echo "Exit RimWorld before deploying." >&2
      exit 1
    fi
  elif ! command -v powershell.exe >/dev/null 2>&1; then
    echo "A process checker (pgrep or powershell.exe) is required before deployment." >&2
    exit 1
  fi
}
if ! "$build_only"; then assert_game_stopped; fi
for project in \
  Source/LeadYourPet.csproj \
  Guard/Source/LeadYourPetContinuedGuard.csproj
do
  dotnet build "$root/$project" -c "$configuration" -p:RimWorldDir="$rimworld" -p:RimWorldVersion="$version" --nologo -v q
done
if "$build_only"; then exit 0; fi
assert_game_stopped

python3 - "$root" "$target" "$version" <<'PY'
import hashlib
import shutil
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

root = Path(sys.argv[1]).absolute()
target = Path(sys.argv[2]).absolute()
version = sys.argv[3]
if target.is_symlink():
    raise SystemExit(f"Target is a symlink: {target}")
if target.exists():
    about = target / "About/About.xml"
    if not about.is_file():
        raise SystemExit(f"Existing target has no mod identity: {target}")
    if (ET.parse(about).findtext("packageId") or "").strip().lower() != "nanaloveyuki.leadyourpet.continued":
        raise SystemExit(f"Target belongs to another mod: {target}")
runtime = "1.7/" if version == "1.7" else ""
folders = ("About", runtime + "Defs", runtime + "Languages", runtime + "Guard/Languages")
assemblies = (runtime + "Assemblies/LeadYourPet.dll", runtime + "Guard/Assemblies/LeadYourPetContinuedGuard.dll")
files = assemblies + ("LoadFolders.xml", "NOTICE", "README.md", "LICENSE")
for relative in assemblies:
    if not (root / relative).is_file():
        raise SystemExit(f"Missing runtime assembly: {relative}")
sources = []
for folder in folders:
    path = root / folder
    if path.is_dir():
        sources.extend(item for item in path.rglob("*") if item.is_file())
for relative in files:
    path = root / relative
    if path.is_file():
        sources.append(path)

def assert_destination(path):
    for parent in (path, *path.parents):
        if parent.is_symlink():
            raise SystemExit(f"Destination is a symlink: {parent}")
        if parent == target:
            return
    raise SystemExit(f"Destination is outside target: {path}")

copied = []
for source in sources:
    relative = source.relative_to(root)
    destination = target / relative
    assert_destination(destination)
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, destination)
    if hashlib.sha256(source.read_bytes()).digest() != hashlib.sha256(destination.read_bytes()).digest():
        raise SystemExit(f"hash mismatch: {relative}")
    copied.append(relative)

# Prune only selected runtime assets and shared About metadata, never legacy files in 1.7 mode.
for folder in folders:
    live = target / folder
    if not live.is_dir():
        continue
    assert_destination(live)
    for destination in live.rglob("*"):
        assert_destination(destination)
        if destination.is_file() and not (root / destination.relative_to(target)).is_file():
            destination.unlink()

print(f"Deployed RimWorld {version} runtime and SHA-256 verified {len(copied)} files: {target}")
PY
