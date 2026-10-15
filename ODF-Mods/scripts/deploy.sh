#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
rimworld="${RIMWORLD_DIR:-/mnt/e/Apps/Steam/steamapps/common/RimWorld}"
configuration="${CONFIGURATION:-Release}"
mod="${1:-all}"
mode="${2:-}"
if [[ $# -gt 2 || ( -n "$mode" && "$mode" != "--build-only" ) || "$mod" == *'/'* || "$mod" == *'..'* ]]; then
  echo "Usage: bash scripts/deploy.sh [all|PrisonerNames] [--build-only]" >&2
  exit 1
fi
check_stopped() {
  if command -v powershell.exe >/dev/null 2>&1; then
    local count
    count="$(powershell.exe -NoProfile -Command '@(Get-Process -Name RimWorldWin64,RimWorldWin64Steam,RimWorldWin,RimWorld -ErrorAction SilentlyContinue).Count' | tr -d '\r')"
    if [[ "$count" != "0" ]]; then echo "Exit RimWorld before deploying. Use --build-only to package without deployment." >&2; exit 1; fi
  elif pgrep -x 'RimWorld|RimWorldLinux|RimWorldWin64' >/dev/null; then
    echo "Exit RimWorld before deploying." >&2; exit 1
  fi
}
mods=()
if [[ "$mod" == "all" ]]; then
  for folder in "$root"/Mods/*; do [[ -f "$folder/About/About.xml" ]] && mods+=("${folder##*/}"); done
elif [[ -f "$root/Mods/$mod/About/About.xml" ]]; then
  mods=("$mod")
else
  echo "Unknown Mod: $mod" >&2; exit 1
fi
if [[ "$mode" != "--build-only" ]]; then check_stopped; fi
dotnet build "$root/ODF-Mods.slnx" -c "$configuration" -p:RimWorldDir="$rimworld" --nologo
for name in "${mods[@]}"; do python3 "$root/scripts/package.py" "$name"; done
if [[ "$mode" == "--build-only" ]]; then exit 0; fi
check_stopped
for name in "${mods[@]}"; do python3 "$root/scripts/package.py" "$name" --target "$rimworld/Mods/ODF-$name"; done
