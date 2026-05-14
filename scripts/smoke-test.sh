#!/usr/bin/env bash
# Smoke test: must be green before any commit during the overnight push.
# See scripts/smoke-test.ps1 header for full description.

set -euo pipefail
cd "$(dirname "$0")/.."

step() {
    echo ""
    echo "=== $1 ==="
    shift
    if ! "$@"; then
        echo "FAIL: $1" >&2
        exit 1
    fi
}

step "Build WarOfKings.sln (Release)" \
    dotnet build WarOfKings.sln -c Release --nologo -v q

step "Build WarOfKings.Game.sln (Release)" \
    dotnet build WarOfKings.Game.sln -c Release --nologo -v q

step "Test WarOfKings.sln" \
    dotnet test WarOfKings.sln --nologo -v q

step "Headless determinism (--twice)" \
    dotnet run --project src/App/Headless -c Release -- --spawn 4 --twice --ticks 1000 --every 100

echo ""
echo "All smoke checks PASSED."
