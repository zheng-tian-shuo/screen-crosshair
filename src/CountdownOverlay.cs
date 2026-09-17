using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace ScreenCrosshair
{
    internal class CountdownDisplay
    {
        public string Name;
        public int RemainingSeconds;
        public bool Ended;
        /// <summary>时钟这类现成文本直接放这儿；为空才按 RemainingSeconds 格式化</summary>
        public string Text;
    }

    /// <summary>
    /// 屏幕上的倒计时 HUD，鼠标穿透且不会抢游戏焦点。
    /// 采用低透明度圆角底板和固定列宽，确保复杂背景上可读，数字跳动时也不会左右抖动。
    /// </summary>
    internal sealed class CountdownOverlayForm : Form
    {
        private readonly List<CountdownDisplay> _items = new List<CountdownDisplay>();
        private readonly LayeredSurface _surf = new LayeredSurface();
        private int _fontSize = 28;
        private int _hudOpacity = 78;
        private bool _showPlate = true;
        private bool _showMarkers = true;
        private bool _dragMode;
        private bool _updatingBounds;
        private int _nameW, _timeW;

        private const int PadX = 14;      // 下面这些都按 96 DPI 写，用的时候过一遍 Theme.S
        private const int PadY = 10;
        private const int Gap = 10;
        private const int RowGap = 2;
        private const int ClockGap = 6;
        private const int NameMinW = 8;
        private const int HintH = 24;     // 「拖动定位」提示自己占一条底边，只在拖动时才有
        private const string DragHint = "拖到合适位置，再点「完成定位」";

        public event EventHandler PositionChangedByUser;

        public CountdownOverlayForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            MinimizeBox = false;
            MaximizeBox = false;
            Text = "";
            SetStyle(ControlStyles.Opaque, true);
            ClientSize = new Size(Theme.S(160), Theme.S(PadY * 2));
        }
        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= Native.WS_EX_TOOLWINDOW | Native.WS_EX_LAYERED;
                // 拖动定位时必须去掉穿透，否则鼠标直接透到底下的窗口，面板抓不住。
                // 这里一定要跟着 _dragMode 走：面板是懒加载的，第一次点「拖动定位」时
                // 句柄还没建好，SetMoveMode 里的 SetWindowLong 会被跳过，样式全靠这里定。
                if (!_dragMode)
                    cp.ExStyle |= Native.WS_EX_TRANSPARENT | Native.WS_EX_NOACTIVATE;
                return cp;
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // 分层窗口的内容全部由 UpdateLayeredWindow 提供，这里什么都不用画
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Redraw();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_NCHITTEST)
            {
                m.Result = (IntPtr)(_dragMode ? Native.HTCAPTION : Native.HTTRANSPARENT);
                return;
            }
            base.WndProc(ref m);
        }

        protected override void OnLocationChanged(EventArgs e)
        {
            base.OnLocationChanged(e);
            if (_updatingBounds || !_dragMode) return;
            if (PositionChangedByUser != null)
                PositionChangedByUser(this, EventArgs.Empty);
        }
        /// <summary>
        /// 字号一律用像素单位。pt 单位的字体会被系统再按 DPI 放大一次，
        /// 而面板尺寸是这边自己用 Theme.S 算的，两套缩放对不上就会把数字挤出格子。
        /// </summary>
        private Font TimerFont()
        {
            return new Font("Consolas", Theme.S(_fontSize), FontStyle.Bold, GraphicsUnit.Pixel);
        }

        private Font NameFont()
        {
            return new Font("Microsoft YaHei UI", Theme.S(12), FontStyle.Bold, GraphicsUnit.Pixel);
        }

        private Font HintFont()
        {
            return new Font("Microsoft YaHei UI", Theme.S(11), FontStyle.Regular, GraphicsUnit.Pixel);
        }

        private int RowHeight()
        {
            return Theme.S(_fontSize) + Theme.S(IsClockOnly() ? 4 : 12);
        }

        private bool IsClockOnly()
        {
            return _items.Count == 1 && !string.IsNullOrEmpty(_items[0].Text);
        }

        public void SetFontSize(int size)
        {
            if (size < 14) size = 14;
            if (size > 72) size = 72;
            if (_fontSize == size) return;
            _fontSize = size;
            Relayout();
            Redraw();
        }

        public void SetStyle(int opacity, bool showPlate, bool showMarkers)
        {
            if (opacity < 20) opacity = 20;
            if (opacity > 100) opacity = 100;
            if (_hudOpacity == opacity && _showPlate == showPlate && _showMarkers == showMarkers)
                return;
            _hudOpacity = opacity;
            _showPlate = showPlate;
            _showMarkers = showMarkers;
            Redraw();
        }

        public void SetItems(IList<CountdownDisplay> items)
        {
            // 开了时钟以后这里每 500ms 就会被调一次，秒没跳就什么都不用做
            if (SameAsCurrent(items)) return;
            _items.Clear();
            if (items != null)
                for (int i = 0; i < items.Count; i++) _items.Add(items[i]);
            Relayout();
            Redraw();
        }

        private bool SameAsCurrent(IList<CountdownDisplay> items)
        {
            int n = items == null ? 0 : items.Count;
            if (n != _items.Count) return false;
            for (int i = 0; i < n; i++)
            {
                if (_items[i].Name != items[i].Name) return false;
                if (_items[i].Ended != items[i].Ended) return false;
                if (ValueOf(_items[i]) != ValueOf(items[i])) return false;
            }
            return true;
        }

        /// <summary>一行右边显示什么：时钟给的是现成文本，倒计时按剩余秒数格式化</summary>
        private static string ValueOf(CountdownDisplay it)
        {
            if (it.Ended) return "00:00";
            return string.IsNullOrEmpty(it.Text) ? Format(it.RemainingSeconds) : it.Text;
        }
        public void SetMoveMode(bool on)
        {
            if (_dragMode == on) return;
            _dragMode = on;
            if (IsHandleCreated)
            {
                // 和 CreateParams 里那组标志保持一致，两边不一致就会出现「按钮变成
                // 完成定位了，面板却还在穿透」的怪状态
                int ex = Native.GetWindowLong(Handle, Native.GWL_EXSTYLE);
                if (on) ex &= ~(Native.WS_EX_TRANSPARENT | Native.WS_EX_NOACTIVATE);
                else ex |= Native.WS_EX_TRANSPARENT | Native.WS_EX_NOACTIVATE;
                Native.SetWindowLong(Handle, Native.GWL_EXSTYLE, ex);
            }
            Relayout();
            Redraw();
        }

        /// <summary>
        /// 按当前内容把面板收到刚好的大小。锚点是右上角，所以宽度变了数字也不会左右跳。
        /// 拖动提示那条底边是往下长的，同样不会顶着数字动。
        /// </summary>
        private void Relayout()
        {
            using (Bitmap probe = new Bitmap(1, 1))
            using (Graphics g = Graphics.FromImage(probe))
            using (Font nf = NameFont())
            using (Font tf = TimerFont())
            {
                g.TextRenderingHint = TextRenderingHint.AntiAlias;
                float nw = 0f;
                for (int i = 0; i < _items.Count; i++)
                {
                    nw = Math.Max(nw, g.MeasureString(_items[i].Name, nf).Width);
                }
                bool clockOnly = IsClockOnly();
                _nameW = clockOnly ? 0 : Math.Max(Theme.S(NameMinW), (int)Math.Ceiling(nw));
                // 预留完整的时分秒宽度，避免从 59:59 切到 1:00:00 时面板变窄。
                _timeW = (int)Math.Ceiling(g.MeasureString("00:00:00", tf).Width);

                int pad = Theme.S(clockOnly ? 8 : PadX);
                int padY = Theme.S(clockOnly ? 4 : PadY);
                int w = pad * 2 + _timeW + (clockOnly ? 0 : _nameW + Theme.S(Gap));
                int h = padY * 2 + Math.Max(0, _items.Count * RowHeight()
                    + Math.Max(0, _items.Count - 1) * Theme.S(RowGap));
                if (_items.Count > 1 && !string.IsNullOrEmpty(_items[0].Text))
                    h += Theme.S(ClockGap);
                if (_dragMode)
                {
                    h += Theme.S(HintH);
                    using (Font hf = HintFont())
                    {
                        int need = (int)Math.Ceiling(g.MeasureString(DragHint, hf).Width) + pad * 2;
                        if (w < need) w = need;
                    }
                }
                int min = Theme.S(120);
                if (w < min) w = min;
                ClientSize = new Size(w, h);
            }
        }
        public void PlaceOnScreen(Screen screen, int rightOffset, int topOffset)
        {
            if (screen == null) screen = Screen.PrimaryScreen;
            if (screen == null) return;
            Rectangle b = screen.WorkingArea;
            if (rightOffset < 0) rightOffset = 0;
            if (topOffset < 0) topOffset = 0;
            int x = b.Right - Width - rightOffset;
            int y = b.Top + topOffset;
            if (x < b.Left) x = b.Left;
            if (y + Height > b.Bottom) y = Math.Max(b.Top, b.Bottom - Height);
            // 开了时钟这里每 500ms 走一遍，位置没变就不用再推一次分层窗口
            Point want = new Point(x, y);
            if (Location == want) return;
            _updatingBounds = true;
            try { Location = want; }
            finally { _updatingBounds = false; }
            Redraw();
        }

        /// <summary>
        /// 显示并压到最上层，但绝不激活。
        ///
        /// 原来这里是 Show() + BringToFront()。BringToFront() 对顶层窗口走的是
        /// SetWindowPos(HWND_TOP, SWP_NOMOVE|SWP_NOSIZE)，唯独没带 SWP_NOACTIVATE，
        /// 于是每次调用都会激活一次本窗口。开着倒计时或时钟时这行代码每 500ms 就跑一遍，
        /// 托盘右键菜单靠激活状态维持，一被打断就自动收起——表现就是菜单闪一下点不着。
        ///
        /// 而且 Z 序只在窗口刚显示出来时需要摆一次：本身是 TopMost，之后一直待在最上层。
        /// </summary>
        public void ShowTopNoActivate()
        {
            bool wasVisible = Visible;
            if (!wasVisible) Show();
            if (wasVisible || !IsHandleCreated) return;
            Native.SetWindowPos(Handle, Native.HWND_TOPMOST, 0, 0, 0, 0,
                Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
        }

        /// <summary>重画整块面板并推给系统</summary>
        public void Redraw()
        {
            if (!IsHandleCreated || IsDisposed) return;
            Bitmap bmp = _surf.Begin(Width, Height);
            if (bmp == null) return;

            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAlias;
                DrawPanelBackground(g);
                DrawRows(g);
                if (_dragMode) DrawDragPlate(g);
            }
            _surf.Push(Handle, Left, Top);
        }
        private void DrawRows(Graphics g)
        {
            if (_items.Count == 0) return;
            bool clockOnly = IsClockOnly();
            int pad = Theme.S(clockOnly ? 8 : PadX);
            int padY = Theme.S(clockOnly ? 4 : PadY);
            int rowH = RowHeight();
            float nameRing = Math.Max(1.5f, Theme.S(12) / 9f);
            float timeRing = Math.Max(2f, Theme.S(_fontSize) / 14f);

            using (Font nf = NameFont())
            using (Font tf = TimerFont())
            using (StringFormat nameSf = new StringFormat())
            using (StringFormat timeSf = new StringFormat())
            {
                nameSf.Alignment = StringAlignment.Near;
                nameSf.LineAlignment = StringAlignment.Center;
                timeSf.Alignment = StringAlignment.Far;
                timeSf.LineAlignment = StringAlignment.Center;

                for (int i = 0; i < _items.Count; i++)
                {
                    CountdownDisplay it = _items[i];
                    float y = padY + i * (rowH + Theme.S(RowGap));
                    if (i > 0 && !string.IsNullOrEmpty(_items[0].Text)) y += Theme.S(ClockGap);
                    bool clock = !string.IsNullOrEmpty(it.Text);
                    Color timeColor = clock ? Theme.Hud.TextMuted : TimeColor(it.RemainingSeconds);
                    Color marker = clock ? Theme.Hud.BorderLit : timeColor;

                    if (!clock && _showMarkers)
                    {
                        using (SolidBrush b = new SolidBrush(marker))
                            g.FillRectangle(b, Theme.S(6), y + Theme.S(8), Theme.S(3), rowH - Theme.S(16));
                    }
                    if (i > 0 && !clock)
                    {
                        using (Pen p = new Pen(Color.FromArgb(80, Theme.Hud.BorderLit), 1f))
                            g.DrawLine(p, pad, y - Theme.S(2), Width - pad, y - Theme.S(2));
                    }
                    if (!clockOnly && !clock)
                    {
                        OutlineText(g, it.Name, nf,
                            new RectangleF(pad + Theme.S(7), y, _nameW - Theme.S(7), rowH), nameSf,
                            Theme.Hud.Text, nameRing);
                    }
                    OutlineText(g, ValueOf(it), tf,
                        new RectangleF(Width - pad - _timeW, y, _timeW, rowH), timeSf,
                        timeColor, timeRing);
                }
            }
        }

        private static Color TimeColor(int seconds)
        {
            if (seconds <= 10) return Theme.Hud.Danger;
            if (seconds <= 60) return Theme.Hud.Warn;
            return Theme.Hud.Accent;
        }

        private void DrawPanelBackground(Graphics g)
        {
            if (!_showPlate && !_dragMode) return;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            int alpha = (int)Math.Round(255 * _hudOpacity / 100.0);
            using (GraphicsPath p = Ui.Round(r, Theme.S(IsClockOnly() ? 7 : 9)))
            using (SolidBrush b = new SolidBrush(Color.FromArgb(alpha, 12, 14, 18)))
                g.FillPath(b, p);
            using (GraphicsPath p = Ui.Round(r, Theme.S(IsClockOnly() ? 7 : 9)))
            using (Pen pen = new Pen(Color.FromArgb(Math.Min(220, alpha + 30), Theme.Hud.BorderLit), Theme.S(1)))
                g.DrawPath(pen, p);
        }

        /// <summary>
        /// 拖动定位时在 HUD 上叠加金色提示层，让用户看清能抓的范围，
        /// 也保证像素 alpha 不为 0——分层窗口上全透明的像素是抓不住的。
        /// </summary>
        private void DrawDragPlate(Graphics g)
        {
            using (GraphicsPath plate = Ui.Round(new Rectangle(0, 0, Width - 1, Height - 1), Theme.S(9)))
            using (SolidBrush b = new SolidBrush(Color.FromArgb(62, Theme.Hud.Accent)))
                g.FillPath(b, plate);
            using (Pen p = new Pen(Color.FromArgb(190, Theme.Hud.Accent), 1f))
            {
                p.DashStyle = DashStyle.Dash;
                using (GraphicsPath border = Ui.Round(new Rectangle(1, 1, Width - 3, Height - 3), Theme.S(8)))
                    g.DrawPath(p, border);
            }

            int hintH = Theme.S(HintH);
            using (Font f = HintFont())
            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                OutlineText(g, DragHint, f,
                    new RectangleF(0, Height - hintH, Width, hintH), sf, Theme.Hud.Accent, 1.5f);
            }
        }
        /// <summary>
        /// 文字仍保留深色描边，避免半透明底板边缘和亮背景叠加时降低对比度。
        /// 走 GraphicsPath 而不是「错开几像素画好几遍」：描边一圈粗细一致，也不糊。
        /// </summary>
        private static void OutlineText(Graphics g, string s, Font f, RectangleF r,
            StringFormat sf, Color fill, float ring)
        {
            if (string.IsNullOrEmpty(s)) return;
            using (GraphicsPath p = new GraphicsPath())
            {
                // 字体是像素单位建的，f.Size 就是 em 的像素数，正好是 AddString 要的值
                p.AddString(s, f.FontFamily, (int)f.Style, f.Size, r, sf);
                using (Pen pen = new Pen(Color.FromArgb(215, 8, 9, 12), ring * 2f))
                {
                    pen.LineJoin = LineJoin.Round;
                    g.DrawPath(pen, p);
                }
                using (SolidBrush b = new SolidBrush(fill))
                    g.FillPath(b, p);
            }
        }

        public static string Format(int seconds)
        {
            if (seconds < 0) seconds = 0;
            TimeSpan t = TimeSpan.FromSeconds(seconds);
            if (t.TotalHours >= 1)
                return string.Format("{0:00}:{1:00}:{2:00}",
                    (int)t.TotalHours, t.Minutes, t.Seconds);
            return string.Format("{0:00}:{1:00}", t.Minutes, t.Seconds);
        }

        protected override void Dispose(bool disposing)
        {
            _surf.Dispose();
            base.Dispose(disposing);
        }
    }
}
