param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path $MyInvocation.MyCommand.Path
$ProjectDir = Join-Path $ScriptDir "..\mod"
$ProjectFile = Join-Path $ProjectDir "BlueprintDumper.csproj"

Write-Host "Building BlueprintDumper ($Configuration)..."
dotnet build $ProjectFile -c $Configuration

if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed."
}
Write-Host "Build success."
