using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ScreenCrosshair
{
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

        public void SetSilent(bool value)
        {
            _checked = value;
            Invalidate();
        }

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
            int boxSize = Theme.S(16);
            float scale = boxSize / 16f;
            Rectangle box = new Rectangle(0, (Height - boxSize) / 2, boxSize, boxSize);

            if (_checked)
            {
                Ui.FillRound(g, box, Theme.S(4), Enabled ? Theme.Accent : Theme.TextFaint);
                using (Pen p = new Pen(Theme.Window, 2f * scale))
                {
                    p.StartCap = LineCap.Round;
                    p.EndCap = LineCap.Round;
                    g.DrawLines(p, new PointF[] {
                        new PointF(box.X + 3.5f * scale, box.Y + 8.5f * scale),
                        new PointF(box.X + 6.5f * scale, box.Y + 11.5f * scale),
                        new PointF(box.X + 12.5f * scale, box.Y + 4.5f * scale) });
                }
            }
            else
            {
                Ui.FillRound(g, box, Theme.S(4), Theme.CardAlt);
                Ui.DrawRound(g, box, Theme.S(4), _hover ? Theme.BorderLit : Theme.Border, 1f);
            }

            int textX = boxSize + Theme.S(7);
            TextRenderer.DrawText(g, Text, Font,
                new Rectangle(textX, 0, Width - textX, Height),
                Enabled ? Theme.Text : Theme.TextFaint,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        }
    }
}
