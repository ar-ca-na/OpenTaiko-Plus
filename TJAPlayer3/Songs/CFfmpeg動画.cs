using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;

namespace TJAPlayer3
{
    /// <summary>
    /// BGMOVIE を ffmpeg で 1 コマずつ BGRA に開いて渡す。
    /// DirectShow / AVIFile では mp4（H.264）が開けない PC が多く、背景動画が出なかった。
    /// また DirectShow は実時間で再生するので、書き出し（ゲーム内時刻を 1 コマずつ進める）と合わない。
    /// ここでは「何ミリ秒目のコマ」を指定して取り出すので、どちらの問題も起きない。
    /// 裏のスレッドが ffmpeg の出力を少し先まで読んでおく。後ろへ飛んだり大きく先へ飛んだりしたら -ss で開き直す。
    /// </summary>
    internal sealed class CFfmpeg動画 : IDisposable
    {
        public readonly int W, H;
        public readonly double Fps;
        public readonly double 長さms;

        private readonly string ffmpeg, path;
        private readonly int frameBytes;
        private readonly object lk = new object();
        private readonly Queue<KeyValuePair<int, byte[]>> q = new Queue<KeyValuePair<int, byte[]>>();
        private readonly Stack<byte[]> 空き = new Stack<byte[]>();
        private const int 先読み = 4;

        private Process proc;
        private Thread reader;
        private int 世代;           // 開き直すたびに増やす。古い読み手の結果を捨てるため
        private int 開始idx;
        private int 次のidx;        // 読み手が次に積むコマ番号
        private bool eof;
        private bool disposed;

        private byte[] 最新;
        private int 最新idx = -1;
        /// <summary>前回 t取り出す が返したコマ番号。テクスチャへの転写を省くのに使う。</summary>
        public int 渡したidx { get; private set; } = -1;

        private CFfmpeg動画(string ffmpeg, string path, int srcW, int srcH, double fps, double durMs)
        {
            this.ffmpeg = ffmpeg;
            this.path = path;
            // 1280x720 に収まる大きさ（縦横比はそのまま、偶数）にしてから受け取る。
            double s = Math.Min(1280.0 / srcW, 720.0 / srcH);
            W = Math.Max(2, (int)Math.Round(srcW * s / 2) * 2);
            H = Math.Max(2, (int)Math.Round(srcH * s / 2) * 2);
            Fps = Math.Max(1, Math.Min(60, fps));
            長さms = durMs;
            frameBytes = W * H * 4;
        }

        /// <summary>ffmpeg が無い・映像が無い・読めないときは null。</summary>
        public static CFfmpeg動画 t開く(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
                string ff = COfflineExport.FfmpegPath;
                if (string.IsNullOrEmpty(ff) || !File.Exists(ff)) ff = COfflineExport.FindFfmpeg();
                if (string.IsNullOrEmpty(ff)) { Trace.TraceWarning("[BGMOVIE] ffmpeg が見つからないので、ffmpeg では開きません。"); return null; }

                string err;
                using (var p = new Process())
                {
                    p.StartInfo = new ProcessStartInfo(ff, "-hide_banner -i \"" + path + "\"")
                    {
                        UseShellExecute = false, CreateNoWindow = true,
                        RedirectStandardError = true, RedirectStandardOutput = true,
                        StandardErrorEncoding = System.Text.Encoding.UTF8,
                    };
                    p.Start();
                    p.StandardOutput.ReadToEndAsync();
                    err = p.StandardError.ReadToEnd();
                    p.WaitForExit(5000);
                }
                var mv = Regex.Match(err, @"Stream #\S+.*?: Video: .*?, (\d{2,5})x(\d{2,5})");
                if (!mv.Success) { Trace.TraceWarning("[BGMOVIE] 映像が見つかりません: " + path); return null; }
                int w = int.Parse(mv.Groups[1].Value), h = int.Parse(mv.Groups[2].Value);
                double fps = 30;
                var mf = Regex.Match(err.Substring(mv.Index), @"([\d.]+) (fps|tbr)");
                if (mf.Success) double.TryParse(mf.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out fps);
                if (fps <= 0 || double.IsNaN(fps)) fps = 30;
                double dur = double.MaxValue;
                var md = Regex.Match(err, @"Duration: (\d+):(\d+):(\d+(?:\.\d+)?)");
                if (md.Success)
                    dur = (int.Parse(md.Groups[1].Value) * 3600 + int.Parse(md.Groups[2].Value) * 60
                        + double.Parse(md.Groups[3].Value, CultureInfo.InvariantCulture)) * 1000.0;

                var v = new CFfmpeg動画(ff, path, w, h, fps, dur);
                Trace.TraceInformation("[BGMOVIE] ffmpeg で開きます: {0} ({1}x{2} → {3}x{4}, {5:0.###} fps, {6:0} ms)", path, w, h, v.W, v.H, v.Fps, dur);
                return v;
            }
            catch (Exception e)
            {
                Trace.TraceWarning("[BGMOVIE] ffmpeg で開けませんでした: " + e.Message);
                return null;
            }
        }

        /// <summary>
        /// 動画の先頭から ms ミリ秒目のコマを返す（BGRA、W*H*4 バイト）。まだ無ければ null。
        /// wait=true なら届くまで待つ（書き出し用。時刻と映像を必ず合わせる）。
        /// false なら待たずに、手元にある一番近いコマを返す（遊ぶとき用。画面を止めない）。
        /// </summary>
        public byte[] t取り出す(double ms, bool wait)
        {
            if (disposed || ms < 0) return null;
            int idx = (int)Math.Floor(ms * Fps / 1000.0 + 1e-6);
            lock (lk)
            {
                if (idx != 最新idx)
                {
                    bool 開き直す = proc == null
                        || idx < 開始idx
                        || (最新idx >= 0 && idx < 最新idx)
                        || idx > 次のidx + (int)(Fps * 2);
                    if (開き直す && !(eof && idx >= 次のidx))
                        t開き直す(idx);

                    var sw = Stopwatch.StartNew();
                    while (true)
                    {
                        while (q.Count > 0 && q.Peek().Key <= idx)
                        {
                            var kv = q.Dequeue();
                            if (最新 != null) 空き.Push(最新);
                            最新 = kv.Value;
                            最新idx = kv.Key;
                            Monitor.PulseAll(lk);
                        }
                        if (最新idx == idx || !wait || (eof && q.Count == 0)) break;
                        if (sw.ElapsedMilliseconds > 5000)
                        {
                            Trace.TraceWarning("[BGMOVIE] コマ {0} が 5 秒届かないので、あきらめます。", idx);
                            break;
                        }
                        Monitor.Wait(lk, 200);
                    }
                }
                渡したidx = 最新idx;
                return 最新;
            }
        }

        // lk を持って呼ぶ
        private void t開き直す(int idx)
        {
            t止める();
            世代++;
            開始idx = idx;
            次のidx = idx;
            eof = false;
            while (q.Count > 0) 空き.Push(q.Dequeue().Value);
            最新idx = -1;

            string ss = (idx / Fps).ToString("0.######", CultureInfo.InvariantCulture);
            var p = new Process();
            p.StartInfo = new ProcessStartInfo(ffmpeg,
                "-hide_banner -loglevel error -nostdin -ss " + ss + " -i \"" + path + "\" -an -sn"
                + " -vf \"scale=" + W + ":" + H + ":flags=bicubic,fps=" + Fps.ToString("0.######", CultureInfo.InvariantCulture) + "\""
                + " -f rawvideo -pix_fmt bgra pipe:1")
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true,
            };
            p.ErrorDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) Trace.TraceWarning("[BGMOVIE] ffmpeg: " + e.Data); };
            p.Start();
            p.BeginErrorReadLine();
            proc = p;

            int gen = 世代;
            var st = p.StandardOutput.BaseStream;
            reader = new Thread(() => t読み続ける(st, gen)) { IsBackground = true, Name = "BGMOVIE" };
            reader.Start();
        }

        private void t読み続ける(Stream st, int gen)
        {
            try
            {
                while (true)
                {
                    byte[] buf;
                    lock (lk)
                    {
                        while (gen == 世代 && q.Count >= 先読み) Monitor.Wait(lk);
                        if (gen != 世代) return;
                        buf = 空き.Count > 0 ? 空き.Pop() : new byte[frameBytes];
                    }
                    int got = 0;
                    while (got < frameBytes)
                    {
                        int n = st.Read(buf, got, frameBytes - got);
                        if (n <= 0) break;
                        got += n;
                    }
                    lock (lk)
                    {
                        if (gen != 世代) return;
                        if (got < frameBytes) { eof = true; Monitor.PulseAll(lk); return; }
                        q.Enqueue(new KeyValuePair<int, byte[]>(次のidx++, buf));
                        Monitor.PulseAll(lk);
                    }
                }
            }
            catch (Exception)
            {
                lock (lk) { if (gen == 世代) { eof = true; Monitor.PulseAll(lk); } }
            }
        }

        // lk を持って呼ぶ
        private void t止める()
        {
            世代++;
            Monitor.PulseAll(lk);
            if (proc != null)
            {
                try { if (!proc.HasExited) proc.Kill(); } catch { }
                try { proc.Dispose(); } catch { }
                proc = null;
            }
            reader = null;
        }

        public void Dispose()
        {
            lock (lk)
            {
                if (disposed) return;
                disposed = true;
                t止める();
                q.Clear();
                空き.Clear();
                最新 = null;
            }
        }
    }
}
