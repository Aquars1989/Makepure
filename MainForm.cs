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
        /// 控制區域中心半徑
        /// </summary>
        private const int _AllowanceCenterWidth = 4;

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
        /// 圖片繪製位置
        /// </summary>
        private Rectangle _ImageRect = new Rectangle();

        /// <summary>
        /// 說明繪製位置
        /// </summary>
        private Rectangle _InfoRect = new Rectangle();

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
        /// 圖片二次縮放值
        /// </summary>
        private double _Scale = 1;

        /// <summary>
        /// 原始圖片
        /// </summary>
        private ImageObject _BaseImage = null;

        /// <summary>
        /// 預覽圖片
        /// </summary>
        private ImageObject _ScaleImage = null;

        /// <summary>
        /// 濾色轉化器
        /// </summary>
        private Pick.PureConverter _Converter = new Pick.PureConverter();

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
                using (Pick.PureConverter converter = new Pick.PureConverter())
                {
                    converter.BaseImage = _BaseImage;

                    double scale = (double)_BaseImage.Width / _ScaleImage.Width;
                    foreach (Pick pick in _Converter.GetPickList())
                    {
                        Point basePoint = new Point((int)(pick.PickPoint.X * scale), (int)(pick.PickPoint.Y * scale));
                        converter.AddPick(pick.Color, basePoint, pick.Allowance, pick.Range);
                    }

                    string type = Path.GetExtension(saveFileDialog1.FileName).ToLower();
                    switch (type)
                    {
                        case ".bmp":
                            converter.ConvertImage.Save(saveFileDialog1.FileName, ImageFormat.Bmp);
                            break;
                        case ".jpg":
                            converter.ConvertImage.Save(saveFileDialog1.FileName, ImageFormat.Jpeg);
                            break;
                        case ".gif":
                            converter.ConvertImage.Save(saveFileDialog1.FileName, ImageFormat.Gif);
                            break;
                        case ".png":
                            converter.ConvertImage.Save(saveFileDialog1.FileName, ImageFormat.Png);
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
                int dX = e.X - _MouseDownPoint.X;
                int dY = e.Y - _MouseDownPoint.Y;
                Cursor.Position = new Point(Cursor.Position.X - dX, Cursor.Position.Y - dY);

                _Converter.CurrentValuePlus(-dY, dX * 10);
                splitContainer1.Panel1.Invalidate(_ImageRect);
                splitContainer1.Panel2.Invalidate(_ImageRect);
                splitContainer1.Update();
            }
            else
            {
                if (Function.InRectangle(e.Location, _ImageRect))
                {
                    int pickIdx = -1;
                    int potX = e.X - _ImageRect.Left;
                    int potY = e.Y - _ImageRect.Top;
                    double checkDist = Math.Pow(_AllowanceHalfWidth, 2); // 控制區域半徑平方
                    var picks = _Converter.GetPickList();
                    for (int i = 0; i < picks.Count; i++)
                    {
                        Pick pick = picks[i];
                        if (Math.Pow(pick.DisplayPoint.X - potX, 2) + Math.Pow(pick.DisplayPoint.Y - potY, 2) < checkDist)
                        {
                            pickIdx = i;
                            break;
                        }
                    }

                    bool refresh = _HoverIndex != pickIdx;
                    if (pickIdx < 0)
                    {
                        int x = (int)(potX / _Scale);
                        int y = (int)(potY / _Scale);
                        if (x >= _ScaleImage.Width) x = _ScaleImage.Width - 1;
                        if (y >= _ScaleImage.Height) y = _ScaleImage.Height - 1;
                        _HoverIndex = -1;
                        _HoverColor = _ScaleImage.GetPixel(x, y);
                        Cursor = Cursors.Cross;
                    }
                    else
                    {
                        Cursor = Cursors.Hand;
                        _HoverIndex = pickIdx;
                        _HoverColor = _Converter.GetPick(pickIdx).Color;
                    }

                    if (refresh)
                    {
                        splitContainer1.Panel2.Invalidate(_ImageRect);
                    }
                }
                else
                {
                    _HoverIndex = -1;
                    _HoverColor = Color.Empty;
                    Cursor = Cursors.Default;
                }
            }
            splitContainer1.Panel1.Invalidate(_InfoRect);
        }

        private void splitContainer1_Panel1_MouseDown(object sender, MouseEventArgs e)
        {
            if (Function.InRectangle(e.Location, _ImageRect))
            {
                if (_HoverIndex >= 0)
                {
                    switch (e.Button)
                    {
                        case System.Windows.Forms.MouseButtons.Left:
                            _MouseDown = true;
                            _MouseDownPoint = e.Location;
                            _Converter.SetCurrentIndex(_HoverIndex);
                            Cursor = _NullCursor;
                            break;
                        case System.Windows.Forms.MouseButtons.Right:
                            _Converter.RemovePick(_HoverIndex);
                            _HoverIndex = -1;
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
                            int x = (int)((e.X - _ImageRect.Left) / _Scale);
                            int y = (int)((e.Y - _ImageRect.Top) / _Scale);
                            if (x >= _ScaleImage.Width) x = _ScaleImage.Width - 1;
                            if (y >= _ScaleImage.Height) y = _ScaleImage.Height - 1;
                            Point pot = new Point(x, y);
                            _Converter.AddPick(_HoverColor, pot);
                            _Converter.SetCurrentIndex(_Converter.PickCount - 1);
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
                //Cursor.Position = splitContainer1.Panel1.PointToScreen(_PickInfos[_PickInfoIndex].DisplayPoint);
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
                    e.Graphics.DrawString("點選圖片選擇取樣顏色", _InfoFont, Brushes.Blue, _InfoRect, _InfoFormat2);
                }
                else
                {
                    using (SolidBrush brush = new SolidBrush(_HoverColor))
                    {
                        Rectangle rectEll = new Rectangle(_InfoRect.Left + 2, _InfoRect.Top + (_InfoRect.Height - _ColorPotWidth) / 2, _ColorPotWidth, _ColorPotWidth);

                        e.Graphics.FillEllipse(Brushes.Gray, rectEll);
                        rectEll.Offset(-2, -2);
                        e.Graphics.FillEllipse(brush, rectEll);

                        string info = null;
                        string tip = null;
                        if (_MouseDown)
                        {
                            Pick pick = _Converter.GetPick(_Converter.CurrentPickIndex);
                            info = string.Format("色差容許值：{0}\n檢測範圍：{1}", pick.Allowance, pick.Range);
                            tip = "滑鼠上下：調整容許值\n左右：調整範圍";
                        }
                        else if (_HoverIndex >= 0)
                        {
                            Pick pick = _Converter.GetPick(_HoverIndex);
                            info = string.Format("色差容許值：{0}\n檢測範圍：{1}", pick.Allowance, pick.Range);
                            tip = "左鍵：調整容許值與範圍\n右鍵：刪除採樣點";
                        }
                        else
                        {
                            tip = "左鍵：新增採樣點";
                        }

                        int left = _InfoRect.Left + _ColorPotWidth + 8;
                        int top = _InfoRect.Top;
                        int width = _InfoRect.Width - _ColorPotWidth - 5;
                        int height = _InfoRect.Height;
                        if (info == null)
                        {
                            Rectangle rectInfo = new Rectangle(left, top, width, height);
                            e.Graphics.DrawString(tip, _InfoFont, Brushes.Maroon, rectInfo, _InfoFormat2);
                        }
                        else
                        {
                            //Font font = pane new Font(_InfoFont.FontFamily, 12);
                            if (width < 300)
                            {
                                int width1 = (int)(width * 0.65F);
                                Font font = new Font(_InfoFont.FontFamily, width / 30);
                                Rectangle rectInfo1 = new Rectangle(left, top, width / 2, height);
                                Rectangle rectInfo2 = new Rectangle(left + width / 2, top, width / 2, height);
                                e.Graphics.DrawString(info, font, Brushes.RoyalBlue, rectInfo1, _InfoFormat2);
                                e.Graphics.DrawString(tip, font, Brushes.Maroon, rectInfo2, _InfoFormat2);
                            }
                            else
                            {
                                Rectangle rectInfo1 = new Rectangle(left, top, 150, height);
                                Rectangle rectInfo2 = new Rectangle(left + 150, top, width - 150, height);
                                e.Graphics.DrawString(info, _InfoFont, Brushes.RoyalBlue, rectInfo1, _InfoFormat2);
                                e.Graphics.DrawString(tip, _InfoFont, Brushes.Maroon, rectInfo2, _InfoFormat2);
                            }
                        }
                    }
                }

                e.Graphics.DrawImage(_ScaleImage.Image, _ImageRect);
                foreach (Pick pick in _Converter.GetPickList())
                {
                    int potX = pick.DisplayPoint.X + _ImageRect.Left;
                    int potY = pick.DisplayPoint.Y + _ImageRect.Top;
                    Rectangle rectFull = new Rectangle(potX - _AllowanceHalfWidth, potY - _AllowanceHalfWidth, _AllowanceHalfWidth * 2, _AllowanceHalfWidth * 2);
                    Rectangle rectCenter = new Rectangle(potX - _AllowanceCenterWidth, potY - _AllowanceCenterWidth, _AllowanceCenterWidth * 2, _AllowanceCenterWidth * 2);
                    using (SolidBrush fillBrush = new SolidBrush(Color.FromArgb(140, 255 - pick.Color.R, 255 - pick.Color.G, 255 - pick.Color.B)))
                    using (SolidBrush centerBrush = new SolidBrush(pick.Color))
                    using (Pen centerPen = new Pen(pick.Color, _AllowanceCenterWidth))
                    {
                        e.Graphics.FillEllipse(fillBrush, rectFull);
                        e.Graphics.FillEllipse(centerBrush, rectCenter);

                        int maxWid = _AllowanceHalfWidth - _AllowanceCenterWidth;
                        int lineLenV = (int)(maxWid * ((float)pick.Allowance / Pick.MaxAllowance));
                        int lineLenH = (int)(maxWid * ((float)pick.Range / Pick.MaxRange));

                        if (lineLenV > 0)
                        {
                            e.Graphics.DrawLine(centerPen, potX, potY + lineLenV + _AllowanceCenterWidth,
                                                           potX, potY - lineLenV - _AllowanceCenterWidth);
                        }

                        if (lineLenH > 0)
                        {
                            e.Graphics.DrawLine(centerPen, potX + lineLenH + _AllowanceCenterWidth, potY,
                                                           potX - lineLenH - _AllowanceCenterWidth, potY);
                        }
                    }
                    e.Graphics.DrawEllipse(Pens.Black, rectFull);
                    e.Graphics.DrawEllipse(Pens.White, rectFull.Left + 1, rectFull.Top + 1, rectFull.Width - 2, rectFull.Height - 2);
                }
            }
            else
            {
                e.Graphics.DrawString("選擇開啟圖片或將檔案拖曳至此區域", _InfoFont, Brushes.Red, splitContainer1.Panel1.ClientRectangle, _InfoFormat);
            }
        }

        private void splitContainer1_Panel2_Paint(object sender, PaintEventArgs e)
        {
            if (_Converter.ConvertImage != null)
            {
                e.Graphics.DrawString("預覽", _InfoFont2, Brushes.Maroon, 5, 5);
                e.Graphics.DrawImage(_Converter.ConvertImage.Image, _ImageRect);

                int pickIdx = _MouseDown ? _Converter.CurrentPickIndex : _HoverIndex;
                if (pickIdx >= 0)
                {
                    e.Graphics.Clip = new Region(_ImageRect);
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    Pick pick = _Converter.GetPick(pickIdx);

                    int potX = pick.DisplayPoint.X + _ImageRect.Left;
                    int potY = pick.DisplayPoint.Y + _ImageRect.Top;
                    int rangeWid = (int)(_Converter.ConvertImage.MaxDistance * pick.Range / Pick.MaxRange * _Scale);
                    e.Graphics.DrawEllipse(Pens.Black, potX - rangeWid, potY - rangeWid, rangeWid * 2, rangeWid * 2);
                    e.Graphics.DrawEllipse(Pens.White, potX - rangeWid + 1, potY - rangeWid + 1, rangeWid * 2 - 2, rangeWid * 2 - 2);
                    e.Graphics.ResetClip();
                }
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

            _BaseImage = new ImageObject(new Bitmap(tempImage));

            double maxHei = 500, maxWid = 500;
            double scale = Math.Max(_BaseImage.Height / maxHei, _BaseImage.Width / maxWid);
            Size newSize = new Size((int)(_BaseImage.Width / scale), (int)(_BaseImage.Height / scale));
            _ScaleImage = new ImageObject(new Bitmap(tempImage, newSize));           // 產生縮小預覽圖片
            _Converter.BaseImage = _ScaleImage;

            SetImageSize();
            saveFileDialog1.InitialDirectory = Path.GetDirectoryName(path);
            tsbtnSave.Enabled = true;
            tempImage.Dispose();
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
            _Scale = Math.Min(maxHei / _ScaleImage.Height, maxWid / _ScaleImage.Width);

            Size size = new Size((int)(_ScaleImage.Width * _Scale), (int)(_ScaleImage.Height * _Scale));
            int locX = (splitContainer1.Panel1.Width - size.Width) / 2;
            int locY = (splitContainer1.Panel1.Height - size.Height) / 2;

            if (locX < _ImagePadding.Left) locX = _ImagePadding.Left;
            if (locY < _ImagePadding.Top) locY = _ImagePadding.Top;
            Point loc = new Point(locX, locY);
            _ImageRect = new Rectangle(loc, size);
            _InfoRect = new Rectangle(_ImagePadding.Left, 5, splitContainer1.Panel1.Width - _ImagePadding.Horizontal, _ImagePadding.Top - 10);
            _Converter.DisplayScale = _Scale;

            splitContainer1.Invalidate(true);
        }
    }
}
