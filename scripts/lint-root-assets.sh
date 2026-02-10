#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

mapfile -t cs_files < <(find "$ROOT_DIR/Assets" -type f -name '*.cs' | sort)

if ((${#cs_files[@]} > 0)); then
  echo "ERROR: Root placeholder Assets/ contains C# files."
  echo "Use UnityProject/Assets/ as the source of truth and remove these files:"
  for file in "${cs_files[@]}"; do
    echo "  - ${file#$ROOT_DIR/}"
  done
  exit 1
fi

echo "OK: no C# files found under root Assets/."
