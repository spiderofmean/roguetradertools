# Build script for Viewer Mod
# Usage: .\scripts\build.ps1 [-Configuration Release|Debug]

param(
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$ModDir = Join-Path $PSScriptRoot ".." "mod"

Write-Host "Building ViewerMod ($Configuration)..." -ForegroundColor Cyan

# Build the mod
Push-Location $ModDir
try {
    dotnet build ViewerMod.csproj -c $Configuration
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed with exit code $LASTEXITCODE"
    }
    Write-Host "Build succeeded!" -ForegroundColor Green
    
    # Show output location
    $OutputDir = Join-Path $ModDir "bin" $Configuration "net472"
    Write-Host "Output: $OutputDir" -ForegroundColor Gray
    
    # List output files
    Get-ChildItem $OutputDir -Filter "*.dll" | ForEach-Object {
        Write-Host "  - $($_.Name)" -ForegroundColor Gray
    }
}
finally {
    Pop-Location
}
