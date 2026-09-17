using System;
using System.Drawing;
using System.Windows.Forms;

namespace ScreenCrosshair
{
    public partial class MainForm
    {
        private NotifyIcon _tray;
        private bool _trayTipShown;

        private void BuildTray()
        {
            _tray = new NotifyIcon();
            // 托盘按系统小图标尺寸取（100% 是 16，150% 是 24），拿整张多尺寸图标交给 shell
            // 会被缩得发毛；气泡通知里的那张图标也是从这儿来的。
            try { _tray.Icon = Brand.IconAt(SystemInformation.SmallIconSize.Width); }
            catch { }
            _tray.Text = AppInfo.AppName + " " + AppInfo.Version;
            _tray.Visible = true;
            _tray.DoubleClick += delegate { ShowPanel(); };

            ContextMenuStrip m = new ContextMenuStrip();
            m.BackColor = Theme.Card;
            m.ForeColor = Theme.Text;
            m.ShowImageMargin = false;
            m.Font = Theme.Body;
            AddMenu(m, "打开设置", delegate { ShowPanel(); });
            AddMenu(m, "显示 / 隐藏全部准星", delegate { ToggleGlobal(); });
            AddMenu(m, "切换到下一预设", delegate { NextProfile(); });
            AddMenu(m, "启动撤离点倒计时", delegate { StartCountdown(0); });
            AddMenu(m, "启动火箭倒计时", delegate { StartCountdown(1); });
            AddMenu(m, "启动自由倒计时", delegate { StartCountdown(2); });
            AddMenu(m, "停止全部倒计时", delegate { StopAllCountdowns(); });
            m.Items.Add(new ToolStripSeparator());
            AddMenu(m, "开启 / 关闭弱网", delegate { ToggleWeakNetworkFromHotkey(); });
            m.Items.Add(new ToolStripSeparator());
            AddMenu(m, "退出", delegate { ExitApp(); });
            _tray.ContextMenuStrip = m;
        }

        private static void AddMenu(ContextMenuStrip m, string text, EventHandler h)
        {
            ToolStripMenuItem it = new ToolStripMenuItem(text);
            it.BackColor = Theme.Card;
            it.ForeColor = Theme.Text;
            it.Click += h;
            m.Items.Add(it);
        }

        private void ShowPanel()
        {
            if (!Visible) Show();
            if (WindowState != FormWindowState.Normal) WindowState = FormWindowState.Normal;
            EnsureWindowVisible();
            Activate();
            BringToFront();
            PushToUi();
        }

        /// <summary>关窗口 = 收进托盘，准星照常显示</summary>
        private void HideToTray()
        {
            if (_dragMode) ToggleDrag();
            if (_countdownDragMode) ToggleCountdownDrag();
            if (_clockDragMode) ToggleClockDrag();
            _cfg.Save();
            Hide();
            if (!_trayTipShown && _tray != null)
            {
                _trayTipShown = true;
                TrayToast.Pop(AppInfo.AppName + "还在运行",
                    "准星继续显示。双击托盘图标回到设置，右键菜单可以退出。");
            }
        }

        private void ToggleGlobal()
        {
            _cfg.GlobalVisible = !_cfg.GlobalVisible;
            ApplyVisibility();
            _cfg.Save();
        }

        private void ExitApp()
        {
            _reallyExit = true;
            Close();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) { HideToTray(); e.SuppressKeyPress = true; }
            base.OnKeyDown(e);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_reallyExit && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                HideToTray();
                return;
            }
            Cleanup();
            base.OnFormClosing(e);
        }

        private void Cleanup()
        {
            try
            {
                Native.UnregisterHotKey(Handle, HkToggle);
                Native.UnregisterHotKey(Handle, HkSwitch);
                Native.UnregisterHotKey(Handle, HkEvacuation);
                Native.UnregisterHotKey(Handle, HkRocket);
                Native.UnregisterHotKey(Handle, HkFreeCountdown);
                Native.UnregisterHotKey(Handle, HkWeak);
            }
            catch { }

            if (_tick != null) { _tick.Stop(); _tick.Dispose(); _tick = null; }
            if (_grabTimer != null) { _grabTimer.Stop(); _grabTimer.Dispose(); _grabTimer = null; }
            if (_weakGrabTimer != null) { _weakGrabTimer.Stop(); _weakGrabTimer.Dispose(); _weakGrabTimer = null; }
            if (_pickTimer != null) { _pickTimer.Stop(); _pickTimer.Dispose(); _pickTimer = null; }
            if (_toastTimer != null) { _toastTimer.Stop(); _toastTimer.Dispose(); _toastTimer = null; }

            RestoreWeakNetworkOnExit();

            try { _cfg.SaveToDisk(); }
            catch { }

            for (int i = 0; i < _ovl.Count; i++)
            {
                try { _ovl[i].Close(); _ovl[i].Dispose(); }
                catch { }
            }
            _ovl.Clear();

            if (_countdownOverlay != null)
            {
                try { _countdownOverlay.Close(); _countdownOverlay.Dispose(); }
                catch { }
                _countdownOverlay = null;
            }

            if (_clockOverlay != null)
            {
                try { _clockOverlay.Close(); _clockOverlay.Dispose(); }
                catch { }
                _clockOverlay = null;
            }

            if (_weakIndicator != null)
            {
                try { _weakIndicator.Close(); _weakIndicator.Dispose(); }
                catch { }
                _weakIndicator = null;
            }

            if (_tray != null)
            {
                _tray.Visible = false;
                _tray.Dispose();
                _tray = null;
            }
        }
    }
}
