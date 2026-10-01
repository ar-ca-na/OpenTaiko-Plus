using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using SlimDX.Direct3D9;
using FDK;

namespace TJAPlayer3
{
    /// <summary>
    /// 譜面を「実時間を待たずに」動画へ書き出すモード。
    ///
    /// 普通に遊ぶときは何もしない。コマンドラインに --export が付いたときだけ働く。
    ///   OpenTaiko.exe --export "曲.tja" [--out "出力.mp4"] [--fps 60]
    ///
    /// やっていることは 3 つ。
    ///   1. 演奏タイマを 1/60 秒ずつの固定値にする（実時間と切り離す）
    ///   2. フレーム毎のスリープを飛ばす
    ///   3. EndScene の直後にバックバッファを読んで ffmpeg に渡す
    /// 描くのは本体そのものなので、出てくる絵は普通に遊んだときと同一。
    /// 実時間で走らないので、PC が重くてもコマ落ちしない。
    /// </summary>
    internal static class COfflineExport
    {
        /// <summary>コマンドラインで --export が指定されたか。</summary>
        public static bool Enabled;
        public static string TjaPath;
        public static string OutPath;
        public static string FfmpegPath;
        public static int Fps = 60;
        public static int Crf = 18;
        public static string Preset = "medium";
        public static int OutWidth, OutHeight;   // 0 なら本体の解像度のまま
        /// <summary>1P に演奏させる難易度。-1 なら指定なし（おにを頼む）。</summary>
        public static int 難易度1P = -1;
        /// <summary>2P に演奏させる難易度。-1 なら 2P を使わない。-2 なら指定なし（Config.ini のまま）。</summary>
        public static int 難易度2P = 指定なし;
        public const int 指定なし = -2;
        public const int 使わない = -1;
        private static string 分からなかった指定;

        private static long frame;
        private static Process ffmpeg;
        private static Stream ffmpegIn;
        private static Surface sysSurface;
        private static byte[] frameBuffer;
        private static byte[] rowBuffer;
        private static int width, height;
        private static FileStream audioStream;
        private static byte[] audioBuffer;
        private static long audioSamplesWritten;
        private static string videoTmpPath;
        private static string audioTmpPath;
        /// <summary>演奏画面に入ってからのコマ数。譜面の時刻はこちらで決める。</summary>
        /// <summary>曲名の幕を見せる秒数。--curtain で変える。</summary>
        public static double CurtainSec = 3.0;
        private static long 幕コマ = 0;
        private static long playFrame = 0;
        private static long songStartFrame = -1;
        private static bool capturing;
        /// <summary>遊びながら録るときに控える「鳴った音」。--export ではミキサーから取るので使わない。</summary>
        private struct S鳴った音
        {
            public long コマ;
            public string パス;
            public double 音量;
        }
        private static List<S鳴った音> 鳴った音リスト;
        private static bool finished;
        private static bool sawPlayStage;
        private static StreamWriter log;

        // ---- 起動時 ----------------------------------------------------

        /// <summary>コマンドラインを見て、書き出しモードかどうかを決める。</summary>
        public static void ParseCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 1; i < args.Length; i++)
            {
                string a = args[i];
                if (a == "--export" && i + 1 < args.Length)
                {
                    TjaPath = args[++i];
                    Enabled = true;
                }
                else if (a == "--out" && i + 1 < args.Length) OutPath = args[++i];
                else if (a == "--ffmpeg" && i + 1 < args.Length) FfmpegPath = args[++i];
                else if (a == "--curtain" && i + 1 < args.Length)
                    double.TryParse(args[++i], NumberStyles.Float, CultureInfo.InvariantCulture, out CurtainSec);
                else if (a == "--difficulty" && i + 1 < args.Length)
                    難易度1P = 難易度を数にする(args[++i]);
                else if (a == "--difficulty2" && i + 1 < args.Length)
                {
                    string v = args[++i];
                    if (v.Equals("none", StringComparison.OrdinalIgnoreCase)
                        || v.Equals("off", StringComparison.OrdinalIgnoreCase)
                        || v == "なし" || v == "使わない")
                        難易度2P = 使わない;
                    else
                    {
                        int n = 難易度を数にする(v);
                        // 分からない綴りのときに「2P を使わない」にしてしまうと、
                        // 打ち間違いに気づけない。指定なし扱いにして先へ進める。
                        難易度2P = n >= 0 ? n : 指定なし;
                        if (n < 0) 分からなかった指定 = v;
                    }
                }
                else if (a == "--fps" && i + 1 < args.Length) int.TryParse(args[++i], out Fps);
                else if (a == "--crf" && i + 1 < args.Length) int.TryParse(args[++i], out Crf);
                else if (a == "--preset" && i + 1 < args.Length) Preset = args[++i];
                else if (a == "--size" && i + 1 < args.Length)
                {
                    string[] wh = args[++i].Split('x', 'X');
                    if (wh.Length == 2)
                    {
                        int.TryParse(wh[0], out OutWidth);
                        int.TryParse(wh[1], out OutHeight);
                    }
                }
            }
            if (!Enabled) return;

            // 相対パスのまま渡されると、CDTX が譜面のフォルダを "\\" と勘違いして
            // 音源も譜面画像も見つけられなくなる。ここで絶対パスに直しておく。
            try { if (!string.IsNullOrEmpty(TjaPath)) TjaPath = Path.GetFullPath(TjaPath); }
            catch { }

            if (Fps <= 0) Fps = 60;
            if (Crf < 0 || Crf > 51) Crf = 18;
            if (string.IsNullOrEmpty(Preset)) Preset = "medium";
            if (string.IsNullOrEmpty(FfmpegPath)) FfmpegPath = FindFfmpeg();
            if (string.IsNullOrEmpty(OutPath))
                OutPath = Path.ChangeExtension(TjaPath, ".mp4");

            try
            {
                log = new StreamWriter(Path.ChangeExtension(OutPath, ".log"), false);
                log.AutoFlush = true;
            }
            catch { }
            Write("書き出しモードで起動します。");
            Write("  譜面 : " + TjaPath);
            Write("  出力 : " + OutPath);
            Write("  fps  : " + Fps);
            Write("  ffmpeg: " + (FfmpegPath ?? "(見つかりません)"));
            try
            {
                if (!File.Exists(TjaPath))
                    Write("  ※ 譜面のファイルが見つかりません。パスを確かめてください。");
            }
            catch { }
            if (分からなかった指定 != null)
                Write("  ※ 難易度の指定「" + 分からなかった指定 + "」が分かりません。指定なしとして進めます。");
            Write("  1P   : " + 難易度の名前(難易度1P < 0 ? (int)Difficulty.Oni : 難易度1P));
            Write("  2P   : " + (難易度2P == 使わない ? "使わない"
                : 難易度2P == 指定なし ? "指定なし（Config.ini のまま）"
                : 難易度の名前(難易度2P)));
        }

        /// <summary>
        /// 難易度の指定（かんたん / oni / 3 など）を数に直す。分からなければ -1。
        /// </summary>
        private static int 難易度を数にする(string s)
        {
            if (string.IsNullOrEmpty(s)) return -1;
            s = s.Trim();

            int n;
            if (int.TryParse(s, out n))
                return (n >= 0 && n < (int)Difficulty.Total) ? n : -1;

            switch (s.ToLowerInvariant())
            {
                case "easy": case "かんたん": case "簡単": return (int)Difficulty.Easy;
                case "normal": case "ふつう": case "普通": return (int)Difficulty.Normal;
                case "hard": case "むずかしい": case "難しい": return (int)Difficulty.Hard;
                case "oni": case "おに": case "鬼": return (int)Difficulty.Oni;
                case "edit": case "うら": case "裏": case "ura": return (int)Difficulty.Edit;
                case "tower": case "タワー": return (int)Difficulty.Tower;
                case "dan": case "段位": return (int)Difficulty.Dan;
            }
            return -1;
        }

        private static string 難易度の名前(int n)
        {
            switch (n)
            {
                case (int)Difficulty.Easy: return "かんたん";
                case (int)Difficulty.Normal: return "ふつう";
                case (int)Difficulty.Hard: return "むずかしい";
                case (int)Difficulty.Oni: return "おに";
                case (int)Difficulty.Edit: return "うら";
                case (int)Difficulty.Tower: return "タワー";
                case (int)Difficulty.Dan: return "段位";
            }
            return "?";
        }

        /// <summary>
        /// --export のときだけ、設定を書き出し向きに上書きする。
        /// Config.ini はこのモードでは保存しないので、本人の設定は書き換わらない。
        /// 呼ぶのは Config.ini を読んだ直後。
        /// </summary>
        public static void 書き出し用の設定にする()
        {
            if (!Enabled || TJAPlayer3.ConfigIni == null) return;

            // 作譜支援モード（＝特訓モードの見た目）は動画に要らない。
            TJAPlayer3.ConfigIni.bChartAuthoring = false;
            TJAPlayer3.ConfigIni.bTokkunMode = false;
            // 誰も叩かないので、オートでないと譜面がただ流れて全部不可になる。
            TJAPlayer3.ConfigIni.b太鼓パートAutoPlay = true;
            TJAPlayer3.ConfigIni.b太鼓パートAutoPlay2P = true;

            Write("設定を上書き: 作譜支援モード=OFF / 特訓モード=OFF / オート=1P,2P とも ON");
        }

        /// <summary>
        /// --export のときに、どの難易度を何人で演奏するかを決める。
        /// 選曲画面を通らないので、通したときと同じ場所へ値を置いてやる必要がある。
        /// 曲読み込み画面から、譜面を読む前に呼ばれる。
        /// </summary>
        public static void 難易度と人数を決める()
        {
            if (!Enabled) return;
            var 選曲 = TJAPlayer3.stage選曲;
            if (選曲 == null || 選曲.n確定された曲の難易度 == null) return;

            選曲.n確定された曲の難易度[0] = 難易度1P >= 0 ? 難易度1P : (int)Difficulty.Oni;

            if (難易度2P == 使わない)
            {
                TJAPlayer3.ConfigIni.nPlayerCount = 1;
            }
            else if (難易度2P >= 0)
            {
                TJAPlayer3.ConfigIni.nPlayerCount = 2;
                選曲.n確定された曲の難易度[1] = 難易度2P;
            }
            else
            {
                // 指定なし。人数は Config.ini のまま、2P は 1P と同じ難易度にする。
                選曲.n確定された曲の難易度[1] = 選曲.n確定された曲の難易度[0];
            }
        }

        /// <summary>
        /// 頼んだ難易度が tja に無いと、本体は別のコースへ勝手に落ちる。
        /// そのままだと画面には頼んだほうの難易度が出てしまい、
        /// 実際に流れている譜面と食い違うので、読み込んだ側に合わせ直す。
        /// 曲読み込み画面から、譜面を読んだ直後に呼ばれる。
        /// </summary>
        public static void 実際に読み込んだ難易度に合わせる()
        {
            var 選曲 = TJAPlayer3.stage選曲;
            if (選曲 == null || 選曲.n確定された曲の難易度 == null) return;

            // 段位・タワーは仕組みが別（難易度の番号が「そのモード」を表している）ので触らない。
            合わせる(0, TJAPlayer3.DTX);
            if (TJAPlayer3.ConfigIni != null && TJAPlayer3.ConfigIni.nPlayerCount == 2)
                合わせる(1, TJAPlayer3.DTX_2P);
        }

        private static void 合わせる(int player, CDTX dtx)
        {
            var 選曲 = TJAPlayer3.stage選曲;
            if (dtx == null || dtx.n実際に読み込んだ難易度 < 0) return;

            int 頼んだ = 選曲.n確定された曲の難易度[player];
            if (頼んだ == (int)Difficulty.Dan || 頼んだ == (int)Difficulty.Tower) return;
            if (頼んだ == dtx.n実際に読み込んだ難易度) return;

            string s = (player + 1) + "P: 頼んだ難易度（" + 難易度の名前(頼んだ)
                + "）が無いので " + 難易度の名前(dtx.n実際に読み込んだ難易度) + " にします。";
            if (Enabled) Write(s); else Trace.TraceInformation("[難易度] " + s);
            選曲.n確定された曲の難易度[player] = dtx.n実際に読み込んだ難易度;
        }

        /// <summary>
        /// ffmpeg.exe を探す。人によって置き場所が違うので、順に当たる。
        /// </summary>
        private static string FindFfmpeg()
        {
            string exeDir = Path.GetDirectoryName(
                System.Reflection.Assembly.GetExecutingAssembly().Location);
            string[] candidates =
            {
                Path.Combine(exeDir, "ffmpeg.exe"),
                Path.Combine(exeDir, "ffmpeg", "ffmpeg.exe"),
                Path.Combine(exeDir, "ffmpeg", "bin", "ffmpeg.exe"),
                @"C:\tools\ffmpeg\ffmpeg.exe",
                @"C:\ffmpeg\bin\ffmpeg.exe",
            };
            foreach (string c in candidates)
            {
                try { if (File.Exists(c)) return c; }
                catch { }
            }
            // PATH から探す
            try
            {
                string path = Environment.GetEnvironmentVariable("PATH") ?? "";
                foreach (string dir in path.Split(';'))
                {
                    if (dir.Length == 0) continue;
                    string c = Path.Combine(dir.Trim('"'), "ffmpeg.exe");
                    if (File.Exists(c)) return c;
                }
            }
            catch { }
            return null;
        }

        // ---- 毎コマ ----------------------------------------------------

        /// <summary>
        /// 演奏タイマを次のコマへ進める。実時間は見ない。
        /// CTimerBase.n現在時刻 の setter は基準時刻をずらすだけなので、
        /// t更新() を呼ばずにこれを毎コマ入れると固定ステップになる。
        /// </summary>
        /// <summary>
        /// 時刻を固定ステップにしてよいか。演奏画面の間だけ。
        /// 起動や曲読み込みの間まで固定にすると、本体側の初期化が
        /// 実時間前提で書かれているところで破綻する。
        /// </summary>
        /// <summary>
        /// いま書き出しをするか。--export で起動したときと、
        /// Config 画面の「動画を書き出す」を ON にしたときの両方で働く。
        /// </summary>
        public static bool IsActive()
        {
            if (finished) return false;
            if (Enabled) return true;
            return TJAPlayer3.ConfigIni != null && TJAPlayer3.ConfigIni.bVideoExport;
        }

        public static bool ShouldFixTime()
        {
            return IsActive() && IsPlaying();
        }

        /// <summary>
        /// 曲名の幕をもう少し見せておきたいか。
        /// 読み込みが速いと幕が一瞬で通り過ぎ、動画にほとんど写らないので、
        /// 書き出し中だけコマ数で下限を作る。曲読み込み画面から呼ばれる。
        /// </summary>
        public static bool 幕をまだ見せる()
        {
            if (!IsActive() || !capturing) return false;
            return 幕コマ < (long)(CurtainSec * Fps);
        }

        /// <summary>演奏画面かどうか。演奏タイマを固定するのはこの間だけ。</summary>
        private static bool IsPlaying()
        {
            return TJAPlayer3.r現在のステージ != null
                && TJAPlayer3.r現在のステージ.eステージID == CStage.Eステージ.演奏;
        }

        /// <summary>
        /// 動画に入れる画面かどうか。
        /// 曲名が出る幕（曲読み込み画面）から録りたいので、演奏画面だけでなくこちらも含める。
        /// </summary>
        private static bool IsRecordable()
        {
            if (TJAPlayer3.r現在のステージ == null) return false;
            var id = TJAPlayer3.r現在のステージ.eステージID;
            return id == CStage.Eステージ.演奏 || id == CStage.Eステージ.曲読み込み;
        }

        public static void AdvanceTime()
        {
            // 演奏の時刻は「演奏画面に入ってからのコマ数」で決める。
            // 書き出し全体のコマ数（frame）を使うと、曲名の幕のぶんだけ
            // 譜面が先に進んだ状態で演奏が始まってしまう。
            long ms = playFrame * 1000L / Fps;
            if (TJAPlayer3.Timer != null)
            {
                TJAPlayer3.Timer.n現在時刻 = ms;
                TJAPlayer3.Timer.db現在時刻 = ms;
            }
            if (CSound管理.rc演奏用タイマ != null)
            {
                CSound管理.rc演奏用タイマ.n現在時刻 = ms;
                CSound管理.rc演奏用タイマ.db現在時刻 = ms;
            }
            playFrame++;
        }

        /// <summary>EndScene の直後に呼ぶ。演奏画面の間だけ書き出す。</summary>
        public static void Capture()
        {
            if (finished) return;
            if (!IsActive())
            {
                // トグルを切った直後などに、開きっぱなしを残さない
                if (capturing) Finish("書き出しをやめました");
                return;
            }

            bool recordable = IsRecordable();

            if (recordable)
            {
                // 演奏画面まで来たことを覚える。曲読み込み画面だけで
                // 終わってしまった場合に「演奏が終わった」と誤判定しないため。
                if (IsPlaying()) sawPlayStage = true;
            }
            else if (sawPlayStage)
            {
                Finish("演奏が終わりました");
                return;
            }
            if (!recordable) return;

            try
            {
                if (!capturing) Start();
                if (!capturing) return;
                if (!IsPlaying()) 幕コマ++;
                if (!Read()) return;
                ffmpegIn.Write(frameBuffer, 0, frameBuffer.Length);
                WriteAudioForOneFrame();
                frame++;
                if (CSound管理.b曲の再生が始まった)
                {
                    CSound管理.b曲の再生が始まった = false;
                    // 曲名の幕の間に鳴る読み込み音や、選曲画面のプレビュー音も
                    // 同じグループなので、演奏画面に入ってからのぶんだけ拾う。
                    if (songStartFrame < 0 && IsPlaying())
                    {
                        songStartFrame = frame;
                        Write("曲が鳴り始めたコマ: " + frame + "（" + (frame / (double)Fps).ToString("0.000") + " 秒）");
                    }
                }
            }
            catch (Exception e)
            {
                Write("エラー: " + e.Message);
                Finish("エラーのため中断");
            }
        }

        // ---- 中身 ------------------------------------------------------

        private static void Start()
        {
            // Config のトグルから使うときは、いま演奏している曲の隣に出す
            if (string.IsNullOrEmpty(OutPath))
            {
                string tja = CurrentTjaPath();
                if (string.IsNullOrEmpty(tja))
                {
                    // まだ曲が決まっていないだけかもしれないので、打ち切らずに次のコマで出直す。
                    // ここで finished を立てると、以降ずっと書き出さなくなる。
                    return;
                }
                TjaPath = tja;
                OutPath = Path.ChangeExtension(tja, ".mp4");
                if (log == null) OpenLog();
                Write("書き出します: " + OutPath);
            }
            OutPath = 空いている名前にする(OutPath);
            if (string.IsNullOrEmpty(FfmpegPath)) FfmpegPath = FindFfmpeg();
            if (string.IsNullOrEmpty(FfmpegPath) || !File.Exists(FfmpegPath))
            {
                Write("ffmpeg.exe が見つかりません。--ffmpeg でパスを指定してください。");
                finished = true;
                return;
            }
            var dev = TJAPlayer3.app.Device;
            using (Surface back = dev.GetBackBuffer(0, 0))
            {
                SurfaceDescription d = back.Description;
                width = d.Width;
                height = d.Height;
                sysSurface = Surface.CreateOffscreenPlain(
                    dev.UnderlyingDevice, width, height, d.Format, Pool.SystemMemory);
            }
            frameBuffer = new byte[width * height * 4];
            rowBuffer = new byte[width * 4];

            // 映像だけを先に一時ファイルへエンコードする。
            // 音はゲームのミキサーから 1 コマぶんずつ引き抜いて生のまま貯め、
            // 最後にまとめて重ねる（下の Finish）。
            // 曲の音源をそのまま重ねる作りだと、太鼓の音が入らず、
            // 演奏開始の待ち時間や OFFSET のぶんもズレる。
            videoTmpPath = OutPath + ".映像一時.mp4";
            audioTmpPath = OutPath + ".音一時.f32";
            audioSamplesWritten = 0;
            if (CSound管理.b書き出しモード)
            {
                // --export。音はゲームのミキサーから 1 コマぶんずつ引き抜ける。
                audioStream = new FileStream(audioTmpPath, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16);
                audioBuffer = new byte[CSound管理.n書き出し用の1サンプルのバイト数 * (CSound管理.n書き出し用の周波数 / 10 + 16)];
            }
            else
            {
                // 遊びながら録るモード。音はスピーカーへ出す必要があるので引き抜けない。
                // 代わりに「いつ・どの音が鳴ったか」を控えて、最後に並べ直す。
                鳴った音リスト = new List<S鳴った音>();
                CSound管理.鳴った音を知らせる = 音が鳴った;
            }

            string args = "-y -hide_banner -loglevel error"
                + " -f rawvideo -pix_fmt bgra"
                + " -s " + width + "x" + height
                + " -r " + Fps + " -i -"
                + " -c:v libx264 -preset " + Preset + " -crf " + Crf
                + " -pix_fmt yuv420p";
            if (OutWidth > 0 && OutHeight > 0)
                args += " -vf scale=" + OutWidth + ":" + OutHeight + ":flags=lanczos";
            args += " \"" + videoTmpPath + "\"";

            ffmpeg = new Process();
            ffmpeg.StartInfo.FileName = FfmpegPath;
            ffmpeg.StartInfo.Arguments = args;
            ffmpeg.StartInfo.UseShellExecute = false;
            ffmpeg.StartInfo.RedirectStandardInput = true;
            ffmpeg.StartInfo.CreateNoWindow = true;
            ffmpeg.Start();
            ffmpegIn = ffmpeg.StandardInput.BaseStream;
            capturing = true;
            Write("書き出し開始 " + width + "x" + height
                + " / 音の取り方: " + (CSound管理.b書き出しモード
                    ? "ミキサーから直接引き抜く"
                    : "鳴った打音を控えて最後に並べ直す"));
        }

        /// <summary>
        /// このコマぶんの音をミキサーから引き抜いて貯める。
        /// 「これまでに何サンプル書いたか」から今回ぶんを決めるので、
        /// fps が割り切れなくても誤差が溜まらない。
        /// </summary>
        /// <summary>
        /// 遊びながら録るモードで、音が鳴るたびに呼ばれる。
        /// ここでは並べ直すのに要る情報だけ控える（音そのものは触らない）。
        /// </summary>
        private static void 音が鳴った(string パス, double 音量)
        {
            if (鳴った音リスト == null || !capturing) return;
            if (鳴った音リスト.Count > 200000) return;   // 念のための歯止め
            鳴った音リスト.Add(new S鳴った音 { コマ = frame, パス = パス, 音量 = 音量 });
        }

        private static void WriteAudioForOneFrame()
        {
            if (audioStream == null) return;

            int rate = CSound管理.n書き出し用の周波数;
            int sampleBytes = CSound管理.n書き出し用の1サンプルのバイト数;
            long 今回まで = (long)(frame + 1) * rate / Fps;
            int samples = (int)(今回まで - audioSamplesWritten);
            if (samples <= 0) return;

            int bytes = samples * sampleBytes;
            if (audioBuffer.Length < bytes) audioBuffer = new byte[bytes];

            CSound管理.t書き出し用に音を取り出す(audioBuffer, bytes);
            audioStream.Write(audioBuffer, 0, bytes);
            audioSamplesWritten = 今回まで;
        }

        /// <summary>
        /// すでに同じ名前の動画があったら、後ろに _2, _3 … を付けて空いている名前にする。
        /// 上書きしないのは、前に書き出したものを黙って壊さないため。
        /// </summary>
        private static string 空いている名前にする(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return path;

            string dir = Path.GetDirectoryName(path);
            string name = Path.GetFileNameWithoutExtension(path);
            string ext = Path.GetExtension(path);
            for (int i = 2; i < 10000; i++)
            {
                string 候補 = Path.Combine(dir ?? "", name + "_" + i + ext);
                if (!File.Exists(候補))
                {
                    Write("同じ名前の動画があるので " + Path.GetFileName(候補) + " にします。");
                    return 候補;
                }
            }
            return path;
        }

        private static bool Read()
        {
            if (sysSurface == null) return false;
            var dev = TJAPlayer3.app.Device;
            using (Surface back = dev.GetBackBuffer(0, 0))
            {
                dev.UnderlyingDevice.GetRenderTargetData(back, sysSurface);
            }
            SlimDX.DataRectangle dr = sysSurface.LockRectangle(LockFlags.ReadOnly);
            try
            {
                SlimDX.DataStream s = dr.Data;
                int stride = dr.Pitch;
                int rowBytes = width * 4;
                for (int y = 0; y < height; y++)
                {
                    s.Position = (long)y * stride;
                    s.Read(rowBuffer, 0, rowBytes);
                    Buffer.BlockCopy(rowBuffer, 0, frameBuffer, y * rowBytes, rowBytes);
                }
            }
            finally
            {
                sysSurface.UnlockRectangle();
            }
            return true;
        }

        /// <summary>
        /// 控えておいた「鳴った音」を並べ直して、打音の音声ファイルを作る。
        /// 遊びながら録るモード専用（--export はミキサーから直接取れる）。
        /// 位置はコマ番号から決めるので、映像とぴったり合う。
        /// </summary>
        private static void 打音を組み立てる()
        {
            if (鳴った音リスト == null || 鳴った音リスト.Count == 0) return;
            if (string.IsNullOrEmpty(FfmpegPath) || string.IsNullOrEmpty(audioTmpPath)) return;

            int rate = CSound管理.n書き出し用の周波数;
            const int ch = 2;
            long 総サンプル = (long)frame * rate / Fps;
            if (総サンプル <= 0) return;

            // 同じファイルは一度だけ読む。打音は数種類しかない。
            var 波形 = new Dictionary<string, float[]>(StringComparer.OrdinalIgnoreCase);
            foreach (S鳴った音 e in 鳴った音リスト)
            {
                if (e.パス == null || 波形.ContainsKey(e.パス)) continue;
                波形[e.パス] = 波形を読む(e.パス, rate);
            }

            long 最大長 = 0;
            foreach (var w in 波形.Values)
                if (w != null && w.Length / ch > 最大長) 最大長 = w.Length / ch;
            if (最大長 <= 0) return;

            鳴った音リスト.Sort((a, b) => a.コマ.CompareTo(b.コマ));

            int ブロック = rate;                       // 1 秒ずつ作る
            float[] 混ぜる = new float[ブロック * ch];  // 曲の長さぶんを一度に持つと 32bit では足りない
            byte[] 生 = new byte[混ぜる.Length * 4];
            int 先頭 = 0;

            using (var fs = new FileStream(audioTmpPath, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16))
            {
                for (long b = 0; b < 総サンプル; b += ブロック)
                {
                    long 終わり = Math.Min(b + ブロック, 総サンプル);
                    int 長さ = (int)(終わり - b);
                    Array.Clear(混ぜる, 0, 長さ * ch);

                    // もう鳴り終わったものは、次からは見ない
                    while (先頭 < 鳴った音リスト.Count
                        && (鳴った音リスト[先頭].コマ * rate / Fps) + 最大長 <= b) 先頭++;

                    for (int i = 先頭; i < 鳴った音リスト.Count; i++)
                    {
                        long st = 鳴った音リスト[i].コマ * rate / Fps;
                        if (st >= 終わり) break;

                        float[] w;
                        if (鳴った音リスト[i].パス == null
                            || !波形.TryGetValue(鳴った音リスト[i].パス, out w) || w == null) continue;

                        long wlen = w.Length / ch;
                        if (st + wlen <= b) continue;

                        float v = (float)鳴った音リスト[i].音量;
                        long 始 = Math.Max(b, st);
                        long 終 = Math.Min(終わり, st + wlen);
                        for (long q = 始; q < 終; q++)
                        {
                            int d = (int)((q - b) * ch);
                            int t = (int)((q - st) * ch);
                            混ぜる[d] += w[t] * v;
                            混ぜる[d + 1] += w[t + 1] * v;
                        }
                    }

                    for (int k = 0; k < 長さ * ch; k++)
                    {
                        float x = 混ぜる[k];
                        if (x > 1f) x = 1f; else if (x < -1f) x = -1f;
                        混ぜる[k] = x;
                    }
                    Buffer.BlockCopy(混ぜる, 0, 生, 0, 長さ * ch * 4);
                    fs.Write(生, 0, 長さ * ch * 4);
                }
            }
            Write("打音を並べ直しました: " + 鳴った音リスト.Count + " 個（"
                + 波形.Count + " 種類）");
        }

        /// <summary>音のファイルを ffmpeg で生の波形（44100Hz 2ch float）にして読む。</summary>
        private static float[] 波形を読む(string path, int rate)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
                var p = new Process();
                p.StartInfo.FileName = FfmpegPath;
                p.StartInfo.Arguments = "-v error -i \"" + path + "\""
                    + " -f f32le -acodec pcm_f32le -ac 2 -ar " + rate + " -";
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.CreateNoWindow = true;
                p.Start();
                byte[] bytes;
                using (var ms = new MemoryStream())
                {
                    p.StandardOutput.BaseStream.CopyTo(ms);
                    bytes = ms.ToArray();
                }
                p.WaitForExit(30000);
                p.Dispose();
                if (bytes.Length < 8) return null;
                var f = new float[bytes.Length / 4];
                Buffer.BlockCopy(bytes, 0, f, 0, f.Length * 4);
                return f;
            }
            catch (Exception e)
            {
                Write("音が読めません: " + path + " (" + e.Message + ")");
                return null;
            }
        }

        /// <summary>映像の一時ファイルと、貯めた音を重ねて完成品にする。</summary>
        private static void Mux()
        {
            if (videoTmpPath == null || !File.Exists(videoTmpPath)) return;
            try
            {
                // --export ならミキサーから引き抜いたもの、
                // 遊びながら録るモードなら並べ直したもの。どちらも同じ形式。
                bool 打音あり = audioTmpPath != null && File.Exists(audioTmpPath)
                    && new FileInfo(audioTmpPath).Length > 0;

                // 曲（BGM）はミキサーに混ぜていないので、ここで音源ファイルから重ねる。
                // ずらす量は「ゲームが曲を鳴らし始めたコマ」そのもの。
                // 音源をそのまま使うので、リサンプルによる音質の劣化もない。
                string 音源 = FindAudio();
                bool 曲あり = 音源 != null && songStartFrame >= 0;
                int 遅延ms = 曲あり ? (int)(songStartFrame * 1000 / Fps) : 0;
                double 曲の音量 = CSound管理.d曲の音量;

                string args = "-y -hide_banner -loglevel error -i \"" + videoTmpPath + "\"";
                if (打音あり)
                    args += " -f f32le -ar " + CSound管理.n書き出し用の周波数 + " -ac 2 -i \"" + audioTmpPath + "\"";
                if (曲あり)
                    args += " -i \"" + 音源 + "\"";

                if (打音あり && 曲あり)
                {
                    args += " -filter_complex \"[2:a]adelay=" + 遅延ms + "|" + 遅延ms
                         + ",volume=" + 曲の音量.ToString("0.####", CultureInfo.InvariantCulture) + "[bgm];"
                         + "[1:a][bgm]amix=inputs=2:duration=first:normalize=0[a]\""
                         + " -map 0:v:0 -map \"[a]\" -c:v copy -c:a aac -b:a 192k";
                }
                else if (打音あり)
                {
                    args += " -c:v copy -c:a aac -b:a 192k -map 0:v:0 -map 1:a:0 -shortest";
                }
                else if (曲あり)
                {
                    // 打音を録れていない場合（遊びながら録るモードなど）は曲だけ重ねる。
                    args += " -filter_complex \"[1:a]adelay=" + 遅延ms + "|" + 遅延ms
                         + ",volume=" + 曲の音量.ToString("0.####", CultureInfo.InvariantCulture) + "[a]\""
                         + " -map 0:v:0 -map \"[a]\" -c:v copy -c:a aac -b:a 192k -shortest";
                }
                else
                {
                    args += " -c:v copy";
                }
                args += " \"" + OutPath + "\"";
                Write("音を重ねます: 打音=" + 打音あり + " / 曲=" + 曲あり
                    + (曲あり ? " (" + (遅延ms / 1000.0).ToString("0.000") + " 秒から, 音量 "
                        + 曲の音量.ToString("0.###", CultureInfo.InvariantCulture) + ")" : ""));

                var mux = new Process();
                mux.StartInfo.FileName = FfmpegPath;
                mux.StartInfo.Arguments = args;
                mux.StartInfo.UseShellExecute = false;
                mux.StartInfo.CreateNoWindow = true;
                mux.Start();
                mux.WaitForExit(120000);
                mux.Dispose();
                Write("音を重ねました。");
            }
            catch (Exception e)
            {
                Write("音の合成でエラー: " + e.Message);
            }
            finally
            {
                try { if (File.Exists(videoTmpPath)) File.Delete(videoTmpPath); } catch { }
                try { if (audioTmpPath != null && File.Exists(audioTmpPath)) File.Delete(audioTmpPath); } catch { }
                videoTmpPath = null;
                audioTmpPath = null;
            }
        }

        public static void Finish(string reason)
        {
            if (finished) return;
            finished = true;
            try
            {
                if (ffmpegIn != null) { ffmpegIn.Flush(); ffmpegIn.Close(); }
                if (ffmpeg != null) ffmpeg.WaitForExit(60000);
                if (audioStream != null) { audioStream.Flush(); audioStream.Dispose(); audioStream = null; }
                CSound管理.鳴った音を知らせる = null;
                打音を組み立てる();
                Mux();
                Write("書き出し終了 (" + reason + ")。" + frame + " コマ（うち曲名の幕 " + 幕コマ + " コマ）。");
                Write("  → " + OutPath);
            }
            catch (Exception e)
            {
                Write("終了処理でエラー: " + e.Message);
            }
            finally
            {
                ffmpegIn = null;
                if (ffmpeg != null) { ffmpeg.Dispose(); ffmpeg = null; }
                if (sysSurface != null) { sysSurface.Dispose(); sysSurface = null; }
                capturing = false;
            }

            // Config のトグルから使っているときは、次の曲でもまた書き出せるように戻す
            if (!Enabled)
            {
                finished = false;
                sawPlayStage = false;
                frame = 0;
                playFrame = 0;
                幕コマ = 0;
                songStartFrame = -1;
                鳴った音リスト = null;
                OutPath = null;
                TjaPath = null;
                if (log != null) { log.Dispose(); log = null; }
            }
        }

        /// <summary>いま読み込まれている譜面のファイルパス。</summary>
        private static string CurrentTjaPath()
        {
            if (!string.IsNullOrEmpty(TjaPath)) return TjaPath;

            // 曲名の幕（曲読み込み画面）から録るようにしたので、
            // その時点ではまだ TJAPlayer3.DTX が読み込まれていない。
            // 選曲画面が確定した譜面のパスなら幕の最初から分かる。
            try
            {
                if (TJAPlayer3.stage選曲 != null
                    && TJAPlayer3.stage選曲.r確定されたスコア != null)
                {
                    string p = TJAPlayer3.stage選曲.r確定されたスコア.ファイル情報.ファイルの絶対パス;
                    if (!string.IsNullOrEmpty(p)) return p;
                }
            }
            catch { }

            try
            {
                if (TJAPlayer3.DTX != null
                    && !string.IsNullOrEmpty(TJAPlayer3.DTX.strファイル名の絶対パス))
                    return TJAPlayer3.DTX.strファイル名の絶対パス;
            }
            catch { }
            return null;
        }

        private static void OpenLog()
        {
            try
            {
                log = new StreamWriter(Path.ChangeExtension(OutPath, ".log"), false);
                log.AutoFlush = true;
            }
            catch { }
        }

        /// <summary>.tja の WAVE を読んで音源のパスを返す。</summary>
        private static string FindAudio()
        {
            try
            {
                if (string.IsNullOrEmpty(TjaPath) || !File.Exists(TjaPath)) return null;
                string[] lines;
                try
                {
                    lines = File.ReadAllLines(TjaPath, new System.Text.UTF8Encoding(false, true));
                }
                catch
                {
                    lines = File.ReadAllLines(TjaPath, System.Text.Encoding.GetEncoding(932));
                }
                foreach (string line in lines)
                {
                    string t = line.Trim();
                    if (t.StartsWith("#START", StringComparison.OrdinalIgnoreCase)) break;
                    if (t.StartsWith("WAVE:", StringComparison.OrdinalIgnoreCase))
                    {
                        string wave = t.Substring(5).Trim();
                        string p = Path.Combine(Path.GetDirectoryName(TjaPath), wave);
                        return File.Exists(p) ? p : null;
                    }
                }
            }
            catch { }
            return null;
        }

        /// <summary>書き出しモードで落ちたとき、原因をログに残す。</summary>
        public static void LogFatal(Exception e)
        {
            if (!Enabled || e == null) return;
            Write("落ちました: " + e.GetType().Name + ": " + e.Message);
            Write(e.StackTrace ?? "(スタックなし)");
            if (e.InnerException != null)
            {
                Write("  内側: " + e.InnerException.GetType().Name
                    + ": " + e.InnerException.Message);
                Write(e.InnerException.StackTrace ?? "(スタックなし)");
            }
        }

        private static void Write(string s)
        {
            Trace.TraceInformation("[Export] " + s);
            if (log == null) return;
            log.WriteLine(DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture)
                + "  " + s);
        }
    }
}
