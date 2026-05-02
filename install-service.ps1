# BambuToElegoo Service Installer (PowerShell)
# Run as Administrator

Write-Host "====================================" -ForegroundColor Cyan
Write-Host "BambuToElegoo Service Installer" -ForegroundColor Cyan
Write-Host "====================================" -ForegroundColor Cyan
Write-Host ""

# Check for admin rights
if (-NOT ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
    Write-Host "ERROR: This script requires Administrator privileges." -ForegroundColor Red
    Write-Host "Please right-click and select 'Run as Administrator'" -ForegroundColor Yellow
    pause
    exit 1
}

# Get the current directory
$ServicePath = Join-Path $PSScriptRoot "BambuToElegooService.exe"

if (-not (Test-Path $ServicePath)) {
    Write-Host "ERROR: BambuToElegooService.exe not found at: $ServicePath" -ForegroundColor Red
    pause
    exit 1
}

Write-Host "Installing service..." -ForegroundColor Yellow
sc.exe create "BambuToElegooService" binPath= "`"$ServicePath`" --service" start= auto
sc.exe description "BambuToElegooService" "Bridges BambuLab slicer to Elegoo 3D printers"

Write-Host ""
Write-Host "Starting service..." -ForegroundColor Yellow
sc.exe start "BambuToElegooService"

Write-Host ""
Write-Host "====================================" -ForegroundColor Green
Write-Host "Installation Complete!" -ForegroundColor Green
Write-Host "====================================" -ForegroundColor Green
Write-Host ""
Write-Host "The service is now running and will start automatically with Windows." -ForegroundColor Cyan
Write-Host ""
pause
