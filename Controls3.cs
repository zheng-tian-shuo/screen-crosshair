using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ScreenCrosshair
{
    /// <summary>深色复选框（系统 CheckBox 在深底上会露出白框）</summary>
    public class Chk : Control
    {
        private bool _checked;
        private bool _hover;
        public event EventHandler CheckedChanged;

        public Chk()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Font = Theme.Body;
            Cursor = Cursors.Hand;
            Height = 22;
        }

        public bool Checked
        {
            get { return _checked; }
            set
            {
                if (_checked == value) return;
                _checked = value;
                Invalidate();
                if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty);
            }
        }

        /// <summary>只改显示不触发事件</summary>
        public void SetSilent(bool v) { _checked = v; Invalidate(); }

        protected override void OnClick(EventArgs e)
        {
            Checked = !_checked;
            base.OnClick(e);
        }

        protected override void OnMouseEnter(EventArgs e)
        { _hover = true; Invalidate(); base.OnMouseEnter(e); }

        protected override void OnMouseLeave(EventArgs e)
        { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int bs = Theme.S(16);
            float f = bs / 16f;
            Rectangle box = new Rectangle(0, (Height - bs) / 2, bs, bs);

            if (_checked)
            {
                Ui.FillRound(g, box, Theme.S(4), Enabled ? Theme.Accent : Theme.TextFaint);
                using (Pen p = new Pen(Theme.Window, 2f * f))
                {
                    p.StartCap = LineCap.Round;
                    p.EndCap = LineCap.Round;
                    g.DrawLines(p, new PointF[] {
                        new PointF(box.X + 3.5f * f, box.Y + 8.5f * f),
                        new PointF(box.X + 6.5f * f, box.Y + 11.5f * f),
                        new PointF(box.X + 12.5f * f, box.Y + 4.5f * f) });
                }
            }
            else
            {
                Ui.FillRound(g, box, Theme.S(4), Theme.CardAlt);
                Ui.DrawRound(g, box, Theme.S(4), _hover ? Theme.BorderLit : Theme.Border, 1f);
            }

            int tx = bs + Theme.S(7);
            TextRenderer.DrawText(g, Text, Font,
                new Rectangle(tx, 0, Width - tx, Height),
                Enabled ? Theme.Text : Theme.TextFaint,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        }
    }

    /// <summary>预览框。可以换几种典型背景，检查准星在不同场景下够不够显眼。</summary>
    public class PreviewBox : Control
    {
        public CrosshairItemSettings Item;
        public int BgKind;   // 0 棋盘 1 暗 2 亮 3 草地 4 沙地

        public PreviewBox()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Rectangle r = ClientRectangle;
            DrawBg(g, r);

            if (Item != null)
            {
                int n = Item.CanvasSize();
                int avail = Math.Min(r.Width, r.Height) - Theme.S(6);
                float scale = 1f;
                if (n > avail && n > 0) scale = (float)avail / n;

                GraphicsState st = g.Save();
                try
                {
                    g.ScaleTransform(scale, scale);
                    Painter.Draw(g, Item, r.Width / 2f / scale, r.Height / 2f / scale);
                }
                finally { g.Restore(st); }

                if (scale < 0.999f)
                    TextRenderer.DrawText(g, "缩放显示 " + (int)Math.Round(scale * 100) + "%",
                        Theme.Small, new Point(Theme.S(6), r.Height - Theme.S(19)),
                        Color.FromArgb(200, 255, 255, 255));
            }

            using (Pen p = new Pen(Theme.Border, 1f))
                g.DrawRectangle(p, 0, 0, r.Width - 1, r.Height - 1);
        }

        private void DrawBg(Graphics g, Rectangle r)
        {
            Color a, b;
            switch (BgKind)
            {
                case 1: a = Color.FromArgb(24, 25, 29); b = a; break;
                case 2: a = Color.FromArgb(216, 216, 218); b = a; break;
                case 3: a = Color.FromArgb(56, 72, 42); b = Color.FromArgb(46, 60, 34); break;
                case 4: a = Color.FromArgb(178, 160, 126); b = Color.FromArgb(164, 146, 112); break;
                default: a = Color.FromArgb(58, 60, 66); b = Color.FromArgb(44, 46, 52); break;
            }

            using (SolidBrush br = new SolidBrush(a))
                g.FillRectangle(br, r);

            if (BgKind == 0)
            {
                // 棋盘格：最容易看出半透明到底透了多少
                int q = Theme.S(10);
                using (SolidBrush br = new SolidBrush(b))
                    for (int y = 0; y < r.Height; y += q)
                        for (int x = 0; x < r.Width; x += q)
                            if (((x / q) + (y / q)) % 2 == 0)
                                g.FillRectangle(br, x, y, q, q);
            }
            else if (BgKind == 3 || BgKind == 4)
            {
                // 固定种子的杂色，模拟草地/沙地这种最难看清准星的背景
                Random rnd = new Random(20260831);
                int dw = Theme.S(3), dh = Theme.S(2);
                using (SolidBrush br = new SolidBrush(b))
                    for (int i = 0; i < 900; i++)
                        g.FillRectangle(br, rnd.Next(r.Width), rnd.Next(r.Height), dw, dh);
            }
        }
    }
}
