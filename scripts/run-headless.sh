#!/usr/bin/env bash
# Run the headless simulation harness. Forwards any extra args to the runner.
# Usage:
#   bash scripts/run-headless.sh
#   bash scripts/run-headless.sh --seed 42 --ticks 1000 --twice
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
dotnet run --project "$ROOT/src/App/Headless/Headless.csproj" -- "$@"
