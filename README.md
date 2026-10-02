# OpenTaiko Plus（OpenTaiko 機能追加版・非公式）

> **[OpenTaiko](https://github.com/0auBSQ/OpenTaiko)（作者: 0auBSQ と貢献者の皆さん）v0.5.2.1 に機能を足した、非公式の改造版です。**
> 本家とは関係ありません。困ったことがあっても本家には問い合わせないでください。
> 本家の説明書は [README-OpenTaiko.md](README-OpenTaiko.md)（[English](README-OpenTaiko-EN.md)）に残してあります。

入れ替えるのは `OpenTaiko.exe` と `dll\FDK.dll` の 2 つだけです。曲・スキン・設定は今のものをそのまま使えます。
使うには **OpenTaiko v0.5.2.1 本体**が必要です。

## 入れ方

**かんたん**: [Releases](https://github.com/ar-ca-na/OpenTaiko-Plus/releases/latest) から ZIP を落として展開し、「適用する.bat」をダブルクリック。

**ZIP を使わない**: OpenTaiko.exe があるフォルダのアドレス欄に `cmd` と入れて Enter、開いた黒い窓に次の 1 行を貼って Enter。

```bat
curl -fL -o "%TEMP%\opentaiko_plus.ps1" https://github.com/ar-ca-na/OpenTaiko-Plus/releases/latest/download/install.ps1 && powershell -NoProfile -ExecutionPolicy Bypass -File "%TEMP%\opentaiko_plus.ps1" "%CD%"
```

- 元のファイルは `OpenTaiko_もとの版.exe` / `dll\FDK_もとの版.dll` として残ります。名前を戻せば元どおりです。
- 動画の書き出しに使う **ffmpeg が無ければ、自動で入れるか聞きます**。
- **更新**: 一度入れたら、OpenTaiko のフォルダの「更新する.bat」をダブルクリックするだけで最新版になります。新しい版が出ると、起動したタイトル画面にお知らせが出ます。そのとき **U キー**を押すと、ゲームを閉じて更新し、起動し直します。

## 足した機能

### 譜面を動画（mp4）にする
| | |
|---|---|
| 遊ばずに書き出す | `.tja` を「譜面を動画にする.bat」にドラッグ＆ドロップ（何個でもまとめて）。難易度は 1P / 2P それぞれ選べます |
| 遊びながら書き出す | 設定画面の「動画を書き出す」を ON にすると、演奏するたびに mp4 ができます |
| コマ落ちしない | 時間を待たずに 1 コマずつ計算するので、PC が重くても 60fps のなめらかな動画になります |
| 画面そのまま | ゲームの描画をそのまま取り出すので、普通に遊んだ画面と同じ見た目です。曲名の幕から録ります |
| 音もそのまま | 曲・太鼓の音・声が入ります。曲は音源ファイルから直接重ねるので音質が落ちません |
| 曲の音量を自動で整える | 曲の大きさを測って、太鼓の音とちょうどよい釣り合いにします。手で % を決めることもできます |
| 音割れしない | 曲と太鼓の音を足して音が割れそうなときは、釣り合いを保ったまま全体を少し下げます |
| 上書きしない | 同じ名前の動画があれば `_2` などを付けて別に保存します |
| 細かい指定 | 解像度・画質・難易度・曲の音量などをコマンドで指定できます（`--size 1920x1080` など） |

### 作譜支援モード（譜面を作る人向け）
設定画面で ON にすると、演奏中に次の操作ができます。

| キー | できること |
|---|---|
| Space | 再生 / 停止 |
| ← → / PgUp PgDn | 停止中に小節を移動（1 小節ずつ / まとめて） |
| Home / End | 停止中に最初 / 最後の小節へ |
| A | 停止中に、その場所へジャンプポイントを付ける |
| F5 | tja を読み直す（**いまの小節のまま**。エディタで直して保存 → F5 でその場で反映） |
| F6 / F7 | 1P / 2P のオートを切り替える |

### 操作まわり
- **マウスで操作**: 演奏以外のほぼすべての画面で、ホイール＝移動、左クリック＝決定、右クリック＝1 つ戻る。2 人で遊ぶときは Shift を押しながらで 2P 側を操作
- **スクリーンショット**: Win+Shift+S や PrintScreen が使えるようにしました
- **全画面**: 画面の解像度を変えずに全画面にします（Alt+Enter で窓に戻せます）。全画面で困ったときは Shift を押しながら起動すると窓で立ち上がります

## 直した本体の不具合

- ウィンドウを大きくしたり全画面にしたりすると、画面が真っ白になる
- ビデオカードが 2 枚ある PC で、ウィンドウを別のモニタへ動かすと落ちる
- 譜面の入っていないフォルダを選ぶと落ちる
- 譜面を直接開くモード（エディタとの連携用）で落ちる
- プレイヤー名や称号が NamePlate.json どおりに出ないことがある
- 演奏中の「譜面を読み直す」が働かない
- ゲームを開いたまま別の窓で書き出すと、太鼓の音や声が読めない
- 演奏中に BPM が変わると、キャラ・踊り子・モブ・ぷちキャラの動きが途中のコマへ飛ぶ（直前のコマから新しい速さで続くように）
- tja の `BGIMAGE` で指定した背景が出ない（動画に書き出すと真っ黒、遊ぶときはスキンの背景に隠れる）。画面いっぱいに出すようにした
- tja の `BGMOVIE`（背景動画）が出ない（mp4 が開けず、起動の仕方の不具合で動画の部品も読めなかった）。ffmpeg で読むようにし、書き出す動画でもゲームの時刻とコマを合わせる
- 設定で AVI を ON にすると、背景動画の無い曲でも踊り子・モブ・フッターが消える

## ソースについて

- 最初のコミットは本家のタグ `v0.5.2.1` の中身そのものです。改造はそれ以降のコミットにあります（`git diff v0.5.2.1` で差分が見られます）。
- ビルド: Visual Studio 2022 で `TJAPlayer3/TJAPlayer3.csproj` を Release / x86。

## ライセンス

本家と同じ MIT License です（[LICENSE](LICENSE)）。OpenTaiko 本体の著作権は 0auBSQ と貢献者の皆さんにあります。
配布 ZIP には、本家の決まりに従って `Licenses` フォルダ（使っているライブラリのライセンス）を同梱しています。

---

**English:** *OpenTaiko Plus* is an **unofficial** modified build of [OpenTaiko](https://github.com/0auBSQ/OpenTaiko) v0.5.2.1 by 0auBSQ and contributors.
It adds frame-perfect offline video export of charts (mp4, with automatic music volume), a chart-authoring mode (pause / seek / hot-reload the .tja),
mouse control, and fixes several crashes (white screen on resize, crash when moving between GPUs, etc.).
Not affiliated with the OpenTaiko project — please do not report issues of this build upstream. MIT License, same as the original.
