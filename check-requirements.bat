@echo off
echo ====================================
echo BambuToElegoo Service - Requirements Check
echo ====================================
echo.

echo Checking for .NET 10.0 Runtime...
dotnet --list-runtimes | findstr "Microsoft.NETCore.App 10.0" >nul

if %errorLevel% equ 0 (
    echo [OK] .NET 10.0 Runtime is installed
    echo.
    echo You can now run BambuToElegooService.exe
    echo.
) else (
    echo [ERROR] .NET 10.0 Runtime is NOT installed
    echo.
    echo Please download and install .NET 10.0 Runtime from:
    echo https://dotnet.microsoft.com/download/dotnet/10.0
    echo.
    echo Download the "ASP.NET Core Runtime 10.0.x - Windows Hosting Bundle"
    echo.
)

pause
