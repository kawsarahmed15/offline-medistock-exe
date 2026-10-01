<#
.SYNOPSIS
    Builds the Medistock standalone single-file executable for Windows x64.
.DESCRIPTION
    Compiles all projects in Release mode, bundles the .NET 9 runtime and all dependencies
    into a single self-contained executable file.
#>
param(
    [string]$OutputDir = "dist/Medistock-Standalone-win-x64"
)

Write-Host "========================================================" -ForegroundColor Cyan
Write-Host " Building Medistock Standalone Single-File Executable   " -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan

$dotnetCmd = "dotnet"
if (-not (Get-Command "dotnet" -ErrorAction SilentlyContinue)) {
    if (Test-Path "C:\Program Files\dotnet\dotnet.exe") {
        $dotnetCmd = "C:\Program Files\dotnet\dotnet.exe"
    }
}

# Stop any running Medistock instances before publishing so files are not locked
Get-Process -Name "Medistock*" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 500

$projectPath = "src/Clients/Medistock.Desktop/Medistock.Desktop.csproj"

& $dotnetCmd publish $projectPath `
    -c Release `
    -r win-x64 `
    -p:Platform=x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:EnableMsixTooling=true `
    -p:DebugType=none `
    -p:DebugSymbols=false `
    -o $OutputDir

if ($LASTEXITCODE -eq 0) {
    # Remove any leftover symbol files in destination
    Get-ChildItem -Path $OutputDir -Filter "*.pdb" -ErrorAction SilentlyContinue | Remove-Item -Force
    
    $exePath = Join-Path $OutputDir "Medistock.Desktop.exe"
    if (Test-Path $exePath) {
        # Also copy to root dist/ for convenient direct access
        $rootDist = "dist"
        Copy-Item -Path $exePath -Destination (Join-Path $rootDist "Medistock.exe") -Force
        if (Test-Path (Join-Path $OutputDir "resources.pri")) {
            Copy-Item -Path (Join-Path $OutputDir "resources.pri") -Destination (Join-Path $rootDist "resources.pri") -Force
        }
        if (Test-Path (Join-Path $OutputDir "Medistock.Desktop.pri")) {
            Copy-Item -Path (Join-Path $OutputDir "Medistock.Desktop.pri") -Destination (Join-Path $rootDist "Medistock.pri") -Force
        }

        $sizeMB = [math]::Round((Get-Item $exePath).Length / 1MB, 2)
        Write-Host ""
        Write-Host "========================================================" -ForegroundColor Green
        Write-Host " SUCCESS! Standalone executable created:" -ForegroundColor Green
        Write-Host " 1. $exePath ($sizeMB MB)" -ForegroundColor Green
        Write-Host " 2. $(Join-Path $rootDist 'Medistock.exe') ($sizeMB MB)" -ForegroundColor Green
        Write-Host " This file is completely self-contained and runs on any" -ForegroundColor Green
        Write-Host " 64-bit Windows PC without requiring .NET or runtime installs." -ForegroundColor Green
        Write-Host "========================================================" -ForegroundColor Green
    }
} else {
    Write-Host "BUILD FAILED with exit code: $LASTEXITCODE" -ForegroundColor Red
}
