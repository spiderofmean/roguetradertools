# Deploy script for Viewer Mod
# Requires: config.json with ModsFolder path

$ConfigPath = Join-Path (Join-Path $PSScriptRoot "..") "config.json"

if (-not (Test-Path $ConfigPath)) {
    Write-Host "ERROR: config.json not found" -ForegroundColor Red
    Write-Host "Create viewer-mod/config.json with:" -ForegroundColor Yellow
    Write-Host '{' -ForegroundColor Gray
    Write-Host '  "ModsFolder": "C:\\Path\\To\\Rogue Trader\\Mods"' -ForegroundColor Gray
    Write-Host '}' -ForegroundColor Gray
    exit 1
}

$Config = Get-Content $ConfigPath | ConvertFrom-Json
$ModsFolder = $Config.ModsFolder

if (-not (Test-Path $ModsFolder)) {
    Write-Host "ERROR: ModsFolder path does not exist: $ModsFolder" -ForegroundColor Red
    exit 1
}

Write-Host "Deploying to: $ModsFolder" -ForegroundColor Cyan

# Build
& (Join-Path $PSScriptRoot "build.ps1")
if ($LASTEXITCODE -ne 0) { exit 1 }

# Copy
$ModDir = Join-Path (Join-Path $PSScriptRoot "..") "mod"
$SourceDir = Join-Path (Join-Path (Join-Path $ModDir "bin") "Release") "net472"
$TargetDir = Join-Path $ModsFolder "ViewerMod"

New-Item -ItemType Directory -Path $TargetDir -Force | Out-Null
Copy-Item (Join-Path $SourceDir "ViewerMod.dll") $TargetDir -Force
Copy-Item (Join-Path $ModDir "Info.json") $TargetDir -Force

Write-Host "Deployed to: $TargetDir" -ForegroundColor Green
