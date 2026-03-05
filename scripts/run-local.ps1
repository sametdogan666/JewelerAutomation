# Jeweler Automation - Yerel ortamda ilk çalıştırma
# Gereksinim: PostgreSQL çalışıyor olmalı (varsayılan: localhost:5432, kullanıcı postgres, şifre postgres)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$apiPath = Join-Path $root "src\JewelerAutomation.WebAPI"
$clientPath = Join-Path $root "client"

Write-Host "=== Jeweler Automation - Yerel calistirma ===" -ForegroundColor Cyan
Write-Host ""

# 1) Backend
Write-Host "[1/2] Backend (API) baslatiliyor..." -ForegroundColor Yellow
Write-Host "      Dizin: $apiPath"
Write-Host "      Ilk calistirmada migration + seed otomatik uygulanir."
Write-Host "      API: https://localhost:7177" -ForegroundColor Gray
Write-Host ""
Set-Location $apiPath
Start-Process powershell -ArgumentList "-NoExit", "-Command", "dotnet run"
Start-Sleep -Seconds 3

# 2) Frontend
Write-Host "[2/2] Frontend (Angular) baslatiliyor..." -ForegroundColor Yellow
Write-Host "      Dizin: $clientPath"
Write-Host "      Tarayici: http://localhost:4200" -ForegroundColor Gray
Write-Host ""
Set-Location $clientPath
if (-not (Test-Path "node_modules")) {
    Write-Host "      npm install calistiriliyor..." -ForegroundColor Gray
    npm install --silent
}
Start-Process powershell -ArgumentList "-NoExit", "-Command", "npm start"

Write-Host ""
Write-Host "Her iki uygulama ayri pencerelerde acildi." -ForegroundColor Green
Write-Host "Giris: http://localhost:4200  ->  Kullanici: admin  Sifre: Admin123!" -ForegroundColor Green
Write-Host ""
