using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace ScreenCrosshair
{
    /// <summary>弱网状态牌。只显示状态，可用鼠标拖动移动。</summary>
    internal sealed class WeakStatusOverlayForm : Form
    {
        private readonly LayeredSurface _surface = new LayeredSurface();
        private bool _active;
        private bool _busy;
        private bool _dragging;
        private int _plateOpacity = 100;
        private Point _dragOffset;

        public event EventHandler PositionChangedByUser;

        public WeakStatusOverlayForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            Cursor = Cursors.SizeAll;
            ClientSize = new Size(Theme.S(210), Theme.S(38));
            Rectangle b = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(b.Right - Width - Theme.S(18), b.Top + Theme.S(92));
            ApplyRegion();
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE | Native.WS_EX_LAYERED;
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Redraw();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // The complete window is supplied by UpdateLayeredWindow.
        }

        public void SetState(bool active, bool busy)
        {
            _active = active;
            _busy = busy;
            Redraw();
        }

        public void SetOpacityPercent(int percent)
        {
            if (percent < 20) percent = 20;
            if (percent > 100) percent = 100;
            if (_plateOpacity == percent) return;
            _plateOpacity = percent;
            Redraw();
        }

        public void ShowTopNoActivate()
        {
            bool wasVisible = Visible;
            if (!wasVisible) Show();
            if (wasVisible || !IsHandleCreated) return;
            Native.SetWindowPos(Handle, Native.HWND_TOPMOST, 0, 0, 0, 0,
                Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
        }

        private void Redraw()
        {
            if (!IsHandleCreated || IsDisposed) return;
            Bitmap bmp = _surface.Begin(Width, Height);
            if (bmp == null) return;

            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
                Color accent = _active ? Theme.Hud.Green : (_busy ? Theme.Hud.Warn : Theme.Hud.TextFaint);
                // Keep a readable plate even at the lowest user-selected opacity.
                int alpha = 150 + (int)Math.Round(105 * _plateOpacity / 100.0);
                using (GraphicsPath p = Ui.Round(r, Theme.S(7)))
                using (SolidBrush bg = new SolidBrush(Color.FromArgb(alpha, Theme.Hud.Card)))
                using (Pen border = new Pen(Color.FromArgb(Math.Min(255, alpha + 20), accent), 1f))
                {
                    g.FillPath(bg, p);
                    g.DrawPath(border, p);
                }

                string state = _busy ? "切换中…" : (_active ? "已开启" : "未开启");
                string title = "灵魂出窍";
                using (Font titleFont = new Font(Theme.Small.FontFamily, Theme.S(12),
                    FontStyle.Bold, GraphicsUnit.Pixel))
                using (Font stateFont = new Font(Theme.Small.FontFamily, Theme.S(11),
                    FontStyle.Bold, GraphicsUnit.Pixel))
                {
                    TextRenderingHint oldHint = g.TextRenderingHint;
                    g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                    TextFormatFlags textFlags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix |
                        TextFormatFlags.VerticalCenter;
                    Size titleSize = TextRenderer.MeasureText(g, title, titleFont,
                        new Size(Width, Height), TextFormatFlags.NoPadding);
                    Size stateSize = TextRenderer.MeasureText(g, state, stateFont,
                        new Size(Width, Height), TextFormatFlags.NoPadding);
                    int stateWidth = stateSize.Width + Theme.S(14);
                    int dotSize = Theme.S(12);
                    int gap = Theme.S(10);
                    int contentWidth = dotSize + gap + titleSize.Width + gap + stateWidth;
                    int contentX = Math.Max(Theme.S(8), (Width - contentWidth) / 2);
                    int dotY = (Height - dotSize) / 2;
                    int textX = contentX + dotSize + gap;
                    int stateX = textX + titleSize.Width + gap;
                    int stateHeight = Theme.S(24);
                    int stateY = (Height - stateHeight) / 2;
                    Rectangle stateRect = new Rectangle(stateX, stateY, stateWidth, stateHeight);
                    Color stateBg = _active ? Color.FromArgb(55, Theme.Hud.Green)
                        : (_busy ? Color.FromArgb(60, Theme.Hud.Warn) : Color.FromArgb(55, Theme.Hud.TextFaint));
                    Color stateFg = _active ? Theme.Hud.Green
                        : (_busy ? Theme.Hud.Warn : Theme.Hud.Text);
                    using (SolidBrush dot = new SolidBrush(accent))
                        g.FillEllipse(dot, contentX, dotY, dotSize, dotSize);
                    using (GraphicsPath statePath = Ui.Round(stateRect, Theme.S(5)))
                    using (SolidBrush stateBrush = new SolidBrush(stateBg))
                    {
                        g.FillPath(stateBrush, statePath);
                    }
                    TextRenderer.DrawText(g, title, titleFont,
                        new Rectangle(textX, stateY, titleSize.Width, stateHeight), Theme.Hud.Text, textFlags);
                    TextRenderer.DrawText(g, state, stateFont, stateRect, stateFg,
                        textFlags | TextFormatFlags.HorizontalCenter);
                    g.TextRenderingHint = oldHint;
                }
            }
            _surface.Push(Handle, Left, Top);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _dragging = true;
                _dragOffset = e.Location;
                Capture = true;
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_dragging && e.Button == MouseButtons.Left)
            {
                Point p = PointToScreen(e.Location);
                Location = new Point(p.X - _dragOffset.X, p.Y - _dragOffset.Y);
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && _dragging)
            {
                _dragging = false;
                Capture = false;
                if (PositionChangedByUser != null) PositionChangedByUser(this, EventArgs.Empty);
            }
            base.OnMouseUp(e);
        }

        private void ApplyRegion()
        {
            using (GraphicsPath p = Ui.Round(new Rectangle(0, 0, Width, Height), Theme.S(7)))
                Region = new Region(p);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _surface.Dispose();
            base.Dispose(disposing);
        }
    }
}
