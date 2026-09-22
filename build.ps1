<#
.SYNOPSIS
    Automated build and packaging script for AltPowerPlan.

.DESCRIPTION
    Builds the solution in Release mode, executes unit tests, and outputs properly
    named standalone packages, portable packages, zip archives, Inno Setup installers
    (for Windows x64 and ARM64), and SHA-256 checksums to the 'Build' directory.

.PARAMETER Configuration
    Build configuration: 'Release' (default) or 'Debug'.

.PARAMETER Version
    Target release version (e.g. '1.0.0'). If not specified, automatically extracted
    from Directory.Build.props.

.PARAMETER Architectures
    Target Windows runtimes to publish and package: 'win-x64', 'win-arm64', or both (default).

.PARAMETER SkipTests
    Skip running automated tests before packaging.

.PARAMETER NoZip
    Skip creating .zip archives of the published output.

.PARAMETER Clean
    Wipe and recreate the 'Build' directory before building.

.PARAMETER UsePipeline
    Delegate execution to the ModularPipelines C# orchestrator (AltPowerPlan.Pipeline).

.EXAMPLE
    .\build.ps1
    Builds and packages everything (x64, arm64, installers, and zip archives).

.EXAMPLE
    .\build.ps1 -Architectures win-x64 -SkipTests
    Quick local build for x64 only without tests.
#>

[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Version = "",
    [string[]]$Architectures = @("win-x64", "win-arm64"),
    [switch]$SkipTests,
    [switch]$NoZip,
    [switch]$Clean,
    [switch]$UsePipeline
)

$ErrorActionPreference = "Stop"
$sw = [System.Diagnostics.Stopwatch]::StartNew()

function Write-Header([string]$text) {
    Write-Host ""
    Write-Host ("=" * 70) -ForegroundColor Cyan
    Write-Host " $text" -ForegroundColor Cyan
    Write-Host ("=" * 70) -ForegroundColor Cyan
}

function Write-Success([string]$text) {
    Write-Host "[OK] $text" -ForegroundColor Green
}

function Write-Info([string]$text) {
    Write-Host "[INFO] $text" -ForegroundColor Yellow
}

function Write-WarnMsg([string]$text) {
    Write-Host "[WARN] $text" -ForegroundColor Yellow
}

function Write-Fail([string]$text) {
    Write-Host "[ERROR] $text" -ForegroundColor Red
}

$RepoRoot = $PSScriptRoot
$BuildDir = Join-Path $RepoRoot "Build"
$SolutionPath = Join-Path $RepoRoot "AltPowerPlan.slnx"
$ProjectApp = Join-Path $RepoRoot "AltPowerPlan.App\AltPowerPlan.App.csproj"
$PropsFile = Join-Path $RepoRoot "Directory.Build.props"
$InstallerScript = Join-Path $RepoRoot "installer\setup.iss"

# Normalize target architectures
$targetRids = @()
foreach ($a in $Architectures) {
    $cleanA = $a.Trim().ToLowerInvariant()
    if ($cleanA -in @("x64", "win-x64")) {
        $targetRids += "win-x64"
    } elseif ($cleanA -in @("arm64", "win-arm64")) {
        $targetRids += "win-arm64"
    } else {
        Write-WarnMsg "Unknown architecture '$a'. Supported: 'win-x64', 'win-arm64'."
    }
}
if ($targetRids.Count -eq 0) {
    $targetRids = @("win-x64", "win-arm64")
}

Write-Header "AltPowerPlan Local Build Engine"

# 1. Determine Version
if (-not $Version) {
    if (Test-Path $PropsFile) {
        [xml]$props = Get-Content $PropsFile
        $prefix = $props.Project.PropertyGroup.VersionPrefix
        $suffix = $props.Project.PropertyGroup.VersionSuffix
        if ($suffix) {
            $Version = "$prefix-$suffix"
        } else {
            $Version = $prefix
        }
    }
    if (-not $Version) {
        $Version = "1.0.0"
    }
}

Write-Info "Configuration: $Configuration"
Write-Info "App Version:   $Version"
Write-Info "Architectures: $($targetRids -join ', ')"
Write-Info "Output Folder: $BuildDir"

if ($UsePipeline) {
    Write-Header "AltPowerPlan ModularPipelines Orchestrator"
    $pipeArgs = @("--configuration", $Configuration, "--version", $Version)
    if ($SkipTests) { $pipeArgs += "--skip-tests" }
    if ($NoZip) { $pipeArgs += "--skip-zip" }
    if ($Clean) { $pipeArgs += "--clean" }

    & dotnet run --project AltPowerPlan.Pipeline -- $pipeArgs
    exit $LASTEXITCODE
}

# 2. Clean or Prepare Build Directory
if ($Clean -and (Test-Path $BuildDir)) {
    Write-Info "Cleaning existing Build folder..."
    Remove-Item -Recurse -Force $BuildDir
}

if (-not (Test-Path $BuildDir)) {
    New-Item -ItemType Directory -Force -Path $BuildDir | Out-Null
}

# 3. Restore Dependencies
Write-Header "1/4: Restoring NuGet Packages"
& dotnet restore $SolutionPath
if ($LASTEXITCODE -ne 0) {
    Write-Fail "NuGet package restore failed."
    exit $LASTEXITCODE
}
Write-Success "NuGet dependencies restored."

# 4. Run Automated Tests
if (-not $SkipTests) {
    Write-Header "2/4: Executing Test Suite ($Configuration)"
    & dotnet test $SolutionPath --configuration $Configuration --no-restore --verbosity normal
    if ($LASTEXITCODE -ne 0) {
        Write-Fail "Unit tests failed. Build halted."
        exit $LASTEXITCODE
    }
    Write-Success "All unit tests passed."
} else {
    Write-Info "Skipping unit tests (-SkipTests specified)."
}

# 5. Publish Builds per Target Architecture
Write-Header "3/4: Compiling & Publishing Binaries"

$publishedDirs = @{}

foreach ($rid in $targetRids) {
    Write-Info "--- Publishing for $rid ---"

    # Standalone (Self-Contained single file)
    $standaloneDir = Join-Path $BuildDir "AltPowerPlan-v$Version-$rid-standalone"
    & dotnet publish $ProjectApp `
        --configuration $Configuration `
        --runtime $rid `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:EnableCompressionInSingleFile=true `
        -p:Version=$Version `
        --output $standaloneDir

    if ($LASTEXITCODE -ne 0) {
        Write-Fail "Failed to publish standalone package for $rid."
        exit $LASTEXITCODE
    }
    Write-Success "Published standalone ($rid): $standaloneDir"

    # Portable (Framework-Dependent with portable.dat marker)
    $portableDir = Join-Path $BuildDir "AltPowerPlan-v$Version-$rid-portable"
    & dotnet publish $ProjectApp `
        --configuration $Configuration `
        --runtime $rid `
        --self-contained false `
        -p:Version=$Version `
        --output $portableDir

    if ($LASTEXITCODE -ne 0) {
        Write-Fail "Failed to publish portable package for $rid."
        exit $LASTEXITCODE
    }

    # Add portable.dat marker so the application knows to store config next to the exe
    $portableMarker = Join-Path $portableDir "portable.dat"
    New-Item -ItemType File -Path $portableMarker -Force | Out-Null
    Write-Success "Published portable ($rid): $portableDir"

    $publishedDirs[$rid] = @{
        Standalone = $standaloneDir
        Portable   = $portableDir
    }
}

# 6. Inno Setup Installers
Write-Header "4/4: Compiling Windows Inno Setup Installers"

# Search for Inno Setup Compiler (ISCC.exe)
$iscc = $null
$isccLocations = @(
    (Get-Command "iscc.exe" -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source),
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 6\ISCC.exe"
)
foreach ($loc in $isccLocations) {
    if ($loc -and (Test-Path $loc)) {
        $iscc = $loc
        break
    }
}

if ($iscc -and (Test-Path $InstallerScript)) {
    Write-Info "Found Inno Setup Compiler: $iscc"

    # 1. Standard x64 Installer (Framework-Dependent)
    if ($publishedDirs.ContainsKey("win-x64")) {
        $x64Portable = $publishedDirs["win-x64"].Portable
        Write-Info "Compiling standard Inno Setup installer (win-x64)..."
        & $iscc "/DAppVersion=$Version" "/DSourceDir=$x64Portable" "/O$BuildDir" $InstallerScript
        if ($LASTEXITCODE -eq 0) {
            Write-Success "Standard Inno Setup installer compiled."
        } else {
            Write-WarnMsg "ISCC compilation failed with exit code $LASTEXITCODE."
        }

        # 2. Standalone x64 Installer
        $x64Standalone = $publishedDirs["win-x64"].Standalone
        Write-Info "Compiling standalone Inno Setup installer (win-x64)..."
        & $iscc "/DAppVersion=$Version" "/DSourceDir=$x64Standalone" "/DStandalone=1" "/DOutputBaseFilename=AltPowerPlan-v$Version-Setup-Standalone" "/O$BuildDir" $InstallerScript
        if ($LASTEXITCODE -eq 0) {
            Write-Success "Standalone x64 Inno Setup installer compiled."
        } else {
            Write-WarnMsg "ISCC compilation for standalone x64 installer failed."
        }
    }

    # 3. Standalone ARM64 Installer
    if ($publishedDirs.ContainsKey("win-arm64")) {
        $arm64Standalone = $publishedDirs["win-arm64"].Standalone
        Write-Info "Compiling standalone Inno Setup installer (win-arm64)..."
        & $iscc "/DAppVersion=$Version" "/DSourceDir=$arm64Standalone" "/DStandalone=1" "/DOutputBaseFilename=AltPowerPlan-v$Version-win-arm64-Setup-Standalone" "/O$BuildDir" $InstallerScript
        if ($LASTEXITCODE -eq 0) {
            Write-Success "Standalone ARM64 Inno Setup installer compiled."
        } else {
            Write-WarnMsg "ISCC compilation for standalone ARM64 installer failed."
        }
    }
} else {
    Write-Info "Inno Setup Compiler (ISCC.exe) not found. Skipping .exe installer generation."
    Write-Info "Install Inno Setup 6 (or 'choco install innosetup') to compile setup installers."
}

# 7. Create ZIP Archives
if (-not $NoZip) {
    Write-Info "Creating compressed ZIP archives..."
    foreach ($rid in $targetRids) {
        $standaloneDir = $publishedDirs[$rid].Standalone
        $portableDir = $publishedDirs[$rid].Portable

        $standaloneZip = Join-Path $BuildDir "AltPowerPlan-v$Version-$rid-standalone.zip"
        $portableZip = Join-Path $BuildDir "AltPowerPlan-v$Version-$rid-portable.zip"

        Compress-Archive -Path "$standaloneDir\*" -DestinationPath $standaloneZip -Force
        Compress-Archive -Path "$portableDir\*" -DestinationPath $portableZip -Force

        Write-Success "Created: $(Split-Path $standaloneZip -Leaf)"
        Write-Success "Created: $(Split-Path $portableZip -Leaf)"
    }
}

# 8. Generate Checksums
Write-Info "Computing SHA-256 checksums..."
$ChecksumsFile = Join-Path $BuildDir "SHA256SUMS.txt"
$hashes = @()

Get-ChildItem -Path (Join-Path $BuildDir "*") -Include "*.zip", "*.exe" | ForEach-Object {
    $hash = Get-FileHash -Path $_.FullName -Algorithm SHA256
    $hashes += "$($hash.Hash)  $($_.Name)"
}

if ($hashes.Count -gt 0) {
    $hashes | Out-File -FilePath $ChecksumsFile -Encoding utf8
    Write-Success "Generated: SHA256SUMS.txt ($($hashes.Count) file(s) hashed)"
}

$sw.Stop()

# 9. Summary Table
Write-Header "Build Completed in $([math]::Round($sw.Elapsed.TotalSeconds, 1))s"
Write-Host "Artifacts generated in: $BuildDir" -ForegroundColor Green
Write-Host ""

$artifacts = Get-ChildItem -Path $BuildDir | Select-Object Name, @{
    Name = "Type";
    Expression = {
        if ($_.PSIsContainer) { "Folder" }
        elseif ($_.Extension -eq ".zip") { "ZIP Archive" }
        elseif ($_.Extension -eq ".exe") { "Executable / Installer" }
        else { "Text / Metadata" }
    }
}, @{
    Name = "Size";
    Expression = {
        if ($_.PSIsContainer) {
            $sum = (Get-ChildItem -Path $_.FullName -Recurse -File | Measure-Object -Property Length -Sum).Sum
            "$([math]::Round($sum / 1MB, 1)) MB"
        } else {
            if ($_.Length -ge 1MB) {
                "$([math]::Round($_.Length / 1MB, 2)) MB"
            } else {
                "$([math]::Round($_.Length / 1KB, 1)) KB"
            }
        }
    }
}

$artifacts | Format-Table -AutoSize
