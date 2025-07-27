@echo off
REM Build-Release.bat
REM Simple batch file to build GGPK Explorer release deployment

echo Building GGPK Explorer Release Deployment...
echo ============================================

REM Change to script directory
cd /d "%~dp0"

echo Setting up dependencies...
powershell -ExecutionPolicy Bypass -File "scripts\Setup-All-Dependencies.ps1"

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo Dependency setup failed! Please check the output above.
    pause
    exit /b 1
)

echo.
echo Building release deployment...
powershell -ExecutionPolicy Bypass -File "scripts\Build-Deployment.ps1" -Configuration Release -Clean -BuildMSI

if %ERRORLEVEL% EQU 0 (
    echo.
    echo Build completed successfully!
    echo Check the 'publish' directory for deployment files.
    pause
) else (
    echo.
    echo Build failed! Check the output above for errors.
    pause
    exit /b 1
)