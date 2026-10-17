# OpenTaiko 機能追加版 こうしん script
# 更新する.bat から呼ばれる。GitHub の最新版を落として、適用する.bat と同じ入れ替えを行う。
# 初めて入れるときも、これを install.ps1 として落とし、OpenTaiko のフォルダを引数に渡して使う。
# ゲーム内のお知らせ（U キー）からは -WaitPid <ゲームの PID> -Relaunch 付きで呼ばれる。
# そのときはゲームが閉じるのを待ち、確認なしで入れ替え、終わったらゲームを起動し直す。
# 前の版に戻す.bat からは -Tag previous で呼ばれ、いま入っている版の 1 つ前の Release を入れる（-Tag v1.4 のように版も指定できる）。
# updater-rev: 2  （古い版の ZIP に入っている台本で上書きされないよう、入れ替えたあとに見比べる番号）
param([string]$入れ先 = '', [int]$WaitPid = 0, [switch]$Relaunch, [string]$Tag = '')

$ErrorActionPreference = 'Stop'
$リポジトリ = 'ar-ca-na/OpenTaiko-Plus'

function 待って終わる([int]$code) {
    Write-Host ''
    Write-Host 'Enter キーを押すと閉じます。' -NoNewline
    [void](Read-Host)
    exit $code
}

# "v1.10" と "v1.9" のような版を数字の並びとして比べる。a が新しければ正（ゲーム側の CUpdateCheck と同じ）
function 版を比べる([string]$a, [string]$b) {
    $na = @([regex]::Matches($a, '\d+') | ForEach-Object { [long]$_.Value })
    $nb = @([regex]::Matches($b, '\d+') | ForEach-Object { [long]$_.Value })
    for ($i = 0; $i -lt [Math]::Max($na.Count, $nb.Count); $i++) {
        $x = if ($i -lt $na.Count) { $na[$i] } else { 0 }
        $y = if ($i -lt $nb.Count) { $nb[$i] } else { 0 }
        if ($x -ne $y) { return $(if ($x -gt $y) { 1 } else { -1 }) }
    }
    return 0
}

$ここ = if ($入れ先) { $入れ先.Trim('"').TrimEnd('\') } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
Write-Host ''
Write-Host '=== OpenTaiko 機能追加版 の入れ替え・更新 ===' -ForegroundColor Cyan
Write-Host ''

if ($WaitPid -gt 0) {
    Write-Host 'OpenTaiko が閉じるのを待っています…'
    try { Wait-Process -Id $WaitPid -Timeout 60 -ErrorAction Stop } catch { }
    if (Get-Process -Id $WaitPid -ErrorAction SilentlyContinue) {
        Write-Host 'OpenTaiko が閉じませんでした。閉じてから「更新する.bat」を実行してください。' -ForegroundColor Red
        待って終わる 1
    }
}

if (-not (Test-Path (Join-Path $ここ 'OpenTaiko.exe'))) {
    Write-Host ('ここに OpenTaiko.exe がありません: ' + $ここ) -ForegroundColor Red
    Write-Host 'OpenTaiko.exe があるフォルダで実行してください。'
    待って終わる 1
}

$版ファイル = Join-Path $ここ 'scorevideo_version.txt'
$今の版 = if (Test-Path $版ファイル) { (Get-Content $版ファイル -Raw).Trim() } else { '' }

# --- 最新版を調べる -----------------------------------------------------
try {
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    # つながりにくいことがあるので 3 回まで試す
    for ($i = 1; $i -le 3; $i++) {
        try {
            if (-not $Tag) {
                $最新 = Invoke-RestMethod -Uri "https://api.github.com/repos/$リポジトリ/releases/latest" `
                    -Headers @{ 'User-Agent' = 'OpenTaiko-Plus-updater' } -TimeoutSec 30
            }
            elseif ($Tag -ne 'previous') {
                $最新 = Invoke-RestMethod -Uri "https://api.github.com/repos/$リポジトリ/releases/tags/$Tag" `
                    -Headers @{ 'User-Agent' = 'OpenTaiko-Plus-updater' } -TimeoutSec 30
            }
            else {
                # いま入っている版より古い Release のうち、いちばん新しいもの
                $全部 = Invoke-RestMethod -Uri "https://api.github.com/repos/$リポジトリ/releases?per_page=100" `
                    -Headers @{ 'User-Agent' = 'OpenTaiko-Plus-updater' } -TimeoutSec 30
                $最新 = $null
                foreach ($r in @($全部 | Where-Object { -not $_.draft -and -not $_.prerelease })) {
                    if ($今の版 -and (版を比べる $r.tag_name $今の版) -ge 0) { continue }
                    if (-not $最新 -or (版を比べる $r.tag_name $最新.tag_name) -gt 0) { $最新 = $r }
                }
            }
            break
        }
        catch { if ($i -eq 3) { throw }; Start-Sleep -Seconds 3 }
    }
}
catch {
    Write-Host '最新版を調べられませんでした。インターネットにつながっているか確かめてください。' -ForegroundColor Red
    Write-Host ('  ' + $_.Exception.Message)
    Write-Host ''
    Write-Host ('手で落とす場合: https://github.com/' + $リポジトリ + '/releases/latest')
    待って終わる 1
}

if (-not $最新) {
    Write-Host ('いま入っている版（' + $今の版 + '）より前の版が見つかりませんでした。') -ForegroundColor Red
    待って終わる 1
}
$版 = [string]$最新.tag_name
Write-Host ('いま入っている版: ' + $(if ($今の版) { $今の版 } else { '（まだ入っていません）' }))
Write-Host ($(if ($Tag) { '入れる版        : ' } else { '最新の版        : ' }) + $版)

if ($今の版 -and $今の版 -eq $版) {
    Write-Host ''
    Write-Host $(if ($Tag) { 'その版が入っています。入れ替えは要りません。' } else { '最新版が入っています。更新は要りません。' }) -ForegroundColor Green
    待って終わる 0
}

if ($最新.body -and -not $Tag) {
    Write-Host ''
    Write-Host '--- この版で変わったこと ---'
    Write-Host $最新.body
    Write-Host '----------------------------'
}

$zip = $最新.assets | Where-Object { $_.name -like '*.zip' } | Select-Object -First 1
if (-not $zip) {
    Write-Host ($版 + ' に ZIP が付いていません。しばらくしてからもう一度試してください。') -ForegroundColor Red
    待って終わる 1
}

if (Get-Process -Name 'OpenTaiko' -ErrorAction SilentlyContinue) {
    Write-Host ''
    Write-Host 'OpenTaiko が起動しています。閉じてからもう一度実行してください。' -ForegroundColor Red
    待って終わる 1
}

Write-Host ''
if ($WaitPid -gt 0) {
    Write-Host 'ゲームの中で「更新する」を選んだので、このまま入れ替えます。'
} elseif ($Tag) {
    Write-Host ($版 + ' に戻すと、ゲームを起動したときに「新しい版が出ています」と出ます。U キーを押さなければそのまま使えます。')
    $答え = Read-Host ($版 + ' に戻しますか？ 戻すなら y を入れて Enter')
    if ($答え -ne 'y') { Write-Host 'やめました。'; 待って終わる 0 }
} else {
    $答え = Read-Host $(if ($今の版) { '更新しますか？ 更新するなら y を入れて Enter' } else { '入れますか？ 入れるなら y を入れて Enter' })
    if ($答え -ne 'y') { Write-Host 'やめました。'; 待って終わる 0 }
}

# --- 落として展開して、入れ替え -----------------------------------------
$作業 = Join-Path $env:TEMP ('OpenTaiko-Plus-' + [Guid]::NewGuid().ToString('N'))
try {
    New-Item -ItemType Directory -Path $作業 | Out-Null
    $zipパス = Join-Path $作業 $zip.name
    Write-Host ('ダウンロード中: ' + $zip.name + ' (' + [Math]::Round($zip.size / 1MB, 1) + ' MB)')
    Invoke-WebRequest -Uri $zip.browser_download_url -OutFile $zipパス -UseBasicParsing `
        -Headers @{ 'User-Agent' = 'OpenTaiko-Plus-updater' } -TimeoutSec 300
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

# 古い版の ZIP は古い台本（前の版に戻せない）で上書きするので、この台本を控えておき、番号が新しければ書き戻す
$自分 = [IO.File]::ReadAllBytes($MyInvocation.MyCommand.Path)
function 台本の番号([byte[]]$b) {
    $m = [regex]::Match([Text.Encoding]::UTF8.GetString($b), 'updater-rev:\s*(\d+)')
    if ($m.Success) { [int]$m.Groups[1].Value } else { 0 }
}

# apply.ps1 が入れ替えと「Enter で閉じる」まで行う
& powershell -NoProfile -ExecutionPolicy Bypass -File $apply.FullName $ここ
$code = $LASTEXITCODE
try {
    $入った台本 = Join-Path $ここ 'scorevideo_update.ps1'
    if ((Test-Path $入った台本) -and (台本の番号 $自分) -gt (台本の番号 ([IO.File]::ReadAllBytes($入った台本)))) {
        [IO.File]::WriteAllBytes($入った台本, $自分)
    }
} catch { }
try { Remove-Item -LiteralPath $作業 -Recurse -Force } catch { }
if ($Relaunch -and $code -eq 0) {
    Start-Process -FilePath (Join-Path $ここ 'OpenTaiko.exe') -WorkingDirectory $ここ
}
exit $code
