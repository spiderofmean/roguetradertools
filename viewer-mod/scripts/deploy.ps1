# Deploy script for Viewer Mod
# Copies the mod to UnityModManager mods folder
# Usage: .\scripts\deploy.ps1 [-Configuration Release|Debug]

param(
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release",
    
    [string]$ModsFolder = ""
)

$ErrorActionPreference = "Stop"
$ModDir = Join-Path $PSScriptRoot ".." "mod"

# Find mods folder
if (-not $ModsFolder) {
    $GamePaths = @(
        "C:\Program Files (x86)\Steam\steamapps\common\Warhammer 40,000 Rogue Trader\Mods",
        "C:\Program Files\Steam\steamapps\common\Warhammer 40,000 Rogue Trader\Mods",
        "$env:USERPROFILE\Games\Warhammer 40,000 Rogue Trader\Mods"
    )
    
    foreach ($path in $GamePaths) {
        if (Test-Path $path) {
            $ModsFolder = $path
            break
        }
    }
    
    if (-not $ModsFolder) {
        Write-Host "Could not find mods folder. Specify with -ModsFolder parameter." -ForegroundColor Red
        exit 1
    }
}

Write-Host "Deploying ViewerMod to: $ModsFolder" -ForegroundColor Cyan

# Build first
& (Join-Path $PSScriptRoot "build.ps1") -Configuration $Configuration

# Create mod folder
$TargetDir = Join-Path $ModsFolder "ViewerMod"
if (-not (Test-Path $TargetDir)) {
    New-Item -ItemType Directory -Path $TargetDir | Out-Null
}

# Copy files
$SourceDir = Join-Path $ModDir "bin" $Configuration "net472"

# Copy DLL
Copy-Item (Join-Path $SourceDir "ViewerMod.dll") $TargetDir -Force
Write-Host "  Copied ViewerMod.dll" -ForegroundColor Gray

# Copy Info.json
Copy-Item (Join-Path $ModDir "Info.json") $TargetDir -Force
Write-Host "  Copied Info.json" -ForegroundColor Gray

Write-Host "Deployment complete!" -ForegroundColor Green
Write-Host "Mod installed to: $TargetDir" -ForegroundColor Gray
