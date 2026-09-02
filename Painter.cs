using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace ScreenCrosshair
{
    /// <summary>
    /// 准星绘制。全部用浮点坐标 + PixelOffsetMode，保证 1px 细线也压在中心像素上，
    /// 不会出现旧版本那种"线偏半格 / 中心不对称"的问题。
    /// </summary>
    public static partial class Painter
    {
        /// <summary>把一个准星画到 g 上，(cx,cy) 是中心点</summary>
        public static void Draw(Graphics g, CrosshairItemSettings it, float cx, float cy)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.AntiAlias;

            int op = it.Opacity;
            if (op < 10) op = 10;
            if (op > 100) op = 100;
            int a = (int)Math.Round(255.0 * op / 100.0);
            Color main = Color.FromArgb(a, it.Color.R, it.Color.G, it.Color.B);
            float t = Math.Max(1, it.Thickness);

            if (it.Glow)
            {
                // 由外到内三层暗色描边：亮背景（雪地、天空）上能托住轮廓，
                // 暗背景上几乎看不出来，比旧版本的硬黑描边自然得多。
                for (int i = 3; i >= 1; i--)
                {
                    int ga = a * (11 - i * 2) / 100;
                    if (ga < 5) ga = 5;
                    using (Pen gp = MakePen(Color.FromArgb(ga, 0, 0, 0), t + i * 2.4f))
                        StrokeShape(g, it, cx, cy, gp, true);
                }
            }

            using (Pen p = MakePen(main, t))
                StrokeShape(g, it, cx, cy, p, false);
        }

        private static Pen MakePen(Color c, float w)
        {
            Pen p = new Pen(c, w);
            p.StartCap = LineCap.Flat;
            p.EndCap = LineCap.Flat;
            p.LineJoin = LineJoin.Round;
            p.Alignment = PenAlignment.Center;
            return p;
        }

        /// <summary>glow=true 表示这是发光衬底层，不画文字</summary>
        private static void StrokeShape(Graphics g, CrosshairItemSettings it,
            float cx, float cy, Pen p, bool glow)
        {
            float size = Math.Max(4, it.Size);
            float r = size / 2f;
            float t = p.Width;

            switch (it.Shape)
            {
                case CrosshairShape.Dot:
                    FillDot(g, cx, cy, Math.Max(2f, size / 4f) + (glow ? t : 0f), p.Color);
                    break;

                case CrosshairShape.Circle:
                    g.DrawEllipse(p, cx - r, cy - r, size, size);
                    break;

                case CrosshairShape.Cross:
                    g.DrawLine(p, cx - r, cy, cx + r, cy);
                    g.DrawLine(p, cx, cy - r, cx, cy + r);
                    break;

                case CrosshairShape.X:
                    {
                        float k = r * 0.7071f;
                        g.DrawLine(p, cx - k, cy - k, cx + k, cy + k);
                        g.DrawLine(p, cx - k, cy + k, cx + k, cy - k);
                        break;
                    }

                case CrosshairShape.CircleCross:
                    {
                        float gap = Math.Max(2f, size * 0.16f);
                        g.DrawEllipse(p, cx - r, cy - r, size, size);
                        g.DrawLine(p, cx - r, cy, cx - gap, cy);
                        g.DrawLine(p, cx + gap, cy, cx + r, cy);
                        g.DrawLine(p, cx, cy - r, cx, cy - gap);
                        g.DrawLine(p, cx, cy + gap, cx, cy + r);
                        break;
                    }

                case CrosshairShape.TShape:
                    // 中心下方完全留空，炸点落地瞬间不被线挡住
                    g.DrawLine(p, cx - r, cy, cx + r, cy);
                    g.DrawLine(p, cx, cy - r, cx, cy);
                    break;

                case CrosshairShape.HLine:
                    g.DrawLine(p, cx - r, cy, cx + r, cy);
                    break;

                case CrosshairShape.DotRing:
                    g.DrawEllipse(p, cx - r, cy - r, size, size);
                    FillDot(g, cx, cy, Math.Max(2f, size / 5f) + (glow ? t : 0f), p.Color);
                    break;

                case CrosshairShape.Mildot:
                    DrawMildot(g, it, cx, cy, p, glow);
                    break;
            }
        }

        private static void FillDot(Graphics g, float cx, float cy, float d, Color c)
        {
            using (SolidBrush b = new SolidBrush(c))
                g.FillEllipse(b, cx - d / 2f, cy - d / 2f, d, d);
        }
    }
}
