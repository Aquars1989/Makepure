using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;

namespace Makepure
{
    /// <summary>
    /// 取樣點資訊
    /// </summary>
    public class Pick
    {
        /// <summary>
        /// 範圍最大值
        /// </summary>
        public static int MaxRange = 10000;

        /// <summary>
        /// 容許值最大值
        /// </summary>
        public static int MaxAllowance = 255;

        /// <summary>
        /// 取樣點位置
        /// </summary>
        public Point PickPoint { get; protected set; }

        /// <summary>
        /// 預覽圖位置
        /// </summary>
        public Point DisplayPoint { get; protected set; }

        /// <summary>
        /// 取樣顏色
        /// </summary>
        public Color Color { get; protected set; }

        private int _Allowance;
        /// <summary>
        /// 色差容許值
        /// </summary>
        public int Allowance
        {
            get { return _Allowance; }
            protected set
            {
                _Allowance = value;
                if (_Allowance > MaxAllowance) _Allowance = MaxAllowance;
                else if (_Allowance < 0) _Allowance = 0;
            }
        }

        private int _Range;
        /// <summary>
        /// 檢測範圍
        /// </summary>
        public int Range
        {
            get { return _Range; }
            protected set
            {
                _Range = value;
                if (_Allowance > MaxRange) _Allowance = MaxRange;
                else if (_Allowance < 0) _Allowance = 0;
            }
        }

        /// <summary>
        /// 紀錄符合值的地圖
        /// </summary>
        public BitArray UseMap { get; protected set; }

        /// <summary>
        /// 轉化器
        /// </summary>
        public class PureConverter
        {
            /// <summary>
            /// 採樣點列表
            /// </summary>
            private List<Pick> _PickInfos = new List<Pick>();

            /// <summary>
            /// 色彩差異暫存表
            /// </summary>
            private byte[] _ColorMap;

            /// <summary>
            /// 距離暫存表
            /// </summary>
            private short[] _DistanceMap;

            /// <summary>
            /// 紀錄除目前取樣點外符合值的暫存表
            /// </summary>
            private BitArray _UseMap;

            /// <summary>
            /// 主要取樣點Index
            /// </summary>
            public int CurrentPickIndex { get; private set; }

            private ImageObject _BaseImage;
            /// <summary>
            /// 原始圖片
            /// </summary>
            public ImageObject BaseImage
            {
                get { return _BaseImage; }
                set
                {
                    if (BaseImage == value) return;
                    if (ConvertImage != null)
                    {
                        ConvertImage.Dispose();
                        ConvertImage = null;
                    }

                    BaseImage = value;
                    if (BaseImage == null)
                    {
                        _ColorMap = null;
                        _DistanceMap = null;
                        _UseMap = null;
                    }
                    else
                    {
                        ConvertImage = BaseImage.Copy();
                        _ColorMap = new byte[BaseImage.PixelCount];
                        _DistanceMap = new short[BaseImage.PixelCount];
                        _UseMap = new BitArray(BaseImage.PixelCount);
                    }
                    ClearPick();
                }
            }

            /// <summary>
            /// 轉化圖片
            /// </summary>
            public ImageObject ConvertImage { get; private set; }

            /// <summary>
            /// 取得指定索引的採樣點
            /// </summary>
            /// <param name="index">採樣點索引</param>
            /// <returns>採樣點物件</returns>
            public Pick GetPick(int index)
            {
                return _PickInfos[index];
            }

            /// <summary>
            /// 取得取樣點列表
            /// </summary>
            /// <returns>取樣點列表</returns>
            public List<Pick>.Enumerator GetPicks()
            {
                return _PickInfos.GetEnumerator();
            }

            /// <summary>
            /// 使用採樣暫存地圖增加主要採樣點的數值
            /// </summary>
            public void CurrentValuePlus(int allowance, int range)
            {
                if (CurrentPickIndex >= _PickInfos.Count || CurrentPickIndex < 0) return;
                Pick currentPick = _PickInfos[CurrentPickIndex];

                CurrentValueSet(currentPick.Allowance + allowance, currentPick.Range + range);
            }

            /// <summary>
            /// 使用採樣暫存地圖調整主要採樣點的數值
            /// </summary>
            public void CurrentValueSet(int allowance, int range)
            {
                if (CurrentPickIndex >= _PickInfos.Count || CurrentPickIndex < 0) return;
                Pick currentPick = _PickInfos[CurrentPickIndex];

                int oldAllowance = currentPick.Allowance;
                int oldRange = currentPick.Range;
                currentPick.Allowance = allowance;
                currentPick.Range = range;
                if (oldAllowance == currentPick.Allowance && currentPick.Range == range) return;

                int cot = BaseImage.PixelCount;
                IntPtr basePtr = BaseImage.LockBitsAndGetScan0(ImageLockMode.ReadOnly);
                IntPtr convertPtr = ConvertImage.LockBitsAndGetScan0(ImageLockMode.WriteOnly);
                unsafe
                {
                    byte* baseP = (byte*)basePtr.ToPointer();
                    byte* convertP = (byte*)convertPtr.ToPointer();
                    for (int i = 0; i < cot; i++)
                    {
                        if (_ColorMap[i] <= currentPick.Allowance && _DistanceMap[i] <= currentPick.Range)
                        {
                            if (!_UseMap[i] && !currentPick.UseMap[i])
                            {
                                convertP[0] = baseP[0];
                                convertP[1] = baseP[1];
                                convertP[2] = baseP[2];
                            }
                            currentPick.UseMap[i] = true;
                        }
                        else
                        {
                            if (!_UseMap[i] && currentPick.UseMap[i])
                            {
                                byte v = (byte)((baseP[0] + baseP[1] + baseP[2]) / 3);
                                convertP[0] = v;
                                convertP[1] = v;
                                convertP[2] = v;
                            }
                            currentPick.UseMap[i] = false;
                        }
                        baseP += 4;
                        convertP += 4;
                    }
                }
                BaseImage.UnlockBits();
                ConvertImage.UnlockBits();
            }

            /// <summary>
            /// 設定主要採樣點並建立採樣暫存地圖
            /// </summary>
            /// <param name="currentPickIndex">主要採樣點索引</param>
            public void SetCurrentIndex(int currentPickIndex)
            {
                if (currentPickIndex >= _PickInfos.Count || CurrentPickIndex < 0) return;

                Pick currentPick = _PickInfos[currentPickIndex];
                IntPtr basePtr = BaseImage.LockBitsAndGetScan0(ImageLockMode.ReadOnly);
                unsafe
                {
                    byte* baseP = (byte*)basePtr.ToPointer();
                    double maxDistance = MaxRange / BaseImage.MaxDistance;
                    int i = 0;
                    for (int y = 0; y < BaseImage.Height; y++)
                    {
                        for (int x = 0; x < BaseImage.Width; x++)
                        {
                            byte r = baseP[2];
                            byte g = baseP[1];
                            byte b = baseP[0];

                            int dR = Math.Abs(currentPick.Color.R - r);
                            int dG = Math.Abs(currentPick.Color.G - g);
                            int dB = Math.Abs(currentPick.Color.B - b);

                            short d = (short)(Function.GetDistance(x, y, currentPick.PickPoint.X, currentPick.PickPoint.Y) * maxDistance);
                            _ColorMap[i] = (byte)Math.Max(Math.Max(dR, dG), dB);
                            _DistanceMap[i] = d;
                            baseP += 4;
                            i++;
                        }
                    }
                }

                _UseMap.SetAll(false);
                foreach (Pick pick in _PickInfos)
                {
                    if (pick == currentPick) continue;
                    _UseMap.Or(pick.UseMap);
                }
                BaseImage.UnlockBits();
            }

            /// <summary>
            /// 刪除取樣點
            /// </summary>
            /// <param name="removePickIndex">移除取樣點索引</param>
            public void RemovePick(int removePickIndex)
            {
                if (removePickIndex >= _PickInfos.Count || removePickIndex < 0) return;

                Pick removePick = _PickInfos[removePickIndex];
                _PickInfos.Remove(removePick);
                CurrentPickIndex = -1;

                int cot = ConvertImage.PixelCount;
                BitArray useMap = new BitArray(cot);
                foreach (Pick pick in _PickInfos)
                {
                    useMap.Or(pick.UseMap);
                }

                IntPtr convertPtr = ConvertImage.LockBitsAndGetScan0(ImageLockMode.WriteOnly);
                unsafe
                {
                    byte* convertP = (byte*)convertPtr.ToPointer();
                    for (int i = 0; i < cot; i++)
                    {
                        if (removePick.UseMap[i] && !useMap[i])
                        {
                            byte v = (byte)((convertP[0] + convertP[1] + convertP[2]) / 3);
                            convertP[0] = v;
                            convertP[1] = v;
                            convertP[2] = v;
                        }
                        convertP += 4;
                    }
                }
                ConvertImage.UnlockBits();
            }

            /// <summary>
            /// 清除取樣點
            /// </summary>
            public void ClearPick()
            {
                _UseMap.SetAll(false);
                _PickInfos.Clear();
                CurrentPickIndex = -1;

                int cot = BaseImage.PixelCount;
                IntPtr basePtr = BaseImage.LockBitsAndGetScan0(ImageLockMode.ReadOnly);
                IntPtr convertPtr = ConvertImage.LockBitsAndGetScan0(ImageLockMode.WriteOnly);
                unsafe
                {
                    byte* baseP = (byte*)basePtr.ToPointer();
                    byte* convertP = (byte*)convertPtr.ToPointer();
                    for (int i = 0; i < cot; i++)
                    {
                        byte v = (byte)((baseP[0] + baseP[1] + baseP[2]) / 3);
                        convertP[0] = v;
                        convertP[1] = v;
                        convertP[2] = v;
                        baseP += 4;
                        convertP += 4;
                    }
                }
                BaseImage.UnlockBits();
                ConvertImage.UnlockBits();
            }
        }
    }
}
