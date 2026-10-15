#!/usr/bin/env bash
# 统一入口负责门禁、进程检查与白名单部署
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
exec python3 "$root/scripts/rhah_pipeline.py" deploy "$@"
