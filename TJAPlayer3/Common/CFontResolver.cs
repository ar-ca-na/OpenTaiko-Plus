using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Text;
using Microsoft.Win32;

namespace TJAPlayer3
{
    /// <summary>
    /// フォント名から FontFamily を得る。
    /// Windows に読み込まれていないフォント（ユーザー単位で入れたフォントが再起動後に読まれない等）は、
    /// フォントのファイルを探して直接読む。どこにも無ければ MS UI Gothic にする（例外で落とさない）。
    /// 探す場所: レジストリのフォント一覧（HKCU・HKLM）→ exe の隣の Fonts フォルダ → ユーザー単位のフォントフォルダ。
    /// </summary>
    internal static class CFontResolver
    {
        public static FontFamily Family(string name)
        {
            if (string.IsNullOrEmpty(name)) name = 既定;

            try { return new FontFamily(name); }
            catch (ArgumentException) { }

            lock (同期)
            {
                string 実名;
                if (!解決済み.TryGetValue(name, out 実名))
                {
                    実名 = ファイルから読む(name);
                    解決済み[name] = 実名;
                    if (実名 != null)
                        Trace.TraceInformation("[フォント] {0} は Windows に読み込まれていないため、ファイルから直接読みました。", name);
                    else
                        Trace.TraceWarning("[フォント] {0} が見つかりません。{1} で代用します。", name, 既定);
                }
                if (実名 != null)
                    return new FontFamily(実名, 直接読んだフォント);
            }

            try { return new FontFamily(既定); }
            catch (ArgumentException) { return new FontFamily(GenericFontFamilies.SansSerif); }
        }

        private const string 既定 = "MS UI Gothic";
        private static readonly object 同期 = new object();
        private static readonly Dictionary<string, string> 解決済み = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly PrivateFontCollection 直接読んだフォント = new PrivateFontCollection();

        // 見つけたら PrivateFontCollection 上のファミリ名を返す。無ければ null。
        private static string ファイルから読む(string name)
        {
            string 探す名 = 正規化(name);
            var 試した = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. レジストリの登録名が一致するもの（例: "FOT-大江戸勘亭流 Std E (OpenType)"）
            foreach (var 登録 in レジストリのフォント())
            {
                if (正規化(登録.Key).Contains(探す名))
                {
                    string 実名 = 試す(登録.Value, 探す名, 試した);
                    if (実名 != null) return 実名;
                }
            }

            // 2. exe の隣の Fonts フォルダ（インストールせずに置くだけで使える）
            // 3. ユーザー単位のフォントフォルダ（登録名がファミリ名と違うとき）
            var フォルダ = new List<string>
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fonts"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\Windows\Fonts"),
            };
            foreach (string dir in フォルダ)
            {
                string[] files;
                try
                {
                    if (!Directory.Exists(dir)) continue;
                    files = Directory.GetFiles(dir);
                }
                catch (Exception) { continue; }
                foreach (string f in files)
                {
                    string ext = Path.GetExtension(f).ToLowerInvariant();
                    if (ext != ".ttf" && ext != ".otf" && ext != ".ttc") continue;
                    string 実名 = 試す(f, 探す名, 試した);
                    if (実名 != null) return 実名;
                }
            }
            return null;
        }

        private static string 試す(string path, string 探す名, HashSet<string> 試した)
        {
            if (string.IsNullOrEmpty(path) || !試した.Add(path) || !File.Exists(path)) return null;
            try
            {
                // 別の入れ物で中身の名前を確かめ、当たったものだけ本番の入れ物へ足す
                using (var 試し = new PrivateFontCollection())
                {
                    試し.AddFontFile(path);
                    foreach (var ff in 試し.Families)
                    {
                        if (!名前が合う(ff, 探す名)) continue;
                        直接読んだフォント.AddFontFile(path);
                        foreach (var 本番 in 直接読んだフォント.Families)
                            if (名前が合う(本番, 探す名)) return 本番.Name;
                    }
                }
            }
            catch (Exception e)
            {
                Trace.TraceWarning("[フォント] {0} を読めませんでした: {1}", Path.GetFileName(path), e.Message);
            }
            return null;
        }

        private static bool 名前が合う(FontFamily ff, string 探す名)
        {
            if (正規化(ff.Name) == 探す名) return true;
            foreach (int lcid in new[] { 1041, 1033 })   // 日本語名・英語名
            {
                try { if (正規化(ff.GetName(lcid)) == 探す名) return true; }
                catch (Exception) { }
            }
            return false;
        }

        private static string 正規化(string s)
        {
            return (s ?? "").Normalize(NormalizationForm.FormKC).Replace(" ", "").ToLowerInvariant();
        }

        // 登録名 → ファイルの絶対パス
        private static IEnumerable<KeyValuePair<string, string>> レジストリのフォント()
        {
            string winFonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
            foreach (var root in new[] { Registry.CurrentUser, Registry.LocalMachine })
            {
                var list = new List<KeyValuePair<string, string>>();
                try
                {
                    using (var key = root.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts"))
                    {
                        if (key == null) continue;
                        foreach (string v in key.GetValueNames())
                        {
                            string p = key.GetValue(v) as string;
                            if (string.IsNullOrEmpty(p)) continue;
                            if (!Path.IsPathRooted(p)) p = Path.Combine(winFonts, p);
                            list.Add(new KeyValuePair<string, string>(v, p));
                        }
                    }
                }
                catch (Exception) { }
                foreach (var kv in list) yield return kv;
            }
        }
    }
}
