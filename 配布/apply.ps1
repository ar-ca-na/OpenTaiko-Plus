# OpenTaiko 譜面動画版 いれかえ script
# 適用する.bat から呼ばれる。直接ダブルクリックしても動く。

$ErrorActionPreference = 'Stop'

function 待って終わる([int]$code) {
    Write-Host ''
    Write-Host 'Enter キーを押すと閉じます。' -NoNewline
    [void](Read-Host)
    exit $code
}

$ここ = Split-Path -Parent $MyInvocation.MyCommand.Path
$中身 = Join-Path $ここ '中身'

Write-Host ''
Write-Host '=== OpenTaiko 譜面動画版 のいれかえ ===' -ForegroundColor Cyan
Write-Host ''

if (-not (Test-Path (Join-Path $中身 'OpenTaiko.exe'))) {
    Write-Host '「中身」フォルダが見つかりません。' -ForegroundColor Red
    Write-Host 'ZIP を展開したフォルダごと、そのまま使ってください。'
    待って終わる 1
}

# --- 入れ先を決める -----------------------------------------------------
$先 = $null
if ($args.Count -ge 1 -and $args[0]) {
    $p = [string]$args[0]
    if (Test-Path $p -PathType Leaf) { $p = Split-Path -Parent $p }
    $先 = $p
}
elseif (Test-Path (Join-Path (Split-Path -Parent $ここ) 'OpenTaiko.exe')) {
    # ZIP を OpenTaiko のフォルダの中に展開した場合
    $先 = Split-Path -Parent $ここ
}
elseif (Test-Path (Join-Path $ここ 'OpenTaiko.exe')) {
    # 中身を OpenTaiko のフォルダに直接展開した場合
    $先 = $ここ
}

if (-not $先) {
    Write-Host 'OpenTaiko のフォルダが分かりませんでした。' -ForegroundColor Yellow
    Write-Host ''
    Write-Host '  OpenTaiko.exe が入っているフォルダを、'
    Write-Host '  この「適用する.bat」の上へドラッグ＆ドロップしてください。'
    待って終わる 1
}

$先 = (Resolve-Path $先).Path
$先exe = Join-Path $先 'OpenTaiko.exe'
$先dll = Join-Path $先 'dll'

if (-not (Test-Path $先exe)) {
    Write-Host ('そこに OpenTaiko.exe がありません: ' + $先) -ForegroundColor Red
    待って終わる 1
}
if (-not (Test-Path $先dll)) {
    Write-Host ('そこに dll フォルダがありません: ' + $先) -ForegroundColor Red
    Write-Host 'OpenTaiko のフォルダを指定してください。'
    待って終わる 1
}

Write-Host ('入れ先: ' + $先)

# --- 動いていたら止めてもらう -------------------------------------------
if (Get-Process -Name 'OpenTaiko' -ErrorAction SilentlyContinue) {
    Write-Host ''
    Write-Host 'OpenTaiko が起動しています。閉じてからもう一度実行してください。' -ForegroundColor Red
    待って終わる 1
}

# --- 版の確認（違っても止めはしない） -----------------------------------
$版 = (Get-Item $先exe).VersionInfo.FileVersion
if ($版 -and ($版 -notlike '0.5.2*')) {
    Write-Host ''
    Write-Host ('※ この OpenTaiko は ' + $版 + ' です。この改造は 0.5.2.1 用です。') -ForegroundColor Yellow
    Write-Host '  そのまま入れると動かないかもしれません。'
    $答え = Read-Host '  続けますか？ 続けるなら y を入れて Enter'
    if ($答え -ne 'y') { Write-Host 'やめました。'; 待って終わる 0 }
}

# --- 元のファイルを残す -------------------------------------------------
$控えexe = Join-Path $先 'OpenTaiko_もとの版.exe'
$控えdll = Join-Path $先 'dll\FDK_もとの版.dll'
$先FDK  = Join-Path $先 'dll\FDK.dll'

try {
    if (-not (Test-Path $控えexe)) {
        Copy-Item $先exe $控えexe -Force
        Write-Host '元の OpenTaiko.exe を OpenTaiko_もとの版.exe として残しました。'
    } else {
        Write-Host '控えは既にあります（OpenTaiko_もとの版.exe）。上書きしません。'
    }
    if ((Test-Path $先FDK) -and (-not (Test-Path $控えdll))) {
        Copy-Item $先FDK $控えdll -Force
        Write-Host '元の dll\FDK.dll を dll\FDK_もとの版.dll として残しました。'
    }
}
catch {
    Write-Host ('控えを作れませんでした: ' + $_.Exception.Message) -ForegroundColor Red
    待って終わる 1
}

# --- 入れ替え -----------------------------------------------------------
try {
    Copy-Item (Join-Path $中身 'OpenTaiko.exe') $先exe -Force
    Copy-Item (Join-Path $中身 'dll\FDK.dll') $先FDK -Force
    Copy-Item (Join-Path $中身 '譜面を動画にする.bat') (Join-Path $先 '譜面を動画にする.bat') -Force
    # 更新用。実行中の 更新する.bat を書き換えると cmd が読み違えるので、中身が違うときだけ写す
    foreach ($名前 in @('更新する.bat', 'scorevideo_update.ps1', 'scorevideo_version.txt')) {
        $元 = Join-Path $中身 $名前
        $行き先 = Join-Path $先 $名前
        if (-not (Test-Path $元)) { continue }
        # Get-FileHash は PowerShell 7 から呼ばれると見つからないことがあるので、中身を直接比べる
        if ((Test-Path $行き先) -and ([Convert]::ToBase64String([IO.File]::ReadAllBytes($元)) -eq
            [Convert]::ToBase64String([IO.File]::ReadAllBytes($行き先)))) { continue }
        Copy-Item $元 $行き先 -Force
    }
}
catch {
    Write-Host ('入れ替えに失敗しました: ' + $_.Exception.Message) -ForegroundColor Red
    Write-Host 'ウイルス対策ソフトや、書き込み権限を確かめてください。'
    待って終わる 1
}

Write-Host ''
$版ファイル = Join-Path $中身 'scorevideo_version.txt'
$版 = if (Test-Path $版ファイル) { ' (' + (Get-Content $版ファイル -Raw).Trim() + ')' } else { '' }
Write-Host ('入れ替えました' + $版 + '。') -ForegroundColor Green
Write-Host '次からは OpenTaiko のフォルダにある「更新する.bat」で最新版に更新できます。'

# --- ffmpeg があるか ----------------------------------------------------
$候補 = @(
    (Join-Path $先 'ffmpeg.exe'),
    (Join-Path $先 'ffmpeg\ffmpeg.exe'),
    (Join-Path $先 'ffmpeg\bin\ffmpeg.exe'),
    'C:\tools\ffmpeg\ffmpeg.exe',
    'C:\ffmpeg\bin\ffmpeg.exe'
)
$ffmpeg = $null
foreach ($c in $候補) { if (Test-Path $c) { $ffmpeg = $c; break } }
if (-not $ffmpeg) {
    $cmd = Get-Command 'ffmpeg.exe' -ErrorAction SilentlyContinue
    if ($cmd) { $ffmpeg = $cmd.Source }
}

Write-Host ''
if ($ffmpeg) {
    Write-Host ('ffmpeg: ' + $ffmpeg)
} else {
    Write-Host 'ffmpeg.exe が見つかりません。動画の書き出しには必要です。' -ForegroundColor Yellow
    Write-Host '  https://www.gyan.dev/ffmpeg/builds/ の release essentials を落として、'
    Write-Host ('  中の ffmpeg.exe を ' + $先 + ' に置いてください。')
}

Write-Host ''
Write-Host '元に戻したいときは、OpenTaiko_もとの版.exe と dll\FDK_もとの版.dll を'
Write-Host 'それぞれ OpenTaiko.exe / dll\FDK.dll に名前を戻してください。'
待って終わる 0
