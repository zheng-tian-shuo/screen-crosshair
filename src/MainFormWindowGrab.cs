using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace ScreenCrosshair
{
    public partial class MainForm
    {
        private FlatBtn _btnWindowGrab;
        private Timer _windowGrabTimer;
        private int _windowGrabLeft;
        private CrosshairItemSettings _windowGrabItem;

        private void StartWindowGrab()
        {
            if (_windowGrabItem != null)
            {
                CancelWindowGrab();
                ResetFovResult("已取消窗口识别");
                return;
            }
            if (_windowGrabTimer == null)
            {
                _windowGrabTimer = new Timer();
                _windowGrabTimer.Interval = 1000;
                _windowGrabTimer.Tick += WindowGrabTick;
                Disposed += delegate { _windowGrabTimer.Dispose(); };
            }
            _windowGrabItem = Cur();
            if (_windowGrabItem == null) return;
            _windowGrabLeft = 3;
            _btnWindowGrab.Text = "取消识别（3）";
            ResetFovResult("请在 3 秒内切换到游戏窗口");
            Toast("请切换到游戏，3 秒后自动识别画面大小和位置");
            _windowGrabTimer.Start();
        }

        private void CancelWindowGrab()
        {
            if (_windowGrabTimer != null) _windowGrabTimer.Stop();
            _windowGrabItem = null;
            if (_btnWindowGrab != null) _btnWindowGrab.Text = "识别游戏窗口";
        }

        private void WindowGrabTick(object sender, EventArgs e)
        {
            if (_windowGrabItem == null || !object.ReferenceEquals(_windowGrabItem, Cur()))
            {
                CancelWindowGrab();
                return;
            }
            _windowGrabLeft--;
            if (_windowGrabLeft > 0)
            {
                _btnWindowGrab.Text = "取消识别（" + _windowGrabLeft + "）";
                return;
            }
            CancelWindowGrab();
            CaptureGameWindow(Native.GetForegroundWindow());
        }

        private void CaptureGameWindow(IntPtr window)
        {
            uint processId;
            Native.GetWindowThreadProcessId(window, out processId);
            using (Process self = Process.GetCurrentProcess())
            {
                if (processId == 0 || processId == (uint)self.Id ||
                    window == Native.GetShellWindow() || window == Native.GetDesktopWindow() ||
                    (Native.GetWindowLong(window, Native.GWL_EXSTYLE) & Native.WS_EX_TOOLWINDOW) != 0)
                {
                    FovFailure("未选中游戏窗口，请点击识别后切到游戏");
                    return;
                }
            }
            Rectangle area;
            if (!WindowGeometry.TryGetClientArea(window, out area))
            {
                FovFailure("无法读取游戏画面，请保持窗口展开后重试");
                return;
            }
            Screen screen = Screen.FromRectangle(area);
            Rectangle relative;
            if (screen == null || !WindowGeometry.TryRelativeArea(area, screen.Bounds, out relative))
            {
                FovFailure("请把游戏窗口完整移入一块屏幕后重试");
                return;
            }
            CrosshairItemSettings it = Cur();
            if (it == null) return;
            it.TargetWidth = relative.Width;
            it.TargetHeight = relative.Height;
            it.WindowOriginX = relative.X;
            it.WindowOriginY = relative.Y;
            it.ScreenName = screen.DeviceName;
            it.DisplayMode = ProjectionMath.KeepsAspect(SelectedProjectionMode())
                ? ProjectionDisplayMode.WindowFit : ProjectionDisplayMode.Window;
            // Fill only the captured fields; retain the user's FOV and aspect input.
            _loading = true;
            try
            {
                _tbTargetWidth.Text = it.TargetWidth.ToString();
                _tbTargetHeight.Text = it.TargetHeight.ToString();
                _tbWindowX.Text = it.WindowOriginX.ToString();
                _tbWindowY.Text = it.WindowOriginY.ToString();
                SetProjectionMode(it.DisplayMode);
                FillScreens();
                _cbScreen.SelectedIndex = ScreenIndexOf(it.ScreenName);
            }
            finally { _loading = false; }
            UpdateFovModeUi();
            AfterEdit();
            ResetFovResult("已识别 " + relative.Width + "×" + relative.Height + "，点击生成");
            Toast("游戏画面已识别，请返回点击「自动生成准星位置」");
        }
    }
}
