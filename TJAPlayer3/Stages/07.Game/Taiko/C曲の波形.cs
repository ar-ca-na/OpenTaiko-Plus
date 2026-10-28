using FDK;
using SlimDX;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Un4seen.Bass;

namespace TJAPlayer3
{
	/// <summary>
	/// 作譜支援（特訓）で、曲の音声波形を音符と同じ縮尺でレーンの下に流す。
	/// 音符の位置と曲の位置（OFFSET）が合っているかを目で確かめるためのもの。
	///
	/// 音符の横位置は「判定位置 + (音符の時刻 − いまの時刻) × BPM × SCROLL × (スクロール速度 + 1) / 502.8594 / 5」
	/// （CStage演奏画面共通 の nバーからの距離dot.Taiko）。波形も同じ式で、その時刻に鳴る音を同じ横位置に描く。
	/// BPM・SCROLL はその時刻の直前の音符（小節線）の値を使う。
	/// </summary>
	internal class C曲の波形 : IDisposable
	{
		/// <summary>波形を 1ms ごとの値にしたもの。高さ（0〜255）と、立ち上がりの強さ（0〜255、色に使う）。</summary>
		private byte[] b高さ;
		private byte[] b立ち上がり;
		private volatile bool b読み終えた;
		/// <summary>譜面の時刻（ms）で、曲が鳴り始める時刻（0x01 のチップ）。</summary>
		private double db曲の開始ms;

		private CancellationTokenSource cts;
		private CDTX dtx対象;
		private List<double> l速さが変わる時刻 = new List<double>();
		private List<double> l速さ = new List<double>();   // BPM × SCROLL

		private CTexture tx白;
		private TransformedColoredTexturedVertex[] v頂点 = new TransformedColoredTexturedVertex[0];
		private float[] f列の高さ = new float[0];
		private float[] f列の立ち上がり = new float[0];

		private static readonly object BASSの鍵 = new object();

		#region [ 読み込み ]
		/// <summary>譜面の曲（0x01 チップの音）を裏で読み、波形を作り始める。前の読み込みは止める。</summary>
		public void t読み込み開始(CDTX dtx)
		{
			this.t止める();
			this.dtx対象 = dtx;
			this.b読み終えた = false;
			this.b高さ = null;
			this.b立ち上がり = null;
			if (dtx == null) return;

			this.t速さの表を作る(dtx);

			string strパス = null;
			foreach (var chip in dtx.listChip)
			{
				if (chip.nチャンネル番号 != 0x01) continue;
				if (dtx.listWAV.TryGetValue(chip.n整数値_内部番号, out var wc) && wc != null)
				{
					string strフォルダ = string.IsNullOrEmpty(dtx.PATH_WAV) ? dtx.strフォルダ名 : dtx.PATH_WAV;
					strパス = strフォルダ + wc.strファイル名;
					this.db曲の開始ms = chip.n発声時刻ms;
				}
				break;
			}
			if (string.IsNullOrEmpty(strパス) || !File.Exists(strパス))
			{
				Trace.TraceInformation("波形: 曲のファイルが見つからないので出しません。({0})", strパス ?? "0x01 のチップなし");
				return;
			}

			var cts = new CancellationTokenSource();
			this.cts = cts;
			Task.Run(() =>
			{
				try
				{
					var sw = Stopwatch.StartNew();
					if (!this.t解析する(strパス, cts.Token)) return;
					if (cts.IsCancellationRequested) return;
					this.b読み終えた = true;
					Trace.TraceInformation("波形: {0} を読みました（{1:0.0} 秒ぶん、{2} ms）。", Path.GetFileName(strパス), this.b高さ.Length / 1000.0, sw.ElapsedMilliseconds);
				}
				catch (Exception e)
				{
					Trace.TraceWarning("波形: 読み込みに失敗しました。{0}", e.Message);
				}
			});
		}

		public void t止める()
		{
			if (this.cts != null)
			{
				this.cts.Cancel();
				this.cts = null;
			}
			this.b読み終えた = false;
			this.dtx対象 = null;
		}

		/// <summary>この譜面の波形を読んでいる（読み終えた）か。F5 で DTX が作り直されたら false。</summary>
		public bool b対象か(CDTX dtx)
		{
			return dtx != null && this.dtx対象 == dtx;
		}

		/// <summary>音符と小節線の BPM × SCROLL を、時刻順に変わり目だけ控える。</summary>
		private void t速さの表を作る(CDTX dtx)
		{
			this.l速さが変わる時刻.Clear();
			this.l速さ.Clear();
			double db前 = double.NaN;
			foreach (var chip in dtx.listChip)
			{
				int ch = chip.nチャンネル番号;
				bool b対象 = ch == 0x50 || (ch >= 0x11 && ch <= 0x18) || ch == 0x1A || ch == 0x1B;
				if (!b対象) continue;
				double v = chip.dbBPM * chip.dbSCROLL;
				if (v == db前) continue;
				this.l速さが変わる時刻.Add(chip.n発声時刻ms);
				this.l速さ.Add(v);
				db前 = v;
			}
		}

		/// <summary>BASS で曲をデコードし、1ms ごとの高さと立ち上がりを作る。</summary>
		private bool t解析する(string strパス, CancellationToken token)
		{
			double[] dbパワー;   // 1ms ごとの平均パワー（左右の平均）
			lock (BASSの鍵)
			{
				bool b自分で初期化した = false;
				int h = Bass.BASS_StreamCreateFile(strパス, 0, 0, BASSFlag.BASS_STREAM_DECODE | BASSFlag.BASS_SAMPLE_FLOAT);
				if (h == 0 && Bass.BASS_ErrorGetCode() == BASSError.BASS_ERROR_INIT)
				{
					// DirectSound で動いているときは BASS が初期化されていない。音の出ないデバイス（0）で読むだけ初期化する。
					if (Bass.BASS_Init(0, 44100, BASSInit.BASS_DEVICE_DEFAULT, IntPtr.Zero))
						b自分で初期化した = true;
					h = Bass.BASS_StreamCreateFile(strパス, 0, 0, BASSFlag.BASS_STREAM_DECODE | BASSFlag.BASS_SAMPLE_FLOAT);
				}
				if (h == 0)
				{
					Trace.TraceWarning("波形: 曲を開けませんでした。({0}, {1})", Path.GetFileName(strパス), Bass.BASS_ErrorGetCode());
					if (b自分で初期化した) Bass.BASS_Free();
					return false;
				}
				try
				{
					var info = Bass.BASS_ChannelGetInfo(h);
					int nch = Math.Max(1, info.chans);
					int freq = Math.Max(1, info.freq);
					long nバイト = Bass.BASS_ChannelGetLength(h);   // バイト数
					long nサンプル = nバイト > 0 ? nバイト / 4 / nch : (long)freq * 600;
					int n区間 = (int)Math.Min(int.MaxValue - 1, nサンプル * 1000 / freq + 2);
					dbパワー = new double[n区間];
					var n数 = new int[n区間];

					float[] buf = new float[65536 - 65536 % nch];
					long n位置 = 0;   // ここまでに読んだサンプル数（1ch あたり）
					while (!token.IsCancellationRequested)
					{
						int n読んだ = Bass.BASS_ChannelGetData(h, buf, buf.Length * 4);
						if (n読んだ <= 0) break;
						int n個 = n読んだ / 4 / nch;
						for (int i = 0; i < n個; i++)
						{
							double s = 0;
							for (int c = 0; c < nch; c++) s += buf[i * nch + c];
							s /= nch;
							long k = (n位置 + i) * 1000 / freq;
							if (k >= n区間) break;
							dbパワー[k] += s * s;
							n数[k]++;
						}
						n位置 += n個;
					}
					int n使う = (int)Math.Min(n区間, n位置 * 1000 / freq + 1);
					Array.Resize(ref dbパワー, n使う);
					for (int k = 0; k < n使う; k++)
						if (n数[k] > 0) dbパワー[k] /= n数[k];
				}
				finally
				{
					Bass.BASS_StreamFree(h);
					if (b自分で初期化した) Bass.BASS_Free();
				}
			}
			if (token.IsCancellationRequested || dbパワー.Length == 0) return false;

			// 8ms の RMS を dB にする。
			int n = dbパワー.Length;
			var db = t移動平均(dbパワー, 8);
			for (int k = 0; k < n; k++) db[k] = 10.0 * Math.Log10(db[k] + 1e-12);

			// 大きいところの目安（99.5 パーセンタイル）
			var 並べ = (double[])db.Clone();
			Array.Sort(並べ);
			double db上 = 並べ[Math.Min(n - 1, (int)(n * 0.995))];

			// ±200ms の平均より大きいぶん＝立ち上がり（打楽器のアタック）
			var db遅い = t移動平均(db, 401);

			var b高さ = new byte[n];
			var b立ち上がり = new byte[n];
			const double db幅 = 24.0;
			for (int k = 0; k < n; k++)
			{
				double 大きさ = Math.Max(0, Math.Min(1, (db[k] - (db上 - db幅)) / db幅));
				double 立ち上がり = Math.Max(0, Math.Min(1, (db[k] - db遅い[k]) / 10.0));
				// 大小のメリハリが出るよう、大きさは 2 乗して、立ち上がりを上乗せする。
				double 高さ = Math.Min(1, 0.7 * 大きさ * 大きさ + 0.8 * 立ち上がり);
				b高さ[k] = (byte)(高さ * 255);
				b立ち上がり[k] = (byte)(立ち上がり * 255);
			}
			this.b高さ = b高さ;
			this.b立ち上がり = b立ち上がり;
			return true;
		}

		private static double[] t移動平均(double[] v, int k)
		{
			int n = v.Length;
			var 累積 = new double[n + 1];
			for (int i = 0; i < n; i++) 累積[i + 1] = 累積[i] + v[i];
			var r = new double[n];
			int 前 = k / 2, 後 = k - 1 - k / 2;
			for (int i = 0; i < n; i++)
			{
				int a = Math.Max(0, i - 前), b = Math.Min(n - 1, i + 後);
				r[i] = (累積[b + 1] - 累積[a]) / (b - a + 1);
			}
			return r;
		}
		#endregion

		#region [ 描画 ]
		/// <param name="x左">描く範囲の左端</param>
		/// <param name="x右">描く範囲の右端（含まない）</param>
		/// <param name="y中心">波形の中心の高さ</param>
		/// <param name="n半分の高さ">いちばん大きい音のときの、中心から上（下）への高さ</param>
		/// <param name="x判定">判定位置（音符の中心がここに来た時刻に鳴る）</param>
		/// <param name="db今の譜面ms">いまの時刻（譜面の ms）</param>
		/// <param name="dbスクロール速度">譜面スクロール速度（音符の式の値）</param>
		public void t描画(int x左, int x右, int y中心, int n半分の高さ, int x判定, double db今の譜面ms, double dbスクロール速度)
		{
			if (!this.b読み終えた || this.b高さ == null || this.l速さ.Count == 0 || x右 <= x左) return;
			if (this.tx白 == null)
			{
				using (var bmp = new Bitmap(4, 4))
				{
					using (var g = Graphics.FromImage(bmp)) g.Clear(Color.White);
					this.tx白 = TJAPlayer3.tテクスチャの生成(bmp, false);
				}
				if (this.tx白 == null) return;
			}

			int nW = x右 - x左;
			if (this.f列の高さ.Length != nW)
			{
				this.f列の高さ = new float[nW];
				this.f列の立ち上がり = new float[nW];
				this.v頂点 = new TransformedColoredTexturedVertex[nW * 6 + 6];
			}
			Array.Clear(this.f列の高さ, 0, nW);
			Array.Clear(this.f列の立ち上がり, 0, nW);

			double k係数 = (dbスクロール速度 + 1.0) / 502.8594 / 5.0;
			var b高さ = this.b高さ;
			var b立ち上がり = this.b立ち上がり;

			// いまの時刻から前（左）と後ろ（右）へ 1ms ずつ進み、各時刻の音を、その時刻の音符が来る横位置の列に入れる。
			for (int 向き = -1; 向き <= 1; 向き += 2)
			{
				for (int i = (向き < 0 ? 1 : 0); i < 20000; i++)
				{
					double t = Math.Floor(db今の譜面ms) + 向き * i;
					double v = this.t速さ(t) * k係数;
					double x = x判定 + (t - db今の譜面ms) * v;
					if (向き > 0 && x >= x右 && v > 0) break;
					if (向き < 0 && x < x左 && v > 0) break;
					int px = (int)Math.Floor(x) - x左;
					if (px < 0 || px >= nW) continue;
					long k = (long)Math.Floor(t - this.db曲の開始ms);
					if (k < 0 || k >= b高さ.Length) continue;
					float h = b高さ[k] / 255f, o = b立ち上がり[k] / 255f;
					if (h > this.f列の高さ[px]) this.f列の高さ[px] = h;
					if (o > this.f列の立ち上がり[px]) this.f列の立ち上がり[px] = o;
				}
			}

			int n頂点 = 0;
			for (int px = 0; px < nW; px++)
			{
				float h = this.f列の高さ[px];
				if (h <= 0.01f) continue;
				float o = (float)Math.Sqrt(this.f列の立ち上がり[px]);   // 色は弱い立ち上がりでも変わるように
				// ふだんは水色、立ち上がりが強いほど白っぽい黄色へ
				int r = (int)(60 + (255 - 60) * o), g = (int)(170 + (240 - 170) * o), b = (int)(255 - (255 - 120) * o);
				int color = Color.FromArgb(235, r, g, b).ToArgb();
				float hh = Math.Max(1f, h * n半分の高さ);
				this.t四角を足す(ref n頂点, x左 + px, y中心 - hh, 1f, hh * 2, color);
			}
			// 判定位置の目印（細い白線）
			this.t四角を足す(ref n頂点, x判定, y中心 - n半分の高さ, 1f, n半分の高さ * 2, Color.FromArgb(140, 255, 255, 255).ToArgb());

			this.tx白.t2D頂点描画(TJAPlayer3.app.Device, this.v頂点, n頂点 / 3);
		}

		/// <summary>色の付いた四角を描く（黒い帯など）。</summary>
		public void t四角を描く(float x, float y, float w, float h, Color color)
		{
			if (this.tx白 == null)
			{
				using (var bmp = new Bitmap(4, 4))
				{
					using (var g = Graphics.FromImage(bmp)) g.Clear(Color.White);
					this.tx白 = TJAPlayer3.tテクスチャの生成(bmp, false);
				}
				if (this.tx白 == null) return;
			}
			var v = new TransformedColoredTexturedVertex[6];
			int n = 0;
			t四角を足す(v, ref n, x, y, w, h, color.ToArgb());
			this.tx白.t2D頂点描画(TJAPlayer3.app.Device, v, 2);
		}

		private void t四角を足す(ref int n, float x, float y, float w, float h, int color)
		{
			if (n + 6 > this.v頂点.Length) Array.Resize(ref this.v頂点, n + 6);
			t四角を足す(this.v頂点, ref n, x, y, w, h, color);
		}

		private static void t四角を足す(TransformedColoredTexturedVertex[] v, ref int n, float x, float y, float w, float h, int color)
		{
			// -0.5 は座標とピクセルのずれの補正（CTexture.t2D描画 と同じ）
			float x0 = x - 0.5f, y0 = y - 0.5f, x1 = x + w - 0.5f, y1 = y + h - 0.5f;
			var uv = new Vector2(0.5f, 0.5f);
			v[n++] = new TransformedColoredTexturedVertex(new Vector4(x0, y0, 1f, 1f), color, uv);
			v[n++] = new TransformedColoredTexturedVertex(new Vector4(x1, y0, 1f, 1f), color, uv);
			v[n++] = new TransformedColoredTexturedVertex(new Vector4(x0, y1, 1f, 1f), color, uv);
			v[n++] = new TransformedColoredTexturedVertex(new Vector4(x1, y0, 1f, 1f), color, uv);
			v[n++] = new TransformedColoredTexturedVertex(new Vector4(x1, y1, 1f, 1f), color, uv);
			v[n++] = new TransformedColoredTexturedVertex(new Vector4(x0, y1, 1f, 1f), color, uv);
		}

		/// <summary>時刻 t（譜面の ms）での BPM × SCROLL。その時刻以前で最後の変わり目の値。</summary>
		private double t速さ(double t)
		{
			var l = this.l速さが変わる時刻;
			int lo = 0, hi = l.Count - 1, ans = 0;
			while (lo <= hi)
			{
				int mid = (lo + hi) / 2;
				if (l[mid] <= t) { ans = mid; lo = mid + 1; }
				else hi = mid - 1;
			}
			return this.l速さ[ans];
		}
		#endregion

		public void Dispose()
		{
			this.t止める();
			TJAPlayer3.tテクスチャの解放(ref this.tx白);
		}
	}
}
