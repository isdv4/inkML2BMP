using System;
using System.Xml;

using System.Linq;

namespace InkML_Reader
{
    public struct InkML_Data
    {
        public double x;                // x座標
        public double y;                // y座標
        public double w;                // ペンの太さ
        public uint plotX;              // digitaizerのx座標
        public uint plotY;              // digitaizerのy座標
    }

    public enum InkML_type
    {
        PEN_UP = 0,                     // penUp
        PEN_DOWN                        // penDown
    };

    public enum InkML_brushRef
    {
        PENCIL = 0,                     // #br_pencil_1_1
        ERASER                          // #br_eraser_1_FF
    };

    public struct InkML_Trace
    {
        public InkML_type tp;           // pen Up 0r Down
        public InkML_brushRef brush;    // ペン種別

        public InkML_Data[] data;
    }

    enum InkML_traceFormat {
        X = 0,              // X: X座標
        Y,                  // Y: Y座標
        F,
        Z,
        OTx,                // Otx: X方向の傾き
        OTy,                // Oty: Y方向の傾き
        W,                  // W: ペンの太さ
        T
    };

    public class InkML
    {
        public const int MIN_PEN_WIDTH = 1;             // ペンの最小の太さ
        public const int MAX_PEN_WIDTH = 11;            // ペンの最大の太さ
        public const int MIN_ERASER_WIDTH = 6;          // 消しゴムの最小の太さ
        public const int MAX_ERASER_WIDTH = 62;			// 消しゴムの最大の太さ

        public double width_paper;        // 電子ペーパー（= ディスプレイ）の幅
        public double height_paper;       // 電子ペーパー（= ディスプレイ）の高さ
        public double width_digi;         // デジタイザー（= センサ）の幅
        public double height_digi;        // デジタイザー（= センサ）の高さ

        public int traceCnt = 0;
        public InkML_Trace[] traces;

        public int x_offset;
        public int y_offset;

        public double pen_mag;
        public double pos_mag;

        /**** 筆跡イメージ全体の幅/高さを取得する ****/
        private void setTraceWH(XmlNode childNode)
        {
            for (int cnt = 0; cnt < childNode.ChildNodes.Count; cnt++)
            {
                XmlNode dataNode = childNode.ChildNodes[cnt];
                for (int attCnt = 0; attCnt < dataNode.Attributes.Count; attCnt++)
                {
                    XmlAttribute xmlAttr = dataNode.Attributes[attCnt];
                    // 筆跡データ全体の幅/高さを取得
                    if (xmlAttr.Name == "type")
                    {
                        if (xmlAttr.Value == "paper")
                        {
                            for (int chiCnt = 0; chiCnt < dataNode.ChildNodes.Count; chiCnt++)
                            {
                                XmlNode node = dataNode.ChildNodes[chiCnt];
                                switch (node.Name)
                                {
                                    case "x":
                                        width_paper = long.Parse(node.InnerText);
                                        break;

                                    case "y":
                                        height_paper = long.Parse(node.InnerText);
                                        break;

                                    default:
                                        break;
                                }
                            }
                        }
                        else if (xmlAttr.Value == "digitizer")
                        {
                            for (int chiCnt = 0; chiCnt < dataNode.ChildNodes.Count; chiCnt++)
                            {
                                XmlNode node = dataNode.ChildNodes[chiCnt];
                                switch (node.Name)
                                {
                                    case "x":
                                        width_digi = long.Parse(node.InnerText);
                                        break;

                                    case "y":
                                        height_digi = long.Parse(node.InnerText);
                                        break;

                                    default:
                                        break;
                                }
                            }

                        }
                    }
                }
            }

            // pos_mag = Math.Round((this.width_paper / this.width_digi), 3, MidpointRounding.AwayFromZero);        // 小数第三位で四捨五入
            pen_mag = Math.Round((this.width_digi / this.width_paper), 2, MidpointRounding.AwayFromZero);           // 実際は、digi / 100 / paper * 1000、小数第二位で四捨五入

            return;
        }

        /**** 筆跡データを1ステップ（= trace）分取得する ****/
        private InkML_Trace getTraceData(XmlNode childNode)
        {
            InkML_Trace retTrace = new InkML_Trace();
            int dataCnt = 0;
            retTrace.data = new InkML_Data[dataCnt];

            for (int attCnt = 0; attCnt < childNode.Attributes.Count; attCnt++)
            {
                XmlAttribute xmlAttr = childNode.Attributes[attCnt];
                if ((xmlAttr.Name == "type") && (xmlAttr.Value == "penUp"))
                {
                    // PenUpのデータは無効
                    retTrace.tp = InkML_type.PEN_UP;
                    break;
                }
                else if ((xmlAttr.Name == "type") && (xmlAttr.Value == "penDown"))
                {
                    // PenDownのデータは有効
                    retTrace.tp = InkML_type.PEN_DOWN;
                    // ","でデータを分割してTraceのデータを取り出す
                    string[] traceStr = (childNode.InnerText.Trim()).Split(',');
                    foreach (String traceStr_div in traceStr)
                    {
                        InkML_Data data_temp;
                        // " "でデータを分割してTrace内のの各データを取り出す
                        string[] dataStr = traceStr_div.Split(' ');

                        var tempX = double.Parse(dataStr[(int)InkML_traceFormat.X]);      // X: X座標
                        var tempY = double.Parse(dataStr[(int)InkML_traceFormat.Y]);      // Y: Y座標
                        var tempW = double.Parse(dataStr[(int)InkML_traceFormat.W]);      // W: ペンの太さ
                        tempW = (uint)Math.Floor(tempW / (this.pen_mag * 10));

                        // この時点では、ペンなのか消しゴムなのかは未確定

                        // 座標補間用の座標を取得する（小数点以下は切捨て）
                        data_temp.plotX = (uint)Math.Floor(tempX / this.pen_mag);
                        data_temp.plotY = (uint)((uint)(this.height_digi - tempY)/ this.pen_mag);

                        // Bitmap描画用の座標を取得する（小数点以下は切捨て）
                        // Y座標はオフセットの影響を受けない
                        data_temp.x = (uint)Math.Floor(tempX / this.pen_mag) - this.x_offset;
                        data_temp.y = (uint)(this.height_paper - 1 - (uint)Math.Floor( (this.height_digi - tempY) / this.pen_mag) );

                        data_temp.w = tempW;
                        /*
                        // 小数点第1位で四捨五入
                        data_temp.x = Math.Round( tempX / this.pen_mag ), MidpointRounding.AwayFromZero);
                        data_temp.y = Math.Round( tempY / this.pen_mag ), MidpointRounding.AwayFromZero);
                        data_temp.w = Math.Round( (tempW / (this.pen_mag + 10) ), MidpointRounding.AwayFromZero);       // ペン/消しゴムの太さ
                        */

                        dataCnt++;
                        Array.Resize(ref retTrace.data, dataCnt);
                        retTrace.data[dataCnt - 1] = data_temp;
                    }
                }
                else if (xmlAttr.Name == "brushRef")
                {
                    // ペン種別の取得
                    switch (xmlAttr.Value)
                    {
                        case "#br_pencil_1_1":
                            retTrace.brush = InkML_brushRef.PENCIL;
                            break;

                        case "#br_eraser_1_FF":
                            retTrace.brush = InkML_brushRef.ERASER;
                            break;

                        default:
                            break;
                    }
                }
                else { }
            }

            return retTrace;
        }

        /**** ペンデータの幅を取得する ****/
        private int getPenWidth(int w)
        {
            int ret = w;

            if (ret > MAX_PEN_WIDTH)
            {
                ret = MAX_PEN_WIDTH;
            }
            else if ((ret < MIN_PEN_WIDTH))
            {
                ret = MIN_PEN_WIDTH;
            }

            return ret;
        }

        /**** 消しゴムデータの幅を取得する ****/
        private int getEraserWidth(int w)
        {
            int ret = w / 2 * 2;

            if (ret > MAX_ERASER_WIDTH)
            {
                ret = MAX_ERASER_WIDTH;
            }
            else if ((ret < MIN_ERASER_WIDTH))
            {
                ret = MIN_ERASER_WIDTH;
            }

            return ret;
        }

        /**** 全筆跡データから連続して重複するデータを取り除く ****/
        private InkML_Trace[] convTraceData(InkML_Trace[] tr)
        {
            InkML_Trace[] retTraces = new InkML_Trace[traceCnt];

            uint invalid_Cnt = 0;           // 不要なデータの数、確認用

            for (int cnt = 0; cnt < tr.Count(); cnt++)
            {
                retTraces[cnt].tp = tr[cnt].tp;
                retTraces[cnt].brush = tr[cnt].brush;

                // 1つ前のX/Y座標とペン/消しゴムの幅を保存する
                double prevX = 0;
                double prevY = 0;
                double prevW = 0;

                int dataCnt = 0;
                foreach (InkML_Data data in tr[cnt].data)
                {
                    double drawX = data.x;
                    double drawY = data.y;
                    double drawW = data.w;

                    // 前回と同じ座標だった場合、格納しない
                    if ((drawX != prevX) || (drawY != prevY) || (drawW != prevW))
                    {
                        InkML_Data tempData = data;

                        // ペン/消しゴムの幅を取得する
                        if (retTraces[cnt].brush == InkML_brushRef.PENCIL)
                        {
                            tempData.w = getPenWidth((int)data.w);
                        }
                        else if (retTraces[cnt].brush == InkML_brushRef.ERASER)
                        {
                            tempData.w = getEraserWidth((int)data.w);
                        }
                        else { /* ### エラー ### */ }

                        dataCnt++;
                        Array.Resize(ref retTraces[cnt].data, dataCnt);
                        retTraces[cnt].data[dataCnt - 1] = tempData;

                        prevX = drawX;
                        prevY = drawY;
                        prevW = drawW;
                    }
                    else
                    {
                        invalid_Cnt++;      // 不要なデータの数、確認用
                    }
                }
            }
            return retTraces;
        }

        /**** 全筆跡データを解析する ****/
        public InkML(String filename, int x_offset, int y_offset)
        {
            InkML_Trace[] rawTraces = new InkML_Trace[traceCnt];    // InkMLデータを変換しないで格納する
            XmlDocument xmlDocument = new XmlDocument();            // XMLファイルのパーサ

            this.x_offset = x_offset;
            this.y_offset = y_offset;

            try
            {
                // InkMLファイルの読込み
                xmlDocument.Load(filename);

                // InkMLファイルの内容の読込み
                XmlElement elem = xmlDocument.DocumentElement;

                if (elem.HasChildNodes == true)
                {
                    // InkMLファイルの階層的に各項目を読込む
                    XmlNode childNode = elem.FirstChild;

                    while (childNode != null)
                    {
                        if (childNode.Name == "annotation")
                        {
                            // 筆跡データ全体の幅/高さを取得
                            setTraceWH(childNode);
                        }
                        else if (childNode.Name == "inkml:definitions")
                        {
                            for (int cnt = 0; cnt < childNode.ChildNodes.Count; cnt++)
                            {
                                XmlNode dataNode = childNode.ChildNodes[cnt];
                                for (int chiCnt = 0; chiCnt < dataNode.Attributes.Count; chiCnt++)
                                {
                                    XmlNode node = dataNode.ChildNodes[chiCnt];
                                    if (node.Name == "inkml:timestamp")
                                    {
                                        // タイムスタンプの取得
                                    }
                                    else if (node.Name == "inkml:inkSource")
                                    {
                                        // インクソースの取得
                                    }
                                    else { }
                                }
                            }
                        }
                        else if (childNode.Name == "trace")
                        {
                            //  取得した筆跡データの追加
                            InkML_Trace tr_temp = getTraceData(childNode);
                            if (tr_temp.tp == InkML_type.PEN_DOWN)
                            {
                                traceCnt++;
                                Array.Resize(ref rawTraces, traceCnt);
                                rawTraces[traceCnt - 1] = tr_temp;
                            }
                        }
                        else { }
                        childNode = childNode.NextSibling;
                    }
                }
                // ここでbitmap用の座標と太さに変換する
                traces = convTraceData(rawTraces);
            }
            catch (System.Xml.XmlException)
            {
                // InkMLファイルが不良の為、エラー
            }
        }
    }

}
