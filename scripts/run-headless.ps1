# Run the headless simulation harness. Forwards any extra args to the runner.
# Usage:
#   .\scripts\run-headless.ps1
#   .\scripts\run-headless.ps1 --seed 42 --ticks 1000 --twice
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
dotnet run --project (Join-Path $repo 'src\App\Headless\Headless.csproj') -- @args
