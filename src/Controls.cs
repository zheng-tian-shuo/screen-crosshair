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
        public int Radius = 7;
        public bool IsDangerClose;
        private bool _hover, _down;

        public FlatBtn()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor | ControlStyles.Selectable, true);
            BackColor = Color.Transparent;
            Font = Theme.Body;
            ForeColor = Theme.Text;
            Cursor = Cursors.Hand;
            Height = 30;
            TabStop = true;
        }

        protected override void OnMouseEnter(EventArgs e)
        { _hover = true; Invalidate(); base.OnMouseEnter(e); }

        protected override void OnMouseLeave(EventArgs e)
        { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (Enabled && e.Button == MouseButtons.Left) { _down = true; Invalidate(); }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (_down) { _down = false; Invalidate(); }
            base.OnMouseUp(e);
        }

        protected override void OnEnabledChanged(EventArgs e)
        { Invalidate(); base.OnEnabledChanged(e); }

        protected override void OnGotFocus(EventArgs e)
        { Invalidate(); base.OnGotFocus(e); }

        protected override void OnLostFocus(EventArgs e)
        { _down = false; Invalidate(); base.OnLostFocus(e); }

        protected override bool IsInputKey(Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            return key == Keys.Space || key == Keys.Enter || base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (Enabled && (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter))
            {
                _down = true;
                Invalidate();
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            if (_down && (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter))
            {
                _down = false;
                Invalidate();
                OnClick(EventArgs.Empty);
                e.Handled = true;
            }
            base.OnKeyUp(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);

            Color bg, fg, bd;
            switch (Kind)
            {
                case 1:
                    bg = Theme.Accent; fg = Theme.IsLight ? Color.White : Color.FromArgb(20, 22, 26); bd = Theme.Accent; break;
                case 2:
                    // 危险按钮平时低调，悬停与点击时再亮红底，避免视觉干扰
                    bg = _hover ? Theme.DangerDim : Theme.CardAlt;
                    fg = Theme.Danger;
                    bd = _hover ? Theme.Danger : Theme.Border;
                    break;
                case 3:
                    bg = Color.Transparent; fg = Theme.TextMuted; bd = Color.Transparent; break;
                default:
                    bg = Theme.CardAlt; fg = ForeColor; bd = Theme.Border; break;
            }

            if (!Enabled)
            {
                bg = Theme.Card; fg = Theme.TextFaint; bd = Theme.Border;
            }
            else if (_down)
            {
                if (Kind == 3 && IsDangerClose)
                {
                    bg = Color.FromArgb(241, 112, 122); fg = Color.White; bd = bg;
                }
                else
                {
                    bg = Theme.Mix(bg.A == 0 ? Theme.CardAlt : bg, Color.Black, 0.2);
                    if (Kind == 3) fg = Theme.Text;
                }
            }
            else if (_hover)
            {
                if (Kind == 3 && IsDangerClose)
                {
                    bg = Color.FromArgb(232, 17, 35); fg = Color.White; bd = bg;
                }
                else if (Kind == 3)
                {
                    bg = Theme.CardAlt; fg = Theme.Text; bd = Theme.Border;
                }
                else if (Kind == 2)
                {
                    bg = Theme.DangerDim; fg = Theme.Danger; bd = Theme.Danger;
                }
                else
                {
                    bg = Theme.Mix(bg, Color.White, Kind == 1 ? 0.1 : 0.06); bd = Theme.BorderLit;
                }
            }

            using (GraphicsPath p = Ui.Round(r, Theme.S(Radius)))
            {
                if (bg.A > 0)
                    using (SolidBrush b = new SolidBrush(bg)) g.FillPath(b, p);
                if (bd.A > 0)
                    using (Pen pen = new Pen(bd, 1f)) g.DrawPath(pen, p);
            }

            if (Focused && Enabled)
                Ui.DrawRound(g, new Rectangle(2, 2, Math.Max(0, Width - 5), Math.Max(0, Height - 5)),
                    Theme.S(Math.Max(2, Radius - 2)), Theme.AccentDim, 1f);

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
                     ControlStyles.SupportsTransparentBackColor | ControlStyles.Selectable, true);
            BackColor = Color.Transparent;
            Font = Theme.TabFont;
            Cursor = Cursors.Hand;
            Height = 38;
            TabStop = true;
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

        protected override void OnGotFocus(EventArgs e)
        { Invalidate(); base.OnGotFocus(e); }

        protected override void OnLostFocus(EventArgs e)
        { Invalidate(); base.OnLostFocus(e); }

        protected override bool IsInputKey(Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            return key == Keys.Space || key == Keys.Enter || base.IsInputKey(keyData);
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            if (Enabled && (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter))
            {
                OnClick(EventArgs.Empty);
                e.Handled = true;
            }
            base.OnKeyUp(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);

            if (Active)
            {
                Color bg = Theme.IsLight ? Theme.Card : Theme.CardAlt;
                Ui.FillRound(g, r, Theme.S(8), bg);
                if (Theme.IsLight)
                    Ui.DrawRound(g, r, Theme.S(8), Theme.Border, 1f);
                using (SolidBrush b = new SolidBrush(Theme.Accent))
                using (GraphicsPath p = Ui.Round(
                    new Rectangle(0, Theme.S(8), Theme.S(4), Height - Theme.S(16)), 2))
                    g.FillPath(b, p);
            }
            else if (_hover)
            {
                Ui.FillRound(g, r, Theme.S(6), Theme.IsLight
                    ? Color.FromArgb(235, 239, 245)
                    : Theme.Mix(Theme.Window, Color.White, 0.045));
            }

            if (Focused && Enabled)
                Ui.DrawRound(g, new Rectangle(2, 2, Math.Max(0, Width - 5), Math.Max(0, Height - 5)),
                    Theme.S(4), Theme.AccentDim, 1f);

            TextRenderer.DrawText(g, Text, Active ? Theme.BodyBold : Font,
                new Rectangle(Theme.S(18), 0, Width - Theme.S(24), Height),
                Active ? (Theme.IsLight ? Theme.Accent : Theme.Text) : Theme.TextMuted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        }
    }
}
