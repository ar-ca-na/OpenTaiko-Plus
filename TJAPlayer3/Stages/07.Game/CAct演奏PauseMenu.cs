using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Drawing;
using System.IO;
using SlimDX;
using FDK;

namespace TJAPlayer3
{
	internal class CAct演奏PauseMenu : CActSelectPopupMenu
	{
		private readonly string QuickCfgTitle = "ポーズ";
		// コンストラクタ

		public CAct演奏PauseMenu()
		{
            CAct演奏PauseMenuMain();
		}

        private void CAct演奏PauseMenuMain()
		{
            this.bEsc有効 = false;
			// この画面に来る度に、メニューを作り直す。オフセットの項目も Config の値から始まるので、
			// 「続ける」で閉じたときの変更はここで捨てられる。
			this.l項目 = MakeListCItemBase();
			base.Initialize( this.l項目, true, QuickCfgTitle, 0 );
		}

		private List<CItemBase> MakeListCItemBase()
		{
			List<CItemBase> l = new List<CItemBase>();

			#region [ 共通 SET切り替え/More/Return ]
			l.Add( this.item続ける = new CSwitchItemList( "続ける(保存しない)", CItemBase.Eパネル種別.通常, 0, "", "", new string[] { "" } ) );
			this.itemやり直し = null;
			if(TJAPlayer3.stage選曲.n確定された曲の難易度[0] != (int)Difficulty.Dan) l.Add( this.itemやり直し = new CSwitchItemList( "やり直し(保存する)", CItemBase.Eパネル種別.通常, 0, "", "", new string[] { "" } ) );
			l.Add( this.item演奏中止 = new CSwitchItemList( "演奏中止", CItemBase.Eパネル種別.通常, 0, "", "", new string[] { "", "" } ) );
			#endregion

			// 全体オフセット（設定画面の GlobalOffset と同じ値・同じ範囲）。
			// ここで変えた値は「やり直し」か「演奏中止」を選んだときだけ Config に入れる。
			l.Add( this.itemオフセット = new CItemInteger( "全体オフセット", -99, 99, TJAPlayer3.ConfigIni.nGlobalOffsetMs ) );

			return l;
		}

		/// <summary>
		/// メニューで変えた全体オフセットを Config に入れる。値が変わったら true。
		/// オフセットは譜面を読むときに足されるので、変わったら譜面を読み直す必要がある。
		/// </summary>
		private bool tオフセットを確定する()
		{
			int n新 = this.itemオフセット.n現在の値;
			if ( n新 == TJAPlayer3.ConfigIni.nGlobalOffsetMs )
				return false;
			Trace.TraceInformation( "ポーズメニューで全体オフセットを変えました。({0} → {1} ms)", TJAPlayer3.ConfigIni.nGlobalOffsetMs, n新 );
			TJAPlayer3.ConfigIni.nGlobalOffsetMs = n新;
			return true;
		}

		// メソッド
		public override void tActivatePopupMenu( E楽器パート einst )
		{
            this.CAct演奏PauseMenuMain();
			CActSelectPopupMenu.b選択した = false;
			this.bやり直しを選択した = false;
			this.bオフセットを変えた = false;
			this.b特訓で止めていた = false;
			base.tActivatePopupMenu( einst );
		}
		//public void tDeativatePopupMenu()
		//{
		//	base.tDeativatePopupMenu();
		//}

		public override void t進行描画sub()
		{
            if( this.bやり直しを選択した )
            {
                if( !sw.IsRunning )
                    this.sw = Stopwatch.StartNew();
                if( sw.ElapsedMilliseconds > 1500 )
                {
                    TJAPlayer3.stage演奏ドラム画面.bPAUSE = false;
                    TJAPlayer3.stage演奏ドラム画面.t演奏やりなおし( this.bオフセットを変えた );
                    this.bオフセットを変えた = false;

	    		    this.tDeativatePopupMenu();
                    this.sw.Reset();
                }
            }
		}

		public override void tEnter押下Main( int nSortOrder )
		{
            // Dan では「やり直し」が無く行がずれるので、行番号ではなく項目そのもので見分ける。
            CItemBase item = ( n現在の選択行 >= 0 && n現在の選択行 < this.l項目.Count ) ? this.l項目[ n現在の選択行 ] : null;
            EOrder eOrder =
                item == this.item続ける ? EOrder.Continue :
                ( item != null && item == this.itemやり直し ) ? EOrder.Redoing :
                item == this.item演奏中止 ? EOrder.Return :
                EOrder.Default;   // 全体オフセット（決定で編集の開始・終了。基底クラスが処理する）

            switch ( eOrder )
            {
				case EOrder.Continue:
                    // 特訓モード（作譜支援）で止めていたときは、止めたままメニューだけ閉じる。
                    // 開いたときに止め直した分（タイマ）だけ戻し、音と動画は触らない。
                    if ( !this.b特訓で止めていた )
                        TJAPlayer3.stage演奏ドラム画面.bPAUSE = false;

                    CSound管理.rc演奏用タイマ.t再開();
					TJAPlayer3.Timer.t再開();
                    if ( !this.b特訓で止めていた )
                    {
					    TJAPlayer3.DTX.t全チップの再生再開();
                        TJAPlayer3.stage演奏ドラム画面.actAVI.tPauseControl();
                    }
					CActSelectPopupMenu.b選択した = true;
					this.tDeativatePopupMenu();
					break;

				case EOrder.Redoing:
					if (TJAPlayer3.stage選曲.n確定された曲の難易度[0] != (int)Difficulty.Dan)
					{
						this.bオフセットを変えた = this.tオフセットを確定する();
						this.bやり直しを選択した = true;
						CActSelectPopupMenu.b選択した = true;
					}
                    else
					{
						CSound管理.rc演奏用タイマ.t再開();
						TJAPlayer3.Timer.t再開();
						TJAPlayer3.stage演奏ドラム画面.t演奏中止();
						CActSelectPopupMenu.b選択した = true;
						this.tDeativatePopupMenu();
					}
					break;

				case EOrder.Return:
					// 選曲画面に戻る。次に譜面を読むときから新しいオフセットが効く。
					this.tオフセットを確定する();
                    CSound管理.rc演奏用タイマ.t再開();
					TJAPlayer3.Timer.t再開();
                    TJAPlayer3.stage演奏ドラム画面.t演奏中止();
					CActSelectPopupMenu.b選択した = true;
					this.tDeativatePopupMenu();
                    break;
                default:
                    break;
            }
		}

		public override void tCancel()
		{
		}

		// CActivity 実装

		public override void On活性化()
		{
			base.On活性化();
			this.bGotoDetailConfig = false;
            this.sw = new Stopwatch();
		}
		public override void On非活性化()
		{
			base.On非活性化();
		}
		public override void OnManagedリソースの作成()
		{
			if( !base.b活性化してない )
			{
				string pathパネル本体 = CSkin.Path( @"Graphics\ScreenSelect popup auto settings.png" );
				if ( File.Exists( pathパネル本体 ) )
				{
					this.txパネル本体 = TJAPlayer3.tテクスチャの生成( pathパネル本体, true );
				}

				base.OnManagedリソースの作成();
			}
		}
		public override void OnManagedリソースの解放()
		{
			if ( !base.b活性化してない )
			{
				TJAPlayer3.tテクスチャの解放( ref this.txパネル本体 );
				TJAPlayer3.tテクスチャの解放( ref this.tx文字列パネル );
				base.OnManagedリソースの解放();
			}
		}

		#region [ private ]
		//-----------------
		private List<CItemBase> l項目;
		private CItemBase item続ける;
		private CItemBase itemやり直し;
		private CItemBase item演奏中止;
		private CItemInteger itemオフセット;
		private bool bオフセットを変えた;
		/// <summary>開いたとき、特訓モード（作譜支援）の Space で止めていたか。開いた側が立てる。</summary>
		public bool b特訓で止めていた;
		private enum EOrder : int
		{
			Continue,
			Redoing,
			Return, END,
			Default = 99
		};

		private bool b選択した;
		private CTexture txパネル本体;
		private CTexture tx文字列パネル;
        private Stopwatch sw;
        private bool bやり直しを選択した;
		//-----------------
		#endregion
	}


}
