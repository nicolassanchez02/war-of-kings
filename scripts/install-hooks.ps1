# One-time install: wire git hooks from scripts/hooks/ into .git/hooks/.
$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$src = Join-Path $repoRoot "scripts\hooks\pre-push"
$dst = Join-Path $repoRoot ".git\hooks\pre-push"
Copy-Item $src $dst -Force
Write-Host "Installed pre-push hook -> $dst"
