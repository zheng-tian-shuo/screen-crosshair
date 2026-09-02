using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace ScreenCrosshair
{
    /// <summary>
    /// 打进 exe 里的头像图（brand.png，带白描边的透明 PNG）。
    /// 标题栏的 logo 用它，取不到就退回画一个准星，界面不会空着。
    /// </summary>
    public static class Brand
    {
        private static Image _img;
        private static bool _tried;
        private static Icon _ico;
        private static bool _triedIco;

        public static Image Bmp
        {
            get
            {
                if (!_tried)
                {
                    _tried = true;
                    try
                    {
                        Stream s = Assembly.GetExecutingAssembly()
                            .GetManifestResourceStream("brand.png");
                        if (s != null) _img = Image.FromStream(s);
                    }
                    catch { }
                }
                return _img;
            }
        }

        /// <summary>
        /// 窗口和托盘用的图标。以前是 Icon.ExtractAssociatedIcon(exe)，那个只回一张 32x32，
        /// 而且 exe 换名或被系统图标缓存挡住时容易拿到旧图；这里直接读打进 exe 的 app.ico，
        /// 多尺寸齐全，任务栏、托盘、气泡通知都能挑到清晰的那一张。
        /// </summary>
        public static Icon AppIcon
        {
            get
            {
                if (!_triedIco)
                {
                    _triedIco = true;
                    try
                    {
                        Stream s = Assembly.GetExecutingAssembly()
                            .GetManifestResourceStream("app.ico");
                        if (s != null) _ico = new Icon(s);
                    }
                    catch { }
                    if (_ico == null)
                    {
                        try { _ico = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
                        catch { }
                    }
                }
                return _ico;
            }
        }

        /// <summary>按指定边长取一张最合适的图标，托盘小图标要用这个，不然会被拉毛</summary>
        public static Icon IconAt(int side)
        {
            Icon big = AppIcon;
            if (big == null) return null;
            try { return new Icon(big, side, side); }
            catch { return big; }
        }

        /// <summary>在给定方框里等比居中画头像</summary>
        public static void Draw(Graphics g, Rectangle r)
        {
            Image im = Bmp;
            if (im == null)
            {
                CrosshairItemSettings it = new CrosshairItemSettings();
                it.Shape = CrosshairShape.CircleCross;
                it.Color = Theme.Accent;
                it.Size = r.Width;
                it.Thickness = 2;
                it.Glow = false;
                Painter.Draw(g, it, r.X + r.Width / 2f, r.Y + r.Height / 2f);
                return;
            }

            int side = Math.Min(r.Width, r.Height);
            Rectangle dst = new Rectangle(
                r.X + (r.Width - side) / 2, r.Y + (r.Height - side) / 2, side, side);
            InterpolationMode oi = g.InterpolationMode;
            PixelOffsetMode op = g.PixelOffsetMode;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(im, dst);
            g.InterpolationMode = oi;
            g.PixelOffsetMode = op;
        }
    }
}
