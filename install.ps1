param(
    [string]$GameDir = "D:\SteamLibrary\steamapps\common\Slay the Spire 2",
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$ProjectDir = $PSScriptRoot
$OutDir = Join-Path $ProjectDir "out"
$DllName = "DwellTargeting.dll"

Write-Host "Building DwellTargeting ($Configuration)..." -ForegroundColor Cyan

dotnet build (Join-Path $ProjectDir "DwellTargeting.csproj") `
    -c $Configuration `
    -o $OutDir `
    -p:GameDir="$GameDir"

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed." -ForegroundColor Red
    exit 1
}

$ModFolder = Join-Path $GameDir "mods\DwellTargeting"
New-Item -ItemType Directory -Force -Path $ModFolder | Out-Null

Copy-Item (Join-Path $OutDir $DllName) (Join-Path $ModFolder $DllName) -Force
Copy-Item (Join-Path $ProjectDir "mod_manifest.json") (Join-Path $ModFolder "DwellTargeting.json") -Force
Copy-Item (Join-Path $ProjectDir "open-settings.ps1") (Join-Path $ModFolder "open-settings.ps1") -Force
Copy-Item (Join-Path $ProjectDir "Open Settings.bat") (Join-Path $ModFolder "Open Settings.bat") -Force

Write-Host "Installed to $ModFolder" -ForegroundColor Green
Write-Host "Launch STS2 -> Play Modded -> enable Dwell Targeting"
Write-Host "Settings: in-game SET button (top-left) or F8; backup: Open Settings.bat" -ForegroundColor Cyan
