# OpenTaiko 機能追加版 しゅうふく script
# セキュリティソフトに OpenTaiko.exe を消されたとき用。OpenTaiko修復.bat から呼ばれる。
#  1. GitHub の最新版 ZIP を落とし、exe・dll・更新用のファイルだけを置き直す（スキン・設定・曲には触らない）
#  2. 置き直した exe がまた消されないか数秒見る
#  3. 除外への登録は、セキュリティソフトの画面で本人がする（外から書き換える方法は無いし、できたら危ない）。
#     exe の場所をクリップボードに写し、入っているセキュリティソフトに合わせて、どこに貼るかを案内する
param([string]$入れ先 = '')

$ErrorActionPreference = 'Stop'
$リポジトリ = 'ar-ca-na/OpenTaiko-Plus'
$試験 = [bool]$env:OPENTAIKO_REPAIR_TEST   # 試験のときはクリップボード・他のアプリの起動・ゲームの起動をしない

function 待って終わる([int]$code) {
    Write-Host ''
    Write-Host 'Enter キーを押すと閉じます。' -NoNewline
    [void](Read-Host)
    exit $code
}

Write-Host ''
Write-Host '=== OpenTaiko 機能追加版 の修復 ===' -ForegroundColor Cyan
Write-Host ''

# --- 入れ先（OpenTaiko のフォルダ）を決める ------------------------------
$ここ = Split-Path -Parent $MyInvocation.MyCommand.Path
$先 = if ($入れ先) { $入れ先.Trim('"').TrimEnd('\') } else { $ここ }
if (Test-Path $先 -PathType Leaf) { $先 = Split-Path -Parent $先 }
# exe は消されているかもしれないので、System と dll のフォルダがあるかで見分ける
if (-not ((Test-Path (Join-Path $先 'System') -PathType Container) -and (Test-Path (Join-Path $先 'dll') -PathType Container))) {
    Write-Host ('ここは OpenTaiko のフォルダではないようです: ' + $先) -ForegroundColor Red
    Write-Host ''
    Write-Host '  この 2 つのファイルを OpenTaiko のフォルダ（System と dll のフォルダがある所）に入れてから、'
    Write-Host '  「OpenTaiko修復.bat」をダブルクリックしてください。'
    Write-Host '  または、OpenTaiko のフォルダを「OpenTaiko修復.bat」の上へドラッグ＆ドロップしてください。'
    待って終わる 1
}
$先 = (Resolve-Path $先).Path
$先exe = Join-Path $先 'OpenTaiko.exe'
Write-Host ('OpenTaiko のフォルダ: ' + $先)
Write-Host ('OpenTaiko.exe       : ' + $(if (Test-Path $先exe) { 'あります' } else { '消えています → 置き直します' }))

if (Get-Process -Name 'OpenTaiko' -ErrorAction SilentlyContinue) {
    Write-Host ''
    Write-Host 'OpenTaiko が起動しています。閉じてからもう一度実行してください。' -ForegroundColor Red
    待って終わる 1
}

# --- 最新版を落として置き直す --------------------------------------------
$作業 = Join-Path $env:TEMP ('OpenTaiko-Plus-repair-' + [Guid]::NewGuid().ToString('N'))
try {
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    for ($i = 1; $i -le 3; $i++) {
        try {
            $最新 = Invoke-RestMethod -Uri "https://api.github.com/repos/$リポジトリ/releases/latest" `
                -Headers @{ 'User-Agent' = 'OpenTaiko-Plus-repair' } -TimeoutSec 30
            break
        }
        catch { if ($i -eq 3) { throw }; Start-Sleep -Seconds 3 }
    }
    $zip = $最新.assets | Where-Object { $_.name -like '*.zip' } | Select-Object -First 1
    if (-not $zip) { throw '最新版に ZIP が付いていません。' }

    New-Item -ItemType Directory -Path $作業 | Out-Null
    $zipパス = Join-Path $作業 $zip.name
    Write-Host ''
    Write-Host ('ダウンロード中: ' + $zip.name + '（' + $最新.tag_name + '）')
    Invoke-WebRequest -Uri $zip.browser_download_url -OutFile $zipパス -UseBasicParsing `
        -Headers @{ 'User-Agent' = 'OpenTaiko-Plus-repair' } -TimeoutSec 300
    Expand-Archive -Path $zipパス -DestinationPath (Join-Path $作業 'x') -Force
    $新exe = Get-ChildItem -Path (Join-Path $作業 'x') -Filter 'OpenTaiko.exe' -Recurse | Select-Object -First 1
    if (-not $新exe) { throw 'ZIP の中に OpenTaiko.exe がありません。' }
    $中身 = $新exe.DirectoryName

    Copy-Item $新exe.FullName $先exe -Force
    Copy-Item (Join-Path $中身 'dll\FDK.dll') (Join-Path $先 'dll\FDK.dll') -Force
    foreach ($名前 in @('譜面を動画にする.bat', '更新する.bat', '前の版に戻す.bat', 'scorevideo_update.ps1', 'scorevideo_version.txt')) {
        $元 = Join-Path $中身 $名前
        if (Test-Path $元) { Copy-Item $元 (Join-Path $先 $名前) -Force }
    }
    Write-Host ('置き直しました（' + $最新.tag_name + '）。') -ForegroundColor Green
}
catch {
    Write-Host ('置き直せませんでした: ' + $_.Exception.Message) -ForegroundColor Red
    if ($_.Exception -is [UnauthorizedAccessException] -or $_.Exception.InnerException -is [UnauthorizedAccessException]) {
        # 消したセキュリティソフトが「再起動で駆除を終える」待ちのあいだは、同じ名前のファイルを作れない
        Write-Host ''
        Write-Host 'セキュリティソフトが、消した OpenTaiko.exe の後始末（駆除）をまだ終えていないようです。' -ForegroundColor Yellow
        Write-Host '  パソコンを 1 回再起動してから、もう一度この「OpenTaiko修復.bat」を実行してください。'
        Write-Host '  （セキュリティソフトの画面に「今すぐ再起動」などのボタンがあれば、それでも同じです）'
        try { Remove-Item -LiteralPath $作業 -Recurse -Force } catch { }
        待って終わる 1
    }
    Write-Host 'インターネットにつながっているか、セキュリティソフトが止めていないか確かめてください。'
    Write-Host ('手で落とす場合: https://github.com/' + $リポジトリ + '/releases/latest')
    try { Remove-Item -LiteralPath $作業 -Recurse -Force } catch { }
    待って終わる 1
}
try { Remove-Item -LiteralPath $作業 -Recurse -Force } catch { }

# --- また消されないか見る ------------------------------------------------
Write-Host ''
Write-Host 'セキュリティソフトがまた消さないか、5 秒ほど見ています…'
Start-Sleep -Seconds 5
if (-not (Test-Path $先exe)) {
    Write-Host '置き直した OpenTaiko.exe が、また消されました。' -ForegroundColor Red
    Write-Host '  セキュリティソフトの「隔離」や「検出の履歴」から OpenTaiko.exe を「復元」し、'
    Write-Host '  そのまま「除外（例外）」に入れてください。入れたら、もう一度この修復を実行してください。'
    待って終わる 1
}
Write-Host 'OpenTaiko.exe は残っています。' -ForegroundColor Green

# --- 除外への登録（ここだけは本人の操作） --------------------------------
Write-Host ''
Write-Host '--- 次に、セキュリティソフトの「除外」に OpenTaiko.exe を入れてください ---' -ForegroundColor Yellow
Write-Host '（入れないと、遊んでいる途中でまた消されることがあります）'
Write-Host ''
Write-Host ('  入れる場所: ' + $先exe)
if (-not $試験) {
    try { Set-Clipboard -Value $先exe; Write-Host '  ↑ この場所をコピーしておきました。入力欄に Ctrl+V で貼れます。' } catch { }
}

# Windows に登録されているセキュリティソフト（有効なもの）を調べ、それに合わせて案内する。
# 他社のソフトは、別の画面（セーフブラウザ等）を開いてしまうおそれがあるので自動では開かない。
$製品 = @()
try {
    $製品 = @(Get-CimInstance -Namespace 'root/SecurityCenter2' -ClassName 'AntiVirusProduct' -ErrorAction Stop |
        Where-Object { (($_.productState -shr 12) -band 0xF) -eq 1 } | ForEach-Object { [string]$_.displayName })
} catch { }
Write-Host ''
Write-Host ('  このパソコンのセキュリティソフト: ' + $(if ($製品.Count) { $製品 -join '、' } else { '（調べられませんでした）' }))
$Defenderだけ = $製品.Count -gt 0 -and -not ($製品 | Where-Object { $_ -notmatch 'Defender' })
Write-Host ''
if ($Defenderだけ) {
    Write-Host '  Windows セキュリティ（Microsoft Defender）の場合:'
    Write-Host '    「ウイルスと脅威の防止」→「設定の管理」→「除外の追加または削除」→「除外の追加」→「ファイル」で、'
    Write-Host '    上の OpenTaiko.exe を選びます（「フォルダー」で OpenTaiko のフォルダごとでも構いません）。'
}
else {
    Write-Host '  タスクバー右下（^ の中）のセキュリティソフトのアイコンから画面を開き、'
    Write-Host '  「設定」の中の「除外」「例外」「許可するアプリ」などに、上の OpenTaiko.exe を追加します。'
    Write-Host '  例）スーパーセキュリティ・Bitdefender: 「保護」→「高度な防御」→「設定」の「例外」'
    Write-Host '  ファイルを選ぶ画面で選べないときは、入力欄に上の場所を貼り付けてみてください。'
}

if (-not $試験 -and $Defenderだけ) {
    Write-Host ''
    Write-Host 'Windows セキュリティの設定を開きます。'
    try { Start-Process 'windowsdefender://threatsettings' } catch { }
}

Write-Host ''
Write-Host '除外に入れ終わったら Enter キーを押してください。' -NoNewline
[void](Read-Host)

if (-not $試験) {
    $答え = Read-Host 'OpenTaiko を起動しますか？ 起動するなら y を入れて Enter'
    if ($答え -eq 'y') { Start-Process -FilePath $先exe -WorkingDirectory $先 }
}
Write-Host '終わりました。'
待って終わる 0
