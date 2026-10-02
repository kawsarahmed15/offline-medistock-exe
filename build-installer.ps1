<#
.SYNOPSIS
    Builds the Medistock Setup Installer Executable (.exe) for Windows x64.
.DESCRIPTION
    1. Publishes the latest Medistock.Desktop binaries in self-contained Release mode.
    2. Packages the binaries into an embedded payload archive (app_payload.zip).
    3. Builds and publishes Medistock.Setup into a single standalone installer exe.
    4. Outputs:
       - dist/Installer/Medistock-Setup-v1.0.0.exe
       - dist/Medistock-Setup.exe (convenient root access)
#>
param(
    [switch]$SkipDesktopPublish = $false
)

$ErrorActionPreference = "Stop"

Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "  Building Medistock Setup Installer (.exe)             " -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan

$root = $PSScriptRoot
if (-not $root) { $root = (Get-Location).Path }

$distReleaseDir = Join-Path $root "dist\Medistock-Release-win-x64"
$setupDir = Join-Path $root "src\Clients\Medistock.Setup"
$payloadZip = Join-Path $setupDir "app_payload.zip"
$installerOutputDir = Join-Path $root "dist\Installer"

# Step 1: Ensure fresh Desktop Release publish unless skipped
if (-not $SkipDesktopPublish -or -not (Test-Path (Join-Path $distReleaseDir "Medistock.Desktop.exe"))) {
    Write-Host "[1/4] Publishing Medistock.Desktop (Release win-x64)..." -ForegroundColor Yellow
    $desktopProj = Join-Path $root "src\Clients\Medistock.Desktop\Medistock.Desktop.csproj"
    
    # Terminate any running Medistock instances
    Get-Process -Name "Medistock*" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Get-Process -Name "Medistock.Desktop" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 1

    dotnet publish $desktopProj `
        -c Release `
        -r win-x64 `
        --self-contained true `
        -o $distReleaseDir
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Desktop publish failed with exit code $LASTEXITCODE" -ForegroundColor Red
        exit $LASTEXITCODE
    }
} else {
    Write-Host "[1/4] Using existing desktop release binaries in $distReleaseDir..." -ForegroundColor Yellow
}

# Remove any debug symbol files
Get-ChildItem -Path $distReleaseDir -Filter "*.pdb" -Recurse -ErrorAction SilentlyContinue | Remove-Item -Force

# Step 2: Create payload zip
Write-Host "[2/4] Packaging release files into app_payload.zip..." -ForegroundColor Yellow
if (Test-Path $payloadZip) {
    Remove-Item $payloadZip -Force
}

Add-Type -AssemblyName System.IO.Compression.FileSystem

# Create zip from directory excluding any runtime cache
$tempZipSource = Join-Path $env:TEMP "Medistock_Payload_Prep"
if (Test-Path $tempZipSource) {
    Remove-Item $tempZipSource -Recurse -Force
}
New-Item -ItemType Directory -Path $tempZipSource | Out-Null

Get-ChildItem -Path $distReleaseDir | ForEach-Object {
    if ($_.Name -notlike "*.WebView2*" -and $_.Name -notlike "*.pdb") {
        Copy-Item -Path $_.FullName -Destination $tempZipSource -Recurse -Force
    }
}

[System.IO.Compression.ZipFile]::CreateFromDirectory(
    $tempZipSource,
    $payloadZip,
    [System.IO.Compression.CompressionLevel]::Optimal,
    $false
)

Remove-Item $tempZipSource -Recurse -Force -ErrorAction SilentlyContinue

$zipSizeMB = [math]::Round((Get-Item $payloadZip).Length / 1MB, 2)
Write-Host "      Payload archive created: $zipSizeMB MB" -ForegroundColor Green

# Step 3: Build & Publish Medistock.Setup into single-file installer
Write-Host "[3/4] Compiling standalone setup wizard executable..." -ForegroundColor Yellow
$setupProj = Join-Path $setupDir "Medistock.Setup.csproj"

if (-not (Test-Path $installerOutputDir)) {
    New-Item -ItemType Directory -Path $installerOutputDir -Force | Out-Null
}

dotnet publish $setupProj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=none `
    -p:DebugSymbols=false `
    -o $installerOutputDir

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Setup compilation failed with exit code $LASTEXITCODE" -ForegroundColor Red
    exit $LASTEXITCODE
}

# Step 4: Verify and distribute
Write-Host "[4/4] Finalizing setup executables..." -ForegroundColor Yellow
$builtSetup = Join-Path $installerOutputDir "Medistock-Setup-v1.0.0.exe"
if (-not (Test-Path $builtSetup)) {
    $found = Get-ChildItem -Path $installerOutputDir -Filter "*Setup*.exe" | Select-Object -First 1
    if ($found) { $builtSetup = $found.FullName }
}

if (Test-Path $builtSetup) {
    $rootSetup = Join-Path (Join-Path $root "dist") "Medistock-Setup.exe"
    Copy-Item -Path $builtSetup -Destination $rootSetup -Force
    
    $setupSizeMB = [math]::Round((Get-Item $builtSetup).Length / 1MB, 2)
    Write-Host ""
    Write-Host "========================================================" -ForegroundColor Green
    Write-Host " SUCCESS! Medistock Installer Executable Created:" -ForegroundColor Green
    Write-Host " 1. $builtSetup ($setupSizeMB MB)" -ForegroundColor Green
    Write-Host " 2. $rootSetup ($setupSizeMB MB)" -ForegroundColor Green
    Write-Host ""
    Write-Host " Features of this Installer (.exe):" -ForegroundColor Green
    Write-Host " - Standalone setup wizard with GUI (Select install folder)" -ForegroundColor Green
    Write-Host " - Creates Desktop and Start Menu shortcuts automatically" -ForegroundColor Green
    Write-Host " - Registers in Windows Add/Remove Programs with uninstaller" -ForegroundColor Green
    Write-Host " - Auto-launches Medistock upon completion" -ForegroundColor Green
    Write-Host " - Completely offline & self-contained (.NET runtime included)" -ForegroundColor Green
    Write-Host "========================================================" -ForegroundColor Green
} else {
    Write-Host "ERROR: Could not find output installer executable in $installerOutputDir" -ForegroundColor Red
    exit 1
}
