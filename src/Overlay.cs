using System;
using System.Drawing;
using System.Windows.Forms;

namespace ScreenCrosshair
{
    /// <summary>
    /// 单个准星的覆盖窗口。用 UpdateLayeredWindow 做逐像素半透明，
    /// 不再依赖旧版本的 TransparencyKey 洋红抠色（抠色会把准星边缘的抗锯齿像素啃掉，
    /// 而且颜色一旦撞上洋红就会整块消失）。
    /// </summary>
    public partial class OverlayForm : Form
    {
        private CrosshairItemSettings _s;
        private bool _dragMode;
        private bool _updatingBounds;   // 程序自己在挪窗口，别当成用户拖动
        private bool _movingByUser;

        /// <summary>用户手动拖动结束后触发，让设置界面把坐标同步回输入框</summary>
        public event EventHandler PositionChangedByUser;

        public OverlayForm(CrosshairItemSettings s)
        {
            _s = s;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            MinimizeBox = false;
            MaximizeBox = false;
            Text = "";
            SetStyle(ControlStyles.Opaque, true);
        }

        public CrosshairItemSettings Item
        {
            get { return _s; }
        }

        /// <summary>Show() 不抢游戏焦点</summary>
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
                // 只有非拖动状态才穿透鼠标，和 v1 的行为一致
                if (!_dragMode)
                    cp.ExStyle |= Native.WS_EX_TRANSPARENT | Native.WS_EX_NOACTIVATE;
                return cp;
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_ENTERSIZEMOVE) _movingByUser = _dragMode;
            if (m.Msg == Native.WM_EXITSIZEMOVE) _movingByUser = false;
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
            if (_updatingBounds || !_movingByUser) return;

            // 走到这里说明是用户拖出来的位移，把中心点换算回目标屏坐标
            Rectangle b = TargetScreen().Bounds;
            _s.Centered = false;
            _s.AutoScreenPosition = false;
            _s.X = Left + Width / 2 - b.Left;
            _s.Y = Top + Height / 2 - b.Top;
            if (PositionChangedByUser != null) PositionChangedByUser(this, EventArgs.Empty);
        }

        /// <summary>切换拖动定位。用 SetWindowLong 改样式，避免重建句柄导致的闪一下。</summary>
        public void SetMoveMode(bool on)
        {
            if (_dragMode == on) return;
            _dragMode = on;
            if (IsHandleCreated)
            {
                int ex = Native.GetWindowLong(Handle, Native.GWL_EXSTYLE);
                if (on) ex &= ~(Native.WS_EX_TRANSPARENT | Native.WS_EX_NOACTIVATE);
                else ex |= Native.WS_EX_TRANSPARENT | Native.WS_EX_NOACTIVATE;
                Native.SetWindowLong(Handle, Native.GWL_EXSTYLE, ex);
            }
            Redraw();
        }

        /// <summary>设置变了：重算尺寸位置并重绘</summary>
        public void Apply(CrosshairItemSettings s)
        {
            _s = s;
            RefreshPosition();
            Redraw();
        }

        internal void SetItem(CrosshairItemSettings s)
        {
            _s = s;
        }

        public bool RefreshPosition()
        {
            return RefreshPosition(TargetScreen().Bounds);
        }

        internal bool RefreshPosition(Rectangle b)
        {
            if (_movingByUser) return false;
            int n = _s.CanvasSize();          // 奇数，保证有唯一中心像素
            int cx, cy;
            if (_s.Centered)
            {
                cx = b.Left + b.Width / 2;
                cy = b.Top + b.Height / 2;
            }
            else
            {
                cx = b.Left + _s.X;
                cy = b.Top + _s.Y;
            }

            Rectangle want = new Rectangle(cx - n / 2, cy - n / 2, n, n);
            if (Bounds == want) return false;
            _updatingBounds = true;
            try { Bounds = want; }
            finally { _updatingBounds = false; }
            return true;
        }

        /// <summary>准星该显示在哪块屏。指定了就用指定的，否则跟随鼠标所在屏。</summary>
        public Screen TargetScreen()
        {
            try
            {
                if (!string.IsNullOrEmpty(_s.ScreenName))
                {
                    Screen[] all = Screen.AllScreens;
                    for (int i = 0; i < all.Length; i++)
                        if (all[i].DeviceName == _s.ScreenName) return all[i];
                }
                return Screen.FromPoint(Control.MousePosition);
            }
            catch { return Screen.PrimaryScreen; }
        }
    }
}
