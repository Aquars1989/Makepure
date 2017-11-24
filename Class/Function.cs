using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace Makepure
{
    class Function
    {
        /// <summary>
        /// 取得兩點距離
        /// </summary>
        /// <param name="x1">點1座標X</param>
        /// <param name="y1">點1座標Y</param>
        /// <param name="x2">點2座標X</param>
        /// <param name="y2">點2座標Y</param>
        /// <returns></returns>
        public static double GetDistance(int x1, int y1, int x2, int y2)
        {
            return Math.Sqrt(Math.Pow(x1 - x2, 2) + Math.Pow(y1 - y2, 2));
        }

        /// <summary>
        /// 判斷點是否在矩形內
        /// </summary>
        /// <param name="point">判斷點</param>
        /// <param name="rectangle">矩形</param>
        /// <returns>點是否在矩型內</returns>
        public static bool InRectangle(Point point, Rectangle rectangle)
        {
            return point.X >= rectangle.Left && point.X <= rectangle.Left + rectangle.Width &&
                   point.Y >= rectangle.Top && point.Y <= rectangle.Top + rectangle.Height;
        }
    }
}
