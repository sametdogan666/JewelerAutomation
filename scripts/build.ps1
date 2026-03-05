# Jeweler Automation - Build (API kapaliyken calistirin; yoksa "file is locked" hatasi alinir)
# Bu script, calisan WebAPI process'ini durdurup sonra dotnet build yapar.

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$apiPath = Join-Path $root "src\JewelerAutomation.WebAPI"

Write-Host "=== Jeweler Automation - Build ===" -ForegroundColor Cyan

# Calisan dotnet process'lerinden JewelerAutomation.WebAPI ile ilgili olanlari durdur
$dotnetProcesses = Get-Process -Name "dotnet" -ErrorAction SilentlyContinue
$stopped = 0
foreach ($p in $dotnetProcesses) {
    try {
        $cmdLine = (Get-CimInstance Win32_Process -Filter "ProcessId = $($p.Id)" -ErrorAction SilentlyContinue).CommandLine
        if ($cmdLine -and $cmdLine -like "*JewelerAutomation.WebAPI*") {
            Write-Host "Durduruluyor: PID $($p.Id) (WebAPI)" -ForegroundColor Yellow
            Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
            $stopped++
        }
    } catch { }
}
if ($stopped -gt 0) {
    Start-Sleep -Seconds 2
}

Set-Location $root
Write-Host "dotnet build calistiriliyor..." -ForegroundColor Yellow
dotnet build
if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "Build basarisiz. API calisiyorsa once kapatip (Ctrl+C) tekrar deneyin." -ForegroundColor Red
    exit 1
}
Write-Host "Build tamamlandi." -ForegroundColor Green
