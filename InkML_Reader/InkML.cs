using System;
using System.Xml;
using System.Linq;
using System.Diagnostics;
using System.Xml.Linq;
using System.Collections.Generic;

/**
 * @mainpage InkML
 * Read IoT Paper INK ML

MIT License

Copyright (c) 2023 Wacom co.,Ltd.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
 */

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
        F,                  // F: Force
        Z,                  // Z: Z座標
        OTx,                // Otx: X方向の傾き
        OTy,                // Oty: Y方向の傾き
        W,                  // W: ペンの太さ
        T,                  // T: Time
        maxSize
    };


    enum InkML_TraceChannelPrefixPlicit
    {
        traceChannelPrefixPlicit           = 0,                     // 明示的な値
        traceChannelPrefixSingleDifference,                         // １次差分
        traceChannelPrefixSecondDifference,                         // ２次差分
        maxSize,
    }

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
        // public double pos_mag;

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
            int traceDataCnt = 0;
            retTrace.data = new InkML_Data[dataCnt];

            bool bDebugPrint = false;
            int iDebugPrintNum = 4;
            bool bDebugPrintRetTracce = false;

            for (int attCnt = 0; attCnt < childNode.Attributes.Count; attCnt++)
            {
                XmlAttribute xmlAttr = childNode.Attributes[attCnt];
                if ((xmlAttr.Name == "type") && (xmlAttr.Value == "penUp"))
                {
                    // PenUpのデータは無効
                    retTrace.tp = InkML_type.PEN_UP;
                    traceDataCnt = 0;
                    break;
                }
                else if ((xmlAttr.Name == "type") && (xmlAttr.Value == "penDown"))
                {
                    // PenDownのデータは有効
                    retTrace.tp = InkML_type.PEN_DOWN;
                    // ","でデータを分割してTraceのデータを取り出す
                    string[] traceStr = (childNode.InnerText.Trim()).Split(',');
                    double prvX = 0, prvY = 0, prvW = 0;
                    double vx = 0, vy = 0, vw = 0;
                    InkML_TraceChannelPrefixPlicit traceMode = InkML_TraceChannelPrefixPlicit.maxSize;
                    InkML_TraceChannelPrefixPlicit lastTraceMode = InkML_TraceChannelPrefixPlicit.traceChannelPrefixPlicit;


                    foreach (String traceStr_div in traceStr)
                    {
                        InkML_Data data_temp;
                        bool secondDifferenceSecondData = false;
                        // データを分割してTrace内のの各データを取り出す

                        if (bDebugPrint&& (0<iDebugPrintNum)) { iDebugPrintNum-=1; Debug.WriteLine(" Trace Data : " + traceStr_div); }

                        double tempX = 0.0, tempY = 0.0, tempW = 0.0;

                        string[] dataStrSP = traceStr_div.Split(' ');
                        string[] dataStrSQ = traceStr_div.Split('\'');
                        string[] dataStrWQ = traceStr_div.Split('\"');
                        string[] dataStrSP22 = null;

                        traceMode = InkML_TraceChannelPrefixPlicit.maxSize;

                        if ((lastTraceMode == InkML_TraceChannelPrefixPlicit.traceChannelPrefixPlicit) &&
                            (dataStrSP.Count() == (int)InkML_traceFormat.maxSize))
                        {
                            // 明示的な
                            traceMode = InkML_TraceChannelPrefixPlicit.traceChannelPrefixPlicit;
                        }

                        if ((lastTraceMode == InkML_TraceChannelPrefixPlicit.traceChannelPrefixPlicit) &&
                            (dataStrSQ.Count() == ((int)InkML_traceFormat.maxSize) + 1))    // スプリットの関係で１つズレいる
                        {
                            // 差分
                            traceMode = InkML_TraceChannelPrefixPlicit.traceChannelPrefixSingleDifference;
                        }

                        if ((lastTraceMode == InkML_TraceChannelPrefixPlicit.traceChannelPrefixSingleDifference) &&
                            (dataStrWQ.Count() == ((int)InkML_traceFormat.maxSize) + 1))    // スプリットの関係１つズレいる
                        {
                            // ２次差分
                            traceMode = InkML_TraceChannelPrefixPlicit.traceChannelPrefixSecondDifference; 
                        }

                        if (lastTraceMode == InkML_TraceChannelPrefixPlicit.traceChannelPrefixSecondDifference)
                        {
                            // ２次差分の２回目以降のは、
                            // マイナス記号の前のスペースが省かれる時がある。
                            string dataInsertSP = "";
                            int forCount = 0;

                            foreach (char charData in traceStr_div)
                            {
                                if ((charData == '-' ) && (forCount != 0))
                                {
                                    // マイナス符号の前にスペースを挿入
                                    // 先頭の時を除く
                                    dataInsertSP = dataInsertSP + ' ';
                                }
                                dataInsertSP = dataInsertSP + charData;

                                forCount++;
                            }

                            // SPでスプリット
                            dataStrSP22 = dataInsertSP.Split(' ');

                            if (dataStrSP22.Count() == ((int)InkML_traceFormat.maxSize))
                            {
                                //要素数が合っている
                                traceMode = InkML_TraceChannelPrefixPlicit.traceChannelPrefixSecondDifference;
                                secondDifferenceSecondData = true;
                            }
                            else 
                            {
                                Debug.WriteLine("Fail decode at (2nd difference and 2nd data).");
                            }
                        }

                        if (traceMode == InkML_TraceChannelPrefixPlicit.traceChannelPrefixPlicit)
                        {
                            // " "で区切られてデータ
                            tempX = double.Parse(dataStrSP[(int)InkML_traceFormat.X]);      // X: X座標
                            tempY = double.Parse(dataStrSP[(int)InkML_traceFormat.Y]);      // Y: Y座標
                            tempW = double.Parse(dataStrSP[(int)InkML_traceFormat.W]);      // W: ペンの太さ
                            vx = 0.0;
                            vy = 0.0;
                            vw = 0.0;
                        }
                        else if (traceMode == InkML_TraceChannelPrefixPlicit.traceChannelPrefixSingleDifference)
                        {
                            // "'"を接頭に持つデータを"'"でスプリットしているため、位置がずれている
                            vx = double.Parse(dataStrSQ[(int)InkML_traceFormat.X + 1]);
                            vy = double.Parse(dataStrSQ[(int)InkML_traceFormat.Y + 1]);
                            vw = double.Parse(dataStrSQ[(int)InkML_traceFormat.W + 1]);
                            tempX = prvX + vx;                                            // X: X座標
                            tempY = prvY + vy;                                            // Y: Y座標
                            tempW = prvW + vw;                                            // W: ペンの太さ
                        }
                        else if (traceMode == InkML_TraceChannelPrefixPlicit.traceChannelPrefixSecondDifference)
                        {
                            if (secondDifferenceSecondData == false)
                            {
                                // 2次差分の最初のデータ
                                // """を接頭に持つデータを"""でスプリットしているため、位置がずれている
                                vx += double.Parse(dataStrWQ[(int)InkML_traceFormat.X + 1]);
                                vy += double.Parse(dataStrWQ[(int)InkML_traceFormat.Y + 1]);
                                vw += double.Parse(dataStrWQ[(int)InkML_traceFormat.W + 1]);
                            }
                            else
                            {
                                // 2次差分の2回目以降のデータ
                                vx += double.Parse(dataStrSP22[(int)InkML_traceFormat.X]);
                                vy += double.Parse(dataStrSP22[(int)InkML_traceFormat.Y]);
                                vw += double.Parse(dataStrSP22[(int)InkML_traceFormat.W]);
                            }
                            tempX = prvX + vx;                                            // X: X座標
                            tempY = prvY + vy;                                            // Y: Y座標
                            tempW = prvW + vw;                                            // W: ペンの太さ
                        }
                        else
                        {
                            Debug.WriteLine("Fali decode trace data.");
                            break;
                        }


                        data_temp.plotX = (uint)Math.Floor(tempX / this.pen_mag);
                        data_temp.plotY = (uint)((uint)(this.height_digi - tempY) / this.pen_mag);

                        // Bitmap描画用の座標を取得する（小数点以下は切捨て）
                        // Y座標はオフセットの影響を受けない
                        data_temp.x = (uint)Math.Floor(tempX / this.pen_mag) - this.x_offset;
                        data_temp.y = (uint)(this.height_paper - 1 - (uint)Math.Floor((this.height_digi - tempY) / this.pen_mag));

                        data_temp.w = (uint)Math.Floor(tempW / (this.pen_mag * 10));
                        /*
                        // 小数点第1位で四捨五入
                        data_temp.x = Math.Round( tempX / this.pen_mag ), MidpointRounding.AwayFromZero);
                        data_temp.y = Math.Round( tempY / this.pen_mag ), MidpointRounding.AwayFromZero);
                        data_temp.w = Math.Round( (tempW / (this.pen_mag + 10) ), MidpointRounding.AwayFromZero);       // ペン/消しゴムの太さ
                        */

                        prvX = tempX;
                        prvY = tempY;
                        prvW = tempW;
                        dataCnt++;
                        traceDataCnt++;
                        Array.Resize(ref retTrace.data, dataCnt);
                        retTrace.data[dataCnt - 1] = data_temp;

                        lastTraceMode = traceMode;
                    }// foreach
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

                    if (bDebugPrint) {
                        string strDebugMsg = "";
                        if (retTrace.brush == InkML_brushRef.PENCIL)
                        {
                            strDebugMsg = "Pen";
                        }
                        else if (retTrace.brush == InkML_brushRef.ERASER)
                        {
                            strDebugMsg = "Eraser";
                        }
                        
                        Debug.WriteLine(" brushRef : " + xmlAttr.Value + " " + strDebugMsg); 
                    }
                }
                else { }
            }

            if (bDebugPrintRetTracce &&
                (0 < retTrace.data.Count()))
            {
                Debug.WriteLine("retTrace data : ");

                foreach(InkML_Data dataTmp in retTrace.data)
                {
                    Debug.WriteLine(dataTmp.x + " " + dataTmp.y + " " + dataTmp.w + " " + dataTmp.plotX + " " + dataTmp.plotY);
                }
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
            const bool bDebugPrint = false;

            this.x_offset = x_offset;
            this.y_offset = y_offset;

            try
            {
                // InkMLファイルの読込み
                xmlDocument.Load(filename);

                // InkMLファイルの内容の読込み
                XmlElement elem = xmlDocument.DocumentElement;

                bool bRootElemIsInk = false;            // Root Element は ink
                bool bRootElemIsPaper = false;          // Root Element は paper

                if (bDebugPrint) { Debug.WriteLine("Element is " + elem.Name + "(" + elem.LocalName + ")"); }

                if(elem.LocalName == "ink")
                {
                    bRootElemIsInk = true;
                }
                if (elem.LocalName == "paper")
                {
                    bRootElemIsPaper = true;
                }

                if (elem.HasChildNodes == true)
                {
                    XmlNode childNode1 = elem.FirstChild;

                    while (childNode1 != null)
                    {
                        if (bDebugPrint) { Debug.WriteLine(" Node is " + childNode1.Name + "(" + childNode1.LocalName + ")"); }

                        if (bRootElemIsInk)
                        {
                            /* bRootElemIsInk */
                            if (childNode1.LocalName == "annotation")
                            {
                                // 筆跡データ全体の幅/高さを取得
                                setTraceWH(childNode1);
                            }
                            else
                            if (childNode1.LocalName == "definitions")
                            {
                                for (int cnt = 0; cnt < childNode1.ChildNodes.Count; cnt++)
                                {
                                    XmlNode dataNode = childNode1.ChildNodes[cnt];
                                    for (int chiCnt = 0; chiCnt < dataNode.Attributes.Count; chiCnt++)
                                    {
                                        XmlNode node = dataNode.ChildNodes[chiCnt];
                                        if (node.LocalName == "timestamp")
                                        {
                                            // タイムスタンプの取得
                                        }
                                        else if (node.LocalName == "inkSource")
                                        {
                                            // インクソースの取得
                                        }
                                        else { }
                                    }
                                }
                            }
                            else
                            if (childNode1.LocalName == "trace")
                            {
                                //  取得した筆跡データの追加
                                InkML_Trace tr_temp = getTraceData(childNode1);
                                if (tr_temp.tp == InkML_type.PEN_DOWN)
                                {
                                    traceCnt++;
                                    Array.Resize(ref rawTraces, traceCnt);
                                    rawTraces[traceCnt - 1] = tr_temp;
                                }
                            }
                            /* End of bRootElemIsInk */
                        }

                        if (bRootElemIsPaper == true)
                        {
                            /* bRootElemIsPaper */
                            XmlNode childNode2 = childNode1.FirstChild;

                            while (childNode2 != null)
                            {
                                if (bDebugPrint) { Debug.WriteLine("  Node is " + childNode2.Name + "(" + childNode2.LocalName + ")"); }

                                if (childNode2.LocalName == "annotation")
                                {
                                    // 筆跡データ全体の幅/高さを取得
                                    // setTraceWH(childNode2);　paperの時は anotation から取得するのは廃止
                                }
                                else
                                if (childNode2.LocalName == "definitions")
                                {
                                    XmlNode childNodeDef = childNode2.FirstChild;
                                    while (childNodeDef != null)
                                    {
                                        if (bDebugPrint) { Debug.WriteLine("   Node is " + childNodeDef.Name + "(" + childNodeDef.LocalName + ")"); }

                                        if (childNodeDef.LocalName == "canvas")
                                        {
                                            XmlNode childNodeCanvas = childNodeDef.FirstChild;
                                            while (childNodeCanvas != null)
                                            {
                                                if (bDebugPrint) { Debug.WriteLine("    Node is " + childNodeCanvas.Name + "(" + childNodeCanvas.LocalName + ")"); }

                                                if (childNodeCanvas.LocalName == "traceFormat")
                                                {
                                                    XmlNode childNodeTraceFormat = childNodeCanvas.FirstChild;
                                                    while (childNodeTraceFormat != null)
                                                    {
                                                        if (bDebugPrint) { Debug.WriteLine("     Node is " + childNodeTraceFormat.Name + "(" + childNodeTraceFormat.LocalName + ")"); }

                                                        bool bFinedX = false;
                                                        bool bFinedY = false;

                                                        for (int attCnt = 0; attCnt < childNodeTraceFormat.Attributes.Count; attCnt++)
                                                        {
                                                            XmlAttribute xmlAttr = childNodeTraceFormat.Attributes[attCnt];

                                                            if ((xmlAttr.Name == "name") && (xmlAttr.Value == "X"))
                                                            {
                                                                bFinedX = true;
                                                            }

                                                            if ((xmlAttr.Name == "name") && (xmlAttr.Value == "Y"))
                                                            {
                                                                bFinedY = true;
                                                            }
                                                        }

                                                        if (bFinedX || bFinedY)
                                                        {
                                                            for (int attCnt = 0; attCnt < childNodeTraceFormat.Attributes.Count; attCnt++)
                                                            {
                                                                XmlAttribute xmlAttr = childNodeTraceFormat.Attributes[attCnt];

                                                                if (xmlAttr.Name == "max")
                                                                {
                                                                    if (bFinedX)
                                                                    {
                                                                        width_paper = long.Parse(xmlAttr.Value);
                                                                        if (bDebugPrint) { Debug.WriteLine("     width_paper " + width_paper); }
                                                                    }
                                                                    if (bFinedY)
                                                                    {
                                                                        height_paper = long.Parse(xmlAttr.Value);
                                                                        if (bDebugPrint) { Debug.WriteLine("     height_paper " + height_paper); }
                                                                    }
                                                                }
                                                            }
                                                        }

                                                        childNodeTraceFormat = childNodeTraceFormat.NextSibling;
                                                    }
                                                }
                                                childNodeCanvas = childNodeCanvas.NextSibling;
                                            }
                                        } 
                                        else
                                        if (childNodeDef.LocalName == "context")
                                        {
                                            XmlNode childNodeContext = childNodeDef.FirstChild;
                                            while (childNodeContext != null)
                                            {
                                                if (bDebugPrint) { Debug.WriteLine("    Node is " + childNodeContext.Name + "(" + childNodeContext.LocalName + ")"); }

                                                if (childNodeContext.LocalName == "inkSource")
                                                {
                                                    XmlNode childNodeInkSource = childNodeContext.FirstChild;

                                                    while (childNodeInkSource != null)
                                                    {
                                                        if (bDebugPrint) { Debug.WriteLine("     Node is " + childNodeInkSource.Name + "(" + childNodeInkSource.LocalName + ")"); }

                                                        if (childNodeInkSource.LocalName == "traceFormat")
                                                        {
                                                            XmlNode childNodeTraceFormat = childNodeInkSource.FirstChild;

                                                            while (childNodeTraceFormat != null)
                                                            {
                                                                if (bDebugPrint) { Debug.WriteLine("      Node is " + childNodeTraceFormat.Name + "(" + childNodeTraceFormat.LocalName + ")"); }

                                                                if (childNodeTraceFormat.LocalName == "channel")
                                                                {
                                                                    bool bFinedX = false;
                                                                    bool bFinedY = false;

                                                                    for (int attCnt = 0; attCnt < childNodeTraceFormat.Attributes.Count; attCnt++)
                                                                    {
                                                                        XmlAttribute xmlAttr = childNodeTraceFormat.Attributes[attCnt];

                                                                        if ((xmlAttr.Name == "name") && (xmlAttr.Value == "X"))
                                                                        {
                                                                            bFinedX = true;
                                                                        }

                                                                        if ((xmlAttr.Name == "name") && (xmlAttr.Value == "Y"))
                                                                        {
                                                                            bFinedY = true;
                                                                        }
                                                                    }


                                                                    if (bFinedX || bFinedY)
                                                                    {
                                                                        for (int attCnt = 0; attCnt < childNodeTraceFormat.Attributes.Count; attCnt++)
                                                                        {
                                                                            XmlAttribute xmlAttr = childNodeTraceFormat.Attributes[attCnt];

                                                                            if (xmlAttr.Name == "max")
                                                                            {
                                                                                if (bFinedX)
                                                                                {
                                                                                    /* 単位と座標系は決め打ち */
                                                                                    width_digi = (long)(float.Parse(xmlAttr.Value) * 100);
                                                                                    if (bDebugPrint) { Debug.WriteLine("     width_digi " + width_digi); }
                                                                                }
                                                                                if (bFinedY)
                                                                                {
                                                                                    height_digi = (long)(float.Parse(xmlAttr.Value) * 100);
                                                                                    if (bDebugPrint) { Debug.WriteLine("     height_digi " + height_digi); }
                                                                                }
                                                                            }
                                                                        }
                                                                    }
                                                                }
                                                                childNodeTraceFormat = childNodeTraceFormat.NextSibling;
                                                            }
                                                        }
                                                        childNodeInkSource = childNodeInkSource.NextSibling;
                                                    }
                                                }
                                                childNodeContext = childNodeContext.NextSibling;
                                            }
                                        }
                                        childNodeDef = childNodeDef.NextSibling;
                                    }
                                }
                                else
                                if (childNode2.LocalName == "trace")
                                {
                                    if (0.0 < width_paper)
                                    {
                                        pen_mag = Math.Round((width_digi / width_paper), 2, MidpointRounding.AwayFromZero);           // 実際は、digi / 100 / paper * 1000、小数第二位で四捨五入
                                        
                                        if (bDebugPrint) { Debug.WriteLine("trace" + traceCnt); }

                                        //  取得した筆跡データの追加
                                        InkML_Trace tr_temp = getTraceData(childNode2);
                                        if (tr_temp.tp == InkML_type.PEN_DOWN)
                                        {
                                            traceCnt++;
                                            Array.Resize(ref rawTraces, traceCnt);
                                            rawTraces[traceCnt - 1] = tr_temp;
                                        }
                                    }
                                    else
                                    {
                                        Debug.WriteLine("ERROR pen_mag is 0.0");
                                    }
                                }

                                childNode2 = childNode2.NextSibling;
                            }// while



                            /* End of bRootElemIsPaper */
                        }
                        childNode1 = childNode1.NextSibling;
                    }
                }

                if (elem.HasChildNodes == true)
                {
                    // InkMLファイルの階層的に各項目を読込む
                    XmlNode childNode = elem.FirstChild;

                    while (childNode != null)
                    {
                        //if (childNode.Name == "annotation")
                        //{
                        //    // 筆跡データ全体の幅/高さを取得
                        //    setTraceWH(childNode);
                        //}
                        //else
                        //if (childNode.Name == "inkml:definitions")
                        //{
                        //    for (int cnt = 0; cnt < childNode.ChildNodes.Count; cnt++)
                        //    {
                        //        XmlNode dataNode = childNode.ChildNodes[cnt];
                        //        for (int chiCnt = 0; chiCnt < dataNode.Attributes.Count; chiCnt++)
                        //        {
                        //            XmlNode node = dataNode.ChildNodes[chiCnt];
                        //            if (node.Name == "inkml:timestamp")
                        //            {
                        //                // タイムスタンプの取得
                        //            }
                        //            else if (node.Name == "inkml:inkSource")
                        //            {
                        //                // インクソースの取得
                        //            }
                        //            else { }
                        //        }
                        //    }
                        //}
                        //else
                        //if ((childNode.Name == "trace") ||
                        //         (childNode.Name == "inkml:trace"))
                        //{
                        //    //  取得した筆跡データの追加
                        //    InkML_Trace tr_temp = getTraceData(childNode);
                        //    if (tr_temp.tp == InkML_type.PEN_DOWN)
                        //    {
                        //        traceCnt++;
                        //        Array.Resize(ref rawTraces, traceCnt);
                        //        rawTraces[traceCnt - 1] = tr_temp;
                        //    }
                        //}
                        //else { }
                        childNode = childNode.NextSibling;
                    }
                }



                // ここでbitmap用の座標と太さに変換する
                traces = convTraceData(rawTraces);
            }
            catch (System.Xml.XmlException)
            {
                // InkMLファイルが不良の為、エラー
                Debug.WriteLine("Fali inkML(xml) structure.");
            }
        }
    }

}
