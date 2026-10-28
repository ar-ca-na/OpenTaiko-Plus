using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using FDK;

namespace TJAPlayer3
{
    internal class CActPlayOption : CActivity
    {
        public CActPlayOption()
        {
            base.b活性化してない = true;
        }

        public override void On活性化()
        {
            if (this.b活性化してる)
                return;

            ctOpen = new CCounter();
            ctClose = new CCounter();

            for (int i = 0; i < OptionType.Length; i++)
                OptionType[i] = new CTexture();

            #region [ Speed ]

            txSpeed[0] = OptionTypeTx("0.5", Color.White, Color.Black);
            txSpeed[1] = OptionTypeTx("1.0", Color.White, Color.Black);
            txSpeed[2] = OptionTypeTx("1.1", Color.White, Color.Black);
            txSpeed[3] = OptionTypeTx("1.2", Color.White, Color.Black);
            txSpeed[4] = OptionTypeTx("1.3", Color.White, Color.Black);
            txSpeed[5] = OptionTypeTx("1.4", Color.White, Color.Black);
            txSpeed[6] = OptionTypeTx("1.5", Color.White, Color.Black);
            txSpeed[7] = OptionTypeTx("1.6", Color.White, Color.Black);
            txSpeed[8] = OptionTypeTx("1.7", Color.White, Color.Black);
            txSpeed[9] = OptionTypeTx("1.8", Color.White, Color.Black);
            txSpeed[10] = OptionTypeTx("1.9", Color.White, Color.Black);
            txSpeed[11] = OptionTypeTx("2.0", Color.White, Color.Black);
            txSpeed[12] = OptionTypeTx("2.5", Color.White, Color.Black);
            txSpeed[13] = OptionTypeTx("3.0", Color.White, Color.Black);
            txSpeed[14] = OptionTypeTx("3.5", Color.White, Color.Black);
            txSpeed[15] = OptionTypeTx("4.0", Color.White, Color.Black);

            #endregion

            txSwitch[0] = OptionTypeTx("しない", Color.White, Color.Black);
            txSwitch[1] = OptionTypeTx("する", Color.White, Color.Black);

            txRandom[0] = OptionTypeTx("なし", Color.White, Color.Black);
            txRandom[1] = OptionTypeTx("きまぐれ", Color.White, Color.Black);
            txRandom[2] = OptionTypeTx("でたらめ", Color.White, Color.Black);

            txGameMode[0] = OptionTypeTx("なし", Color.White, Color.Black);
            txGameMode[1] = OptionTypeTx("特訓モード", Color.White, Color.Black);
            txGameMode[2] = OptionTypeTx("作譜支援", Color.White, Color.Black);

            txNone = OptionTypeTx("使用不可" ,Color.White, Color.Black);

            OptionType[0] = OptionTypeTx("はやさ", Color.White, Color.Black);
            OptionType[1] = OptionTypeTx("ドロン", Color.White, Color.Black);
            OptionType[2] = OptionTypeTx("あべこべ", Color.White, Color.Black);
            OptionType[3] = OptionTypeTx("ランダム", Color.White, Color.Black);
            OptionType[4] = OptionTypeTx("ゲームモード", Color.White, Color.Black);
            OptionType[5] = OptionTypeTx("オート", Color.White, Color.Black);
            OptionType[6] = OptionTypeTx("ボイス", Color.White, Color.Black);
            OptionType[7] = OptionTypeTx("音色", Color.White, Color.Black);

            for (int i = 0; i < OptionType.Length; i++)
                OptionType[i].vc拡大縮小倍率.X = 0.96f;

            base.On活性化();
        }

        public override void On非活性化()
        {
            TJAPlayer3.tテクスチャの解放(ref txマウス矢印[0]);
            TJAPlayer3.tテクスチャの解放(ref txマウス矢印[1]);

            base.On非活性化();
        }
        public override void OnManagedリソースの作成()
        {
            base.OnManagedリソースの作成();
        }
        public override void OnManagedリソースの解放()
        {
            if (this.b活性化してない)
                return;
            base.OnManagedリソースの解放();
        }

        public int On進行描画(int player)
        {
            if (this.b活性化してない)
                return 0;

            ctOpen.t進行();
            ctClose.t進行();

            if (!ctOpen.b進行中)
            {
                ctOpen.t開始(0, 50, 6, TJAPlayer3.Timer);
                // 開くたびに、いまの設定（作譜支援は Config に残る）をゲームモードの表示に写す。
                nGameMode = TJAPlayer3.ConfigIni.bChartAuthoring ? 2 : (TJAPlayer3.ConfigIni.bTokkunMode ? 1 : 0);
                this.bマウスで値を変える = false;
                // 開く前に溜まっていたクリックで、いきなり項目が進まないように捨てる。
                if (player == (TJAPlayer3.bマウスは2P側() ? 1 : 0))
                    this.tマウスを捨てる();
            }

            var act難易度 = TJAPlayer3.stage選曲.act難易度選択画面;

            #region [ Open & Close ]

            float oy1 = ctOpen.n現在の値 * 18;
            float oy2 = (ctOpen.n現在の値 - 30) * 4;
            float oy3 = ctOpen.n現在の値 < 30 ? 410 - oy1 : -80 + oy2;

            float cy1 = ctClose.n現在の値 * 3;
            float cy2 = (ctClose.n現在の値 - 20) * 16;
            float cy3 = ctClose.n現在の値 < 20 ? 0 - cy1 : 20 + cy2;

            float y = oy3 + cy3;

            #endregion

            TJAPlayer3.Tx.Difficulty_Option.t2D描画(TJAPlayer3.app.Device, 0, y);

            TJAPlayer3.Tx.Difficulty_Option_Select.t2D描画(TJAPlayer3.app.Device, 0, y + NowCount * 40.7f);


            for (int i = 0; i < OptionType.Length; i++)
            {
                OptionType[i].t2D描画(TJAPlayer3.app.Device, 16, 379 + i * 40.8f + y);
            }

            txSpeed[nSpeedCount].t2D拡大率考慮描画(TJAPlayer3.app.Device, CTexture.RefPnt.Up, 200, 375 + y);
            txSwitch[nStealth].t2D拡大率考慮描画(TJAPlayer3.app.Device, CTexture.RefPnt.Up, 200, 375 + y + 1 * 40.7f);
            txSwitch[nAbekobe].t2D拡大率考慮描画(TJAPlayer3.app.Device, CTexture.RefPnt.Up, 200, 375 + y + 2 * 40.7f);
            txRandom[nRandom].t2D拡大率考慮描画(TJAPlayer3.app.Device, CTexture.RefPnt.Up, 200, 375 + y + 3 * 40.7f);
            txGameMode[nGameMode].t2D拡大率考慮描画(TJAPlayer3.app.Device, CTexture.RefPnt.Up, 200, 375 + y + 4 * 40.7f);
            txSwitch[nAutoMode].t2D拡大率考慮描画(TJAPlayer3.app.Device, CTexture.RefPnt.Up, 200, 375 + y + 5 * 40.7f);

            for (int i = 6; i < 8; i++)
            {
                txNone.t2D拡大率考慮描画(TJAPlayer3.app.Device, CTexture.RefPnt.Up, 200, 375 + y + i * 40.7f);
            }

            // マウスで値を変えている間は、値の左右に ◀ ▶ を点滅させて出す。
            if (this.bマウスで値を変える && !bEnd)
            {
                if (txマウス矢印[0] == null)
                {
                    txマウス矢印[0] = OptionTypeTx("◀", Color.FromArgb(225, 30, 30), Color.White);
                    txマウス矢印[1] = OptionTypeTx("▶", Color.FromArgb(225, 30, 30), Color.White);
                }
                if ((Environment.TickCount / 300) % 2 == 0)
                {
                    txマウス矢印[0].t2D拡大率考慮描画(TJAPlayer3.app.Device, CTexture.RefPnt.Up, 200 - 66, 375 + y + NowCount * 40.7f);
                    txマウス矢印[1].t2D拡大率考慮描画(TJAPlayer3.app.Device, CTexture.RefPnt.Up, 200 + 63, 375 + y + NowCount * 40.7f);
                }
            }

            if (ctClose.n現在の値 >= 50)
            {
                Decision();
                NowCount = 0;
                ctOpen.t停止();
                ctOpen.n現在の値 = 0;
                ctClose.t停止();
                ctClose.n現在の値 = 0;
                bEnd = false;
                act難易度.bOption[player] = false;
            }

            #region [ Key ]

            if (!ctClose.b進行中)
            {
                #region [ マウス操作（Shift を押していると 2P 側） ]
                // ふだん: ホイール＝項目を移る、左クリック＝その項目の値を変え始める（最後の 2 つは閉じる）、右クリック＝閉じる。
                // 値を変えている間: ホイール＝値を変える、左／右クリック＝決めて項目選びへ戻る。
                int nマウス段数 = 0;
                bool bマウス左 = false;
                bool bマウス右 = false;
                if (player == (TJAPlayer3.bマウスは2P側() ? 1 : 0))
                {
                    nマウス段数 = TJAPlayer3.tマウスホイールの段数を取り出す();
                    bマウス左 = TJAPlayer3.tマウス左クリックを取り出す();
                    bマウス右 = TJAPlayer3.tマウス右クリックを取り出す();
                }
                bool bマウスで閉じる = false;
                if (!bEnd && ctOpen.n現在の値 >= ctOpen.n終了値)
                {
                    if (this.bマウスで値を変える)
                    {
                        // ホイールを上へ回すと値が増える（ポーズメニューの数値と同じ向き）
                        for (int i = 0; i < nマウス段数; i++) { OptionSelect(false); TJAPlayer3.Skin.sound変更音.t再生する(); }
                        for (int i = 0; i > nマウス段数; i--) { OptionSelect(true); TJAPlayer3.Skin.sound変更音.t再生する(); }
                        if (bマウス左 || bマウス右)
                        {
                            TJAPlayer3.Skin.sound決定音.t再生する();
                            this.bマウスで値を変える = false;
                        }
                    }
                    else
                    {
                        // ホイールを上へ回すと上の項目へ
                        int n移動先 = Math.Max(0, Math.Min(7, NowCount - nマウス段数));
                        if (n移動先 != NowCount)
                        {
                            NowCount = n移動先;
                            TJAPlayer3.Skin.sound変更音.t再生する();
                        }
                        if (bマウス左)
                        {
                            if (NowCount <= 5)
                            {
                                TJAPlayer3.Skin.sound決定音.t再生する();
                                this.bマウスで値を変える = true;
                            }
                            else bマウスで閉じる = true;    // ボイス・音色は変えられないので、押したら閉じる
                        }
                        if (bマウス右) bマウスで閉じる = true;
                    }
                }
                if (bマウスで閉じる)
                {
                    TJAPlayer3.Skin.sound決定音.t再生する();
                    bEnd = true;
                    ctClose.t開始(0, 50, 6, TJAPlayer3.Timer);
                }
                #endregion

                if (TJAPlayer3.Pad.b押された(E楽器パート.DRUMS, Eパッド.LBlue)) { OptionSelect(true); TJAPlayer3.Skin.sound変更音.t再生する(); };
                if (TJAPlayer3.Pad.b押された(E楽器パート.DRUMS, Eパッド.RBlue)) { OptionSelect(false); TJAPlayer3.Skin.sound変更音.t再生する(); };

                if ((TJAPlayer3.Pad.b押された(E楽器パート.DRUMS, Eパッド.LRed) || TJAPlayer3.Pad.b押された(E楽器パート.DRUMS, Eパッド.RRed)) && ctOpen.n現在の値 >= ctOpen.n終了値)
                {
                    this.bマウスで値を変える = false;
                    TJAPlayer3.Skin.sound決定音.t再生する();
                    if (NowCount < 7)
                    {
                        NowCount++;
                    }
                    else if (NowCount >= 7 && !bEnd)
                    {
                        bEnd = true;
                        ctClose.t開始(0, 50, 6, TJAPlayer3.Timer);
                    }
                }
            }

           
            #endregion
            return 0;
        }


        public CCounter ctOpen;
        public CCounter ctClose;
        public CTexture[] OptionType = new CTexture[8];

        public int NowCount;
        public int[] NowCountType = new int[8];

        public bool bEnd;

        public CTexture[] txSpeed = new CTexture[16];
        public int nSpeedCount = 1;

        public int nStealth = 0;
        public int nAbekobe = 0;

        public CTexture[] txRandom = new CTexture[3];
        public int nRandom = 0;

        public CTexture[] txGameMode = new CTexture[3];
        private CTexture[] txマウス矢印 = new CTexture[2];
        /// <summary>マウスの左クリックで「値を変える」状態に入っているか（ホイールが値を変える）。</summary>
        private bool bマウスで値を変える;
        public int nGameMode;

        public CTexture[] txAutoMode = new CTexture[2];
        public int nAutoMode = 0;
        public CTexture txNone = new CTexture();

        public CTexture[] txSwitch = new CTexture[2];

        public CTexture OptionTypeTx(string str文字, Color forecolor, Color backcolor)
        {
            using (var bmp = new CPrivateFastFont(CFontResolver.Family(TJAPlayer3.ConfigIni.FontName), 13).DrawPrivateFont(str文字, forecolor, backcolor))
            {
                return TJAPlayer3.tテクスチャの生成(bmp);
            }
        }

        public void OptionSelect(bool left)
        {
            switch (NowCount)
            {
                case 0:
                    if (left)
                    {
                        if (nSpeedCount > 0) nSpeedCount--;
                        else nSpeedCount = 15;
                    }
                    else
                    {
                        if (nSpeedCount < 15) nSpeedCount++;
                        else nSpeedCount = 0;
                    }
                    break;
                case 1:
                    if (nStealth == 0) nStealth = 1;
                    else nStealth = 0;
                    break;
                case 2:
                    if (nAbekobe == 0) nAbekobe = 1;
                    else nAbekobe = 0;
                    break;
                case 3:
                    if (left)
                    {
                        if (nRandom > 0) nRandom--;
                        else nRandom = 2;
                    }
                    else
                    {
                        if (nRandom < 2) nRandom++;
                        else nRandom = 0;
                    }
                    break;
                case 4:
                    // なし → 特訓モード → 作譜支援 の 3 つを回す
                    if (left) nGameMode = (nGameMode + 2) % 3;
                    else nGameMode = (nGameMode + 1) % 3;
                    break;
                case 5:
                    if (nAutoMode == 0) nAutoMode = 1;
                    else nAutoMode = 0;
                    break;

            }
        }
        private void tマウスを捨てる()
        {
            TJAPlayer3.tマウスホイールの段数を取り出す();
            TJAPlayer3.tマウス左クリックを取り出す();
            TJAPlayer3.tマウス右クリックを取り出す();
        }

        /// <summary>
        /// 決めずに閉じる（難易度選択から Shift+F1 で設定画面へ行くとき）。次に開いたとき最初の項目から始まるようにする。
        /// </summary>
        public void t決めずに閉じる()
        {
            NowCount = 0;
            this.bマウスで値を変える = false;
            ctOpen?.t停止();
            if (ctOpen != null) ctOpen.n現在の値 = 0;
            ctClose?.t停止();
            if (ctClose != null) ctClose.n現在の値 = 0;
            bEnd = false;
        }

        public void Decision()
        {
            #region [ Speed ]

            if (nSpeedCount == 0)
            {
                TJAPlayer3.ConfigIni.n譜面スクロール速度[0] = 4;
            }
            else if (nSpeedCount > 0 && nSpeedCount <= 11)
            {
                TJAPlayer3.ConfigIni.n譜面スクロール速度[0] = nSpeedCount + 8;
            }
            else if (nSpeedCount == 12)
            {
                TJAPlayer3.ConfigIni.n譜面スクロール速度[0] = 20 + 4;
            }
            else if (nSpeedCount == 13)
            {
                TJAPlayer3.ConfigIni.n譜面スクロール速度[0] = 20 + 9;
            }
            else if (nSpeedCount == 14)
            {
                TJAPlayer3.ConfigIni.n譜面スクロール速度[0] = 20 + 14;
            }
            else if (nSpeedCount == 15)
            {
                TJAPlayer3.ConfigIni.n譜面スクロール速度[0] = 20 + 19;
            }

            #endregion
            #region [ Doron ]
            if (nStealth == 0)
            {
                TJAPlayer3.ConfigIni.eSTEALTH = Eステルスモード.OFF;
            }
            else
            {
                TJAPlayer3.ConfigIni.eSTEALTH = Eステルスモード.DORON;
            }
            #endregion
            #region [ Random ]
            if(nRandom == 2 && nAbekobe == 1)
            {
                TJAPlayer3.ConfigIni.eRandom.Taiko = Eランダムモード.HYPERRANDOM;
            }
            else if (nRandom == 2 && nAbekobe == 0)
            {
                TJAPlayer3.ConfigIni.eRandom.Taiko = Eランダムモード.SUPERRANDOM;
            }
            else if (nRandom == 1 && nAbekobe == 1)
            {
                TJAPlayer3.ConfigIni.eRandom.Taiko = Eランダムモード.RANDOM;
            }
            else if (nRandom == 1 && nAbekobe == 0)
            {
                TJAPlayer3.ConfigIni.eRandom.Taiko = Eランダムモード.RANDOM;
            }
            else if (nRandom == 0 && nAbekobe == 1)
            {
                TJAPlayer3.ConfigIni.eRandom.Taiko = Eランダムモード.MIRROR;
            }
            else if (nRandom == 0 && nAbekobe == 0)
            {
                TJAPlayer3.ConfigIni.eRandom.Taiko = Eランダムモード.OFF;
            }
            #endregion
            #region [ GameMode ]
            // 作譜支援モードは演奏画面に入るときに特訓モードを借りるので、ここでは特訓モードを立てない。
            // （作譜支援は Config に保存され、次に起動したときも ON のまま）
            TJAPlayer3.ConfigIni.bTokkunMode = (nGameMode == 1);
            TJAPlayer3.ConfigIni.bChartAuthoring = (nGameMode == 2);
            #endregion
            #region [ AutoMode ]
            if(nAutoMode == 1)
            {
                TJAPlayer3.ConfigIni.b太鼓パートAutoPlay = true;
            }
            else
            {
                TJAPlayer3.ConfigIni.b太鼓パートAutoPlay = false;
            }
            #endregion
        }

    }
}
