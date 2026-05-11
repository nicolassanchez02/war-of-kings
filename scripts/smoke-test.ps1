# Smoke test: must be green before any commit during the overnight push.
# Runs the four checks established in the overnight brief:
#   1. Sim solution builds Release
#   2. Game solution builds Release
#   3. Sim tests pass
#   4. Headless determinism --twice produces matching hashes
#
# Exits non-zero on any failure. Intended for use as a pre-push hook
# (install: copy this file to .git/hooks/pre-push or use scripts/install-hooks.ps1).

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

function Step($name, [scriptblock]$body) {
    Write-Host ""
    Write-Host "=== $name ===" -ForegroundColor Cyan
    & $body
    if ($LASTEXITCODE -ne 0) {
        Write-Host "FAIL: $name" -ForegroundColor Red
        exit 1
    }
}

Step "Build WarOfKings.sln (Release)" {
    dotnet build WarOfKings.sln -c Release --nologo -v q
}

Step "Build WarOfKings.Game.sln (Release)" {
    dotnet build WarOfKings.Game.sln -c Release --nologo -v q
}

Step "Test WarOfKings.sln" {
    dotnet test WarOfKings.sln --nologo -v q
}

Step "Headless determinism (--twice)" {
    dotnet run --project src/App/Headless -c Release -- --spawn 4 --twice --ticks 1000 --every 100
}

Write-Host ""
Write-Host "All smoke checks PASSED." -ForegroundColor Green
