using System;
using System.Diagnostics;
using Un4seen.Bass;
using Un4seen.Bass.AddOn.Mix;

namespace FDK
{
	/// <summary>
	/// 動画書き出し専用のサウンドデバイス。
	///
	/// 普通のデバイス（WASAPI / DirectSound）は、スピーカーが実時間で音を吸い出す。
	/// 書き出しは実時間を待たずに走るので、それだと映像と音の速さが合わない。
	///
	/// そこでこのデバイスは
	///   ・音を鳴らさない（BASS を「no sound」デバイスで初期化する）
	///   ・ミキサーはそのまま作る（打音も曲も、普通に遊ぶときと同じ経路でここに集まる）
	///   ・書き出し側が 1 コマぶん（1/fps 秒）ずつ ChannelGetData で引き抜く
	/// という作りにしてある。引き抜いた量がそのまま時間になるので、
	/// 映像 1 コマと音 1/fps 秒が必ず 1 対 1 で対応し、ズレようがない。
	/// </summary>
	internal class CSoundDeviceExport : ISoundDevice
	{
		// ゲームの素材はほぼ 44100Hz なので、ミキサーもそれに合わせる。
		// 合わせておけば、曲も打音もリサンプルされずそのまま混ざる。
		public const int n周波数 = 44100;
		public const int nチャンネル数 = 2;
		/// <summary>1 サンプルは float × チャンネル数。</summary>
		public const int n1サンプルのバイト数 = nチャンネル数 * 4;

		public ESoundDeviceType e出力デバイス { get; private set; }
		public long n実出力遅延ms { get { return 0; } }
		public long n実バッファサイズms { get { return 0; } }
		public long n経過時間ms { get; private set; }
		public long n経過時間を更新したシステム時刻ms { get; private set; }
		public CTimer tmシステムタイマ { get; private set; }

		public int nMasterVolume
		{
			get
			{
				float f = 0f;
				Bass.BASS_ChannelGetAttribute( this.hMixer, BASSAttribute.BASS_ATTRIB_VOL, ref f );
				return (int)( f * 100 );
			}
			set
			{
				Bass.BASS_ChannelSetAttribute( this.hMixer, BASSAttribute.BASS_ATTRIB_VOL, value / 100.0f );
			}
		}

		public CSoundDeviceExport()
		{
			Trace.TraceInformation( "書き出し用サウンドデバイスの初期化を開始します。" );

			// 本体は e出力デバイス を見て「WASAPI 用のサウンドの作り方」に分岐する。
			// このデバイスも同じミキサー方式なので、WASAPI を名乗っておく。
			this.e出力デバイス = ESoundDeviceType.ExclusiveWASAPI;
			this.n経過時間ms = 0;
			this.tmシステムタイマ = new CTimer( CTimer.E種別.MultiMedia );
			// n未使用 のままにしない。CSoundTimer が n未使用 を返し、
			// 本体側の Math.Abs( long.MinValue ) がオーバーフローで落ちる。
			this.n経過時間を更新したシステム時刻ms = this.tmシステムタイマ.nシステム時刻ms;

			BassNet.Registration( "dtx2013@gmail.com", "2X9181017152222" );

			// デバイス 0 = no sound。スピーカーには一切出さない。
			if ( !Bass.BASS_Init( 0, n周波数, BASSInit.BASS_DEVICE_DEFAULT, IntPtr.Zero ) )
				throw new Exception( string.Format( "BASS (no sound) の初期化に失敗しました。[{0}]", Bass.BASS_ErrorGetCode() ) );
			this.bBASSを初期化した = true;

			// リサンプルの品質を最高にする。既定は 2 点補間で、
			// 44100Hz の曲を 48000Hz のミキサーに混ぜると目に見えて音が荒れる。
			Bass.BASS_SetConfig( BASSConfig.BASS_CONFIG_SRC, 4 );

			this.hMixer = BassMix.BASS_Mixer_StreamCreate( n周波数, nチャンネル数,
				BASSFlag.BASS_MIXER_NONSTOP | BASSFlag.BASS_SAMPLE_FLOAT | BASSFlag.BASS_STREAM_DECODE );
			if ( this.hMixer == 0 )
				throw new Exception( string.Format( "BASSミキサ(mixing)の作成に失敗しました。[{0}]", Bass.BASS_ErrorGetCode() ) );

			// 音量調整を効かせるため、WASAPI 版と同じくもう一段かませる。
			this.hMixer_DeviceOut = BassMix.BASS_Mixer_StreamCreate( n周波数, nチャンネル数,
				BASSFlag.BASS_MIXER_NONSTOP | BASSFlag.BASS_SAMPLE_FLOAT | BASSFlag.BASS_STREAM_DECODE );
			if ( this.hMixer_DeviceOut == 0 )
				throw new Exception( string.Format( "BASSミキサ(最終段)の作成に失敗しました。[{0}]", Bass.BASS_ErrorGetCode() ) );

			if ( !BassMix.BASS_Mixer_StreamAddChannel( this.hMixer_DeviceOut, this.hMixer, BASSFlag.BASS_DEFAULT ) )
				throw new Exception( string.Format( "BASSミキサの接続に失敗しました。[{0}]", Bass.BASS_ErrorGetCode() ) );

			Trace.TraceInformation( "書き出し用サウンドデバイスの初期化を完了しました。({0}Hz {1}ch)", n周波数, nチャンネル数 );
		}

		/// <summary>
		/// ミキサーから音を引き抜く。引き抜いた量がそのまま経過時間になる。
		/// </summary>
		/// <returns>実際に取り出したバイト数。</returns>
		public int t音を取り出す( byte[] buffer, int length )
		{
			int n = Bass.BASS_ChannelGetData( this.hMixer_DeviceOut, buffer, length );
			if ( n < 0 ) n = 0;

			// 足りなかったぶんは無音で埋める。埋めないと映像と長さがずれる。
			for ( int i = n; i < length; i++ )
				buffer[ i ] = 0;

			this.n累積取り出しバイト数 += length;
			this.n経過時間ms = this.n累積取り出しバイト数 * 1000 / ( (long)n周波数 * n1サンプルのバイト数 );
			this.n経過時間を更新したシステム時刻ms = this.tmシステムタイマ.nシステム時刻ms;
			return n;
		}

		public CSound tサウンドを作成する( string strファイル名, ESoundGroup soundGroup )
		{
			CSound sound = new CSound( soundGroup );
			sound.tWASAPIサウンドを作成する( strファイル名, this.hMixer, this.e出力デバイス );
			return sound;
		}
		public void tサウンドを作成する( string strファイル名, CSound sound )
		{
			sound.tWASAPIサウンドを作成する( strファイル名, this.hMixer, this.e出力デバイス );
		}
		public void tサウンドを作成する( byte[] byArrWAVファイルイメージ, CSound sound )
		{
			sound.tWASAPIサウンドを作成する( byArrWAVファイルイメージ, this.hMixer, this.e出力デバイス );
		}

		public void Dispose()
		{
			if ( this.hMixer_DeviceOut != -1 ) { Bass.BASS_StreamFree( this.hMixer_DeviceOut ); this.hMixer_DeviceOut = -1; }
			if ( this.hMixer != -1 ) { Bass.BASS_StreamFree( this.hMixer ); this.hMixer = -1; }
			if ( this.bBASSを初期化した ) { Bass.BASS_Free(); this.bBASSを初期化した = false; }
			if ( this.tmシステムタイマ != null ) { this.tmシステムタイマ.Dispose(); this.tmシステムタイマ = null; }
		}

		private int hMixer = -1;
		private int hMixer_DeviceOut = -1;
		private long n累積取り出しバイト数 = 0;
		private bool bBASSを初期化した = false;
	}
}
