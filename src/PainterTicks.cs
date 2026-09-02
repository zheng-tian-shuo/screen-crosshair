using System;
using System.Drawing;

namespace ScreenCrosshair
{
    public static partial class Painter
    {
        /// <summary>
        /// 分划板：中心一个十字，向下一根主尺，每隔 TickSpacing 一道横刻度。
        /// 偶数格的刻度画长一点方便数格；刻度右侧可以标出这一格代表的距离。
        /// </summary>
        private static void DrawMildot(Graphics g, CrosshairItemSettings it,
            float cx, float cy, Pen p, bool glow)
        {
            float size = Math.Max(4, it.Size);
            float r = size / 2f;

            int n = it.TickCount;
            if (n < 1) n = 1;
            if (n > 20) n = 20;
            float sp = Math.Max(4, it.TickSpacing);
            float tl = Math.Max(4, it.TickLength);

            // 中心：完整横线 + 上方短竖线，先把水平基准对准
            g.DrawLine(p, cx - r, cy, cx + r, cy);
            g.DrawLine(p, cx, cy - r, cx, cy - Math.Max(2f, r * 0.35f));

            // 主尺向下贯穿全部刻度
            g.DrawLine(p, cx, cy, cx, cy + n * sp);

            for (int i = 1; i <= n; i++)
            {
                float y = cy + i * sp;
                float half = (i % 2 == 0) ? tl / 2f : tl / 3.2f;
                g.DrawLine(p, cx - half, y, cx + half, y);
            }

            if (!it.ShowLabels) return;

            // 字号跟着刻度间距走，而不是跟着准星总尺寸：以前按 size * 0.34 取，
            // 大小 78 + 间距 26 时字号 26pt（约 35 px）比一格还高，数字全叠在一起。
            // 单位显式用像素：准星画的是屏幕像素，pt 会再被 DPI 放大一次。
            float fs = Math.Min(sp * 0.62f, size * 0.30f);
            if (fs < 8f) fs = 8f;
            using (Font f = new Font("Consolas", fs, FontStyle.Bold, GraphicsUnit.Pixel))
            using (SolidBrush b = new SolidBrush(p.Color))
            {
                StringFormat sf = new StringFormat();
                sf.LineAlignment = StringAlignment.Center;
                try
                {
                    float x = cx + tl / 2f + 4f;
                    for (int i = 1; i <= n; i++)
                    {
                        int dist = it.LabelStart + (i - 1) * it.LabelStep;
                        string s = dist.ToString();
                        float y = cy + i * sp;
                        if (glow)
                        {
                            // 发光层：文字向四周糊一圈，亮背景上数字也能看清
                            g.DrawString(s, f, b, x - 1f, y, sf);
                            g.DrawString(s, f, b, x + 1f, y, sf);
                            g.DrawString(s, f, b, x, y - 1f, sf);
                            g.DrawString(s, f, b, x, y + 1f, sf);
                        }
                        else
                        {
                            g.DrawString(s, f, b, x, y, sf);
                        }
                    }
                }
                finally { sf.Dispose(); }
            }
        }
    }
}
