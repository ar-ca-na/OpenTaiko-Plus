"""OpenTaiko 機能追加版のリリースを 1 コマンドで行う。

  リリースの本文は 配布/notes/<版>.md があればそれを使う（無ければコミットの 1 行目を並べる）。

  python 配布/release.py 1.2            確認を挟んでリリース
  python 配布/release.py 1.2 --yes      確認なし（本人が「出して」と言ったときだけ）
  python 配布/release.py 1.2 --zip-only ビルドと ZIP 作成だけ（タグ・push・GitHub は触らない）
  python 配布/release.py 1.2 --deploy   リリース後、手元のゲームフォルダにも exe と dll を入れる

やること:
  1. 作業ツリーがきれいで main にいるかを確かめる
  2. Release ビルド（PDB を作らない = ビルドしたフォルダのパスを exe に埋めない）
  3. exe / dll に個人情報（ユーザー名・ホーム・実名・メール・PC のフォルダ）が無いか調べる
  4. 配布/ の中身と exe / dll で ZIP を作る（配布/out/）
  5. 注釈付きタグ v<版> を付け、main とタグを push
  6. GitHub に Release を作り、ZIP を添付する
"""
import os, sys, json, subprocess, zipfile, shutil, urllib.request, urllib.parse

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
REPO = "ar-ca-na/OpenTaiko-Plus"
REMOTE = "github"
MSBUILD = r"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
# --deploy の入れ先（手元のゲームフォルダ）。PC のフォルダ構成を公開しないため、git に入れないファイルから読む
GAME_FILE = os.path.join(HERE, "game_path.txt")
TOP = "OpenTaiko機能追加版"                     # ZIP の中のフォルダ名
# ZIP 直下に置くもの / 中身\ に置くもの
TOP_FILES = ["はじめにお読みください.txt", "LICENSE.txt", "適用する.bat", "apply.ps1"]
INNER_FILES = ["譜面を動画にする.bat", "更新する.bat", "scorevideo_update.ps1"]


def run(*a, **kw):
    kw.setdefault("cwd", ROOT)
    r = subprocess.run(a, capture_output=True, text=True, encoding="utf-8", errors="replace", **kw)
    if r.returncode != 0:
        sys.exit(f"失敗: {' '.join(a)}\n{r.stdout[-2000:]}\n{r.stderr[-2000:]}")
    return r.stdout


def git(*a):
    return run("git", "-c", "core.quotePath=false", *a).strip()


def token():
    r = subprocess.run(["git", "credential", "fill"], input="protocol=https\nhost=github.com\n\n",
                       capture_output=True, text=True)
    for line in r.stdout.splitlines():
        if line.startswith("password="):
            return line[9:]
    sys.exit("GitHub の認証情報が取れません（Git Credential Manager）")


def api(method, url, tok, body=None, data=None, ctype="application/json"):
    if body is not None:
        data = json.dumps(body, ensure_ascii=False).encode("utf-8")
    req = urllib.request.Request(url, data=data, method=method, headers={
        "Authorization": "token " + tok, "Accept": "application/vnd.github+json",
        "Content-Type": ctype + ("; charset=utf-8" if ctype == "application/json" else ""),
        "User-Agent": "OpenTaiko-Plus-release"})
    try:
        with urllib.request.urlopen(req, timeout=300) as r:
            return json.loads(r.read().decode("utf-8") or "{}")
    except urllib.error.HTTPError as e:
        sys.exit(f"GitHub API {method} {url} → {e.code}\n{e.read().decode('utf-8', 'replace')[:1000]}")


def crlf(b, bom):
    """配布する文字ファイルは CRLF にそろえる（.ps1 は BOM 付き。.bat は ASCII のみ）"""
    s = b.decode("utf-8-sig").replace("\r\n", "\n").replace("\n", "\r\n")
    return (b"\xef\xbb\xbf" if bom else b"") + s.encode("utf-8")


def privacy_check(paths):
    home = os.path.expanduser("~")
    g = lambda k: subprocess.run(["git", "config", "--global", k], capture_output=True, text=True,
                                 encoding="utf-8").stdout.strip()
    needles = [os.environ.get("USERNAME", ""), home, g("user.name"), g("user.email"),
               "arcana", "C:\\Users", "Downloads", "AppData"]
    needles = [n for n in needles if n]
    bad = 0
    for p in paths:
        data = open(p, "rb").read().lower()
        for n in needles:
            for enc in (n.lower().encode("utf-8"), n.lower().encode("utf-16le")):
                c = data.count(enc)
                if c:
                    bad += c
                    print(f"  ★ {os.path.basename(p)} に個人情報らしき文字列（{len(n)} 文字）が {c} 件")
    if bad:
        sys.exit("個人情報が見つかったので止めました（値は表示しません）")
    print("  個人情報チェック: 0 件")


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    flags = {a for a in sys.argv[1:] if a.startswith("--")}
    if len(args) != 1:
        print(__doc__); sys.exit(1)
    ver = args[0].lstrip("v")
    tag = "v" + ver
    zip_only = "--zip-only" in flags

    # 1. 状態の確認
    if git("rev-parse", "--abbrev-ref", "HEAD") != "main":
        sys.exit("main ブランチで実行してください")
    dirty = [l for l in git("status", "--porcelain").splitlines() if not l.startswith("??")]
    if dirty and not zip_only:
        sys.exit("コミットしていない変更があります:\n" + "\n".join(dirty))
    if not zip_only and git("tag", "-l", tag):
        sys.exit(f"タグ {tag} は既にあります")
    tags = [t for t in git("tag", "-l", "v*", "--sort=-v:refname").splitlines() if t != "v0.5.2.1"]
    prev = tags[0] if tags else None

    # 2. ビルド（PDB を作らない）
    print("ビルド中…")
    run(MSBUILD, "TJAPlayer3\\TJAPlayer3.csproj", "-t:restore", "-v:q", "-nologo")
    run(MSBUILD, "TJAPlayer3\\TJAPlayer3.csproj", "-t:Rebuild", "-p:Configuration=Release", "-p:Platform=x86",
        "-p:PostBuildEvent=", "-p:DebugType=none", "-p:DebugSymbols=false", "-v:m", "-nologo")
    exe = os.path.join(ROOT, "Test", "OpenTaiko.exe")
    dll = os.path.join(ROOT, "FDK19", "bin", "x86", "Release", "FDK.dll")

    # 3. 個人情報
    privacy_check([exe, dll])

    # 4. ZIP
    out = os.path.join(HERE, "out")
    os.makedirs(out, exist_ok=True)
    zpath = os.path.join(out, f"OpenTaiko-Plus-{tag}.zip")
    with zipfile.ZipFile(zpath, "w", zipfile.ZIP_DEFLATED) as z:
        def add_text(name, arc):
            b = open(os.path.join(HERE, name), "rb").read()
            if name.endswith(".bat"):
                b.decode("ascii")          # 日本語が混ざっていたらここで止まる
            z.writestr(arc, crlf(b, bom=name.endswith((".ps1", ".txt")) and name != "LICENSE.txt"))
        for f in TOP_FILES:
            add_text(f, f"{TOP}/{f}")
        for f in INNER_FILES:
            add_text(f, f"{TOP}/中身/{f}")
        z.writestr(f"{TOP}/中身/scorevideo_version.txt", tag + "\r\n")
        z.write(exe, f"{TOP}/中身/OpenTaiko.exe")
        z.write(dll, f"{TOP}/中身/dll/FDK.dll")
        # 本家の決まり: 改造・再配布するときは Licenses フォルダを必ず同梱する（sparse で手元に無いので git から取る）
        for lp in git("ls-tree", "-r", "--name-only", "HEAD", "Test/Licenses").splitlines():
            data = subprocess.run(["git", "show", "HEAD:" + lp], cwd=ROOT, capture_output=True, check=True).stdout
            z.writestr(f"{TOP}/Licenses/" + lp[len("Test/Licenses/"):], data)
    # 1 行で入れるコマンド（releases/latest/download/install.ps1）が落とすもの
    inst = os.path.join(out, "install.ps1")
    open(inst, "wb").write(crlf(open(os.path.join(HERE, "scorevideo_update.ps1"), "rb").read(), bom=True))
    print(f"ZIP: {zpath} ({os.path.getsize(zpath) // 1024} KB)")
    if zip_only:
        return

    # 変わったこと（前のタグからのコミットの 1 行目）
    rng = f"{prev}..HEAD" if prev else "HEAD~1..HEAD"
    changes = [s for s in git("log", "--format=%s", rng).splitlines() if s]
    notes = os.path.join(HERE, "notes", ver + ".md")
    if os.path.exists(notes):
        body = open(notes, encoding="utf-8-sig").read().strip()
    else:
        body ="\n".join("- " + s for s in changes) + (
        "\n\n**入れ方**: ZIP を展開して「適用する.bat」をダブルクリック。"
        "\n**更新**: 一度入れたら、OpenTaiko のフォルダの「更新する.bat」で最新版になります。")
    print(f"\n{tag} の変わったこと:\n{body}\n")
    if "--yes" not in flags and input(f"{tag} を push して GitHub に公開しますか？ (y/N) ").strip() != "y":
        sys.exit("やめました（ZIP は残してあります）")

    # 5. タグと push（作者・日時は ar-ca-na / +0000）
    env = dict(os.environ)
    now = subprocess.run(["python", "-c", "import datetime;print(datetime.datetime.now(datetime.timezone.utc)"
                          ".strftime('%Y-%m-%dT%H:%M:%S+0000'))"], capture_output=True, text=True).stdout.strip()
    env["GIT_COMMITTER_DATE"] = now
    r = subprocess.run(["git", "tag", "-a", tag, "-m", f"OpenTaiko 機能追加版 {tag}"], cwd=ROOT, env=env)
    if r.returncode: sys.exit("タグを付けられませんでした")
    run("git", "push", REMOTE, "main", tag)

    # 6. Release と添付
    tok = token()
    rel = api("POST", f"https://api.github.com/repos/{REPO}/releases", tok,
              {"tag_name": tag, "name": f"OpenTaiko Plus {tag}（機能追加版・非公式）", "body": body})
    up = rel["upload_url"].split("{")[0] + "?name=" + urllib.parse.quote(os.path.basename(zpath))
    api("POST", up, tok, data=open(zpath, "rb").read(), ctype="application/zip")
    up = rel["upload_url"].split("{")[0] + "?name=install.ps1"
    api("POST", up, tok, data=open(inst, "rb").read(), ctype="application/octet-stream")
    del tok
    print(f"公開しました: {rel['html_url']}")

    if "--deploy" in flags:
        if not os.path.exists(GAME_FILE):
            sys.exit(f"--deploy の入れ先が分かりません。{GAME_FILE} にゲームフォルダのパスを 1 行で書いてください")
        GAME = open(GAME_FILE, encoding="utf-8-sig").read().strip()
        if subprocess.run(["tasklist", "/FI", "IMAGENAME eq OpenTaiko.exe"], capture_output=True,
                          text=True, encoding="cp932", errors="replace").stdout.count("OpenTaiko.exe"):
            print("OpenTaiko が起動中なので、ゲームフォルダへの反映は飛ばしました")
        else:
            shutil.copy2(exe, os.path.join(GAME, "OpenTaiko.exe"))
            shutil.copy2(dll, os.path.join(GAME, "dll", "FDK.dll"))
            # ゲーム内の「U キーで更新」と 更新する.bat が使う台本も、配布物と同じものにそろえる
            for f in ("scorevideo_update.ps1", "更新する.bat"):
                shutil.copy2(os.path.join(HERE, f), os.path.join(GAME, f))
            open(os.path.join(GAME, "scorevideo_version.txt"), "w", newline="\r\n").write(tag + "\n")
            print("ゲームフォルダにも入れました")


if __name__ == "__main__":
    main()
