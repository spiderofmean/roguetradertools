#!/usr/bin/env pwsh
# Undeploys the deprecated BlueprintDumper mod from the game folder

param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

# Load game path from project file
$csprojPath = Join-Path $PSScriptRoot ".." "mod" "BlueprintDumper.csproj"
if (-not (Test-Path $csprojPath)) {
    Write-Error "Could not find BlueprintDumper.csproj at: $csprojPath"
    exit 1
}

$csprojContent = Get-Content $csprojPath -Raw
if ($csprojContent -match '<RogueTraderInstallDir>(.*?)</RogueTraderInstallDir>') {
    $gameDir = $matches[1]
} else {
    Write-Error "Could not find RogueTraderInstallDir in BlueprintDumper.csproj"
    exit 1
}

$modsDir = Join-Path $gameDir "Mods" "BlueprintDumper"
$unityModManagerDir = Join-Path $env:LOCALAPPDATA "Owlcat Games" "Warhammer 40000 Rogue Trader" "UnityModManager" "BlueprintDumper"
$dumpsDir = Join-Path $env:LOCALAPPDATA "Owlcat Games" "Warhammer 40000 Rogue Trader" "BlueprintDumps"

Write-Host "=== BlueprintDumper Undeployment ===" -ForegroundColor Cyan
Write-Host ""

# Remove from game Mods folder
if (Test-Path $modsDir) {
    Write-Host "Removing mod from: $modsDir" -ForegroundColor Yellow
    Remove-Item -Path $modsDir -Recurse -Force
    Write-Host "✓ Removed from game Mods folder" -ForegroundColor Green
} else {
    Write-Host "✓ Mod not found in game Mods folder (already clean)" -ForegroundColor Gray
}

# Remove from UnityModManager folder
if (Test-Path $unityModManagerDir) {
    Write-Host "Removing mod from: $unityModManagerDir" -ForegroundColor Yellow
    Remove-Item -Path $unityModManagerDir -Recurse -Force
    Write-Host "✓ Removed from UnityModManager folder" -ForegroundColor Green
} else {
    Write-Host "✓ Mod not found in UnityModManager folder (already clean)" -ForegroundColor Gray
}

Write-Host ""
Write-Host "=== Undeployment Complete ===" -ForegroundColor Green
Write-Host ""

# Info about dumps
if (Test-Path $dumpsDir) {
    $dumpCount = (Get-ChildItem -Path $dumpsDir -Directory).Count
    Write-Host "Note: Blueprint dumps are still located at:" -ForegroundColor Cyan
    Write-Host "  $dumpsDir" -ForegroundColor White
    Write-Host "  ($dumpCount dump folders)" -ForegroundColor Gray
    Write-Host ""
    Write-Host "To remove dumps, run:" -ForegroundColor Cyan
    Write-Host "  Remove-Item -Path '$dumpsDir' -Recurse -Force" -ForegroundColor White
} else {
    Write-Host "✓ No blueprint dumps found (already clean)" -ForegroundColor Gray
}

Write-Host ""
Write-Host "The mod has been removed. You can now:" -ForegroundColor Cyan
Write-Host "  1. Install viewer-mod for live blueprint access" -ForegroundColor White
Write-Host "  2. Use viewer-mod/scripts/extract-blueprints.js for extraction" -ForegroundColor White
Write-Host ""
Write-Host "See: viewer-mod/README.md" -ForegroundColor Gray
