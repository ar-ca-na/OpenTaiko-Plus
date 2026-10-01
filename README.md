# OpenTaiko 譜面動画版（非公式の改造版）

> **これは [OpenTaiko](https://github.com/0auBSQ/OpenTaiko)（作者: 0auBSQ と貢献者の皆さん）v0.5.2.1 を元にした、非公式の改造版です。**
> OpenTaiko 本体の開発元とは関係がありません。この改造版の不具合を本家に問い合わせないでください。
> 本家の説明書は [README-OpenTaiko.md](README-OpenTaiko.md)（[English](README-OpenTaiko-EN.md)）に残してあります。

OpenTaiko v0.5.2.1 に、次の機能と修正を足したものです。
入れ替えるのは `OpenTaiko.exe` と `dll\FDK.dll` の 2 つだけで、曲・スキン・設定はお使いのものをそのまま使います。

## 追加した機能

| 機能 | 内容 |
|---|---|
| 譜面を動画に書き出す | `.tja` を「譜面を動画にする.bat」にドロップすると mp4 ができます。遊ばずに 1 コマずつ計算して書き出すので、PC が重くてもコマ落ちしません |
| 遊びながら書き出す | 設定画面の「動画を書き出す」を ON にすると、演奏するたびに mp4 ができます |
| 書き出す曲の音量 | 動画に乗せる曲の音量を % で指定するか、音源の大きさから自動で決められます |
| 作譜支援モード | 演奏中に停止・小節移動・tja の読み直しができます（譜面を作る人向け） |
| マウス操作 | 演奏以外のほぼ全画面をホイールとクリックで操作できます |

ほかに、ウィンドウを大きくすると真っ白になる・別モニタへ移すと落ちる、などの本体の不具合をいくつか直しています。

## 入手

[Releases](https://github.com/ar-ca-na/OpenTaiko-ScoreVideo/releases/latest) から ZIP を落とし、中の「はじめにお読みください.txt」に従ってください。
OpenTaiko v0.5.2.1 本体と、動画の書き出しには ffmpeg が別に要ります。

## ソースについて

- 最初のコミットは本家のタグ `v0.5.2.1` の中身そのものです。改造はそれ以降のコミットにあります。
  本家からの差分は `git diff v0.5.2.1` で見られます。
- ビルドは Visual Studio 2022（MSBuild）で `TJAPlayer3/TJAPlayer3.csproj` を Release / x86 で行います。

## ライセンス

本家と同じ MIT License です（[LICENSE](LICENSE)）。
OpenTaiko 本体の著作権は 0auBSQ と貢献者の皆さんにあります。改造部分も同じ MIT License で公開します。

---

**English:** This is an *unofficial* modified build of [OpenTaiko](https://github.com/0auBSQ/OpenTaiko) v0.5.2.1 by 0auBSQ and contributors.
It adds offline video export of charts (mp4), a chart-authoring mode, mouse control and several bug fixes.
It is not affiliated with the OpenTaiko project — please do not report issues of this build upstream. Licensed under MIT, same as the original.
