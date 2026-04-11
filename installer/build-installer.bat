@echo off
setlocal

set VERSION=1.5.6
set PROJECT=..\BaumConfigureGUI\BaumConfigureGUI.csproj

echo === BaumConfigure Installer Build ===
echo Version: %VERSION%
echo.

echo [1/3] Publishing .NET app...
dotnet publish "%PROJECT%" ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true

if errorlevel 1 (
    echo ERROR: dotnet publish failed.
    exit /b 1
)

echo.
echo [2/3] Building Inno Setup installer...
if not exist output mkdir output

where iscc >nul 2>&1
if errorlevel 1 (
    echo ERROR: Inno Setup compiler ^(iscc.exe^) not found in PATH.
    echo Download from: https://jrsoftware.org/isinfo.php
    exit /b 1
)

iscc setup.iss
if errorlevel 1 (
    echo ERROR: Inno Setup compilation failed.
    exit /b 1
)

echo.
echo [3/3] Done.
echo Installer: installer\output\BaumConfigure-Setup-%VERSION%.exe
echo.
