using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ScreenCrosshair
{
    /// <summary>弱网状态牌。只显示状态，可用鼠标拖动移动。</summary>
    internal sealed class WeakStatusOverlayForm : Form
    {
        private bool _active;
        private bool _busy;
        private bool _dragging;
        private Point _dragOffset;

        public event EventHandler PositionChangedByUser;

        public WeakStatusOverlayForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            Cursor = Cursors.SizeAll;
            ClientSize = new Size(Theme.S(186), Theme.S(34));
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
                cp.ExStyle |= Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE;
                return cp;
            }
        }

        public void SetState(bool active, bool busy)
        {
            _active = active;
            _busy = busy;
            Invalidate();
        }

        public void ShowTopNoActivate()
        {
            bool wasVisible = Visible;
            if (!wasVisible) Show();
            if (wasVisible || !IsHandleCreated) return;
            Native.SetWindowPos(Handle, Native.HWND_TOPMOST, 0, 0, 0, 0,
                Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Color accent = _active ? Theme.Green : Theme.TextFaint;
            using (GraphicsPath p = Ui.Round(r, Theme.S(7)))
            using (SolidBrush bg = new SolidBrush(Theme.Card))
            using (Pen border = new Pen(_active ? Theme.Green : Theme.Border, 1f))
            using (SolidBrush dot = new SolidBrush(accent))
            {
                e.Graphics.FillPath(bg, p);
                e.Graphics.DrawPath(border, p);
                e.Graphics.FillEllipse(dot, Theme.S(12), Theme.S(11), Theme.S(12), Theme.S(12));
            }
            string state = _busy ? "切换中…" : (_active ? "已开启" : "未开启");
            string text = "灵魂出窍  " + state;
            TextRenderer.DrawText(e.Graphics, text, Theme.Small,
                new Rectangle(Theme.S(32), 0, Width - Theme.S(40), Height),
                _active ? Theme.Text : Theme.TextMuted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
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
    }
}
