using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace Makepure
{
    public partial class MainForm : Form
    {
        /// <summary>
        /// 控制區域半徑
        /// </summary>
        private const int _AllowanceHalfWidth = 20;

        /// <summary>
        /// 說明的顏色標示點寬度
        /// </summary>
        private const int _ColorPotWidth = 20;

        /// <summary>
        /// 隱藏的滑鼠圖示
        /// </summary>
        private static Cursor _NullCursor = new Cursor(new Bitmap(1, 1).GetHicon());

        /// <summary>
        /// 資訊文字字型
        /// </summary>
        private Font _InfoFont2 = new Font("微軟正黑體", 18);

        /// <summary>
        /// 資訊文字字型
        /// </summary>
        private Font _InfoFont = new Font("微軟正黑體", 12);

        /// <summary>
        /// 資訊文字配置
        /// </summary>
        private StringFormat _InfoFormat = new StringFormat()
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };

        /// <summary>
        /// 資訊文字配置2
        /// </summary>
        private StringFormat _InfoFormat2 = new StringFormat()
        {
            Alignment = StringAlignment.Near,
            LineAlignment = StringAlignment.Center
        };

        /// <summary>
        /// 圖片與邊界間距
        /// </summary>
        private Padding _ImagePadding = new Padding(20, 50, 20, 20);

        /// <summary>
        /// 滑鼠是否按下
        /// </summary>
        private bool _MouseDown;

        /// <summary>
        /// 滑鼠按下點
        /// </summary>
        private Point _MouseDownPoint;

        /// <summary>
        /// 目前指向採樣點索引
        /// </summary>
        private int _HoverIndex = -1;

        /// <summary>
        /// 目前指向顏色
        /// </summary>
        private Color _HoverColor = Color.Empty;

        /// <summary>
        /// 設定中採樣點索引
        /// </summary>
        private int _PickInfoIndex = 0;

        /// <summary>
        /// 採樣點列表
        /// </summary>
        private List<PickInfo> _PickInfos = new List<PickInfo>();

        /// <summary>
        /// 圖片二次縮放值
        /// </summary>
        private double _Scale = 1;

        /// <summary>
        /// 原始圖片
        /// </summary>
        private Bitmap _BaseImage = null;

        /// <summary>
        /// 預覽圖片
        /// </summary>
        private Bitmap _ScaleImage = null;

        /// <summary>
        /// 預覽變更圖片
        /// </summary>
        private Bitmap _ConvertedImage = null;

        /// <summary>
        /// 色彩差異暫存表
        /// </summary>
        private byte[] _ColorMap;

        /// <summary>
        /// 距離暫存表
        /// </summary>
        private short[] _DistanceMap;

        /// <summary>
        /// 紀錄除目前取樣點外符合值的地圖
        /// </summary>
        private BitArray _UseMap;

        /// <summary>
        /// 圖片繪製位置
        /// </summary>
        private Rectangle _ImageRect = new Rectangle();

        /// <summary>
        /// 說明繪製位置
        /// </summary>
        private Rectangle _InfoRect = new Rectangle();

        public MainForm(string[] args)
        {
            InitializeComponent();

            PropertyInfo doubleBuffered = splitContainer1.Panel1.GetType().GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
            if (doubleBuffered != null)
            {
                doubleBuffered.SetValue(splitContainer1.Panel1, true, null);
                doubleBuffered.SetValue(splitContainer1.Panel2, true, null);
            }

            if (args.Length > 0 && File.Exists(args[0]))
            {
                LoadData(args[0]);
            }
        }

        private void tsbtnOpen_Click(object sender, EventArgs e)
        {
            if (openFileDialog1.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                LoadData(openFileDialog1.FileName);
            }
        }

        private void tsbtnSave_Click(object sender, EventArgs e)
        {
            if (saveFileDialog1.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                using (Bitmap savetImage = new Bitmap(_BaseImage.Width, _BaseImage.Height))
                {
                    PickColor(_BaseImage, savetImage, _PickInfos, 0);
                    string type = Path.GetExtension(saveFileDialog1.FileName).ToLower();
                    switch (type)
                    {
                        case ".bmp":
                            savetImage.Save(saveFileDialog1.FileName, ImageFormat.Bmp);
                            break;
                        case ".jpg":
                            savetImage.Save(saveFileDialog1.FileName, ImageFormat.Jpeg);
                            break;
                        case ".gif":
                            savetImage.Save(saveFileDialog1.FileName, ImageFormat.Gif);
                            break;
                        case ".png":
                            savetImage.Save(saveFileDialog1.FileName, ImageFormat.Png);
                            break;
                        default:
                            goto case ".bmp";
                    }
                }
            }
        }

        private void splitContainer1_SizeChanged(object sender, EventArgs e)
        {
            SetImageSize();
        }

        private void splitContainer1_Panel1_MouseMove(object sender, MouseEventArgs e)
        {
            if (_MouseDown)
            {
                PickInfo pictInfo = _PickInfos[_PickInfoIndex];
                int offset = e.X - _MouseDownPoint.X;
                if (offset < 0) offset = 0;
                else if (offset > 255) offset = 255;

                int range = _MouseDownPoint.Y - e.Y;
                if (offset < 0) offset = 0;
                else if (offset > 255) offset = 255;

                PickColor(_ScaleImage, _ConvertedImage, _ColorMap, _DistanceMap, pictInfo);
                pictInfo.Allowance = offset;
                splitContainer1.Panel1.Invalidate(_ImageRect);
                splitContainer1.Panel2.Invalidate(_ImageRect);
            }
            else
            {
                _HoverIndex = -1;
                if (InRectangle(e.Location, _ImageRect))
                {
                    bool fid = false;
                    double checkDist = Math.Pow(_AllowanceHalfWidth, 2); // 控制區域半徑平方
                    for (int i = 0; i < _PickInfos.Count; i++)
                    {
                        PickInfo pickInfo = _PickInfos[i];
                        if (Math.Pow(pickInfo.DisplayPoint.X - e.X, 2) + Math.Pow(pickInfo.DisplayPoint.Y - e.Y, 2) < checkDist)
                        {
                            Cursor = Cursors.Hand;
                            _HoverColor = pickInfo.Color;
                            _HoverIndex = i;
                            fid = true;
                            break;
                        }
                    }

                    if (!fid)
                    {
                        int x = (int)((e.X - _ImageRect.Left) * _Scale);
                        int y = (int)((e.Y - _ImageRect.Top) * _Scale);
                        if (x >= _ScaleImage.Width) x = _ScaleImage.Width - 1;
                        if (y >= _ScaleImage.Height) y = _ScaleImage.Height - 1;
                        _HoverColor = _ScaleImage.GetPixel(x, y);
                        Cursor = Cursors.Cross;
                    }
                }
                else
                {
                    _HoverColor = Color.Empty;
                    Cursor = Cursors.Default;
                }
            }
            splitContainer1.Panel1.Invalidate(_InfoRect);
        }

        private void splitContainer1_Panel1_MouseDown(object sender, MouseEventArgs e)
        {
            if (InRectangle(e.Location, _ImageRect))
            {
                if (_HoverIndex >= 0)
                {
                    switch (e.Button)
                    {
                        case System.Windows.Forms.MouseButtons.Left:
                            _MouseDown = true;
                            _PickInfoIndex = _HoverIndex;
                            _MouseDownPoint = new Point(e.Location.X - _PickInfos[_HoverIndex].Allowance, e.Location.Y);
                            Cursor = _NullCursor;
                            break;
                        case System.Windows.Forms.MouseButtons.Right:
                            _PickInfos.RemoveAt(_HoverIndex);
                            _HoverIndex = -1;
                            PickColor(_ScaleImage, _ConvertedImage, _PickInfos, -1);
                            splitContainer1.Invalidate(true);
                            break;
                    }
                }
                else
                {
                    switch (e.Button)
                    {
                        case System.Windows.Forms.MouseButtons.Left:
                            _MouseDown = true;
                            _MouseDownPoint = e.Location;
                            int x = (int)((e.X - _ImageRect.Left) * _Scale);
                            int y = (int)((e.Y - _ImageRect.Top) * _Scale);
                            if (x >= _ScaleImage.Width) x = _ScaleImage.Width - 1;
                            if (y >= _ScaleImage.Height) y = _ScaleImage.Height - 1;
                            Point pot = new Point(x, y);
                            _PickInfos.Add(new PickInfo()
                            {
                                Allowance = 0,
                                PickPoint = pot,
                                DisplayPoint = e.Location,
                                Color = _HoverColor,
                                UseMap = new BitArray(_ScaleImage.Width * _ScaleImage.Height)
                            });
                            _PickInfoIndex = _PickInfos.Count - 1;

                            BuildMap(_ColorMap, _DistanceMap, _ScaleImage, _PickInfos[_PickInfoIndex]);
                            PickColor(_ScaleImage, _ConvertedImage, _ColorMap, _DistanceMap, _PickInfos[_PickInfoIndex]);
                            splitContainer1.Invalidate(true);
                            Cursor = _NullCursor;
                            break;
                    }
                }
            }
        }

        private void splitContainer1_Panel1_MouseUp(object sender, MouseEventArgs e)
        {
            if (_MouseDown)
            {
                _MouseDown = false;
                Cursor.Position = splitContainer1.Panel1.PointToScreen(_PickInfos[_PickInfoIndex].DisplayPoint);
                Cursor = Cursors.Default;
            }
        }

        private void splitContainer1_Panel1_Paint(object sender, PaintEventArgs e)
        {
            if (_ScaleImage != null)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

                if (_HoverColor == Color.Empty)
                {
                    e.Graphics.DrawString("點選圖片取樣顏色", _InfoFont, Brushes.Blue, _InfoRect, _InfoFormat2);
                }
                else
                {
                    using (SolidBrush brush = new SolidBrush(_HoverColor))
                    {
                        Rectangle rectEll = new Rectangle(_InfoRect.Left + 2, _InfoRect.Top + (_InfoRect.Height - _ColorPotWidth) / 2, _ColorPotWidth, _ColorPotWidth);

                        e.Graphics.FillEllipse(Brushes.Gray, rectEll);
                        rectEll.Offset(-2, -2);
                        e.Graphics.FillEllipse(brush, rectEll);

                        string info;
                        if (_MouseDown)
                        {
                            info = string.Format("容許值{0}(移動滑鼠調整容許值 <<減少  增加>>)", _PickInfos[_PickInfoIndex].Allowance);
                        }
                        else if (_HoverIndex >= 0)
                        {
                            info = string.Format("容許值{0}(左鍵:調整容許值 右鍵:刪除採樣點)", _PickInfos[_HoverIndex].Allowance);
                        }
                        else
                        {
                            info = "左鍵:設定採樣容許值";
                        }
                        Rectangle rectInfo = new Rectangle(_InfoRect.Left + _ColorPotWidth + 5, _InfoRect.Top, _InfoRect.Width - _ColorPotWidth - 5, _InfoRect.Height);
                        e.Graphics.DrawString(info, _InfoFont, Brushes.Blue, rectInfo, _InfoFormat2);
                    }
                }

                e.Graphics.DrawImage(_ScaleImage, _ImageRect);
                foreach (PickInfo pickInfo in _PickInfos)
                {
                    Rectangle rectFull = new Rectangle(pickInfo.DisplayPoint.X - _AllowanceHalfWidth, pickInfo.DisplayPoint.Y - _AllowanceHalfWidth, _AllowanceHalfWidth * 2, _AllowanceHalfWidth * 2);
                    using (SolidBrush fillEllipseBrush = new SolidBrush(Color.FromArgb(140, 255 - pickInfo.Color.R, 255 - pickInfo.Color.G, 255 - pickInfo.Color.B)))
                    {
                        e.Graphics.FillEllipse(fillEllipseBrush, rectFull);
                    }

                    int allowWid = (int)(_AllowanceHalfWidth * (pickInfo.Allowance / 255F));
                    if (allowWid > 0)
                    {
                        Rectangle rectAllowance = new Rectangle(pickInfo.DisplayPoint.X - allowWid, pickInfo.DisplayPoint.Y - allowWid, allowWid * 2, allowWid * 2);
                        using (SolidBrush allowanceBrush = new SolidBrush(Color.FromArgb(200, pickInfo.Color)))
                        {
                            e.Graphics.FillEllipse(allowanceBrush, rectAllowance);
                        }
                    }
                    e.Graphics.DrawEllipse(Pens.Red, rectFull);
                }
            }
            else
            {
                e.Graphics.DrawString("選擇開啟圖片或將檔案拖曳至此區域", _InfoFont, Brushes.Red, splitContainer1.Panel1.ClientRectangle, _InfoFormat);
            }
        }

        private void splitContainer1_Panel2_Paint(object sender, PaintEventArgs e)
        {
            if (_ConvertedImage != null)
            {
                e.Graphics.DrawString("預覽", _InfoFont2, Brushes.Maroon, 5, 5);
                e.Graphics.DrawImage(_ConvertedImage, _ImageRect);
            }
            else
            {
                e.Graphics.DrawString("選擇開啟圖片或將檔案拖曳至此區域", _InfoFont, Brushes.Red, splitContainer1.Panel2.ClientRectangle, _InfoFormat);
            }
        }

        private void splitContainer1_DragDrop(object sender, DragEventArgs e)
        {
            string[] s = (string[])e.Data.GetData(DataFormats.FileDrop, false);
            if (s.Length > 0)
            {
                LoadData(s[0]);
            }
        }

        private void splitContainer1_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.All;
            else
                e.Effect = DragDropEffects.None;
        }

        /// <summary>
        /// 判斷點是否在矩形內
        /// </summary>
        /// <param name="point">判斷點</param>
        /// <param name="rectangle">矩形</param>
        /// <returns>點是否在矩型內</returns>
        private bool InRectangle(Point point, Rectangle rectangle)
        {
            return point.X >= rectangle.Left && point.X <= rectangle.Left + rectangle.Width &&
                   point.Y >= rectangle.Top && point.Y <= rectangle.Top + rectangle.Height;
        }

        /// <summary>
        /// 開啟指定圖片
        /// </summary>
        /// <param name="path">圖片路徑</param>
        /// <returns>是否成功</returns>
        private bool LoadData(string path)
        {
            Bitmap tempImage;
            try
            {
                tempImage = new Bitmap(path);
            }
            catch
            {
                MessageBox.Show("圖片載入失敗。", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            if (_BaseImage != null) _BaseImage.Dispose();
            if (_ScaleImage != null) _ScaleImage.Dispose();
            if (_ConvertedImage != null) _ConvertedImage.Dispose();

            _PickInfos.Clear();
            _BaseImage = new Bitmap(tempImage);
            double maxHei = 500, maxWid = 500;
            double scale = Math.Max(_BaseImage.Height / maxHei, _BaseImage.Width / maxWid);
            Size newSize = new Size((int)(_BaseImage.Width / scale), (int)(_BaseImage.Height / scale));
            _ScaleImage = new Bitmap(_BaseImage, newSize);           //產生縮小預覽圖片
            _ConvertedImage = new Bitmap(_ScaleImage.Width, _ScaleImage.Height);

            int mapLen = _ScaleImage.Width * _ScaleImage.Height;
            _ColorMap = new byte[mapLen]; //色彩差異暫存表配置大小
            _DistanceMap = new short[mapLen];
            _UseMap = new BitArray(mapLen);
            PickColor(_ScaleImage, _ConvertedImage, _PickInfos, 0);
            SetImageSize();
            saveFileDialog1.InitialDirectory = Path.GetDirectoryName(path);
            tsbtnSave.Enabled = true;
            return true;
        }

        /// <summary>
        /// 調整圖片縮放值
        /// </summary>
        private void SetImageSize()
        {
            if (_ScaleImage == null) return;

            double maxHei = splitContainer1.Panel1.Height - _ImagePadding.Vertical;
            double maxWid = splitContainer1.Panel1.Width - _ImagePadding.Horizontal;
            _Scale = Math.Max(_ScaleImage.Height / maxHei, _ScaleImage.Width / maxWid);

            Size size = new Size((int)(_ScaleImage.Width / _Scale), (int)(_ScaleImage.Height / _Scale));
            int locX = (splitContainer1.Panel1.Width - size.Width) / 2;
            int locY = (splitContainer1.Panel1.Height - size.Height) / 2;

            if (locX < _ImagePadding.Left) locX = _ImagePadding.Left;
            if (locY < _ImagePadding.Top) locY = _ImagePadding.Top;
            Point loc = new Point(locX, locY);
            _ImageRect = new Rectangle(loc, size);
            _InfoRect = new Rectangle(_ImagePadding.Left, 5, splitContainer1.Panel1.Width - _ImagePadding.Horizontal, _ImagePadding.Top - 10);
            foreach (PickInfo pickInfo in _PickInfos)
            {
                int x = (int)(pickInfo.PickPoint.X / _Scale) + _ImageRect.Left;
                int y = (int)(pickInfo.PickPoint.Y / _Scale) + _ImageRect.Top;
                Point pot = new Point(x, y);
                pickInfo.DisplayPoint = new Point(x, y);
            }

            splitContainer1.Invalidate(true);
        }

        /// <summary>
        /// 建立暫存表,加快調整速度
        /// </summary>
        /// <param name="colorMap">色彩差異暫存表</param>
        /// <param name="distanceMap">距離暫存表</param>
        /// <param name="baseImage">原始圖</param>
        /// <param name="currentPick">目前取樣點</param>
        private void BuildMap(byte[] colorMap, short[] distanceMap, BitArray useMap, Bitmap baseImage, List<PickInfo> allPick, PickInfo currentPick)
        {
            unsafe
            {
                Rectangle rect = new Rectangle(0, 0, baseImage.Width, baseImage.Height);
                BitmapData baseData = baseImage.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                IntPtr basePtr = baseData.Scan0;
                byte* baseP = (byte*)basePtr.ToPointer();
                double maxDistance = Math.Sqrt(Math.Pow(baseImage.Width, 2) * Math.Pow(baseImage.Height, 2)) / 1000;
                int i = 0;
                for (int y = 0; y < baseImage.Height; y++)
                {
                    for (int x = 0; x < baseImage.Width; x++)
                    {
                        byte r = baseP[2];
                        byte g = baseP[1];
                        byte b = baseP[0];

                        int dR = Math.Abs(currentPick.Color.R - r);
                        int dG = Math.Abs(currentPick.Color.G - g);
                        int dB = Math.Abs(currentPick.Color.B - b);

                        short z = (short)(Math.Sqrt(Math.Pow(currentPick.PickPoint.X - x, 2) + Math.Pow(currentPick.PickPoint.Y - y, 2)) * maxDistance);
                        colorMap[i] = (byte)Math.Max(Math.Max(dR, dG), dB);
                        distanceMap[i] = z;
                        baseP += 4;
                        i++;
                    }
                }
                baseImage.UnlockBits(baseData);
            }
        }

        /// <summary>
        /// 產生濾色後的圖片
        /// </summary>
        /// <param name="baseImage">原始圖片</param>
        /// <param name="convertImage">處理後圖片</param>
        /// <param name="pickInfos">取樣點列表</param>
        /// <param name="mode">模式 0:完整　1:增加 -1:減少</param>
        private void PickColor(Bitmap baseImage, Bitmap convertImage, List<PickInfo> pickInfos, int mode)
        {
            unsafe
            {
                Rectangle rect = new Rectangle(0, 0, baseImage.Width, baseImage.Height);
                int cot = baseImage.Height * baseImage.Width;
                BitmapData baseData = baseImage.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                BitmapData convertData = convertImage.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                IntPtr basePtr = baseData.Scan0;
                IntPtr convertPtr = convertData.Scan0;
                byte* baseP = (byte*)basePtr.ToPointer();
                byte* convertP = (byte*)convertPtr.ToPointer();

                for (int i = 0; i < cot; i++)
                {
                    byte r2 = convertP[2];
                    byte g2 = convertP[1];
                    byte b2 = convertP[0];
                    bool gray = r2 == g2 && g2 == b2;
                    if (mode == 0 || (mode > 0 && gray) || (mode < 0 && !gray))
                    {
                        byte r = baseP[2];
                        byte g = baseP[1];
                        byte b = baseP[0];
                        convertP[3] = baseP[3];

                        bool match = false;
                        foreach (var pickInfo in pickInfos)
                        {
                            if (Math.Abs(pickInfo.Color.R - r) <= pickInfo.Allowance &&
                                Math.Abs(pickInfo.Color.G - g) <= pickInfo.Allowance &&
                                Math.Abs(pickInfo.Color.B - b) <= pickInfo.Allowance)
                            {
                                match = true;
                                break;
                            }
                        }

                        if (match)
                        {
                            convertP[0] = b;
                            convertP[1] = g;
                            convertP[2] = r;
                        }
                        else
                        {
                            byte v = (byte)((r + g + b) / 3);
                            convertP[0] = v;
                            convertP[1] = v;
                            convertP[2] = v;
                        }
                    }

                    convertP += 4;
                    baseP += 4;
                }
                baseImage.UnlockBits(baseData);
                convertImage.UnlockBits(convertData);
            }
        }

        /// <summary>
        /// 使用色彩差異暫存表產生濾色後的圖片
        /// </summary>
        /// <param name="baseImage">原始圖片</param>
        /// <param name="convertImage">處理後圖片</param>
        /// <param name="map">色彩差異暫存表</param>
        /// <param name="pickInfos">原始容許值</param>
        private void PickColor(Bitmap baseImage, Bitmap convertImage, byte[] map, short[] distance, PickInfo pickInfo)
        {
            unsafe
            {
                Rectangle rect = new Rectangle(0, 0, baseImage.Width, baseImage.Height);
                int cot = baseImage.Height * baseImage.Width;
                BitmapData baseData = baseImage.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                BitmapData convertData = convertImage.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                IntPtr basePtr = baseData.Scan0;
                IntPtr convertPtr = convertData.Scan0;
                byte* baseP = (byte*)basePtr.ToPointer();
                byte* convertP = (byte*)convertPtr.ToPointer();
                for (int i = 0; i < cot; i++)
                {
                    if (map[i] <= pickInfo.Allowance)
                    {
                        if (!pickInfo.UseMap[i])
                        {
                            convertP[0] = baseP[2];
                            convertP[1] = baseP[1];
                            convertP[2] = baseP[0];
                            pickInfo.UseMap[i] = true;
                        }
                    }
                    else
                    {
                        byte v = (byte)((baseP[0] + baseP[1] + baseP[2]) / 3);
                        convertP[0] = v;
                        convertP[1] = v;
                        convertP[2] = v;
                        pickInfo.UseMap[i] = false;
                    }
                    baseP += 4;
                    convertP += 4;
                }
                baseImage.UnlockBits(baseData);
                convertImage.UnlockBits(convertData);
            }
        }

        /// <summary>
        /// 取樣點資訊
        /// </summary>
        private class PickInfo
        {
            /// <summary>
            /// 取樣點位置
            /// </summary>
            public Point PickPoint;

            /// <summary>
            /// 預覽圖位置
            /// </summary>
            public Point DisplayPoint;

            /// <summary>
            /// 取樣顏色
            /// </summary>
            public Color Color;

            /// <summary>
            /// 色差容許值
            /// </summary>
            public int Allowance;

            /// <summary>
            /// 檢測範圍
            /// </summary>
            public int Range;

            /// <summary>
            /// 紀錄符合值的地圖
            /// </summary>
            public BitArray UseMap;
        }
    }
}
