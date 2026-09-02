using System;
using System.Drawing;

namespace ScreenCrosshair
{
    /// <summary>深色主题配色与字体。取色来自软件图标：黑金骷髅 + 白描边。</summary>
    public static class Theme
    {
        public static readonly Color Window = Color.FromArgb(18, 19, 23);
        public static readonly Color Card = Color.FromArgb(26, 28, 33);
        public static readonly Color CardAlt = Color.FromArgb(34, 37, 44);
        public static readonly Color Border = Color.FromArgb(48, 52, 60);
        public static readonly Color BorderLit = Color.FromArgb(70, 76, 88);
        public static readonly Color Text = Color.FromArgb(240, 241, 244);
        public static readonly Color TextMuted = Color.FromArgb(160, 166, 176);
        public static readonly Color TextFaint = Color.FromArgb(110, 116, 128);

        // 图标上的那点金色，整套界面的强调色都从它来
        public static readonly Color Accent = Color.FromArgb(245, 190, 58);
        public static readonly Color AccentDim = Color.FromArgb(176, 131, 28);
        public static readonly Color AccentDeep = Color.FromArgb(92, 68, 14);
        public static readonly Color Green = Color.FromArgb(122, 214, 138);
        public static readonly Color Danger = Color.FromArgb(232, 84, 80);
        public static readonly Color DangerDim = Color.FromArgb(112, 42, 42);
        // 金色已经占了强调位，警告改用橙红，免得两个黄互相打架
        public static readonly Color Warn = Color.FromArgb(255, 138, 60);

        private const string Face = "Microsoft YaHei UI";

        public static readonly Font Title = new Font(Face, 14f, FontStyle.Bold);
        public static readonly Font Sub = new Font(Face, 8.5f);
        public static readonly Font Body = new Font(Face, 9f);
        public static readonly Font BodyBold = new Font(Face, 9f, FontStyle.Bold);
        public static readonly Font Small = new Font(Face, 8.5f);
        public static readonly Font SectionHead = new Font(Face, 9.5f, FontStyle.Bold);
        public static readonly Font Mono = new Font("Consolas", 9.5f, FontStyle.Bold);
        public static readonly Font TabFont = new Font(Face, 9.5f);
        public static readonly Font HelpBody = new Font(Face, 9f);

        /// <summary>在给定底色上取可读的前景色</summary>
        public static Color ContrastOn(Color c)
        {
            double lum = c.R * 0.299 + c.G * 0.587 + c.B * 0.114;
            return lum > 150 ? Color.FromArgb(16, 18, 22) : Color.White;
        }

        public static Color Mix(Color a, Color b, double t)
        {
            if (t < 0) t = 0;
            if (t > 1) t = 1;
            return Color.FromArgb(
                (int)Math.Round(a.R + (b.R - a.R) * t),
                (int)Math.Round(a.G + (b.G - a.G) * t),
                (int)Math.Round(a.B + (b.B - a.B) * t));
        }

        public static string HexOf(Color c)
        {
            return string.Format("#{0:X2}{1:X2}{2:X2}", c.R, c.G, c.B);
        }

        /// <summary>界面缩放系数，Program 启动时按屏幕 DPI 设一次</summary>
        public static float K = 1f;

        /// <summary>
        /// 自绘代码里写死的像素都要过一遍这个。字体是 pt 单位，系统会自动按 DPI 放大，
        /// 而 OnPaint 里的方框、间距不会——不换算的话 150% 缩放下框和字就对不上。
        /// </summary>
        public static int S(int v)
        {
            return (int)Math.Round(v * K);
        }
    }
}
