using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ScreenCrosshair
{
    /// <summary>自绘滑块。深色下比系统 TrackBar 干净，也能一直显示当前值。</summary>
    public class Slider : Control
    {
        private int _min = 0, _max = 100, _val = 50;
        private bool _drag;
        public event EventHandler ValueChanged;

        public Slider()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor | ControlStyles.Selectable, true);
            BackColor = Color.Transparent;
            Height = 22;
            Cursor = Cursors.Hand;
            TabStop = true;
        }

        public int Min
        {
            get { return _min; }
            set
            {
                _min = value;
                if (_max < _min) _max = _min;
                SetSilent(_val);
            }
        }

        public int Max
        {
            get { return _max; }
            set
            {
                _max = value;
                if (_min > _max) _min = _max;
                SetSilent(_val);
            }
        }

        public int Value
        {
            get { return _val; }
            set
            {
                int v = value;
                if (v < _min) v = _min;
                if (v > _max) v = _max;
                if (v == _val) return;
                _val = v;
                Invalidate();
                if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
            }
        }

        /// <summary>只改显示不触发事件，用于界面回填</summary>
        public void SetSilent(int v)
        {
            if (v < _min) v = _min;
            if (v > _max) v = _max;
            _val = v;
            Invalidate();
        }

        private static int Knob { get { return Theme.S(13); } }

        private void Pick(int x)
        {
            int usable = Width - Knob;
            if (usable <= 0) return;
            double t = (double)(x - Knob / 2) / usable;
            if (t < 0) t = 0;
            if (t > 1) t = 1;
            Value = _min + (int)Math.Round(t * (_max - _min));
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (Enabled && e.Button == MouseButtons.Left) { Focus(); _drag = true; Pick(e.X); }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_drag) Pick(e.X);
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        { _drag = false; base.OnMouseUp(e); }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            // 滑块的滚轮不改数值，但焦点落在滑块上时仍要让设置页能继续滚动。
            if (Ui.ScrollPage(this, e.Delta))
            {
                HandledMouseEventArgs handled = e as HandledMouseEventArgs;
                if (handled != null) handled.Handled = true;
            }
            base.OnMouseWheel(e);
        }

        protected override void OnGotFocus(EventArgs e)
        { Invalidate(); base.OnGotFocus(e); }

        protected override void OnLostFocus(EventArgs e)
        { _drag = false; Invalidate(); base.OnLostFocus(e); }

        protected override bool IsInputKey(Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            return key == Keys.Left || key == Keys.Right || key == Keys.Up || key == Keys.Down ||
                key == Keys.Home || key == Keys.End || base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (Enabled)
            {
                if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Down) Value--;
                else if (e.KeyCode == Keys.Right || e.KeyCode == Keys.Up) Value++;
                else if (e.KeyCode == Keys.Home) Value = Min;
                else if (e.KeyCode == Keys.End) Value = Max;
                else { base.OnKeyDown(e); return; }
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int cy = Height / 2;
            int usable = Width - Knob;
            if (usable <= 0) return;
            double t = (_max > _min) ? (double)(_val - _min) / (_max - _min) : 0;
            int kx = Knob / 2 + (int)Math.Round(t * usable);
            int th = Theme.S(4);

            Ui.FillRound(g, new Rectangle(Knob / 2, cy - th / 2, usable, th), th / 2, Theme.Border);
            if (kx > Knob / 2)
                Ui.FillRound(g, new Rectangle(Knob / 2, cy - th / 2, kx - Knob / 2, th), th / 2,
                    Enabled ? Theme.Accent : Theme.TextFaint);

            Rectangle kr = new Rectangle(kx - Knob / 2, cy - Knob / 2, Knob, Knob);
            using (SolidBrush b = new SolidBrush(Enabled ? Theme.Accent : Theme.TextFaint))
                g.FillEllipse(b, kr);
            int inset = Theme.S(4);
            using (SolidBrush b = new SolidBrush(Theme.Window))
                g.FillEllipse(b, kr.X + inset, kr.Y + inset, Knob - inset * 2, Knob - inset * 2);
            if (Focused && Enabled)
                using (Pen p = new Pen(Theme.AccentDim, 1f))
                    g.DrawEllipse(p, kr);
        }
    }

    /// <summary>圆角卡片容器，顶部可带小标题</summary>
    public class Card : Panel
    {
        public string Caption = "";

        public Card()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Card;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (SolidBrush b = new SolidBrush(Theme.Window))
                g.FillRectangle(b, ClientRectangle);

            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Ui.FillRound(g, r, Theme.S(8), Theme.Card);
            Ui.DrawRound(g, r, Theme.S(8), Theme.Border, 1f);

            if (!string.IsNullOrEmpty(Caption))
            {
                int barW = Theme.S(3);
                int barH = Theme.S(14);
                int barX = Theme.S(14);
                int barY = Theme.S(11);
                using (GraphicsPath bp = Ui.Round(new Rectangle(barX, barY, barW, barH), Theme.S(1)))
                using (SolidBrush bb = new SolidBrush(Theme.Accent))
                    g.FillPath(bb, bp);

                TextRenderer.DrawText(g, Caption, Theme.SectionHead,
                    new Point(barX + barW + Theme.S(6), Theme.S(8)), Theme.Text);
                using (Pen p = new Pen(Theme.Border, 1f))
                    g.DrawLine(p, Theme.S(14), Theme.S(32), Width - Theme.S(15), Theme.S(32));
            }
        }
    }
}
