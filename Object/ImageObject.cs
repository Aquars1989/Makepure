using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;

namespace Makepure
{
    /// <summary>
    /// 圖片資訊物件
    /// </summary>
    public class ImageObject : IDisposable
    {
        private BitmapData _bitmapData;

        /// <summary>
        /// 圖片物件
        /// </summary>
        public Bitmap Image { get; private set; }

        /// <summary>
        /// 像素數量 Width * Height
        /// </summary>
        public int PixelCount { get; private set; }

        /// <summary>
        /// 最大距離 為對角線長度
        /// </summary>
        public double MaxDistance { get; private set; }

        /// <summary>
        /// 圖片寬度
        /// </summary>
        public int Width { get { return Image.Width; } }

        /// <summary>
        /// 圖片高度
        /// </summary>
        public int Height { get { return Image.Height; } }

        /// <summary>
        /// 使用圖片建立圖片資訊物件
        /// </summary>
        /// <param name="image">Bitmap圖片</param>
        public ImageObject(Bitmap image)
        {
            if (image == null) throw new ArgumentNullException();

            Image = image;
            PixelCount = image.Width * image.Height;
            MaxDistance = Function.GetDistance(0, 0, image.Width, image.Height);
        }

        private ImageObject() { }

        /// <summary>
        /// 鎖定圖片並傳回Scan0記憶體位置
        /// </summary>
        /// <param name="imageLockMode">縮定模式</param>
        /// <returns>Scan0記憶體位置</returns>
        public IntPtr LockBitsAndGetScan0(ImageLockMode imageLockMode)
        {
            UnlockBits();
            Rectangle rect = new Rectangle(0, 0, Width, Height);
            _bitmapData = Image.LockBits(rect, imageLockMode, PixelFormat.Format32bppArgb);
            return _bitmapData.Scan0;
        }

        /// <summary>
        /// 解除圖片鎖定
        /// </summary>
        public void UnlockBits()
        {
            if (_bitmapData != null)
            {
                Image.UnlockBits(_bitmapData);
                _bitmapData = null;
            }
        }

        /// <summary>
        /// 取得指定座標點上的顏色
        /// </summary>
        /// <param name="x">X座標</param>
        /// <param name="y">Y座標</param>
        /// <returns></returns>
        public Color GetPixel(int x, int y)
        {
            return Image.GetPixel(x, y);
        }

        /// <summary>
        /// 儲存圖片到指定路徑
        /// </summary>
        /// <param name="path">路徑</param>
        /// <param name="format">儲存格式</param>
        public void Save(string path, ImageFormat format)
        {
            Image.Save(path, format);
        }

        /// <summary>
        /// 複製圖片物件
        /// </summary>
        /// <param name="imageObject">圖片物件</param>
        /// <returns>複製圖片物件</returns>
        public ImageObject Copy()
        {
            return new ImageObject()
            {
                Image = new Bitmap(this.Image),
                MaxDistance = this.MaxDistance,
                PixelCount = this.PixelCount
            };
        }

        #region IDisposable Support
        private bool disposedValue = false; // 偵測多餘的呼叫
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Image.Dispose();
                }
                disposedValue = true;
            }
        }

        // 加入這個程式碼的目的在正確實作可處置的模式。
        public void Dispose()
        {
            Dispose(true);
        }
        #endregion
    }
}
