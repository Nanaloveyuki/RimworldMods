#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
rimworld="${RIMWORLD_DIR:-/mnt/e/Apps/Steam/steamapps/common/RimWorld}"
configuration="${CONFIGURATION:-Release}"
mode="${1:-}"
if [[ $# -gt 1 || ( -n "$mode" && "$mode" != "--build-only" ) ]]; then
  echo "Usage: bash scripts/deploy.sh [--build-only]" >&2
  exit 1
fi
check_stopped() {
  if command -v powershell.exe >/dev/null 2>&1; then
    local count
    count="$(powershell.exe -NoProfile -Command '@(Get-Process -Name RimWorldWin64,RimWorldWin64Steam,RimWorldWin,RimWorld -ErrorAction SilentlyContinue).Count' | tr -d '\r')"
    if [[ "$count" != "0" ]]; then echo "Exit RimWorld before deploying." >&2; exit 1; fi
  elif pgrep -x 'RimWorld|RimWorldLinux|RimWorldWin64' >/dev/null; then
    echo "Exit RimWorld before deploying." >&2
    exit 1
  fi
}
if [[ "$mode" != "--build-only" ]]; then check_stopped; fi
npm --prefix "$root/Tools/vscode" ci --no-audit --no-fund
node "$root/Tools/web-editor/build.mjs"
irismenus="${IRISMENUS_SOURCE_DIR:-$root/../IrisMenus}"
dotnet build "$irismenus/Source/IrisMenus.csproj" -c "$configuration" -p:RimWorldVersion=1.7 -p:RimWorldManagedDir="$rimworld" --nologo
dotnet build "$root/Oxygendsl.slnx" -c "$configuration" -p:RimWorldDir="$rimworld" -p:IrisMenusSourceDir="$irismenus" --nologo
if [[ "$mode" == "--build-only" ]]; then exit 0; fi
check_stopped
python3 "$root/scripts/deploy.py" "$root" "$rimworld/Mods/Oxygendsl"
