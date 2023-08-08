using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace InkML_Reader
{
	/// <summary>
	/// SubWindow.xaml の相互作用ロジック
	/// </summary>

	struct Draw_Area
	{
		// 描画範囲
		public int startX;         // X座標：開始位置
		public int startY;         // Y座標：開始位置
		public int endX;           // X座標：終了位置
		public int endY;           // Y座標：終了位置
	}

	public partial class SubWindow : Window
	{
		public const int SUBWINDOW_OUTLINE_W = 34;			// サブウインドウの外枠分の幅
		public const int SUBWINDOW_OUTLINE_H = 92;          // サブウインドウの外枠分の高さ

		public const int MIN_PEN_WIDTH = 2;					// ペンの最小の太さ
		public const int MIN_ERASER_WIDTH = 6;				// 消しゴムの最小の太さ

		private bool fInitialized = false;

		private InkML ink;

		private BMP[] pens;
		private BMP[] erasers;

		private int imgWidth;
		private int imgHeight;

		public WriteableBitmap bitmap;                      // WritableBitmap

		private long size;
		private int stride;                                 // 1行あたりのバイト数
		private byte[] pixs;                                // 描画データを格納するバイト列

		private uint x_offset;
		private uint y_offset;

//        PixelFormat bmForm = PixelFormats.BlackWhite;       // 1bit/1pixcel : サイズ重視
		PixelFormat bmForm = PixelFormats.Gray8;            // 8bit/1pixcel : 速度重視

		#region "最大化・最小化・閉じるボタンの非表示設定"

		[DllImport("user32.dll")]
		private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

		[DllImport("user32.dll")]
		private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

		const int GWL_STYLE = -16;
		const int WS_SYSMENU = 0x80000;

		protected override void OnSourceInitialized(EventArgs e)
		{
			base.OnSourceInitialized(e);
			IntPtr handle = new WindowInteropHelper(this).Handle;
			int style = GetWindowLong(handle, GWL_STYLE);
			style = style & (~WS_SYSMENU);
			SetWindowLong(handle, GWL_STYLE, style);
		}

		#endregion

		/**** SubWindowを生成する ****/
		public SubWindow(string title, InkML ink, BMP[] pens, BMP[] erasers, uint x_off, uint y_off, uint rate)
		{
			x_offset = x_off;
			y_offset = y_off;

			long width = (long)ink.width_paper - x_offset;      // 筆跡イメージの幅 = 
			long height = (long)ink.height_paper - y_offset;

			InitializeComponent();

			this.Title = title;                         // サブウインドウのタイトル：InkMLファイル名

			// 各描画領域の初期化
			// 筆跡イメージ
			imgWidth = (int)width;                      // 筆跡イメージの幅
			imgHeight = (int)height;                    // 筆跡イメージの高さ

			// ウインドウサイズ
			this.Width = width + SUBWINDOW_OUTLINE_W;   // サブウインドウの幅
			this.Height = height + SUBWINDOW_OUTLINE_H; // サブウインドウの高さ

			// 最大のウインドウサイズ
			this.MaxWidth = this.Width;                 // サブウインドウの幅
			this.MaxHeight = this.Height;               // サブウインドウの高さ

			// キャンバスのサイズ
			Cnv_LogImg.Width = imgWidth;
			Cnv_LogImg.Height = imgHeight;

			fInitialized = true;

			// 筆跡イメージの表示倍率を決定する
			foreach (var tick in Sld_Rate.Ticks)
			{
				Sld_Rate.Value = tick;                  // 予め用意した倍率のみ代入する
				if (tick >= rate)
				{
					break;
				}
			}

			// 筆跡イメージの幅 x 高さの表示
			Tbk_ImgSize.Text = width.ToString() + " x " + height.ToString();

			this.ink = ink;                             // InkMLデータの代入
			this.pens = pens;                           // ペンデータの代入
			this.erasers = erasers;                     // 消しゴムデータの代入

			// 初期化
			InitImage();
		}

		/**** SubWindowを閉じる ****/
		private void SubWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			// メモリの解放
			ink = null;
			pens = null;
			erasers = null;
			pixs = null;

			img = null;
			bitmap = null;

			return;
		}

		/**** 筆跡イメージの表示倍率を変更する ****/
		public void ChangeImageScale(double scale)
		{
			// canvasサイズの変更
			if (Sld_Rate.Value == 100)
			{
				Cnv_LogImg.Width = imgWidth;
				Cnv_LogImg.Height = imgHeight;
			}
			else
			{
				Cnv_LogImg.Width *= scale;
				Cnv_LogImg.Height *= scale;
			}

			// canvasの拡大縮小
			Matrix m0 = new Matrix();
			m0.Scale(Sld_Rate.Value * 0.01, Sld_Rate.Value * 0.01);//元のサイズとの比
			matrixTransform.Matrix = m0;


			// 最大のウインドウサイズ
			// サブウインドウの縮小
			this.MaxWidth = imgWidth * Sld_Rate.Value * 0.01 + SUBWINDOW_OUTLINE_W;
			this.MaxHeight = imgHeight * Sld_Rate.Value * 0.01 + SUBWINDOW_OUTLINE_H;

			// サブウインドウの縮小
			if (Sld_Rate.Value <= 100)
			{
				// Sizeが100%以下の場合はウインドウを縮小する
				this.Width = this.MaxWidth;
				this.Height = this.MaxHeight;
			}
		}

		/**** 筆跡イメージの表示倍率を変更するスライドバーが更新されたときの処理 ****/
		private void Sld_Rate_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
		{
			if (fInitialized == false)
			{
				return;
			}

			if (e.OldValue != 0)
			{
				//            Double scale = e.NewValue / Sld_Rate.Value;
				Double scale = e.NewValue / e.OldValue;

				if (scale != 1)
				{
					// スライドバー操作時（scaleが1でないとき）のみ、ここで画像を拡大/縮小する
					ChangeImageScale(scale);
				}
			}
		}

		/**** Ctrl+マウスホイールで筆跡イメージの表示倍率が更新されたときの処理 ****/
		private void Cnv_LogImg_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
		{

			if ((Keyboard.Modifiers & ModifierKeys.Control) <= 0)
			{
				return;
			}

			// 後続のイベントを実行禁止
			e.Handled = true;

			//  マウスホイールのイベントを受け取り、スライダーをずらす
			var index = Sld_Rate.Ticks.IndexOf(Sld_Rate.Value);
			if (0 < e.Delta)
			{
				index++;
			}
			else
			{
				index--;
			}

			if (index < 0)
			{
				index = 0;
				return;
			}
			else if (Sld_Rate.Ticks.Count <= index)
			{
				// 何もしない
			}
			else
			{
				// 画像がはみ出してしまうのでスクロール無し状態で拡大する
				Scv_LogImg.ScrollToHorizontalOffset(0);
				Scv_LogImg.ScrollToVerticalOffset(0);

				Double scale = Sld_Rate.Ticks[index] / Sld_Rate.Value;
				Sld_Rate.Value = Sld_Rate.Ticks[index];         // この変更直後にSld_Rate_ValueChanged()が呼ばれる
			}

			// 後続のイベントを実行禁止解除
			e.Handled = false;
			return;
		}

		/**** 筆跡イメージのサイズと1行辺りのバイト数を設定する ****/
		private void setImageParam(PixelFormat format)
		{
			if (format == PixelFormats.Gray8)
			{
				stride = imgWidth;                          // 1行あたりのバイト数
				size = imgWidth * imgHeight;
			}
			else if (format == PixelFormats.BlackWhite)
			{
				if ((imgWidth % 32) != 0)
				{
					stride = (imgWidth / 32 + 1) * 32;
					stride = stride / 8;                    // 1行あたりのバイト数（4byteの倍数）
				}
				else
				{
					stride = imgWidth / 8;                  // 1行あたりのバイト数
				}

				size = stride * imgHeight;
			}
			else { }
		}

		/**** ペン/消しゴムのサイズを含めた描画範囲を取得する ****/
		private Draw_Area getDrawArea(int x, int y, int w)
		{
			Draw_Area area = new Draw_Area();

			if ((w % 2) == 1)
			{
				// 幅が奇数の場合
				area.startX = x - ((w - 1) / 2);
				area.startY = y - ((w - 1) / 2);
				area.endX = x + ((w - 1) / 2);
				area.endY = y + ((w - 1) / 2);
			}
			else
			{
				// 幅が偶数の場合（左下（タブレットの原点）方向に＋１）
				area.startX = x - (w / 2);
				area.startY = y - (w / 2 - 1);
				area.endX   = x + (w / 2 - 1);
				area.endY   = y + (w / 2);
			}

			// 開始位置がマイナスでも良い

			if (area.endX >= imgWidth)
			{
				area.endX = imgWidth - 1;	// 座標を表すので最大幅 - 1
			}
			if (area.endY >= imgHeight)
			{
				area.endY = imgHeight - 1;  // 座標を表すので最大高さ - 1
			}

			return area;
		}

		/**** 筆跡イメージを初期化する ****/
		public void InitImage ()
		{
			// 描画先になるImageを生成する
			img = new Image();

			// WritableBitmapを生成する
			var dpi = 96;
			bitmap = new WriteableBitmap(imgWidth, imgHeight,
											dpi, dpi,                // x/y方向の解像度
											bmForm, null);

			// 描画データを格納するバイト列を生成する
			setImageParam(bitmap.Format);                           // 1行あたりのバイト数 & バッファサイズ
			pixs = new byte[size];
			Array.Fill<byte>(pixs, 0xff);

			bitmap.WritePixels(new Int32Rect(0, 0, imgWidth, imgHeight), pixs, stride, 0, 0);
			// Image.Sourceに作成したWriteableBitmapを指定する
			img.Source = bitmap;

			// Canvasに登録する
			Cnv_LogImg.Children.Add(img);
		}

		/**** 筆跡イメージをクリアする ****/
		public void ClearImage()
		{
			// WritableBitmapを再生成する
			var dpi = 96;
			bitmap = new WriteableBitmap(imgWidth, imgHeight,
											dpi, dpi,                // x/y方向の解像度
											bmForm, null);

			// 描画データを格納するバイト列をクリアする
			Array.Fill<byte>(pixs, 0xff);
		}

		/**** startCnt～endCntまでのステップ（= trace）分の筆跡イメージを描画する ****/
		public void DrawTrace(uint startCnt, uint endCnt)
		{
			// 描画
			for (uint cnt = startCnt; cnt < endCnt; cnt++)
			{
				double prevX = 0;
				double prevY = 0;

				// 処理速度を優先する為、ここで分岐
				if (ink.traces[cnt].brush == InkML_brushRef.PENCIL)
				{
					// ペンの描画
					foreach (InkML_Data data in ink.traces[cnt].data)
					{
						BMP pen = pens[(uint)data.w];
						if (pen == null)
						{
							// ペンデータが存在しない場合、最小の太さのペンを使用する
							pen = pens[MIN_PEN_WIDTH];
						}
						DrawImage((uint)data.x, (uint)data.y, pen);

						if ((prevX != 0) || (prevY != 0))
						{
							// 座標間の補間
							// 補間処理はタブレット用なのでXとYを入替えて使用する
							Plot pl = new Plot(prevY, prevX,
												data.plotY, data.plotX,
												InkML_brushRef.PENCIL, (byte)data.w);

							for (long plot_cnt = 0; plot_cnt < pl.ful_plot_interpolation_cnt; plot_cnt++)
							{
								DrawImage((uint)pl.plot_interpolation_buff[plot_cnt].y_point - this.x_offset,
											(uint)(ink.height_paper - 1 - pl.plot_interpolation_buff[plot_cnt].x_point),
											pen);
							}
						}

						// 補間の為、前回座標を保存
						prevX = data.plotX;
						prevY = data.plotY;
					}
				}

				else if (ink.traces[cnt].brush == InkML_brushRef.ERASER)
				{
					// 消しゴムの描画
					foreach (InkML_Data data in ink.traces[cnt].data)
					{
						BMP eraser = erasers[(uint)data.w / 2];
						if (eraser == null)
						{
							// 消しゴムデータが存在しない場合、最小の太さのペンを使用する
							eraser = erasers[MIN_ERASER_WIDTH / 2];
						}

						EraseImage((uint)data.x, (uint)data.y, eraser);

						if ( (prevX != 0) || (prevY != 0) )
						{
							// 座標間の補間
							// 補間処理はタブレット用なのでXとYを入替えて使用する
							Plot pl = new Plot(prevY, prevX,
												data.plotY, data.plotX,
												InkML_brushRef.ERASER, (byte)data.w);

							for (long plot_cnt = 0; plot_cnt < pl.ful_plot_interpolation_cnt; plot_cnt++)
							{
								EraseImage((uint)pl.plot_interpolation_buff[plot_cnt].y_point - this.x_offset,
											(uint)(ink.height_paper - 1 - pl.plot_interpolation_buff[plot_cnt].x_point),
											eraser);
							}
						}

						// 補完の為、前回座標を保存
						prevX = data.plotX;
						prevY = data.plotY;

					}
				}
				else { /* ### エラー ### */ }
			}
			SetImage();
		}

		/**** ペンデータを描画する ****/
		public void DrawImage(uint x, uint y, BMP pen)
		{
			Draw_Area area = getDrawArea((int)x, (int)y, (int)pen.bm_width);      // 描画範囲の取得

			int pen_x = 0;      // ペンのX座標
			int pen_y = 0;      // ペンのY座標

			if (bitmap.Format == PixelFormats.Gray8)
			{
				for (int imag_y = area.startY; imag_y <= area.endY; imag_y++)
				{
					if (imag_y >= 0)
					{
						for (int imag_x = area.startX; imag_x <= area.endX; imag_x++)
						{
							if (imag_x >= 0)
							{
								// 画像が白いときだけ、ペンの色を代入する
								if (pixs[imag_y * stride + imag_x] == 0xff)
								{
									pixs[imag_y * stride + imag_x] = pen.bm_data[pen_y * pen.bm_width + pen_x];
								}
							}
							pen_x++;
						}
						pen_x = 0;
					}
					pen_y++;
				}
			}
		}

		/**** 消しゴムデータを描画する ****/
		public void EraseImage(uint x, uint y, BMP eraser)
		{
			Draw_Area area = getDrawArea( (int)x, (int)y, (int)eraser.bm_width);      // 描画範囲の取得

			int eraser_x = 0;      // 消しゴムのX座標
			int eraser_y = 0;      // 消しゴムのY座標

			if (bitmap.Format == PixelFormats.Gray8)
			{
				for (int imag_y = area.startY; imag_y <= area.endY; imag_y++)
				{
					if (imag_y >= 0)				// マイナスの座標には描画しない
					{
						for (int imag_x = area.startX; imag_x <= area.endX; imag_x++)
						{
							if (imag_x >= 0)		// マイナスの座標には描画しない
							{
								// 画像が黒いときだけ、消しゴムの色を代入する
								if (pixs[imag_y * stride + imag_x] == 0x00)
								{
									pixs[imag_y * stride + imag_x] = eraser.bm_data[eraser_y * eraser.bm_width + eraser_x];
								}
							}
							eraser_x++;
						}
						eraser_x = 0;
					}
					eraser_y++;
				}
			}
		}

		/**** ペン/消しゴムデータを描画した後の処理 ****/
		public void SetImage()
		{
			// バイト列 -> BitmapImage
			bitmap.WritePixels(new Int32Rect(0, 0, imgWidth, imgHeight), pixs, stride, 0, 0);
			// Image.Sourceに作成したWriteableBitmapを指定する
			img.Source = bitmap;
		}
	}
}
