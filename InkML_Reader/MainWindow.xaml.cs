using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Configuration;

using Microsoft.Win32;

using Excel = Microsoft.Office.Interop.Excel;
//using System.Windows.Forms;
//using Microsoft.Office.Interop.Excel;
using System.Runtime.InteropServices;
using System.Diagnostics;
//using System.Windows.Shapes;
//using System.Reflection;
//using System.Drawing;

namespace InkML_Reader
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		public const int MIN_PEN_WIDTH = 2;					// ペンの最小の太さ
		public const int MAX_PEN_WIDTH = 11;				// ペンの最大の太さ
		public const int MIN_ERASER_WIDTH = 6;				// 消しゴムの最小の太さ
		public const int MAX_ERASER_WIDTH = 62;				// 消しゴムの最大の太さ

		private SubWindow sw;

		private PixelFormat form = PixelFormats.Gray8;

		/**** MainWindowを開く ****/
		public MainWindow()
		{
			InitializeComponent();

			Txb_FilePath.Text = ConfigurationManager.AppSettings["input_file_path"];
			if (Txb_FilePath.Text == "")
			{
				Txb_FilePath.Text = "開くファイルを選択してください";
			}

			// ステップ数の変更禁止
			rock_StepChange();

			// BMP出力ボタン/メニューの操作禁止
			Btn_MakeBMPFile.IsEnabled = false;
			Menu_Output.IsEnabled = false;
		}

		/**** MainWindowを閉じる ****/
		private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			if (sw != null)
			{
				sw.Close();
			}
		}

		/**** MainWindowを閉じる（メニューの閉じるボタン押下） ****/
		private void MainWindow_Closing_Click(object sender, RoutedEventArgs e)
		{
			this.Close();
		}

		/**** MainWindowを初期化する ****/
		public void Init_Display()
		{
			// ステップ数の変更禁止
			rock_StepChange();

			// BMP出力ボタン/メニューの操作禁止
			Btn_MakeBMPFile.IsEnabled = false;
			Menu_Output.IsEnabled = false;
		}


		/**** ステップ数の変更操作を禁止する ****/
		private void rock_StepChange()
		{
			// ステップ数の変更操作禁止
			Sld_LogCount.IsEnabled = false;
			Btn_LogBackward.IsEnabled = false;
			Btn_LogForward.IsEnabled = false;
		}

		/**** ステップ数の変更操作を禁止解除する ****/
		private void unrock_StepChange()
		{
			// ステップ数の変更操作禁止解除
			Sld_LogCount.IsEnabled = true;
			Btn_LogBackward.IsEnabled = true;
			Btn_LogForward.IsEnabled = true;
		}

		/**** 指定したフォルダにある全てのペンデータを取得する ****/
		private BMP[] getPensData(string path)
        {
			// ペンデータの取得
			BMP[] pens = new BMP[MAX_PEN_WIDTH + 1];

			try
			{
				string[] files = Directory.GetFiles(path, "*pix.bmp");  // 指定フォルダにある*pix.bmpファイルを全て取得する

				foreach (string file in files)
				{
					BMP bmp = new BMP(file, form);                      // ペンデータのビットマップを取得
					pens[bmp.bm_width] = bmp;                           // ペンデータ群の太さ番目に代入する
				}

				// 太さ1のペンはbitmapデータを持たないので直接作成する
				if (pens[1] == null)
				{
					pens[1] = new BMP(1, 0x00);

				}
			}
			catch (System.IO.DirectoryNotFoundException)
			{
				// ペンデータのファイルが存在しない為、エラー
			}

			return pens;
        }

		/**** 指定したフォルダにある全ての消しゴムデータを取得する ****/
		private BMP[] getErasersData(string path)
		{
			// 消しゴムデータの取得
			BMP[] erasers = new BMP[MAX_ERASER_WIDTH / 2 + 1];      // 消しゴムの太さは偶数

			try
			{
				string[] files = Directory.GetFiles(path, "*pix.bmp");  // 指定フォルダにある*pix.bmpファイルを全て取得する

				foreach (string file in files)
				{
					BMP bmp = new BMP(file, form);                      // 消しゴムデータのビットマップを取得
					erasers[bmp.bm_width / 2] = bmp;                        // 消しゴムデータ群の(太さ/2)番目に代入する
				}
			}
			catch (System.IO.DirectoryNotFoundException)
			{
				// 消しゴムデータのファイルが存在しない為、エラー
			}

			return erasers;
		}

		/**** ステップ数を表示する ****/
		private void dispSldVal(double curr, double max)
		{
			Tbk_LogCount.Text = curr.ToString() + " / " + max.ToString();
		}

		/**** サブウインドウにstartCnt～endCntまでのステップ（= trace）分の筆跡イメージを描画する ****/
		private void drawTrace(uint startCnt, uint endCnt)
		{
			if (startCnt > Sld_LogCount.Maximum)
			{
				// 開始ステップ数が最大値を超えているので、エラー
				return;
			}
			if (endCnt > Sld_LogCount.Maximum)
			{
				// 終了ステップ数が最大値を超えているので、エラー
				return;
			}
			if (startCnt > endCnt)
			{
				// 開始ステップ数が終了ステップを超えているので、エラー
				return;
			}

			sw.DrawTrace(startCnt, endCnt);
		}

		/**** スライダが更新されたときの処理 ****/
		private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
		{
			// ステップ数の変更操作禁止
			rock_StepChange();

				// 筆跡イメージの更新
			if (e.NewValue < e.OldValue)        // ステップを戻した場合、クリア & 再描画
			{
				// 筆跡イメージのクリア
				sw.ClearImage();

				// 筆跡イメージの再描画
				drawTrace(0, (uint)e.NewValue);
			}
			else
			{
				// 筆跡イメージの更新
				drawTrace((uint)e.OldValue, (uint)e.NewValue);
			}

			// ステップ数表示の更新
			dispSldVal(e.NewValue, Sld_LogCount.Maximum);
			// ステップ数の変更操作禁止解除
			unrock_StepChange();
		}

		/**** ステップを戻すボタンが押下されたときの処理 ****/
		private void Btn_LogBackward_Click(object sender, RoutedEventArgs e)
		{
			if (Sld_LogCount.Value > 0)
			{
				Sld_LogCount.Value--;		// ステップ数を戻す
			}
		}

		/**** ステップを進めるボタンが押下されたときの処理 ****/
		private void Btn_LogForward_Click(object sender, RoutedEventArgs e)
		{
			if (Sld_LogCount.Value < Sld_LogCount.Maximum)
			{
				Sld_LogCount.Value++;		// ステップ数を進める
			}
		}

		/**** 開くボタンが押下されたときの処理 ****/
		private void Btn_FileOpenDialog_Click(object sender, RoutedEventArgs e)
		{
			// 開くボタンの押下
			// OpenFileDialogクラスのインスタンスを作成する
			OpenFileDialog ofd = new OpenFileDialog();

			// はじめに表示されるファイル名を指定する
			var input_file_path = ConfigurationManager.AppSettings["input_file_path"];
			if (input_file_path != "")
			{
				ofd.FileName = Path.GetFileName(input_file_path);
			}
			else
			{
				ofd.FileName = "";
			}

			// はじめに表示されるフォルダを指定する（空白だと、現在のフォルダ）
			if (input_file_path != "")
			{
				ofd.InitialDirectory = Path.GetDirectoryName(input_file_path);
			}
			else
			{
				ofd.InitialDirectory = "";
			}

			// [ファイルの種類]に表示される選択肢を指定する（指定しないとすべてのファイル）
			ofd.Filter = "Ink MLファイル(*.InkML)|*.InkML";

			// [ファイルの種類]ではじめに選択されるものを指定する
			// 2番目の「すべてのファイル」が選択されているようにする
			ofd.FilterIndex = 1;

			// タイトルを設定する
			ofd.Title = "開くファイルを選択してください";

			// ダイアログボックスを閉じる前に現在のディレクトリを復元するようにする
			ofd.RestoreDirectory = true;

			// 存在しないファイルの名前が指定されたとき警告を表示する（デフォルトはtrue）
			ofd.CheckFileExists = true;

			// 存在しないパスが指定されたとき警告を表示する（デフォルトはtrue）
			ofd.CheckPathExists = true;

			// ダイアログを表示する
			if (ofd.ShowDialog() == true)
			{
				// 既に、サブウインドウが開いている場合は、ここで閉じる
				if (sw != null)
				{
					sw.Close();
					sw = null;
				}

				// OKボタンがクリックされたとき、選択されたファイル名を表示する
				Txb_FilePath.Text = ofd.FileName;

				// 設定ファイルの読込み
				// オフセットの取得
				var x_offset = Convert.ToUInt16(ConfigurationManager.AppSettings["x_offset"]);		// X座標のオフセット
				var y_offset = Convert.ToUInt16(ConfigurationManager.AppSettings["y_offset"]);		// Y座標のオフセット

				var initial_draw_rate = Convert.ToUInt16(ConfigurationManager.AppSettings["initial_draw_rate"]);        // 筆跡イメージの表示倍率

				// InkMLファイルの解析
				// ここで、ウインドウサイズ、最大ステップ数、筆跡データを取得する
				InkML ink = new InkML(Txb_FilePath.Text, x_offset, y_offset);

				bool bRequestErrorReturn = false;
				string errorMsg = "";

				if (ink.traceCnt == 0)
				{
					errorMsg += "Trace data is zero.\n";
                    bRequestErrorReturn = true;
				}

                if ((ink.width_paper == 0.0) || (ink.height_paper == 0.0))
                {
                    errorMsg += "Not found paper size.\n";
                    bRequestErrorReturn = true;
                }

                if ((ink.width_digi == 0.0) || (ink.height_digi == 0.0))
                {
                    errorMsg += "Not found digi size.\n";
                    bRequestErrorReturn = true;
                }

				if (bRequestErrorReturn)
				{
                    MessageBox.Show(errorMsg + "筆跡ファイルを読込めませんでした。" + "\nファイルの内容を確認して下さい。");
                    return;
				}

                // ペン/消しゴムデータの取得
#if false
				// ファイルを開く
				var pen_folder = ConfigurationManager.AppSettings["pen_folder"];					// ペンデータ格納フォルダ
				var eraser_folder = ConfigurationManager.AppSettings["eraser_folder"];				// 消しゴムデータ格納フォルダ

				BMP[] pens = getPensData(pen_folder);												// ペンデータの取得
				BMP[] erasers = getErasersData(eraser_folder);                                      // 消しゴムデータの取得

				if (pens[MIN_PEN_WIDTH] == null)
				{
					// 最小の太さのペンデータがないので、エラー
					MessageBox.Show("ペンデータを読込めませんでした。" + "\nInkMLファイルの復元を中止します。");
					return;
				}
				if (erasers[MIN_ERASER_WIDTH / 2] == null)
				{
					// 最小の太さの消しゴムデータがないので、エラー
					MessageBox.Show("消しゴムデータを読込めませんでした。" + "\nInkMLファイルの復元を中止します。");
					return;
				}
#else
                // リソースを開く
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();

                // ペンデータの取得
                BMP[] pens = new BMP[MAX_PEN_WIDTH + 1];

				// リソースの検索
                foreach (var name in assembly.GetManifestResourceNames())
                {
					if (name.StartsWith("P") && name.EndsWith("pix.bmp"))
					{
                        // 所定の文字列を満たしたとき
                        //Debug.WriteLine($" Name: {name}");

						Stream stream = assembly.GetManifestResourceStream(name);

                        BMP bmp =
							new BMP(stream, form);							// ペンデータのビットマップを取得
                        pens[bmp.bm_width] = bmp;                           // ペンデータ群の太さ番目に代入する
                    }
                }
                // 太さ1のペンはbitmapデータを持たないので直接作成する
                if (pens[1] == null)
                {
                    pens[1] = new BMP(1, 0x00);

                }

                // 消しゴムデータの取得
                BMP[] erasers = new BMP[MAX_ERASER_WIDTH / 2 + 1];      // 消しゴムの太さは偶数

                // リソースの検索
                foreach (var name in assembly.GetManifestResourceNames())
                {
                    if (name.StartsWith("E") && name.EndsWith("pix.bmp"))
                    {
                        // 所定の文字列を満たしたとき
                        //Debug.WriteLine($" Name: {name}");

                        Stream stream = assembly.GetManifestResourceStream(name);

                        BMP bmp =
                            new BMP(stream, form);                          // ペンデータのビットマップを取得
                        erasers[bmp.bm_width / 2] = bmp;                    // ペンデータ群の太さ番目に代入する
                    }
                }
#endif
                // サブウインドウ
                // 生成
                sw = new SubWindow(Txb_FilePath.Text, ink, pens, erasers, x_offset, y_offset, initial_draw_rate);
//				sw.Owner = this;        // メインウインドウを親、サブウインドウを子に設定する ⇒ 親ウインドウは前面に出せない

				// 筆跡ステップ数（スライダ）の変更 & イメージの描画
				// ステップ数を変更すると筆跡イメージも描画されるので、先にサブウインドウを生成すること
				// 現在/最大ステップ数の初期化
				Sld_LogCount.Value = 0;
				Sld_LogCount.Maximum = ink.traceCnt;

				var initial_draw_steps = ConfigurationManager.AppSettings["initial_draw_steps"];	// 初期ステップ数の取得
				if (initial_draw_steps == "NONE")
				{
					// 初期状態で筆跡イメージを全く復元しない（白紙）
					// ステップ数 = 0だと更新関数が呼ばれないのでここで更新
					dispSldVal(Sld_LogCount.Value, Sld_LogCount.Maximum);		// ステップ数表示の更新
					unrock_StepChange();										// ステップ数の変更禁止解除
				}
				else
				{
					// 初期状態で筆跡イメージを全て復元する
					Sld_LogCount.Value = ink.traceCnt;
				}

				// 入力InkMLファイル名の更新
				Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
				config.AppSettings.Settings["input_file_path"].Value = Txb_FilePath.Text;
				config.Save();

				// BMP出力ボタン/メニューの操作禁止解除
				Btn_MakeBMPFile.IsEnabled = true;
				Menu_Output.IsEnabled = true;

				// 表示
				sw.Show();
			}
		}

		/**** BMP出力ボタンが押下されたときの処理 ****/
		private void Btn_MakeBMPFile_Click(object sender, RoutedEventArgs e)
		{
			// BMP出力ボタンの押下
			// SaveFileDialogクラスのインスタンスを作成
			SaveFileDialog sfd = new SaveFileDialog();

			var output_file_path = ConfigurationManager.AppSettings["output_file_path"];
			if (output_file_path == "") {
				output_file_path = Txb_FilePath.Text;
			}
			// はじめに表示されるファイル名を指定する
			sfd.FileName = Path.GetFileNameWithoutExtension(output_file_path) + ".bmp";

			// はじめに表示されるフォルダを指定する（空白だと、現在のフォルダ）
			sfd.InitialDirectory = Path.GetDirectoryName(output_file_path);

			// [ファイルの種類]に表示される選択肢を指定する（指定しないとすべてのファイル）
			sfd.Filter = "BMPファイル(*.bmp)|*.bmp";

			sfd.Title = "保存するファイル名を入力してください";

			// ダイアログを表示する
			if (sfd.ShowDialog() == true)
			{
				// BMPファイルを保存する
				using (FileStream stream = new FileStream(sfd.FileName, FileMode.Create, FileAccess.Write))
				{
					// サブウインドウのbitmapをBMPファイルにエンコードする
					BmpBitmapEncoder encoder = new BmpBitmapEncoder();
					encoder.Frames.Add(BitmapFrame.Create(sw.bitmap));
					encoder.Save(stream);

					// 出力BMPファイル名の更新
					Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
					config.AppSettings.Settings["output_file_path"].Value = sfd.FileName;
					config.Save();

					MessageBox.Show("筆跡イメージを\n" + sfd.FileName + "\nに保存しました。");
				}
			}
		}

		private void showHelpFile(object sender, RoutedEventArgs e)
		{
			var help_file_path = ConfigurationManager.AppSettings["help_file_path"];

			try
			{
				var file = new System.Diagnostics.Process();
				file.StartInfo.FileName = @help_file_path;
				file.StartInfo.UseShellExecute = true;
				file.Start();
			}
			catch (System.ComponentModel.Win32Exception)
			{
				MessageBox.Show("ヘルプファイルが開けませんでした。");
			}
		}
	}
}
