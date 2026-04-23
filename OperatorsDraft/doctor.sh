#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

export PYTHONPYCACHEPREFIX="${ROOT_DIR}/Library/Caches/com.apple.python"
export HOME="${ROOT_DIR}"
export DOTNET_CLI_HOME="${ROOT_DIR}"
export PATH="${ROOT_DIR}/.dotnet:${PATH}"

echo "EvoFlow backend doctor"
echo "root: ${ROOT_DIR}"

echo
echo "[1/6] Python"
python3 --version
python3 -m py_compile "${ROOT_DIR}/server.py"
python3 -m py_compile "${ROOT_DIR}/scripts/validate_render_contract.py"

echo
echo "[2/6] EvoFlow CLI"
"${ROOT_DIR}/run_evoflow.sh" --help >/dev/null
echo "run_evoflow.sh --help: ok"

echo
echo "[3/6] Local .NET SDK"
dotnet --version

echo
echo "[4/6] OperatorRunner build"
dotnet build "${ROOT_DIR}/OperatorRunner/OperatorRunner.csproj" --no-restore

echo
echo "[5/6] Demo assets"
test -f "${ROOT_DIR}/exports/test3.json"
test -f "${ROOT_DIR}/demo_data/hurricane_sandy_2012_100k_sample.csv"
echo "exports/test3.json: ok"
echo "demo_data/hurricane_sandy_2012_100k_sample.csv: ok"

echo
echo "[6/6] Contract tests"
python3 -m unittest discover -s "${ROOT_DIR}/tests"
python3 "${ROOT_DIR}/scripts/validate_render_contract.py" "${ROOT_DIR}/exports/test3.json" --limit 3 --no-selected-ids

echo
echo "Doctor check completed."
