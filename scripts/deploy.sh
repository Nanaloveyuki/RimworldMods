#!/usr/bin/env bash
# Build only this mod; deploy only its own DLL and data, never game/mod dependencies.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd -P)"
rimworld="${RIMWORLD_DIR:-${RimWorldDir:-/mnt/e/Apps/Steam/steamapps/common/RimWorld}}"
configuration="${CONFIGURATION:-Release}"
build_only=0
if [[ $# -gt 0 ]]; then
  if [[ $# -ne 1 || "$1" != "--build-only" ]]; then
    echo "Usage: $0 [--build-only]" >&2
    exit 1
  fi
  build_only=1
fi
if [[ "$configuration" != "Release" && "$configuration" != "Debug" ]]; then
  echo "CONFIGURATION must be Release or Debug." >&2
  exit 1
fi
managed="$rimworld/RimWorldWin64_Data/Managed"
if [[ ! -f "$managed/Assembly-CSharp.dll" ]]; then
  managed="$rimworld/RimWorldLinux_Data/Managed"
fi
if [[ ! -f "$managed/Assembly-CSharp.dll" ]]; then
  echo "RimWorld managed assemblies not found: $rimworld" >&2
  exit 1
fi
build_args=("-p:RimWorldDir=$rimworld" "-p:RimWorldManagedDir=$managed")
if [[ -n "${HARMONY_ASSEMBLY_PATH:-}" ]]; then
  build_args+=("-p:HarmonyAssemblyPath=$HARMONY_ASSEMBLY_PATH")
fi

check_game_stopped() {
  # WSL must check Windows processes as well as Linux processes. Fail closed
  # if Windows interop exists but querying the host processes fails.
  if command -v powershell.exe >/dev/null 2>&1; then
    local count
    count="$(powershell.exe -NoProfile -NonInteractive -Command '$ErrorActionPreference = "Stop"; @(Get-Process -Name RimWorldWin64,RimWorldWin64Steam,RimWorldWin,RimWorld -ErrorAction SilentlyContinue).Count' | tr -d '\r')"
    if [[ "$count" != "0" ]]; then
      echo "Exit RimWorld before deploying (Windows process check)." >&2
      exit 1
    fi
  elif [[ -n "${WSL_INTEROP:-}" || -n "${WSL_DISTRO_NAME:-}" ]]; then
    echo "Cannot check Windows RimWorld processes: powershell.exe is unavailable in WSL." >&2
    exit 1
  fi
  if ! command -v pgrep >/dev/null 2>&1; then
    echo "pgrep is required to confirm RimWorld is stopped." >&2
    exit 1
  fi
  local result=0
  pgrep -x 'RimWorld|RimWorldLinux|RimWorldWin64|RimWorldWin64Steam|RimWorldWin' >/dev/null || result=$?
  if [[ "$result" == "0" ]]; then
    echo "Exit RimWorld before deploying." >&2
    exit 1
  elif [[ "$result" != "1" ]]; then
    echo "Could not check RimWorld process state." >&2
    exit 1
  fi
}

if [[ "$build_only" == "0" ]]; then
  check_game_stopped
fi
dotnet build "$root/WashingMachine.csproj" --configuration "$configuration" "${build_args[@]}" --nologo
if [[ "$build_only" == "1" ]]; then
  exit 0
fi
check_game_stopped
python3 "$root/scripts/deploy.py" "$root" "$rimworld"
