# Open the Godot editor on this project.
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$godot = 'C:\Users\Nick\AppData\Local\Microsoft\WinGet\Packages\GodotEngine.GodotEngine.Mono_Microsoft.Winget.Source_8wekyb3d8bbwe\Godot_v4.6.2-stable_mono_win64\Godot_v4.6.2-stable_mono_win64.exe'

if (-not (Test-Path $godot)) {
    Write-Error "Godot 4.6.2 (Mono) not found at $godot."
}

dotnet build (Join-Path $repo 'WarOfKings.Game.csproj') | Out-Null
& $godot --editor --path $repo
