param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path $MyInvocation.MyCommand.Path
$ProjectDir = Join-Path $ScriptDir "..\mod"
$LocalLow = Join-Path $env:USERPROFILE "AppData\LocalLow"

function Get-LastUnityModManagerScanPath([string]$GameRoot) {
    $candidates = @(
        (Join-Path $GameRoot 'GameLogFull.txt'),
        (Join-Path $GameRoot 'GameLogFullPrev.txt'),
        (Join-Path $GameRoot 'GameLog.txt'),
        (Join-Path $GameRoot 'GameLogPrev.txt')
    ) | Where-Object { Test-Path $_ }

    foreach ($path in $candidates) {
        $match = Select-String -Path $path -Pattern 'Mods\s+path:\s*(.+)$' -AllMatches | Select-Object -Last 1
        if ($null -ne $match) {
            $raw = $match.Matches[0].Groups[1].Value.Trim()
            # The log line can end with a '.' and can mix '/' and '\'. Normalize.
            $raw = $raw.TrimEnd('.') -replace '/', '\'
            return $raw
        }
    }

    return $null
}

$GameRoot = "$LocalLow\Owlcat Games\Warhammer 40000 Rogue Trader"
$ModId = 'BlueprintDumper'

$scanPath = Get-LastUnityModManagerScanPath -GameRoot $GameRoot
if ([string]::IsNullOrWhiteSpace($scanPath) -or -not (Test-Path $scanPath)) {
    throw "Could not detect a valid mods scan path from game logs under: $GameRoot. Launch the game once, then re-run this script."
}

$TargetDir = Join-Path $scanPath $ModId
Write-Host "Detected Mods path from log: $scanPath" -ForegroundColor DarkGreen

Write-Host "Deploying $ModId to: $TargetDir" -ForegroundColor Cyan

if (-not (Test-Path $TargetDir)) {
    New-Item -ItemType Directory -Force -Path $TargetDir | Out-Null
}

$BinDir = Join-Path $ProjectDir "bin\$Configuration\net472"
$DllPath = Join-Path $BinDir "BlueprintDumper.dll"
$InfoPath = Join-Path $ProjectDir "Info.json"

if (-not (Test-Path $DllPath)) {
    Write-Error "DLL not found at $DllPath. Did you build first?"
}

Copy-Item $DllPath -Destination $TargetDir -Force
Copy-Item $InfoPath -Destination $TargetDir -Force

Write-Host "Deployed successfully."
