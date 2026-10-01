# OpenTaiko 機能追加版（非公式の改造版）

> **[OpenTaiko](https://github.com/0auBSQ/OpenTaiko)（作者: 0auBSQ と貢献者の皆さん）v0.5.2.1 に機能を足した、非公式の改造版です。**
> 本家とは関係ありません。困ったことがあっても本家には問い合わせないでください。
> 本家の説明書は [README-OpenTaiko.md](README-OpenTaiko.md)（[English](README-OpenTaiko-EN.md)）に残してあります。

## できること

- **譜面（.tja）を動画（mp4）にする** — 遊ばずに 1 コマずつ書き出すので、PC が重くてもコマ落ちしません。曲の音量は自動で整えます
- **作譜支援モード** — 演奏中に止める・小節を移動する・譜面を読み直す（F5）
- **マウス操作** — 演奏以外のほぼすべての画面
- **本体の不具合の修正** — 全画面が真っ白になる、別のモニタへ動かすと落ちる、BPM 変化でキャラの動きが飛ぶ、など

入れ替えるのは `OpenTaiko.exe` と `dll\FDK.dll` だけです。曲・スキン・設定は今のものをそのまま使えます。
使うには **OpenTaiko v0.5.2.1 本体**が必要です。

## 入れ方

**ZIP から**: [Releases](https://github.com/ar-ca-na/OpenTaiko-ScoreVideo/releases/latest) から ZIP を落として展開し、「適用する.bat」をダブルクリック。

**コマンド 1 行で**: OpenTaiko.exe があるフォルダのアドレス欄に `cmd` と入れて Enter、開いた黒い窓に次の 1 行を貼って Enter。

```bat
curl -fL -o "%TEMP%\opentaiko_plus.ps1" https://github.com/ar-ca-na/OpenTaiko-ScoreVideo/releases/latest/download/install.ps1 && powershell -NoProfile -ExecutionPolicy Bypass -File "%TEMP%\opentaiko_plus.ps1" "%CD%"
```

どちらも元のファイルを `OpenTaiko_もとの版.exe` として残し、動画の書き出しに使う ffmpeg が無ければ自動で入れるか聞きます。
入れたあとは、OpenTaiko のフォルダの「更新する.bat」で最新版に更新できます。
詳しい使い方は ZIP の中の「はじめにお読みください.txt」にあります。

## ソースについて

- 最初のコミットは本家のタグ `v0.5.2.1` の中身そのものです。改造はそれ以降のコミットにあります（`git diff v0.5.2.1` で差分が見られます）。
- ビルド: Visual Studio 2022 で `TJAPlayer3/TJAPlayer3.csproj` を Release / x86。

## ライセンス

本家と同じ MIT License です（[LICENSE](LICENSE)）。OpenTaiko 本体の著作権は 0auBSQ と貢献者の皆さんにあります。
配布 ZIP には本家の決まりに従って `Licenses` フォルダ（使っているライブラリのライセンス）を同梱しています。

---

**English:** An *unofficial* modified build of [OpenTaiko](https://github.com/0auBSQ/OpenTaiko) v0.5.2.1 by 0auBSQ and contributors.
Adds offline video export of charts (mp4), a chart-authoring mode, mouse control and several bug fixes.
Not affiliated with the OpenTaiko project — please do not report issues of this build upstream. MIT License, same as the original.
