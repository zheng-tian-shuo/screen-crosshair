using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace ScreenCrosshair
{
    /// <summary>设置页里的小型 HUD 预览，和屏幕覆盖层使用同一套视觉参数。</summary>
    public sealed class HudPreviewBox : Control
    {
        private int _clockSize = 22;
        private int _countdownSize = 28;
        private int _opacity = 78;
        private bool _showPlate = true;
        private bool _showMarkers = true;
        private bool _clockSeconds = true;
        private bool _showClock = true;
        private int _freeSeconds = 60;

        public HudPreviewBox()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Hud.Window;
        }

        public void SetStyle(int clockSize, int countdownSize, int opacity,
            bool showPlate, bool showMarkers, bool clockSeconds, bool showClock, int freeSeconds)
        {
            _clockSize = clockSize;
            _countdownSize = countdownSize;
            _opacity = opacity;
            _showPlate = showPlate;
            _showMarkers = showMarkers;
            _clockSeconds = clockSeconds;
            _showClock = showClock;
            _freeSeconds = Math.Max(1, freeSeconds);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAlias;
            g.Clear(Color.FromArgb(23, 25, 30));

            Rectangle area = ClientRectangle;
            using (Pen grid = new Pen(Color.FromArgb(34, 38, 46), 1f))
            {
                int cell = Theme.S(18);
                for (int x = 0; x < area.Width; x += cell) g.DrawLine(grid, x, 0, x, area.Height);
                for (int gy = 0; gy < area.Height; gy += cell) g.DrawLine(grid, 0, gy, area.Width, gy);
            }

            int panelW = Math.Min(area.Width - Theme.S(24), Theme.S(360));
            int panelH = Math.Min(area.Height - Theme.S(20), Theme.S(_showClock ? 180 : 140));
            if (panelW < Theme.S(180)) panelW = area.Width - Theme.S(12);
            if (panelH < Theme.S(80)) panelH = area.Height - Theme.S(8);
            Rectangle r = new Rectangle((area.Width - panelW) / 2, (area.Height - panelH) / 2,
                panelW, panelH);

            if (_showPlate)
            {
                using (GraphicsPath p = Ui.Round(r, Theme.S(9)))
                using (SolidBrush b = new SolidBrush(Color.FromArgb((int)Math.Round(255 * _opacity / 100.0), 12, 14, 18)))
                    g.FillPath(b, p);
                using (Pen p = new Pen(Color.FromArgb(170, Theme.Hud.BorderLit), Theme.S(1)))
                using (GraphicsPath path = Ui.Round(r, Theme.S(9)))
                    g.DrawPath(p, path);
            }

            int pad = Theme.S(14);
            int rowH = Theme.S(Math.Max(24, _countdownSize + 12));
            int y = r.Top + Theme.S(8);
            if (_showClock)
            {
                using (Font f = new Font("Consolas", Theme.S(Math.Max(14, _clockSize)), FontStyle.Bold, GraphicsUnit.Pixel))
                    DrawText(g, _clockSeconds ? "12:45:38" : "12:45", f,
                        new RectangleF(r.Left + pad, y, r.Width - pad * 2, rowH),
                        StringAlignment.Far, Theme.Hud.TextMuted);
                y += rowH + Theme.S(5);
            }

            using (Font nf = new Font("Microsoft YaHei UI", Theme.S(12), FontStyle.Bold, GraphicsUnit.Pixel))
            using (Font tf = new Font("Consolas", Theme.S(Math.Max(14, _countdownSize)), FontStyle.Bold, GraphicsUnit.Pixel))
            {
                string[] names = { "撤离点", "火箭", "自由" };
                string[] values = { "04:32", "03:18", CountdownOverlayForm.Format(_freeSeconds) };
                Color[] colors = { Theme.Hud.Accent, Theme.Hud.Accent, Theme.Hud.Danger };
                for (int i = 0; i < names.Length; i++)
                {
                    if (_showMarkers)
                        using (SolidBrush b = new SolidBrush(colors[i]))
                            g.FillRectangle(b, r.Left + Theme.S(6), y + Theme.S(7), Theme.S(3), rowH - Theme.S(14));
                    DrawText(g, names[i], nf,
                        new RectangleF(r.Left + pad + Theme.S(4), y, r.Width / 2f, rowH),
                        StringAlignment.Near, Theme.Hud.Text);
                    DrawText(g, values[i], tf,
                        new RectangleF(r.Right - pad - Theme.S(128), y, Theme.S(128), rowH),
                        StringAlignment.Far, colors[i]);
                    y += rowH + Theme.S(2);
                }
            }

            using (Pen border = new Pen(Theme.Hud.Border, 1f))
                g.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
        }

        private static void DrawText(Graphics g, string text, Font font, RectangleF bounds,
            StringAlignment align, Color color)
        {
            using (StringFormat sf = new StringFormat())
            using (SolidBrush b = new SolidBrush(color))
            {
                sf.Alignment = align;
                sf.LineAlignment = StringAlignment.Center;
                g.DrawString(text, font, b, bounds, sf);
            }
        }
    }
}
