# OpenTaiko 譜面動画版 こうしん script
# 更新する.bat から呼ばれる。GitHub の最新版を落として、適用する.bat と同じ入れ替えを行う。

$ErrorActionPreference = 'Stop'
$リポジトリ = 'ar-ca-na/OpenTaiko-ScoreVideo'

function 待って終わる([int]$code) {
    Write-Host ''
    Write-Host 'Enter キーを押すと閉じます。' -NoNewline
    [void](Read-Host)
    exit $code
}

$ここ = Split-Path -Parent $MyInvocation.MyCommand.Path
Write-Host ''
Write-Host '=== OpenTaiko 譜面動画版 の更新 ===' -ForegroundColor Cyan
Write-Host ''

if (-not (Test-Path (Join-Path $ここ 'OpenTaiko.exe'))) {
    Write-Host 'このファイルは OpenTaiko.exe と同じフォルダに置いて使ってください。' -ForegroundColor Red
    待って終わる 1
}

$版ファイル = Join-Path $ここ 'scorevideo_version.txt'
$今の版 = if (Test-Path $版ファイル) { (Get-Content $版ファイル -Raw).Trim() } else { '' }

# --- 最新版を調べる -----------------------------------------------------
try {
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    $最新 = Invoke-RestMethod -Uri "https://api.github.com/repos/$リポジトリ/releases/latest" `
        -Headers @{ 'User-Agent' = 'OpenTaiko-ScoreVideo-updater' } -TimeoutSec 30
}
catch {
    Write-Host '最新版を調べられませんでした。インターネットにつながっているか確かめてください。' -ForegroundColor Red
    Write-Host ('  ' + $_.Exception.Message)
    Write-Host ''
    Write-Host ('手で落とす場合: https://github.com/' + $リポジトリ + '/releases/latest')
    待って終わる 1
}

$版 = [string]$最新.tag_name
Write-Host ('いま入っている版: ' + $(if ($今の版) { $今の版 } else { '（不明）' }))
Write-Host ('最新の版        : ' + $版)

if ($今の版 -and $今の版 -eq $版) {
    Write-Host ''
    Write-Host '最新版が入っています。更新は要りません。' -ForegroundColor Green
    待って終わる 0
}

if ($最新.body) {
    Write-Host ''
    Write-Host '--- この版で変わったこと ---'
    Write-Host $最新.body
    Write-Host '----------------------------'
}

$zip = $最新.assets | Where-Object { $_.name -like '*.zip' } | Select-Object -First 1
if (-not $zip) {
    Write-Host '最新版に ZIP が付いていません。しばらくしてからもう一度試してください。' -ForegroundColor Red
    待って終わる 1
}

if (Get-Process -Name 'OpenTaiko' -ErrorAction SilentlyContinue) {
    Write-Host ''
    Write-Host 'OpenTaiko が起動しています。閉じてからもう一度実行してください。' -ForegroundColor Red
    待って終わる 1
}

Write-Host ''
$答え = Read-Host '更新しますか？ 更新するなら y を入れて Enter'
if ($答え -ne 'y') { Write-Host 'やめました。'; 待って終わる 0 }

# --- 落として展開して、入れ替え -----------------------------------------
$作業 = Join-Path $env:TEMP ('OpenTaiko-ScoreVideo-' + [Guid]::NewGuid().ToString('N'))
try {
    New-Item -ItemType Directory -Path $作業 | Out-Null
    $zipパス = Join-Path $作業 $zip.name
    Write-Host ('ダウンロード中: ' + $zip.name + ' (' + [Math]::Round($zip.size / 1MB, 1) + ' MB)')
    Invoke-WebRequest -Uri $zip.browser_download_url -OutFile $zipパス -UseBasicParsing `
        -Headers @{ 'User-Agent' = 'OpenTaiko-ScoreVideo-updater' } -TimeoutSec 300
    Expand-Archive -Path $zipパス -DestinationPath (Join-Path $作業 'x') -Force
    $apply = Get-ChildItem -Path (Join-Path $作業 'x') -Filter 'apply.ps1' -Recurse | Select-Object -First 1
    if (-not $apply) { throw 'ZIP の中に apply.ps1 がありません。' }
}
catch {
    Write-Host ('ダウンロードか展開に失敗しました: ' + $_.Exception.Message) -ForegroundColor Red
    try { Remove-Item -LiteralPath $作業 -Recurse -Force } catch { }
    Write-Host ('手で落とす場合: https://github.com/' + $リポジトリ + '/releases/latest')
    待って終わる 1
}

# apply.ps1 が入れ替えと「Enter で閉じる」まで行う
& powershell -NoProfile -ExecutionPolicy Bypass -File $apply.FullName $ここ
$code = $LASTEXITCODE
try { Remove-Item -LiteralPath $作業 -Recurse -Force } catch { }
exit $code
