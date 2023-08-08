using System;
using System.Drawing;
using System.Windows.Media;
using System.IO;


namespace InkML_Reader
{
    public class BMP
    {
        public const int BMP_OFFSET_POS = 0x0A;         // ファイルの先頭から画像サイズまでのオフセット(4byte)

        public uint bm_width;
        public uint bm_height;
        public uint bm_stride;
        public byte[] bm_data;

        public PixelFormat bm_Form;

        /**** ペン/消しゴムデータの解析と取得 ****/
        public BMP(uint size, uint val)
        {
            bm_width = size;
            bm_height = size;
            bm_stride = size;
            bm_data = new byte[size * size];

            Array.Fill<byte>(bm_data, (byte)val);
        }

    public BMP(String filename, PixelFormat form)
        {
            Bitmap bitmap = new Bitmap(filename);

            // ペン/消しゴムのbitmapはタブレット基準の方向の為、右上→右下を左上→右上方向に変換して取り出す
            bm_width = (uint)bitmap.Height;
            bm_height = (uint)bitmap.Width;

            if (((uint)bitmap.Width % 32) != 0)
            {
                bm_stride = ((uint)bitmap.Width / 32 + 1) * 32;
                bm_stride = bm_stride / 8;                  // 1行あたりのバイト数（4byteの倍数）
            }
            else
            {
                bm_stride = (uint)bitmap.Width / 8;                   // 1行あたりのバイト数
            }

            ImageConverter imgconv = new ImageConverter();
            byte[] data = (byte[])imgconv.ConvertTo(new Bitmap(filename), typeof(byte[]));

            uint offset = data[BMP_OFFSET_POS];
            if (form == PixelFormats.BlackWhite)
            {
                // 1pixel/1bitはその後の演算が複雑になるので対応しない
            }
            else if (form == PixelFormats.Gray8) {
                bm_data = new byte[bm_width * bm_height];
             
                                for (uint y = 0; y < bm_height; y++)
                                {
                                    for (uint x = 0; x < bm_width; x++)
                                    {
                                        uint pos = offset + x * bm_stride + (bm_height - 1 - y) / 8;
                                        byte bit = data[pos];
                                        int shift = (int)((bm_width - 1 - y) % 8);        // 何ビット目に格納されているか
                                        if ( ((0x80 >> shift) & bit) != 0x00)
                                        {
                                            bm_data[y * bm_height + x] = 0xff;          // 黒イメージ
                                        }
                                        else
                                        {
                                            bm_data[y * bm_height + x] = 0x00;          // 白イメージ
                                        }
                                    }
                                }
          

                // strideを元のbitmapのstrideの意味で使っていたので、変換後（1pixcel/1Byte）のstrideに直す
                bm_stride = bm_width;
            }
            else { /* ### エラー ### */ }

            // メモリの解放
            bitmap.Dispose();
        }
    }
}
