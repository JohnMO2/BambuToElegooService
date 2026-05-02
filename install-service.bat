@echo off
echo ====================================
echo BambuToElegoo Service Installer
echo ====================================
echo.

:: Check for admin rights
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo ERROR: This script requires Administrator privileges.
    echo Please right-click and select "Run as administrator"
    pause
    exit /b 1
)

:: Get the current directory
set "SERVICE_PATH=%~dp0BambuToElegooService.exe"

echo Installing service...
sc create "BambuToElegooService" binPath= "\"%SERVICE_PATH%\" --service" start= auto
sc description "BambuToElegooService" "Bridges BambuLab slicer to Elegoo 3D printers"

echo.
echo Starting service...
sc start "BambuToElegooService"

echo.
echo ====================================
echo Installation Complete!
echo ====================================
echo.
echo The service is now running and will start automatically with Windows.
echo.
pause
