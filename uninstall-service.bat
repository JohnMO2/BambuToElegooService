@echo off
echo ====================================
echo BambuToElegoo Service Uninstaller
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

echo Stopping service...
sc stop "BambuToElegooService"

echo.
echo Deleting service...
sc delete "BambuToElegooService"

echo.
echo ====================================
echo Uninstall Complete!
echo ====================================
echo.
echo Note: Configuration files remain at:
echo C:\ProgramData\BambuToElegooService\
echo.
echo Delete that folder manually if you want to remove all settings.
echo.
pause
