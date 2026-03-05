# GitHub'a push için:
# 1. https://github.com/new adresinden "JewelerAutomation" adında yeni bir repo oluşturun (Private veya Public).
# 2. Repo oluştururken "Add a README" vb. işaretlemeyin (boş repo).
# 3. Aşağıdaki $repoUrl değişkenini kendi GitHub kullanıcı adınızla güncelleyin.
# 4. PowerShell'de: .\scripts\push-to-github.ps1

$repoUrl = "https://github.com/KULLANICI_ADINIZ/JewelerAutomation.git"
# SSH kullanacaksanız: $repoUrl = "git@github.com:KULLANICI_ADINIZ/JewelerAutomation.git"

$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
Set-Location $root

if (-not (Test-Path .git)) {
    Write-Host "Bu klasörde git repo yok. Önce git init yapın." -ForegroundColor Red
    exit 1
}

$remotes = git remote
if ($remotes -notmatch "origin") {
    git remote add origin $repoUrl
    Write-Host "Remote 'origin' eklendi: $repoUrl" -ForegroundColor Green
} else {
    git remote set-url origin $repoUrl
    Write-Host "Remote 'origin' güncellendi: $repoUrl" -ForegroundColor Green
}

Write-Host "Push ediliyor (main)..." -ForegroundColor Cyan
git push -u origin main
