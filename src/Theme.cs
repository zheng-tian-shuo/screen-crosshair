using System;
using System.Drawing;

namespace ScreenCrosshair
{
    /// <summary>深色主题配色与字体。取色来自软件图标：黑金骷髅 + 白描边。</summary>
    public static class Theme
    {
        public static bool IsLight { get; private set; }
        public static void SetLight(bool light) { IsLight = light; }
        public static Color Window { get { return IsLight ? Color.FromArgb(243, 245, 249) : Hud.Window; } }
        public static Color Card { get { return IsLight ? Color.FromArgb(255, 255, 255) : Hud.Card; } }
        public static Color CardAlt { get { return IsLight ? Color.FromArgb(241, 245, 249) : Hud.CardAlt; } }
        public static Color Border { get { return IsLight ? Color.FromArgb(226, 232, 240) : Hud.Border; } }
        public static Color BorderLit { get { return IsLight ? Color.FromArgb(148, 163, 184) : Hud.BorderLit; } }
        public static Color Text { get { return IsLight ? Color.FromArgb(15, 23, 42) : Hud.Text; } }
        public static Color TextMuted { get { return IsLight ? Color.FromArgb(71, 85, 105) : Hud.TextMuted; } }
        public static Color TextFaint { get { return IsLight ? Color.FromArgb(148, 163, 184) : Hud.TextFaint; } }
        public static Color Accent { get { return IsLight ? Color.FromArgb(217, 119, 6) : Hud.Accent; } }
        public static Color AccentDim { get { return IsLight ? Color.FromArgb(245, 158, 11) : Hud.AccentDim; } }
        public static Color AccentDeep { get { return IsLight ? Color.FromArgb(254, 243, 199) : Hud.AccentDeep; } }
        public static Color Green { get { return IsLight ? Color.FromArgb(22, 163, 74) : Hud.Green; } }
        public static Color Danger { get { return IsLight ? Color.FromArgb(225, 29, 72) : Hud.Danger; } }
        public static Color DangerDim { get { return IsLight ? Color.FromArgb(255, 241, 242) : Hud.DangerDim; } }
        public static Color Warn { get { return IsLight ? Color.FromArgb(234, 88, 12) : Hud.Warn; } }

        public static Color[] Palette()
        {
            return new Color[] { Window, Card, CardAlt, Border, BorderLit, Text,
                TextMuted, TextFaint, Accent, AccentDim, AccentDeep, Green, Danger, DangerDim, Warn };
        }

        // Screen overlays keep their original contrast independently of the settings UI.
        public static class Hud
        {
            public static readonly Color Window = Color.FromArgb(18, 23, 31);
            public static readonly Color Card = Color.FromArgb(27, 33, 43);
            public static readonly Color CardAlt = Color.FromArgb(36, 43, 55);
            public static readonly Color Border = Color.FromArgb(49, 59, 74);
            public static readonly Color BorderLit = Color.FromArgb(91, 106, 128);
            public static readonly Color Text = Color.FromArgb(237, 241, 247);
            public static readonly Color TextMuted = Color.FromArgb(185, 196, 211);
            public static readonly Color TextFaint = Color.FromArgb(145, 159, 181);

            // 图标上的那点金色，整套界面的强调色都从它来
            public static readonly Color Accent = Color.FromArgb(229, 180, 86);
            public static readonly Color AccentDim = Color.FromArgb(174, 127, 33);
            public static readonly Color AccentDeep = Color.FromArgb(67, 53, 25);
            public static readonly Color Green = Color.FromArgb(117, 211, 139);
            public static readonly Color Danger = Color.FromArgb(247, 139, 133);
            public static readonly Color DangerDim = Color.FromArgb(67, 39, 45);
            // 金色已经占了强调位，警告改用橙红，免得两个黄互相打架
            public static readonly Color Warn = Color.FromArgb(255, 138, 60);
        }

        private const string Face = "Microsoft YaHei UI";

        public static readonly Font Title = new Font(Face, 15f, FontStyle.Bold);
        public static readonly Font Sub = new Font(Face, 9f);
        public static readonly Font Body = new Font(Face, 9.5f);
        public static readonly Font BodyBold = new Font(Face, 9.5f, FontStyle.Bold);
        public static readonly Font Small = new Font(Face, 9f);
        public static readonly Font SectionHead = new Font(Face, 11f, FontStyle.Bold);
        public static readonly Font Mono = new Font("Consolas", 10f);
        public static readonly Font TabFont = new Font(Face, 10f);
        public static readonly Font HelpBody = new Font(Face, 10f);

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
