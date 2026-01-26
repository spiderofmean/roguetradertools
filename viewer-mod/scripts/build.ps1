# Build script for Viewer Mod

$ModDir = Join-Path (Join-Path $PSScriptRoot "..") "mod"

Write-Host "Building ViewerMod..." -ForegroundColor Cyan

Push-Location $ModDir
try {
    dotnet build ViewerMod.csproj -c Release
    if ($LASTEXITCODE -ne 0) {
        exit 1
    }
    Write-Host "Build succeeded!" -ForegroundColor Green
}
finally {
    Pop-Location
}
