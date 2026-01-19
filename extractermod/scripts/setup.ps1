[CmdletBinding()]
param(
    # If set, installs the .NET SDK via winget (even if one is already installed).
    [switch]$InstallDotnetSdk,

    # If set, skips installing the .NET SDK (still performs checks).
    [switch]$SkipDotnetSdk,

    # If set, installs a specific major SDK (e.g. 8, 9, 10) instead of auto-detecting latest.
    [int]$DotnetSdkMajor,

    # If set, opens browser links for anything that can't be installed automatically.
    # Defaults to $false for non-interactive/CI safety.
    [switch]$OpenLinks = $false,

    # If provided, updates RogueTraderInstallDir in the csproj to this path.
    [string]$RogueTraderInstallDir,

    # If set, runs the build after checks (fails fast if prerequisites are missing).
    [switch]$Build,
    
    # If set, auto-accepts winget license agreements. Otherwise prompts user.
    [switch]$AcceptLicenses
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Test-Command([string]$Name) {
    return [bool](Get-Command $Name -ErrorAction SilentlyContinue)
}

function Write-Section([string]$Title) {
    Write-Host "\n=== $Title ===" -ForegroundColor Cyan
}

function Get-LatestStableDotnetSdkWingetId {
    if (-not (Test-Command 'winget')) {
        throw 'winget is not available on PATH.'
    }

    $lines = & winget search Microsoft.DotNet.SDK --source winget 2>$null
    if (-not $lines) {
        throw 'Unable to query winget for Microsoft.DotNet.SDK.'
    }

    # Parse IDs like Microsoft.DotNet.SDK.10, Microsoft.DotNet.SDK.9, etc.
    $ids = @()
    foreach ($line in $lines) {
        if ($line -match '\bMicrosoft\.DotNet\.SDK\.(\d+)\b') {
            $major = [int]$Matches[1]
            $ids += [pscustomobject]@{ Major = $major; Id = "Microsoft.DotNet.SDK.$major" }
        }
    }

    $ids = $ids | Sort-Object Major -Descending | Select-Object -Unique Major, Id
    if (-not $ids) {
        throw 'Could not find any stable Microsoft.DotNet.SDK.<major> packages in winget search output.'
    }

    return $ids[0].Id
}

function Ensure-DotnetSdk {
    if ($SkipDotnetSdk) {
        return
    }

    $hasDotnet = Test-Command 'dotnet'
    $hasSdk = $false
    if ($hasDotnet) {
        $hasSdk = -not [string]::IsNullOrWhiteSpace((& dotnet --list-sdks 2>$null))
    }

    if ($hasSdk -and -not $InstallDotnetSdk) {
        Write-Host 'A .NET SDK is already installed; skipping SDK install.' -ForegroundColor DarkGreen
        return
    }

    if (-not (Test-Command 'winget')) {
        Write-Host 'winget was not found. Install App Installer from Microsoft Store, then re-run this script.' -ForegroundColor Yellow
        if ($OpenLinks) {
            Write-Host 'Opening Microsoft Store...' -ForegroundColor Yellow
            Start-Process 'ms-windows-store://pdp/?productid=9NBLGGH4NNS1' | Out-Null
        } else {
            Write-Host 'Get it from: Microsoft Store > App Installer (or ms-windows-store://pdp/?productid=9NBLGGH4NNS1)' -ForegroundColor Yellow
        }
        throw 'Missing winget.'
    }

    $sdkId = if ($PSBoundParameters.ContainsKey('DotnetSdkMajor')) {
        "Microsoft.DotNet.SDK.$DotnetSdkMajor"
    } else {
        Get-LatestStableDotnetSdkWingetId
    }

    Write-Host "Installing .NET SDK via winget: $sdkId" -ForegroundColor Green
    
    $wingetArgs = @('install', '-e', '--id', $sdkId, '--source', 'winget')
    
    if ($AcceptLicenses) {
        $wingetArgs += '--accept-package-agreements'
        $wingetArgs += '--accept-source-agreements'
    } else {
        Write-Host 'You will be prompted to accept license agreements. Use -AcceptLicenses to skip prompts.' -ForegroundColor Yellow
    }
    
    & winget @wingetArgs

    if (-not (Test-Command 'dotnet')) {
        throw 'dotnet is still not available after SDK install. Try reopening your terminal or rebooting.'
    }

    $sdks = & dotnet --list-sdks 2>$null
    if (-not $sdks) {
        throw 'dotnet is available, but no SDKs are listed. Something went wrong with the installation.'
    }

    Write-Host "Installed SDKs:\n$sdks" -ForegroundColor DarkGreen
}

function Check-NetFx472TargetingPack {
    $refAsm = 'C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2'
    $has = Test-Path $refAsm

    if ($has) {
        Write-Host "NETFx 4.7.2 reference assemblies: OK ($refAsm)" -ForegroundColor DarkGreen
        return $true
    }

    Write-Host 'NETFx 4.7.2 reference assemblies: MISSING' -ForegroundColor Yellow
    Write-Host 'This project targets net472. Install the ".NET Framework 4.7.2 Developer Pack" (targeting pack).' -ForegroundColor Yellow

    if ($OpenLinks) {
        Write-Host 'Opening download page in browser...' -ForegroundColor Yellow
        # Official download page for .NET Framework 4.7.2
        Start-Process 'https://dotnet.microsoft.com/en-us/download/dotnet-framework/net472' | Out-Null
    } else {
        Write-Host 'Download from: https://dotnet.microsoft.com/en-us/download/dotnet-framework/net472' -ForegroundColor Yellow
    }

    return $false
}

function Get-CsprojPath {
    return Join-Path $PSScriptRoot '..\mod\BlueprintDumper.csproj'
}

function Get-RogueTraderInstallDirFromCsproj([string]$CsprojPath) {
    [xml]$xml = Get-Content -Raw $CsprojPath
    return [string]$xml.Project.PropertyGroup.RogueTraderInstallDir
}

function Set-RogueTraderInstallDirInCsproj([string]$CsprojPath, [string]$NewDir) {
    if (-not (Test-Path $CsprojPath)) {
        throw "csproj not found: $CsprojPath"
    }
    
    # Validate the path doesn't contain XML-unsafe content or suspicious patterns
    if ($NewDir -match '[<>&"'']' -or $NewDir -match '\$\(' -or $NewDir -match '\%\(') {
        throw "Invalid RogueTraderInstallDir: path contains potentially unsafe characters. Path: $NewDir"
    }
    
    # Normalize and validate it looks like a real path
    if (-not [System.IO.Path]::IsPathRooted($NewDir)) {
        throw "RogueTraderInstallDir must be an absolute path. Got: $NewDir"
    }

    [xml]$xml = Get-Content -Raw $CsprojPath
    if (-not $xml.Project.PropertyGroup) {
        throw 'Unexpected csproj shape: missing PropertyGroup.'
    }

    if (-not $xml.Project.PropertyGroup.RogueTraderInstallDir) {
        # Create the element if it doesn't exist
        $node = $xml.CreateElement('RogueTraderInstallDir')
        $node.InnerText = $NewDir
        [void]$xml.Project.PropertyGroup.AppendChild($node)
    } else {
        $xml.Project.PropertyGroup.RogueTraderInstallDir = $NewDir
    }

    $xml.Save($CsprojPath)
    Write-Host "Updated RogueTraderInstallDir in csproj to: $NewDir" -ForegroundColor DarkGreen
}

function Check-GameManagedAssemblies([string]$CsprojPath) {
    $installDir = Get-RogueTraderInstallDirFromCsproj -CsprojPath $CsprojPath
    if ([string]::IsNullOrWhiteSpace($installDir)) {
        Write-Host 'RogueTraderInstallDir is missing in the csproj.' -ForegroundColor Yellow
        return $false
    }

    $candidateManagedDirs = @(
        (Join-Path $installDir 'RogueTrader_Data\Managed'),
        (Join-Path $installDir 'WH40KRT_Data\Managed')
    )

    Write-Host "RogueTraderInstallDir: $installDir"

    $managedDir = $null
    foreach ($candidate in $candidateManagedDirs) {
        if (Test-Path $candidate) {
            $managedDir = $candidate
            break
        }
    }

    if (-not $managedDir) {
        Write-Host 'ManagedDir: MISSING (could not find RogueTrader_Data\Managed or WH40KRT_Data\Managed under RogueTraderInstallDir)' -ForegroundColor Yellow
        foreach ($candidate in $candidateManagedDirs) {
            Write-Host ("Tried: $candidate") -ForegroundColor Yellow
        }
        return $false
    }

    Write-Host "ManagedDir: $managedDir" -ForegroundColor DarkGreen

    $files = @('UnityEngine.dll','UnityEngine.CoreModule.dll','Newtonsoft.Json.dll','Owlcat.Runtime.Core.dll')
    $missing = @()
    foreach ($f in $files) {
        $p = Join-Path $managedDir $f
        if (-not (Test-Path $p)) {
            $missing += $f
        }
    }

    if ($missing.Count -gt 0) {
        Write-Host ('Missing required game DLLs in ManagedDir: ' + ($missing -join ', ')) -ForegroundColor Yellow
        return $false
    }

    Write-Host 'Game-managed assembly references: OK' -ForegroundColor DarkGreen
    return $true
}

try {
    Write-Section 'Prerequisites'

    if (-not (Test-Command 'winget')) {
        Write-Host 'winget: MISSING' -ForegroundColor Yellow
    } else {
        Write-Host ('winget: OK (' + (& winget --version) + ')') -ForegroundColor DarkGreen
    }

    $hasDotnet = Test-Command 'dotnet'
    if ($hasDotnet) {
        Write-Host 'dotnet: present on PATH' -ForegroundColor DarkGreen
        $existingSdks = & dotnet --list-sdks 2>$null
        if ([string]::IsNullOrWhiteSpace($existingSdks)) {
            Write-Host 'dotnet SDKs: NONE detected (runtime-only install)' -ForegroundColor Yellow
        } else {
            Write-Host "dotnet SDKs:\n$existingSdks" -ForegroundColor DarkGreen
        }
    } else {
        Write-Host 'dotnet: not found on PATH (SDK not installed yet)' -ForegroundColor Yellow
    }

    Write-Section 'Install .NET SDK'
    Ensure-DotnetSdk

    Write-Section '.NET Framework Targeting Pack'
    $hasNetFx = Check-NetFx472TargetingPack

    Write-Section 'Rogue Trader References'
    $csproj = Get-CsprojPath
    if (-not (Test-Path $csproj)) {
        throw "csproj not found: $csproj"
    }

    if ($PSBoundParameters.ContainsKey('RogueTraderInstallDir')) {
        Set-RogueTraderInstallDirInCsproj -CsprojPath $csproj -NewDir $RogueTraderInstallDir
    }

    $hasGameRefs = Check-GameManagedAssemblies -CsprojPath $csproj

    if ($Build) {
        Write-Section 'Build'

        if (-not $hasNetFx) {
            throw 'Cannot build: missing .NET Framework 4.7.2 targeting pack.'
        }
        if (-not $hasGameRefs) {
            throw 'Cannot build: missing Rogue Trader Managed DLLs (check RogueTraderInstallDir).'
        }

        $repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
        $buildScript = Join-Path $repoRoot 'scripts\build.ps1'

        Write-Host "Running: $buildScript -Configuration Release" -ForegroundColor Green
        & $buildScript -Configuration Release

        Write-Host 'Build completed.' -ForegroundColor DarkGreen
    }

    Write-Section 'Summary'
    $hasSdkNow = $false
    if (Test-Command 'dotnet') {
        $hasSdkNow = -not [string]::IsNullOrWhiteSpace((& dotnet --list-sdks 2>$null))
    }

    if (-not $hasSdkNow) {
        if ($SkipDotnetSdk) {
            Write-Host 'NEXT: Install a .NET SDK (re-run without -SkipDotnetSdk to install via winget).' -ForegroundColor Yellow
        } else {
            Write-Host 'NEXT: Install a .NET SDK (this script can do it via winget).' -ForegroundColor Yellow
        }
    }
    if (-not $hasNetFx) {
        Write-Host 'NEXT: Install .NET Framework 4.7.2 Developer Pack, then re-run with -Build.' -ForegroundColor Yellow
    } elseif (-not $hasGameRefs) {
        Write-Host 'NEXT: Fix RogueTraderInstallDir (pass -RogueTraderInstallDir "..."), then re-run with -Build.' -ForegroundColor Yellow
    } else {
        Write-Host 'All prerequisites look good for building.' -ForegroundColor DarkGreen
        Write-Host 'Build command: .\scripts\build.ps1 -Configuration Release' -ForegroundColor DarkGreen
        Write-Host 'Deploy command: .\scripts\pack.ps1 -Configuration Release' -ForegroundColor DarkGreen
    }
}
catch {
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
