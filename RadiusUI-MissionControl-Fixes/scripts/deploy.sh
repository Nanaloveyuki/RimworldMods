#!/usr/bin/env bash
# Build the standalone patch; deployment never edits another mod or copies dependencies.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd -P)"
configuration="${CONFIGURATION:-Release}"
rimworld="${RIMWORLD_DIR:-${RimWorldDir:-/mnt/e/Apps/Steam/steamapps/common/RimWorld}}"
build_only=false

usage() {
  printf '%s\n' 'Usage: scripts/deploy.sh [--build-only] [--rimworld-dir PATH] [--configuration Debug|Release]'
}

while (($#)); do
  case "$1" in
    --build-only) build_only=true; shift ;;
    --rimworld-dir|--configuration)
      if (($# < 2)) || [[ -z "$2" ]]; then
        echo "Missing value for $1" >&2
        exit 2
      fi
      case "$1" in
        --rimworld-dir) rimworld="$2" ;;
        --configuration) configuration="$2" ;;
      esac
      shift 2 ;;
    -h|--help) usage; exit 0 ;;
    *) echo "Unknown argument: $1" >&2; usage >&2; exit 2 ;;
  esac
done
if [[ "$configuration" != Debug && "$configuration" != Release ]]; then
  echo 'Configuration must be Debug or Release.' >&2
  exit 2
fi
# Normalize relative paths before passing them to MSBuild and the deployment helper.
if [[ "$rimworld" != /* ]]; then rimworld="$PWD/$rimworld"; fi

command -v python3 >/dev/null || { echo 'Python 3 is required.' >&2; exit 1; }
command -v dotnet >/dev/null || { echo '.NET SDK is required.' >&2; exit 1; }
preflight=(--rimworld-dir "$rimworld" --preflight)
if "$build_only"; then preflight+=(--build-only); fi
python3 "$root/scripts/deploy.py" "${preflight[@]}"

build=(dotnet build "$root/Source/RadiusUI.MissionControl.Fixes.csproj" -c "$configuration" "-p:RimWorldDir=$rimworld" --nologo)
"${build[@]}"
if "$build_only"; then
  printf '%s\n' "Build complete: $root/1.6/Assemblies/RadiusUI.MissionControl.Fixes.dll"
  exit 0
fi
python3 "$root/scripts/deploy.py" --rimworld-dir "$rimworld"
