@echo off
echo ========================================================
echo Building Medistock Standalone Single-File Executable
echo ========================================================
echo.

set "DOTNET_EXE=dotnet"
where dotnet >nul 2>nul
if %ERRORLEVEL% NEQ 0 (
    if exist "C:\Program Files\dotnet\dotnet.exe" (
        set "DOTNET_EXE=C:\Program Files\dotnet\dotnet.exe"
    )
)

"%DOTNET_EXE%" publish src/Clients/Medistock.Desktop/Medistock.Desktop.csproj -c Release -r win-x64 -p:Platform=x64 --self-contained true -p:PublishSingleFile=true -p:EnableMsixTooling=true -p:DebugType=none -p:DebugSymbols=false -o dist/Medistock-Standalone-win-x64

if %ERRORLEVEL% EQU 0 (
    if exist "dist\Medistock-Standalone-win-x64\*.pdb" del /q "dist\Medistock-Standalone-win-x64\*.pdb"
    copy /y "dist\Medistock-Standalone-win-x64\Medistock.Desktop.exe" "dist\Medistock.exe"
    copy /y "dist\Medistock-Standalone-win-x64\resources.pri" "dist\resources.pri"
    copy /y "dist\Medistock-Standalone-win-x64\Medistock.Desktop.pri" "dist\Medistock.pri"
    echo.
    echo ========================================================
    echo SUCCESS! Standalone executable created at:
    echo  1. dist\Medistock-Standalone-win-x64\Medistock.Desktop.exe
    echo  2. dist\Medistock.exe
    echo ========================================================
) else (
    echo.
    echo BUILD FAILED with error code %ERRORLEVEL%
)
pause
