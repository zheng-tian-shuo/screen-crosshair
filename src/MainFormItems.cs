using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace ScreenCrosshair
{
    public partial class MainForm
    {
        private void AddItem()
        {
            if (_cfg.Items.Count >= 16) { Toast("一套预设最多 16 个准星"); return; }
            CrosshairItemSettings it = new CrosshairItemSettings();
            it.Name = "准星 " + (_cfg.Items.Count + 1);
            _cfg.Items.Add(it);
            _cfg.SelectedItem = _cfg.Items.Count - 1;
            RebuildOverlays();
            PushToUi();
            _cfg.Save();
        }

        private void DupItem()
        {
            CrosshairItemSettings it = Cur();
            if (it == null) return;
            if (_cfg.Items.Count >= 16) { Toast("一套预设最多 16 个准星"); return; }
            CrosshairItemSettings c = it.Clone();
            c.Name = it.Name + " 副本";
            _cfg.Items.Insert(_cfg.SelectedItem + 1, c);
            _cfg.SelectedItem++;
            RebuildOverlays();
            PushToUi();
            _cfg.Save();
        }

        private void RenameItem()
        {
            CrosshairItemSettings it = Cur();
            if (it == null) return;
            string name = AskForm.Ask(this, "准星改名", "输入一个便于识别的名称", it.Name);
            if (string.IsNullOrEmpty(name) || name == it.Name) return;
            it.Name = name;
            PushToUi();
            _cfg.Save();
        }

        private void DelItem()
        {
            if (_cfg.Items.Count <= 1) { Toast("至少要留一个准星"); return; }
            _cfg.Items.RemoveAt(_cfg.SelectedItem);
            if (_cfg.SelectedItem >= _cfg.Items.Count) _cfg.SelectedItem = _cfg.Items.Count - 1;
            RebuildOverlays();
            PushToUi();
            _cfg.Save();
        }

        /// <summary>用户手动拖完覆盖层，把坐标同步回界面</summary>
        private void OverlayMoved(object sender, EventArgs e)
        {
            OverlayForm f = sender as OverlayForm;
            if (f == null) return;
            List<CrosshairItemSettings> list = _cfg.Items;
            int idx = list.IndexOf(f.Item);
            if (idx < 0) return;
            SaveSoon();
            if (idx >= 0 && idx != _cfg.SelectedItem) return;   // 拖的不是当前选中的就只存不刷界面
            ResetFovResult();
            _loading = true;
            try
            {
                _chkCentered.SetSilent(f.Item.Centered);
                _tbX.Text = f.Item.X.ToString();
                _tbY.Text = f.Item.Y.ToString();
            }
            finally { _loading = false; }
            SyncLabels();
            SaveSoon();
        }

        /// <summary>截图取点。取点前把面板和覆盖层都藏掉，免得挡住爆点。</summary>
        private void PickOnScreen()
        {
            CrosshairItemSettings it = Cur();
            if (it == null) return;

            // 取点期间不允许重复打开多个截图窗口。
            if (_pickTimer != null && _pickTimer.Enabled) return;

            for (int i = 0; i < _ovl.Count; i++) if (_ovl[i].Visible) _ovl[i].Hide();
            Hide();

            if (_pickTimer == null)
            {
                _pickTimer = new Timer();
                _pickTimer.Interval = 180;
                _pickTimer.Tick += PickTimerTick;
            }
            _pickTimer.Start();
        }

        private void PickTimerTick(object sender, EventArgs e)
        {
            _pickTimer.Stop();
            CrosshairItemSettings it = Cur();
            if (it == null) { Show(); ApplyVisibility(); return; }

            bool ok = false;
            Point p = Point.Empty;
            try
            {
                using (PickerForm f = new PickerForm())
                {
                    f.ShowDialog();
                    ok = f.Ok;
                    p = f.Picked;
                }
            }
            finally
            {
                Show();
                Activate();
                ApplyVisibility();
            }
            if (!ok) { ApplyVisibility(); return; }

            Screen sc = Screen.FromPoint(p);
            if (!string.IsNullOrEmpty(it.ScreenName) && it.ScreenName != sc.DeviceName)
                it.ScreenName = sc.DeviceName;
            it.Centered = false;
            it.AutoScreenPosition = false;
            it.X = p.X - sc.Bounds.Left;
            it.Y = p.Y - sc.Bounds.Top;

            _loading = true;
            try { _cbScreen.SelectedIndex = ScreenIndexOf(it.ScreenName); }
            finally { _loading = false; }

            AfterEdit();
            Toast("已定位到 X " + it.X + "   Y " + it.Y);
        }

        /// <summary>
        /// 进出拖动定位。退出时对所有覆盖层都解除，包括当前隐藏的——
        /// 旧版本只处理可见的，隐藏窗口一直留在 HTCAPTION 状态，会在游戏里偷吃鼠标点击。
        /// </summary>
        private void ToggleDrag()
        {
            _dragMode = !_dragMode;
            for (int i = 0; i < _ovl.Count; i++) _ovl[i].SetMoveMode(_dragMode);

            _btnDrag.Text = _dragMode ? "完成定位" : "拖动定位";
            _btnDrag.Kind = _dragMode ? 1 : 0;
            _btnDrag.Invalidate();

            if (_dragMode)
            {
                // 抓不住看不见的东西：先保证全局开着、当前准星是显示的
                _cfg.GlobalVisible = true;
                CrosshairItemSettings it = Cur();
                if (it != null && !it.Visible)
                {
                    it.Visible = true;
                    _chkVisible.SetSilent(true);
                }
                ApplyVisibility();
                SyncLabels();
                Toast("直接用鼠标把准星拖到落点，放好后点「完成定位」");
            }
            else
            {
                ApplyVisibility();
                _cfg.Save();
            }
        }

        private void BackToCenter()
        {
            CrosshairItemSettings it = Cur();
            if (it == null) return;
            it.Centered = true;
            it.AutoScreenPosition = false;
            AfterEdit();
        }

        private static string ForegroundExeName()
        {
            try
            {
                IntPtr h = Native.GetForegroundWindow();
                if (h == IntPtr.Zero) return "";
                uint pid;
                Native.GetWindowThreadProcessId(h, out pid);
                if (pid == 0) return "";
                using (Process p = Process.GetProcessById((int)pid)) return p.ProcessName;
            }
            catch { return ""; }
        }
    }
}
