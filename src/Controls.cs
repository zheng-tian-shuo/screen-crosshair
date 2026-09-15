using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ScreenCrosshair
{
    /// <summary>圆角扁平按钮。Kind：0 普通 / 1 主色 / 2 危险 / 3 幽灵</summary>
    public class FlatBtn : Control
    {
        public int Kind;
        public int Radius = 5;
        private bool _hover, _down;

        public FlatBtn()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Font = Theme.Body;
            Cursor = Cursors.Hand;
            Height = 30;
        }

        protected override void OnMouseEnter(EventArgs e)
        { _hover = true; Invalidate(); base.OnMouseEnter(e); }

        protected override void OnMouseLeave(EventArgs e)
        { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        { _down = true; Invalidate(); base.OnMouseDown(e); }

        protected override void OnMouseUp(MouseEventArgs e)
        { _down = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnEnabledChanged(EventArgs e)
        { Invalidate(); base.OnEnabledChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);

            Color bg, fg, bd;
            switch (Kind)
            {
                case 1:
                    bg = Theme.Accent; fg = Color.FromArgb(20, 22, 26); bd = Theme.Accent; break;
                case 2:
                    bg = Theme.DangerDim; fg = Theme.Danger; bd = Color.FromArgb(150, 60, 64); break;
                case 3:
                    bg = Color.Transparent; fg = Theme.TextMuted; bd = Color.Transparent; break;
                default:
                    bg = Theme.CardAlt; fg = Theme.Text; bd = Theme.Border; break;
            }

            if (!Enabled)
            {
                bg = Theme.Card; fg = Theme.TextFaint; bd = Theme.Border;
            }
            else if (_down)
            {
                bg = Theme.Mix(bg.A == 0 ? Theme.CardAlt : bg, Color.Black, 0.2);
                if (Kind == 3) fg = Theme.Text;
            }
            else if (_hover)
            {
                if (Kind == 3) { bg = Theme.CardAlt; fg = Theme.Text; bd = Theme.Border; }
                else { bg = Theme.Mix(bg, Color.White, Kind == 1 ? 0.14 : 0.08); bd = Theme.BorderLit; }
            }

            using (GraphicsPath p = Ui.Round(r, Theme.S(Radius)))
            {
                if (bg.A > 0)
                    using (SolidBrush b = new SolidBrush(bg)) g.FillPath(b, p);
                if (bd.A > 0)
                    using (Pen pen = new Pen(bd, 1f)) g.DrawPath(pen, p);
            }

            TextRenderer.DrawText(g, Text, Font, r, fg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis);
        }
    }

    /// <summary>左侧导航项</summary>
    public class SideTab : Control
    {
        public bool Active;
        private bool _hover;

        public SideTab()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Font = Theme.TabFont;
            Cursor = Cursors.Hand;
            Height = 38;
        }

        public void SetActive(bool on)
        {
            if (Active == on) return;
            Active = on;
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e)
        { _hover = true; Invalidate(); base.OnMouseEnter(e); }

        protected override void OnMouseLeave(EventArgs e)
        { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);

            if (Active)
            {
                Ui.FillRound(g, r, Theme.S(5), Theme.CardAlt);
                using (SolidBrush b = new SolidBrush(Theme.Accent))
                using (GraphicsPath p = Ui.Round(
                    new Rectangle(0, Theme.S(9), Theme.S(3), Height - Theme.S(18)), 1))
                    g.FillPath(b, p);
            }
            else if (_hover)
            {
                Ui.FillRound(g, r, Theme.S(5), Theme.Mix(Theme.Window, Color.White, 0.05));
            }

            TextRenderer.DrawText(g, Text, Font,
                new Rectangle(Theme.S(18), 0, Width - Theme.S(24), Height),
                Active ? Theme.Text : Theme.TextMuted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        }
    }
}
