using FDK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Drawing;

namespace TJAPlayer3
{
	class CAct演奏Drums特訓モード : CActivity
	{
		public CAct演奏Drums特訓モード()
		{
			base.b活性化してない = true;
		}

		public override void On活性化()
		{
			this.n現在の小節線 = 0;
			this.b特訓PAUSE = false;
			this.b小節線で止まっている = true;
			this.n最終演奏位置ms = 0;

			base.On活性化();

			CDTX dTX = TJAPlayer3.DTX;

			var measureCount = 1;
			var bIsInGoGo = false;

			int endtime = 1;
			int bgmlength = 1;

			for (int index = 0; index < TJAPlayer3.DTX.listChip.Count; index++)
			{
				if (TJAPlayer3.DTX.listChip[index].nチャンネル番号 == 0xff)
				{
					endtime = TJAPlayer3.DTX.listChip[index].n発声時刻ms;
					break;
				}
			}
			for (int index = 0; index < TJAPlayer3.DTX.listChip.Count; index++)
			{
				if (TJAPlayer3.DTX.listChip[index].nチャンネル番号 == 0x01)
				{
					bgmlength = TJAPlayer3.DTX.listChip[index].GetDuration() + TJAPlayer3.DTX.listChip[index].n発声時刻ms;
					break;
				}
			}

			length = Math.Max(endtime, bgmlength);

			gogoXList = new List<int>();
			JumpPointList = new List<STJUMPP>();

			for (int i = 0; i < dTX.listChip.Count; i++)
			{
				CDTX.CChip pChip = dTX.listChip[i];

				if (pChip.n整数値_内部番号 > measureCount && pChip.nチャンネル番号 == 0x50) measureCount = pChip.n整数値_内部番号;

				if (pChip.nチャンネル番号 == 0x9E && !bIsInGoGo)
				{
					bIsInGoGo = true;

					var current = ((double)(pChip.db発声時刻ms * (((double)TJAPlayer3.ConfigIni.n演奏速度) / 20.0)));
					var width = 0;
					if (TJAPlayer3.Tx.Tokkun_ProgressBar != null) width = TJAPlayer3.Tx.Tokkun_ProgressBar.szテクスチャサイズ.Width;

					this.gogoXList.Add((int)(width * (current / length)));
				}
				if (pChip.nチャンネル番号 == 0x9F && bIsInGoGo)
				{
					bIsInGoGo = false;
				}
			}

			this.n小節の総数 = measureCount;

			this.波形.t読み込み開始(TJAPlayer3.DTX);
		}

		public override void On非活性化()
		{
			length = 1;
			this.波形.t止める();
			gogoXList = null;
			JumpPointList = null;
			base.On非活性化();
		}

		public override void OnManagedリソースの作成()
		{
			if (!base.b活性化してない)
			{
				if (TJAPlayer3.Tx.Tokkun_Background_Up != null) this.ct背景スクロールタイマー = new CCounter(1, TJAPlayer3.Tx.Tokkun_Background_Up.szテクスチャサイズ.Width, 16, TJAPlayer3.Timer);
				base.OnManagedリソースの作成();
			}
		}

		public override void OnManagedリソースの解放()
		{
			if (!base.b活性化してない)
			{
				this.ctスクロールカウンター = null;
				this.ct背景スクロールタイマー = null;
				TJAPlayer3.tテクスチャの解放(ref this.txヘルプ案内);
				TJAPlayer3.tテクスチャの解放(ref this.txヘルプ);
				TJAPlayer3.tテクスチャの解放(ref this.tx小節番号の黒);
				this.波形.Dispose();
				base.OnManagedリソースの解放();
			}
		}

		public override int On進行描画()
		{
			if (!base.b活性化してない)
			{
				if (base.b初めての進行描画)
				{
					base.b初めての進行描画 = false;
				}

				TJAPlayer3.act文字コンソール.tPrint(0, 0, C文字コンソール.Eフォント種別.白, "TRAINING MODE (BETA)");

				// F5 で読み直したかどうかを、その場で分かるようにする。
				// 譜面を書き換えながら使うので、効いたかどうかが見えないと困る。
				if (!string.IsNullOrEmpty(CStage演奏画面共通.str読み直しの知らせ))
				{
					if (TJAPlayer3.Timer.n現在時刻 < CStage演奏画面共通.n読み直しの知らせを消す時刻ms)
						TJAPlayer3.act文字コンソール.tPrint(0, 16, C文字コンソール.Eフォント種別.白,
							CStage演奏画面共通.str読み直しの知らせ);
					else
						CStage演奏画面共通.str読み直しの知らせ = null;
				}

				// H キーで操作方法（ヘルプ）を出す・消す。ポーズメニューを出している間は受けない。
				bool bポーズ中 = TJAPlayer3.stage演奏ドラム画面.bポーズメニュー表示中;
				if (!bポーズ中 && TJAPlayer3.Input管理.Keyboard.bキーが押された((int)SlimDXKeys.Key.H))
					this.bヘルプ表示 = !this.bヘルプ表示;
				this.tヘルプ案内を描く();

				// ポーズメニューを出している間は、特訓のキー（Space・←→ など）を受けない。
				// メニューの操作（↑↓・決定）と取り合わないように。
				if (!bポーズ中) {
				bool bSpace = TJAPlayer3.Input管理.Keyboard.bキーが押された((int)SlimDXKeys.Key.Space);
				bool bShift = TJAPlayer3.Input管理.Keyboard.bキーが押されている((int)SlimDXKeys.Key.LeftShift)
					|| TJAPlayer3.Input管理.Keyboard.bキーが押されている((int)SlimDXKeys.Key.RightShift);
				if (bSpace && bShift)
				{
					// Shift+Space: 小節線（いまの小節の頭）へ戻して止める。
					if (!this.b特訓PAUSE)
					{
						TJAPlayer3.Skin.sound特訓停止音.t再生する();
						this.t演奏を停止する(false);
					}
					else if (!this.b小節線で止まっている)
					{
						TJAPlayer3.Skin.sound特訓スクロール音.t再生する();
						this.t譜面の表示位置を合わせる(true);
					}
				}
				else if (bSpace || TJAPlayer3.Pad.b押された(E楽器パート.DRUMS, Eパッド.RRed2P))
				{
					if (this.b特訓PAUSE)
					{
						TJAPlayer3.Skin.sound特訓再生音.t再生する();
						this.t演奏を再開する();
					}
					else
					{
						// その場で止める（再開もその位置から）。
						TJAPlayer3.Skin.sound特訓停止音.t再生する();
						this.t演奏を停止する(true);
					}
				}
				if (TJAPlayer3.Input管理.Keyboard.bキーが押された((int)SlimDXKeys.Key.LeftArrow) || TJAPlayer3.Pad.b押された(E楽器パート.DRUMS, Eパッド.LBlue))
				{
					if (this.b特訓PAUSE)
					{
						if (!this.b小節線で止まっている)
						{
							// 小節の途中で止めているときは、まずいまの小節の頭へ。
							this.t譜面の表示位置を合わせる(true);
							TJAPlayer3.Skin.sound特訓スクロール音.t再生する();
						}
						else if (this.n現在の小節線 > 1)
						{
							this.n現在の小節線--;
							TJAPlayer3.stage演奏ドラム画面.actPlayInfo.NowMeasure[0] = this.n現在の小節線;

							this.t譜面の表示位置を合わせる(true);
							TJAPlayer3.Skin.sound特訓スクロール音.t再生する();
						}
						if (t配列の値interval以下か(ref this.LBlue, CSound管理.rc演奏用タイマ.nシステム時刻ms, TJAPlayer3.ConfigIni.TokkunMashInterval))
						{
							for (int index = this.JumpPointList.Count - 1; index >= 0; index--)
							{
								if (this.JumpPointList[index].Time <= CSound管理.rc演奏用タイマ.n現在時刻ms * (((double)TJAPlayer3.ConfigIni.n演奏速度) / 20.0))
								{
									this.n現在の小節線 = this.JumpPointList[index].Measure;
									TJAPlayer3.stage演奏ドラム画面.actPlayInfo.NowMeasure[0] = this.n現在の小節線;
									TJAPlayer3.Skin.sound特訓スキップ音.t再生する();
									this.t譜面の表示位置を合わせる(false);
									break;
								}
							}
						}
					}
				}
				if (TJAPlayer3.Input管理.Keyboard.bキーが押された((int)SlimDXKeys.Key.PageDown))
				{
					if (this.b特訓PAUSE)
					{
						this.n現在の小節線 -= TJAPlayer3.ConfigIni.TokkunSkipMeasures;
						if (this.n現在の小節線 <= 0)
							this.n現在の小節線 = 1;

						TJAPlayer3.stage演奏ドラム画面.actPlayInfo.NowMeasure[0] = this.n現在の小節線;

						this.t譜面の表示位置を合わせる(true);
						TJAPlayer3.Skin.sound特訓スクロール音.t再生する();
					}
				}
				if (TJAPlayer3.Input管理.Keyboard.bキーが押された((int)SlimDXKeys.Key.RightArrow) || TJAPlayer3.Pad.b押された(E楽器パート.DRUMS, Eパッド.RBlue))
				{
					if (this.b特訓PAUSE)
					{
						if (this.n現在の小節線 < this.n小節の総数)
						{
							this.n現在の小節線++;
							TJAPlayer3.stage演奏ドラム画面.actPlayInfo.NowMeasure[0] = this.n現在の小節線;

							this.t譜面の表示位置を合わせる(true);
							TJAPlayer3.Skin.sound特訓スクロール音.t再生する();
						}
						if (t配列の値interval以下か(ref this.RBlue, CSound管理.rc演奏用タイマ.nシステム時刻ms, TJAPlayer3.ConfigIni.TokkunMashInterval))
						{
							for (int index = 0; index < this.JumpPointList.Count; index++)
							{
								if (this.JumpPointList[index].Time >= CSound管理.rc演奏用タイマ.n現在時刻ms * (((double)TJAPlayer3.ConfigIni.n演奏速度) / 20.0))
								{
									this.n現在の小節線 = this.JumpPointList[index].Measure;
									TJAPlayer3.stage演奏ドラム画面.actPlayInfo.NowMeasure[0] = this.n現在の小節線;
									TJAPlayer3.Skin.soundSkip.t再生する();
									this.t譜面の表示位置を合わせる(false);
									break;
								}
							}
						}

					}
				}
				if (TJAPlayer3.Input管理.Keyboard.bキーが押された((int)SlimDXKeys.Key.PageUp))
				{
					if (this.b特訓PAUSE)
					{
						this.n現在の小節線 += TJAPlayer3.ConfigIni.TokkunSkipMeasures;
						if (this.n現在の小節線 > this.n小節の総数)
							this.n現在の小節線 = this.n小節の総数;

						TJAPlayer3.stage演奏ドラム画面.actPlayInfo.NowMeasure[0] = this.n現在の小節線;

						this.t譜面の表示位置を合わせる(true);
						TJAPlayer3.Skin.sound特訓スクロール音.t再生する();
					}
				}
				if (TJAPlayer3.Pad.b押された(E楽器パート.DRUMS, Eパッド.LBlue2P))
				{
					if (this.b特訓PAUSE)
					{
						if (TJAPlayer3.ConfigIni.n演奏速度 > 6)
						{
							TJAPlayer3.ConfigIni.n演奏速度 = TJAPlayer3.ConfigIni.n演奏速度 - 2;
							this.t譜面の表示位置を合わせる(false);
						}
					}
				}
				if (TJAPlayer3.Pad.b押された(E楽器パート.DRUMS, Eパッド.RBlue2P))
				{
					if (this.b特訓PAUSE)
					{
						if (TJAPlayer3.ConfigIni.n演奏速度 < 399)
						{
							TJAPlayer3.ConfigIni.n演奏速度 = TJAPlayer3.ConfigIni.n演奏速度 + 2;
							this.t譜面の表示位置を合わせる(false);
						}
					}
				}
				if (TJAPlayer3.Input管理.Keyboard.bキーが押された((int)SlimDXKeys.Key.Home))
				{
					if (this.b特訓PAUSE)
					{
						if (this.n現在の小節線 > 1)
						{
							this.n現在の小節線 = 1;
							TJAPlayer3.stage演奏ドラム画面.actPlayInfo.NowMeasure[0] = this.n現在の小節線;

							this.t譜面の表示位置を合わせる(true);
							TJAPlayer3.Skin.sound特訓スクロール音.t再生する();
						}
					}
				}
				if (TJAPlayer3.Input管理.Keyboard.bキーが押された((int)SlimDXKeys.Key.End))
				{
					if (this.b特訓PAUSE)
					{
						if (this.n現在の小節線 < this.n小節の総数)
						{
							this.n現在の小節線 = this.n小節の総数;
							TJAPlayer3.stage演奏ドラム画面.actPlayInfo.NowMeasure[0] = this.n現在の小節線;

							this.t譜面の表示位置を合わせる(true);
							TJAPlayer3.Skin.sound特訓スクロール音.t再生する();
						}
					}
				}
				if (TJAPlayer3.Input管理.Keyboard.bキーが押された((int)SlimDXKeys.Key.A))
					this.t現在の位置にジャンプポイントを設定する();
				} // !bポーズ中

				if (this.bスクロール中)
				{
					CSound管理.rc演奏用タイマ.n現在時刻ms = easing.EaseOut(this.ctスクロールカウンター, (int)this.nスクロール前ms, (int)this.nスクロール後ms, Easing.CalcType.Circular);

					this.ctスクロールカウンター.t進行();

					if ((int)CSound管理.rc演奏用タイマ.n現在時刻ms == (int)this.nスクロール後ms)
					{
						this.bスクロール中 = false;
						CSound管理.rc演奏用タイマ.n現在時刻ms = this.nスクロール後ms;
					}
				}
				if (!this.b特訓PAUSE)
				{
					if (this.n現在の小節線 < TJAPlayer3.stage演奏ドラム画面.actPlayInfo.NowMeasure[0])
					{
						this.n現在の小節線 = TJAPlayer3.stage演奏ドラム画面.actPlayInfo.NowMeasure[0];
					}

					if (CSound管理.rc演奏用タイマ.n現在時刻ms * (((double)TJAPlayer3.ConfigIni.n演奏速度) / 20.0) > this.n最終演奏位置ms)
					{
						this.n最終演奏位置ms = (long)(CSound管理.rc演奏用タイマ.n現在時刻ms * (((double)TJAPlayer3.ConfigIni.n演奏速度) / 20.0));
					}
				}

			}

			var current = (double)(CSound管理.rc演奏用タイマ.n現在時刻ms * (((double)TJAPlayer3.ConfigIni.n演奏速度) / 20.0));
			var percentage = current / length;

			var currentWhite = (double)(this.n最終演奏位置ms);
			var percentageWhite = currentWhite / length;

			if (TJAPlayer3.Tx.Tokkun_ProgressBarWhite != null) TJAPlayer3.Tx.Tokkun_ProgressBarWhite.t2D描画(TJAPlayer3.app.Device, TJAPlayer3.Skin.Game_Training_ProgressBar_XY[0], TJAPlayer3.Skin.Game_Training_ProgressBar_XY[1] + n再生バーを下げる, new Rectangle(1, 1, (int)(TJAPlayer3.Tx.Tokkun_ProgressBarWhite.szテクスチャサイズ.Width * percentageWhite), TJAPlayer3.Tx.Tokkun_ProgressBarWhite.szテクスチャサイズ.Height));
			if (TJAPlayer3.Tx.Tokkun_ProgressBar != null) TJAPlayer3.Tx.Tokkun_ProgressBar.t2D描画(TJAPlayer3.app.Device, TJAPlayer3.Skin.Game_Training_ProgressBar_XY[0], TJAPlayer3.Skin.Game_Training_ProgressBar_XY[1] + n再生バーを下げる, new Rectangle(1, 1, (int)(TJAPlayer3.Tx.Tokkun_ProgressBar.szテクスチャサイズ.Width * percentage), TJAPlayer3.Tx.Tokkun_ProgressBar.szテクスチャサイズ.Height));
			if (TJAPlayer3.Tx.Tokkun_GoGoPoint != null)
			{
				foreach (int xpos in gogoXList)
				{
					TJAPlayer3.Tx.Tokkun_GoGoPoint.t2D描画(TJAPlayer3.app.Device, xpos + TJAPlayer3.Skin.Game_Training_ProgressBar_XY[0] - (TJAPlayer3.Tx.Tokkun_GoGoPoint.szテクスチャサイズ.Width / 2), TJAPlayer3.Skin.Game_Training_GoGoPoint_Y + n再生バーを下げる);
				}
			}

			if (TJAPlayer3.Tx.Tokkun_JumpPoint != null)
			{
				foreach (STJUMPP xpos in JumpPointList)
				{
					var width = 0;
					if (TJAPlayer3.Tx.Tokkun_ProgressBar != null) width = TJAPlayer3.Tx.Tokkun_ProgressBar.szテクスチャサイズ.Width;

					int x = (int)((double)width * ((double)xpos.Time / (double)length));
					TJAPlayer3.Tx.Tokkun_JumpPoint.t2D描画(TJAPlayer3.app.Device, x + TJAPlayer3.Skin.Game_Training_ProgressBar_XY[0] - (TJAPlayer3.Tx.Tokkun_JumpPoint.szテクスチャサイズ.Width / 2), TJAPlayer3.Skin.Game_Training_JumpPoint_Y + n再生バーを下げる);
				}
			}

			// ヘルプは一番上に重ねる（ポーズメニューを出している間は隠す）
			if (this.bヘルプ表示 && !base.b活性化してない && !TJAPlayer3.stage演奏ドラム画面.bポーズメニュー表示中)
				this.tヘルプを描く();

			return base.On進行描画();
		}

		#region [ ヘルプ（H キー） ]
		private bool bヘルプ表示;
		private CTexture txヘルプ案内;
		private CTexture txヘルプ;
		private string strヘルプの中身;

		private string strヘルプのフォント()
		{
			// ポーズメニューと同じ読みやすいゴシック（設定のフォントは筆文字のことがある）
			return CSkin.Path(@"Graphics\ipag.ttf");
		}

		/// <summary>「連打」の下に「H キーでヘルプ」と出す（判定数の表示が連打の段を上げて場所を空けている）。</summary>
		private void tヘルプ案内を描く()
		{
			if (this.txヘルプ案内 == null)
			{
				using (var font = new CPrivateFont(this.strヘルプのフォント(), 14))
				using (var bmp = font.DrawPrivateFont(TJAPlayer3.ConfigIni.bChartAuthoring ? "H キーで操作ヘルプ" : "H キーでヘルプ", Color.White, Color.Black))
					this.txヘルプ案内 = TJAPlayer3.tテクスチャの生成(bmp, false);
			}
			this.txヘルプ案内?.t2D描画(TJAPlayer3.app.Device, 26, 662);
		}

		private List<string[]> lヘルプの行()
		{
			bool b作譜 = TJAPlayer3.ConfigIni.bChartAuthoring;
			var l = new List<string[]>();
			l.Add(new[] { b作譜 ? "作譜支援モードの操作" : "特訓モードの操作", "" });
			l.Add(new[] { "Space", "再生 / その場で停止（再開もその位置から）" });
			l.Add(new[] { "Shift+Space", "いまの小節の頭へ戻して停止" });
			l.Add(new[] { "← →", "停止中に 1 小節ずつ移動（小節の途中なら ← でその小節の頭へ。素早く 2 回でジャンプポイントへ）" });
			l.Add(new[] { "", "　再生中は入力タイミング（いま " + TJAPlayer3.ConfigIni.nInputAdjustTimeMs + "ms）を 10ms ずつ変える（Ctrl で 1ms）" });
			l.Add(new[] { "PgUp / PgDn", "停止中に " + TJAPlayer3.ConfigIni.TokkunSkipMeasures + " 小節ずつ移動（設定の TokkunSkipMeasures）" });
			l.Add(new[] { "Home / End", "停止中に最初 / 最後の小節へ" });
			l.Add(new[] { "A", "いまの位置にジャンプポイントを付ける" });
			if (b作譜) l.Add(new[] { "F5", "tja を読み直す（いまの小節のまま）" });
			l.Add(new[] { b作譜 ? "F6 / F7" : "F6", b作譜 ? "1P / 2P のオートを切り替える" : "オートを切り替える" });
			l.Add(new[] { "F1", "ポーズ（続ける(保存しない)・やり直し(保存する)・全体オフセット）" });
			l.Add(new[] { "H", "このヘルプを閉じる" });
			return l;
		}

		/// <summary>半透明の黒い板に操作の一覧を描いた絵を作り、画面の中央に出す。</summary>
		private void tヘルプを描く()
		{
			var 行 = this.lヘルプの行();
			string str中身 = string.Join("\n", 行.Select(r => r[0] + "\t" + r[1]));
			if (this.txヘルプ == null || this.strヘルプの中身 != str中身)
			{
				TJAPlayer3.tテクスチャの解放(ref this.txヘルプ);
				this.strヘルプの中身 = str中身;

				var bmpキー = new List<Bitmap>();
				var bmp説明 = new List<Bitmap>();
				try
				{
					using (var font = new CPrivateFont(this.strヘルプのフォント(), 16))
					{
						foreach (var r in 行)
						{
							bmpキー.Add(r[0] == "" ? null : font.DrawPrivateFont(r[0], r[1] == "" ? Color.Yellow : Color.FromArgb(255, 200, 120), Color.Black));
							bmp説明.Add(r[1] == "" ? null : font.DrawPrivateFont(r[1], Color.White, Color.Black));
						}
					}
					int n行の高さ = 34, n余白 = 24, nキーの幅 = 170;
					int w = 0;
					for (int i = 0; i < 行.Count; i++)
						w = Math.Max(w, bmp説明[i] == null ? bmpキー[i].Width : nキーの幅 + bmp説明[i].Width);
					w += n余白 * 2;
					int h = n行の高さ * 行.Count + n余白 * 2;
					using (var bmp = new Bitmap(w, h))
					{
						using (var g = Graphics.FromImage(bmp))
						{
							g.Clear(Color.FromArgb(215, 0, 0, 0));
							using (var pen = new Pen(Color.FromArgb(255, 255, 255, 255), 2))
								g.DrawRectangle(pen, 1, 1, w - 3, h - 3);
							for (int i = 0; i < 行.Count; i++)
							{
								int y = n余白 + i * n行の高さ;
								if (bmpキー[i] != null) g.DrawImage(bmpキー[i], n余白, y);
								if (bmp説明[i] != null) g.DrawImage(bmp説明[i], n余白 + nキーの幅, y);
							}
						}
						this.txヘルプ = TJAPlayer3.tテクスチャの生成(bmp, false);
					}
				}
				finally
				{
					foreach (var b in bmpキー) b?.Dispose();
					foreach (var b in bmp説明) b?.Dispose();
				}
			}
			if (this.txヘルプ != null)
			{
				int x = (1280 - (int)this.txヘルプ.szテクスチャサイズ.Width) / 2;
				int y = (720 - (int)this.txヘルプ.szテクスチャサイズ.Height) / 2;
				this.txヘルプ.t2D描画(TJAPlayer3.app.Device, Math.Max(0, x), Math.Max(0, y));
			}
		}
		#endregion

		#region [ レーンまわり（小節番号の黒・音声波形） ]
		/// <summary>進行バー（とゴーゴー・ジャンプポイントの印）を下げる量。空いた所に音声波形を出す。</summary>
		private const int n再生バーを下げる = 44;
		private C曲の波形 波形 = new C曲の波形();
		private CTexture tx小節番号の黒;

		/// <summary>
		/// レーンを描いた直後に呼ぶ。小節番号が紫の背景にはみ出さないよう、レーン上の黒い帯を左へ伸ばす。
		/// レーンの下に黒い帯を広げ、曲の音声波形を音符と同じ縮尺で流す（音符と曲の位置が合っているかを見るため）。
		/// </summary>
		public void On進行描画_レーン下()
		{
			if (base.b活性化してない) return;

			#region [ 小節番号の後ろの黒（レーン上の黒い帯を、レーンの左端まで伸ばす） ]
			if (this.tx小節番号の黒 == null)
			{
				// 左上だけ角を丸めた黒い板（元の黒い帯と同じ見た目）
				using (var bmp = new Bitmap(172, 34))
				{
					using (var g = Graphics.FromImage(bmp))
					{
						g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
						g.Clear(Color.Transparent);
						int r = 14;
						using (var path = new System.Drawing.Drawing2D.GraphicsPath())
						{
							path.AddArc(0, 0, r * 2, r * 2, 180, 90);
							path.AddLine(r, 0, 172, 0);
							path.AddLine(172, 0, 172, 34);
							path.AddLine(172, 34, 0, 34);
							path.CloseFigure();
							using (var br = new SolidBrush(Color.Black)) g.FillPath(br, path);
						}
					}
					this.tx小節番号の黒 = TJAPlayer3.tテクスチャの生成(bmp, false);
				}
			}
			int yレーン = TJAPlayer3.Skin.nScrollFieldY[0];
			this.tx小節番号の黒?.t2D描画(TJAPlayer3.app.Device, TJAPlayer3.Skin.nScrollFieldBGX[0], yレーン - 34);
			#endregion

			#region [ 音声波形 ]
			if (this.波形 != null)
			{
				// F5 で譜面を読み直すと DTX が作り直されるので、そのときは読み直す。
				if (!this.波形.b対象か(TJAPlayer3.DTX)) this.波形.t読み込み開始(TJAPlayer3.DTX);

				int y上 = 352;    // レーンの下の文字（ドン・カッ）の帯の下
				int x左 = TJAPlayer3.Skin.nScrollFieldBGX[0];
				// 下の背景（Down.png）の上端には黒い帯と進行バーの溝が描いてある。その部分を下げた位置に描き直し、空いた所を黒で埋める。
				this.波形.t四角を描く(x左, y上, 1280 - x左, 360 + n再生バーを下げる - y上, Color.Black);
				if (TJAPlayer3.Tx.Tokkun_DownBG != null)
				{
					int n溝まで = Math.Max(0, TJAPlayer3.Skin.Game_Training_ProgressBar_XY[1] - 360) + 32;
					n溝まで = Math.Min(n溝まで, (int)TJAPlayer3.Tx.Tokkun_DownBG.szテクスチャサイズ.Height);
					TJAPlayer3.Tx.Tokkun_DownBG.t2D描画(TJAPlayer3.app.Device, x左, 360 + n再生バーを下げる, new Rectangle(x左, 0, 1280 - x左, n溝まで));
				}

				int y中心 = TJAPlayer3.Skin.Game_Training_ProgressBar_XY[1];
				double db今 = CSound管理.rc演奏用タイマ.n現在時刻ms * (((double)TJAPlayer3.ConfigIni.n演奏速度) / 20.0);
				this.波形.t描画(x左, 1280, y中心, 23, TJAPlayer3.Skin.nScrollFieldX[0], db今, TJAPlayer3.stage演奏ドラム画面.dbいまの譜面スクロール速度);
			}
			#endregion
		}
		#endregion

		public int On進行描画_背景()
		{
			if (this.ct背景スクロールタイマー != null)
			{
				this.ct背景スクロールタイマー.t進行Loop();

				double TexSize = 1280 / TJAPlayer3.Tx.Tokkun_Background_Up.szテクスチャサイズ.Width;
				// 1280をテクスチャサイズで割ったものを切り上げて、プラス+1足す。
				int ForLoop = (int)Math.Ceiling(TexSize) + 1;
				TJAPlayer3.Tx.Tokkun_Background_Up.t2D描画(TJAPlayer3.app.Device, 0 - this.ct背景スクロールタイマー.n現在の値, TJAPlayer3.Skin.Background_Scroll_Y[0]);
				for (int l = 1; l < ForLoop + 1; l++)
				{
					TJAPlayer3.Tx.Tokkun_Background_Up.t2D描画(TJAPlayer3.app.Device, +(l * TJAPlayer3.Tx.Tokkun_Background_Up.szテクスチャサイズ.Width) - this.ct背景スクロールタイマー.n現在の値, TJAPlayer3.Skin.Background_Scroll_Y[0]);
				}
			}

			if (TJAPlayer3.Tx.Tokkun_DownBG != null) TJAPlayer3.Tx.Tokkun_DownBG.t2D描画(TJAPlayer3.app.Device, 0, 360);
			if (TJAPlayer3.Tx.Tokkun_BigTaiko != null) TJAPlayer3.Tx.Tokkun_BigTaiko.t2D描画(TJAPlayer3.app.Device, 334, 400);

			return base.On進行描画();
		}

		public void On進行描画_小節_速度()
		{
			if (TJAPlayer3.Tx.Tokkun_Speed_Measure != null)
				TJAPlayer3.Tx.Tokkun_Speed_Measure.t2D描画(TJAPlayer3.app.Device, 0, 360);
			var maxMeasureStr = this.n小節の総数.ToString();
			var measureStr = TJAPlayer3.stage演奏ドラム画面.actPlayInfo.NowMeasure[0].ToString();
			if (TJAPlayer3.Tx.Tokkun_SmallNumber != null)
			{
				var x = TJAPlayer3.Skin.Game_Training_MaxMeasureCount_XY[0];
				foreach (char c in maxMeasureStr)
				{
					var currentNum = int.Parse(c.ToString());
					TJAPlayer3.Tx.Tokkun_SmallNumber.t2D描画(TJAPlayer3.app.Device, x, TJAPlayer3.Skin.Game_Training_MaxMeasureCount_XY[1], new Rectangle(TJAPlayer3.Skin.Game_Training_SmallNumber_Width * currentNum, 0, TJAPlayer3.Skin.Game_Training_SmallNumber_Width, TJAPlayer3.Tx.Tokkun_SmallNumber.szテクスチャサイズ.Height));
					x += TJAPlayer3.Skin.Game_Training_SmallNumber_Width - 2;
				}
			}

			var subtractVal = (TJAPlayer3.Skin.Game_Training_BigNumber_Width - 2) * (measureStr.Length - 1);

			if (TJAPlayer3.Tx.Tokkun_BigNumber != null)
			{
				var x = TJAPlayer3.Skin.Game_Training_CurrentMeasureCount_XY[0];
				foreach (char c in measureStr)
				{
					var currentNum = int.Parse(c.ToString());
					TJAPlayer3.Tx.Tokkun_BigNumber.t2D描画(TJAPlayer3.app.Device, x - subtractVal, TJAPlayer3.Skin.Game_Training_CurrentMeasureCount_XY[1], new Rectangle(TJAPlayer3.Skin.Game_Training_BigNumber_Width * currentNum, 0, TJAPlayer3.Skin.Game_Training_BigNumber_Width, TJAPlayer3.Tx.Tokkun_BigNumber.szテクスチャサイズ.Height));
					x += TJAPlayer3.Skin.Game_Training_BigNumber_Width - 2;
				}

				var PlaySpdtmp = TJAPlayer3.ConfigIni.n演奏速度 / 20.0d * 10.0d;
				PlaySpdtmp = Math.Round(PlaySpdtmp, MidpointRounding.AwayFromZero);

				var playSpd = PlaySpdtmp / 10.0d;
				var playSpdI = playSpd - (int)playSpd;
				var playSpdStr = Decimal.Round((decimal)playSpdI, 1, MidpointRounding.AwayFromZero).ToString();
				var decimalStr = (playSpdStr == "0") ? "0" : playSpdStr[2].ToString();

				TJAPlayer3.Tx.Tokkun_BigNumber.t2D描画(TJAPlayer3.app.Device, TJAPlayer3.Skin.Game_Training_SpeedDisplay_XY[0], TJAPlayer3.Skin.Game_Training_SpeedDisplay_XY[1], new Rectangle(TJAPlayer3.Skin.Game_Training_BigNumber_Width * int.Parse(decimalStr), 0, TJAPlayer3.Skin.Game_Training_BigNumber_Width, TJAPlayer3.Tx.Tokkun_BigNumber.szテクスチャサイズ.Height));

				x = TJAPlayer3.Skin.Game_Training_SpeedDisplay_XY[0] - 25;

				subtractVal = TJAPlayer3.Skin.Game_Training_BigNumber_Width * (((int)playSpd).ToString().Length - 1);

				foreach (char c in ((int)playSpd).ToString())
				{
					var currentNum = int.Parse(c.ToString());
					TJAPlayer3.Tx.Tokkun_BigNumber.t2D描画(TJAPlayer3.app.Device, x - subtractVal, TJAPlayer3.Skin.Game_Training_SpeedDisplay_XY[1], new Rectangle(TJAPlayer3.Skin.Game_Training_BigNumber_Width * currentNum, 0, TJAPlayer3.Skin.Game_Training_BigNumber_Width, TJAPlayer3.Tx.Tokkun_BigNumber.szテクスチャサイズ.Height));
					x += TJAPlayer3.Skin.Game_Training_BigNumber_Width - 2;
				}
			}
		}

		/// <param name="bその場で">
		/// true ならいまの時刻のまま止める（再開もそこから）。false なら本家どおり、いまの小節の頭へ戻して止める。
		/// </param>
		public void t演奏を停止する(bool bその場で = false)
		{
			CDTX dTX = TJAPlayer3.DTX;

			this.nスクロール後ms = CSound管理.rc演奏用タイマ.n現在時刻ms;

			TJAPlayer3.stage演奏ドラム画面.On活性化();
			CSound管理.rc演奏用タイマ.t一時停止();

			for (int i = 0; i < dTX.listChip.Count; i++)
			{
				CDTX.CChip pChip = dTX.listChip[i];
				pChip.bHit = false;
				if (dTX.listChip[i].nチャンネル番号 != 0x50)
				{
					pChip.bShow = true;
					pChip.b可視 = true;
				}
			}

			TJAPlayer3.DTX.t全チップの再生一時停止();
			TJAPlayer3.stage演奏ドラム画面.bPAUSE = true;
			TJAPlayer3.stage演奏ドラム画面.actPlayInfo.NowMeasure[0] = this.n現在の小節線;
			this.b特訓PAUSE = true;

			if (bその場で)
			{
				// 時刻は動かさない。描き始めのチップだけ、いまの小節の頭に合わせる。
				this.tトップChipを小節線に合わせる();
				this.b小節線で止まっている = false;
			}
			else
				this.t譜面の表示位置を合わせる(false);
		}

		/// <summary>
		/// 止めたまま、指定の時刻（演奏用タイマの ms）へ合わせる。F5 で読み直したあと、小節の途中で止めていた位置へ戻すのに使う。
		/// </summary>
		public void t止めた時刻へ戻す(long n時刻ms)
		{
			if (!this.b特訓PAUSE) return;
			this.bスクロール中 = false;
			CSound管理.rc演奏用タイマ.n現在時刻ms = n時刻ms;
			this.nスクロール後ms = n時刻ms;
			this.tトップChipを小節線に合わせる();
			this.b小節線で止まっている = false;
		}

		/// <summary>
		/// ポーズメニューの「やり直し」で頭から始め直したときに、止めていた状態を解く。
		/// （演奏画面の On活性化 は活性化済みだと子を初期化しないので、ここで合わせる。ジャンプポイントは残す）
		/// </summary>
		public void tやり直しに合わせる()
		{
			this.b特訓PAUSE = false;
			this.b小節線で止まっている = true;
			this.bスクロール中 = false;
			this.n現在の小節線 = 0;
			this.n最終演奏位置ms = 0;
		}

		public void t演奏を再開する()
		{
			CDTX dTX = TJAPlayer3.DTX;

			this.bスクロール中 = false;
			CSound管理.rc演奏用タイマ.n現在時刻ms = this.nスクロール後ms;

			int n演奏開始Chip = TJAPlayer3.stage演奏ドラム画面.n現在のトップChip;
			int finalStartBar;

			if (!this.b小節線で止まっている)
			{
				// 小節の途中で止めていた: 止めた時刻そのものから流す。
				// その時刻より前の音符は消して（叩いた扱い）、後ろの音符から判定する。
				long n止めた時刻ms = this.nスクロール後ms;
				double db譜面の時刻 = n止めた時刻ms * (((double)TJAPlayer3.ConfigIni.n演奏速度) / 20.0);
				n演奏開始Chip = dTX.listChip.Count;
				for (int i = 0; i < dTX.listChip.Count; i++)
				{
					if (dTX.listChip[i].n発声時刻ms >= db譜面の時刻) { n演奏開始Chip = i; break; }
				}
				finalStartBar = Math.Max(0, this.n現在の小節線);
				TJAPlayer3.stage演奏ドラム画面.t演奏位置の変更(finalStartBar, 0, n止めた時刻ms);
			}
			else
			{
				// 特訓モードは「少し戻ってから」再開する作りだった。
				// 戻したぶんの小節はノーツを消してから流すので、
				// 譜面を作っているときは「無音の空白が2小節入った」ように見える。
				// 作譜支援モードのときは止めた場所そのものから再開する。
				finalStartBar = this.n現在の小節線 - (TJAPlayer3.ConfigIni.bChartAuthoring ? 0 : 2);
				if (finalStartBar < 0) finalStartBar = 0;

				TJAPlayer3.stage演奏ドラム画面.t演奏位置の変更(finalStartBar, 0);
			}


			int n少し戻ってから演奏開始Chip = TJAPlayer3.stage演奏ドラム画面.n現在のトップChip;

			TJAPlayer3.stage演奏ドラム画面.actPlayInfo.NowMeasure[0] = 0;
			TJAPlayer3.stage演奏ドラム画面.t数値の初期化(true, true);
			TJAPlayer3.stage演奏ドラム画面.On活性化();

			for (int i = 0; i < dTX.listChip.Count; i++)
			{
				if (i < n演奏開始Chip && (dTX.listChip[i].nチャンネル番号 > 0x10 && dTX.listChip[i].nチャンネル番号 < 0x20)) //2020.07.08 ノーツだけ消す。 null参照回避のために順番変更
				{
					dTX.listChip[i].bHit = true;
					dTX.listChip[i].IsHitted = true;
					dTX.listChip[i].b可視 = false;
					dTX.listChip[i].bShow = false;
				}
				if (i < n少し戻ってから演奏開始Chip && dTX.listChip[i].nチャンネル番号 == 0x01)
				{
					dTX.listChip[i].bHit = true;
					dTX.listChip[i].IsHitted = true;
					dTX.listChip[i].b可視 = false;
					dTX.listChip[i].bShow = false;
				}
				if (dTX.listChip[i].nチャンネル番号 == 0x50 && dTX.listChip[i].n整数値_内部番号 < finalStartBar)
				{
					dTX.listChip[i].bHit = true;
					dTX.listChip[i].IsHitted = true;
				}

			}

			for (int i = 0; i < TJAPlayer3.ConfigIni.nPlayerCount; i++)
			{
				TJAPlayer3.stage演奏ドラム画面.chip現在処理中の連打チップ[i] = null;
			}

			this.b特訓PAUSE = false;
		}

		public void t譜面の表示位置を合わせる(bool doScroll)
		{
			this.nスクロール前ms = CSound管理.rc演奏用タイマ.n現在時刻ms;
			this.b小節線で止まっている = true;

			CDTX dTX = TJAPlayer3.DTX;

			this.tトップChipを小節線に合わせる();

			if (doScroll)
			{
				this.nスクロール後ms = (long)(dTX.listChip[TJAPlayer3.stage演奏ドラム画面.n現在のトップChip].n発声時刻ms / (((double)TJAPlayer3.ConfigIni.n演奏速度) / 20.0));
				this.bスクロール中 = true;

				this.ctスクロールカウンター = new CCounter(0, TJAPlayer3.Skin.Game_Training_ScrollTime, 1, TJAPlayer3.Timer);
			}
			else
			{
				CSound管理.rc演奏用タイマ.n現在時刻ms = (long)(dTX.listChip[TJAPlayer3.stage演奏ドラム画面.n現在のトップChip].n発声時刻ms / (((double)TJAPlayer3.ConfigIni.n演奏速度) / 20.0));
				this.nスクロール後ms = CSound管理.rc演奏用タイマ.n現在時刻ms;
			}
		}

		/// <summary>描き始めのチップ（n現在のトップChip）を、いまの小節線（n現在の小節線）のチップに合わせる。時刻は変えない。</summary>
		private void tトップChipを小節線に合わせる()
		{
			CDTX dTX = TJAPlayer3.DTX;

			bool bSuccessSeek = false;
			for (int i = 0; i < dTX.listChip.Count; i++)
			{
				CDTX.CChip pChip = dTX.listChip[i];

				if (pChip.nチャンネル番号 == 0x50 && pChip.n整数値_内部番号 > n現在の小節線 - 1)
				{
					bSuccessSeek = true;
					TJAPlayer3.stage演奏ドラム画面.n現在のトップChip = i;
					break;
				}
			}
			if (!bSuccessSeek)
			{
				TJAPlayer3.stage演奏ドラム画面.n現在のトップChip = 0;
			}
			else
			{
				// （本家は「!= 0」を後で見ていて、0 のとき [-1] を読んでいた。順番を入れ替えた）
				while (TJAPlayer3.stage演奏ドラム画面.n現在のトップChip != 0 && dTX.listChip[TJAPlayer3.stage演奏ドラム画面.n現在のトップChip].n発声時刻ms == dTX.listChip[TJAPlayer3.stage演奏ドラム画面.n現在のトップChip - 1].n発声時刻ms)
					TJAPlayer3.stage演奏ドラム画面.n現在のトップChip--;
			}
		}

		public void t現在の位置にジャンプポイントを設定する()
		{
			if (!this.bスクロール中 && this.b特訓PAUSE)
			{
				if (!JumpPointList.Contains(new STJUMPP() { Time = (long)(CSound管理.rc演奏用タイマ.n現在時刻ms * (((double)TJAPlayer3.ConfigIni.n演奏速度) / 20.0)), Measure = this.n現在の小節線 }))
					JumpPointList.Add(new STJUMPP() { Time = (long)(CSound管理.rc演奏用タイマ.n現在時刻ms * (((double)TJAPlayer3.ConfigIni.n演奏速度) / 20.0)), Measure = this.n現在の小節線 });
				TJAPlayer3.Skin.sound特訓ジャンプポイント.t再生する();
				JumpPointList.Sort((a, b) => a.Time.CompareTo(b.Time));
			}
		}

		private bool t配列の値interval以下か(ref long[] array, long num, int interval)
		{
			long[] arraytmp = array;
			for (int index = 0; index < (array.Length - 1); index++)
			{
				array[index] = array[index + 1];
			}
			array[array.Length - 1] = num;
			return Math.Abs(num - arraytmp[0]) <= interval;
		}

		public int n現在の小節線;
		public int n小節の総数;
		/// <summary>いま停止中か。譜面を読み直すときに、その状態を保つために見る。</summary>
		public bool b停止中 { get { return this.b特訓PAUSE; } }

		#region [private]
		private long nスクロール前ms;
		private long nスクロール後ms;
		private long n最終演奏位置ms;

		private bool b特訓PAUSE;
		private bool bスクロール中;
		/// <summary>止めている位置が小節線（小節の頭）か。false なら小節の途中で止めていて、再開もそこから。</summary>
		public bool b小節線で止まっている { get; private set; } = true;

		private CCounter ctスクロールカウンター;
		private CCounter ct背景スクロールタイマー;
		private Easing easing = new Easing();
		private long length = 1;

		private List<int> gogoXList;
		private List<STJUMPP> JumpPointList;
		private long[] LBlue = new long[] { 0, 0, 0, 0, 0 };
		private long[] RBlue = new long[] { 0, 0, 0, 0, 0 };

		private struct STJUMPP
		{
			public long Time;
			public int Measure;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="time">今の時間</param>
		/// <param name="begin">最初の値</param>
		/// <param name="change">最終の値-最初の値</param>
		/// <param name="duration">全体の時間</param>
		/// <returns></returns>
		private int EasingCircular(int time, int begin, int change, int duration)
		{
			double t = time, b = begin, c = change, d = duration;

			t = t / d * 2;
			if (t < 1)
				return (int)(-c / 2 * (Math.Sqrt(1 - t * t) - 1) + b);
			else
			{
				t = t - 2;
				return (int)(c / 2 * (Math.Sqrt(1 - t * t) + 1) + b);
			}
		}

		#endregion
	}
}

