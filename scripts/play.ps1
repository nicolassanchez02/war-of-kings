# Launch the Godot game windowed. Builds the C# side first to make sure the
# Game and Simulation assemblies are current.
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$godot = 'C:\Users\Nick\AppData\Local\Microsoft\WinGet\Packages\GodotEngine.GodotEngine.Mono_Microsoft.Winget.Source_8wekyb3d8bbwe\Godot_v4.6.2-stable_mono_win64\Godot_v4.6.2-stable_mono_win64_console.exe'

if (-not (Test-Path $godot)) {
    Write-Error "Godot 4.6.2 (Mono) not found at $godot. Install with: winget install GodotEngine.GodotEngine.Mono"
}

dotnet build (Join-Path $repo 'WarOfKings.Game.csproj') | Out-Null
& $godot --path $repo @args
