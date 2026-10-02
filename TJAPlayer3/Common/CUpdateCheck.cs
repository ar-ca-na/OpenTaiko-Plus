using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using FDK;

namespace TJAPlayer3
{
    /// <summary>
    /// 起動時に GitHub の最新 Release を調べ、新しい版があれば画面の上に 12 秒ほどお知らせを出す。
    /// 今の版は exe の隣の scorevideo_version.txt（更新する.bat と同じもの）から読む。
    /// 無ければ（配布物から入れていなければ）調べない。通信に失敗しても何も出さない。
    /// </summary>
    internal static class CUpdateCheck
    {
        private const string 最新版のURL = "https://api.github.com/repos/ar-ca-na/OpenTaiko-Plus/releases/latest";
        private const int 表示ms = 12000;
        private const int フェードms = 600;

        private static string 今の版;
        private static volatile string 新しい版;
        private static CTexture txお知らせ;
        private static Stopwatch sw表示;
        private static bool b表示し終えた;

        /// <summary>起動時に 1 回呼ぶ。裏のスレッドで調べるので、すぐ戻る。</summary>
        public static void t調べ始める()
        {
            if (COfflineExport.Enabled) return;
            今の版 = 今の版を読む();
            if (今の版 == null) return;

            var th = new Thread(() =>
            {
                try
                {
                    string 最新 = 最新の版を聞く();
                    if (最新 != null && 版を比べる(最新, 今の版) > 0)
                    {
                        Trace.TraceInformation("[更新] 新しい版があります: " + 最新 + "（いまは " + 今の版 + "）");
                        新しい版 = 最新;
                    }
                    else
                        Trace.TraceInformation("[更新] 最新です: " + 今の版 + "（Release: " + (最新 ?? "?") + "）");
                }
                catch (Exception e)
                {
                    Trace.TraceWarning("[更新] 新しい版を調べられませんでした: " + e.Message);
                }
            });
            th.IsBackground = true;
            th.Name = "UpdateCheck";
            th.Start();
        }

        private static string 今の版を読む()
        {
            try
            {
                string p = Path.Combine(TJAPlayer3.strEXEのあるフォルダ, "scorevideo_version.txt");
                if (!File.Exists(p)) return null;
                string s = File.ReadAllText(p).Trim();
                return s.Length > 0 ? s : null;
            }
            catch { return null; }
        }

        private static string 最新の版を聞く()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var req = (HttpWebRequest)WebRequest.Create(最新版のURL);
            req.UserAgent = "OpenTaiko-Plus";
            req.Accept = "application/vnd.github+json";
            req.Timeout = 10000;
            req.ReadWriteTimeout = 10000;
            using (var res = (HttpWebResponse)req.GetResponse())
            using (var sr = new StreamReader(res.GetResponseStream(), Encoding.UTF8))
            {
                var m = Regex.Match(sr.ReadToEnd(), "\"tag_name\"\\s*:\\s*\"([^\"]+)\"");
                return m.Success ? m.Groups[1].Value : null;
            }
        }

        /// <summary>"v1.10" と "v1.9" のような版を数字の並びとして比べる。a が新しければ正。</summary>
        internal static int 版を比べる(string a, string b)
        {
            var na = Regex.Matches(a, @"\d+");
            var nb = Regex.Matches(b, @"\d+");
            int n = Math.Max(na.Count, nb.Count);
            for (int i = 0; i < n; i++)
            {
                long x = i < na.Count ? long.Parse(na[i].Value) : 0;
                long y = i < nb.Count ? long.Parse(nb[i].Value) : 0;
                if (x != y) return x > y ? 1 : -1;
            }
            return 0;
        }

        /// <summary>
        /// 毎コマ、ステージを描いたあとに呼ぶ。動画に写らないよう、録画する画面（曲読み込み・演奏）では出さない。
        /// 起動時の注意書き（起動ステージ）の間も待ち、タイトルに入ってから時間を数え始める。
        /// </summary>
        public static void t描画(CStage stage)
        {
            if (b表示し終えた || 新しい版 == null || stage == null) return;
            if (stage.eステージID == CStage.Eステージ.起動 || stage.eステージID == CStage.Eステージ.演奏
                || stage.eステージID == CStage.Eステージ.曲読み込み)
                return;

            if (txお知らせ == null)
            {
                using (var bmp = お知らせの絵を作る(新しい版, 今の版))
                    txお知らせ = TJAPlayer3.tテクスチャの生成(bmp, false);
                if (txお知らせ == null) { b表示し終えた = true; return; }
                sw表示 = Stopwatch.StartNew();
            }

            long t = sw表示.ElapsedMilliseconds;
            if (t >= 表示ms)
            {
                TJAPlayer3.tテクスチャの解放(ref txお知らせ);
                b表示し終えた = true;
                return;
            }
            float a = Math.Min(1f, Math.Min(t, 表示ms - t) / (float)フェードms);
            float y = 12 - (1f - a) * 30f;
            txお知らせ.Opacity = (int)(255 * a);
            txお知らせ.t2D描画(TJAPlayer3.app.Device, (1280 - txお知らせ.sz画像サイズ.Width) / 2, y);
        }

        private static Bitmap お知らせの絵を作る(string 新, string 今)
        {
            string l1 = "新しい版 " + 新 + " が出ています（いまは " + 今 + "）";
            string l2 = "ゲームフォルダの「更新する.bat」で更新できます";
            string fontName = TJAPlayer3.ConfigIni?.FontName;
            if (string.IsNullOrEmpty(fontName)) fontName = "MS UI Gothic";

            const int W = 760, H = 84;
            var bmp = new Bitmap(W, H);
            using (var g = Graphics.FromImage(bmp))
            using (var f1 = new Font(fontName, 22f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var f2 = new Font(fontName, 18f, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var path = 角丸(new Rectangle(1, 1, W - 3, H - 3), 16))
            using (var bg = new SolidBrush(Color.FromArgb(225, 20, 24, 36)))
            using (var edge = new Pen(Color.FromArgb(255, 255, 196, 60), 2f))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center })
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.Clear(Color.Transparent);
                g.FillPath(bg, path);
                g.DrawPath(edge, path);
                g.DrawString(l1, f1, new SolidBrush(Color.FromArgb(255, 255, 214, 90)), new RectangleF(0, 12, W, 30), sf);
                g.DrawString(l2, f2, Brushes.White, new RectangleF(0, 46, W, 26), sf);
            }
            return bmp;
        }

        private static GraphicsPath 角丸(Rectangle r, int d)
        {
            var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }
}
