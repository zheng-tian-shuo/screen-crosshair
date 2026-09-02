using System;
using System.Windows.Forms;

namespace ScreenCrosshair
{
    public partial class MainForm
    {
        private const int HkToggle = 0x0A71;
        private const int HkSwitch = 0x0A72;

        private Timer _tick;
        private string _lastFg = "";   // 哨兵值，保证第一次 tick 一定重算一遍
        private string _lastScreen;

        /// <summary>顺序必须和 AppSettings.HotToggle…HotFree 一致</summary>
        private static readonly int[] HkIds =
            { HkToggle, HkSwitch, HkEvacuation, HkRocket, HkFreeCountdown };

        /// <summary>
        /// 全部注销再按启用状态重登。停用的那条只是跳过注册，组合键本身仍留在配置里。
        /// </summary>
        private void RegisterHotkeys()
        {
            for (int i = 0; i < HkIds.Length; i++) Native.UnregisterHotKey(Handle, HkIds[i]);

            int okCount = 0, onCount = 0;
            string bad = "";
            for (int i = 0; i < AppSettings.HotCount; i++)
            {
                if (!_cfg.HotEnabled(i)) continue;
                onCount++;
                bool ok = _cfg.HotKey(i) != 0 && Native.RegisterHotKey(Handle, HkIds[i],
                    _cfg.HotMod(i) | Native.MOD_NOREPEAT, _cfg.HotKey(i));
                if (ok) okCount++;
                else bad += (bad.Length > 0 ? "、" : "") + AppSettings.HotShort[i];
            }

            if (_lblHotState != null)
            {
                if (onCount == 0)
                {
                    _lblHotState.ForeColor = Theme.TextFaint;
                    _lblHotState.Text = "五条热键全部停用";
                }
                else if (bad.Length == 0)
                {
                    _lblHotState.ForeColor = Theme.Green;
                    _lblHotState.Text = "已生效 " + okCount + " 条，停用 "
                        + (AppSettings.HotCount - onCount) + " 条";
                }
                else
                {
                    _lblHotState.ForeColor = Theme.Warn;
                    _lblHotState.Text = "被占用：" + bad + "，换个组合或先停用";
                }
            }
            UpdateSideHint();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_HOTKEY)
            {
                int id = m.WParam.ToInt32();
                if (id == HkToggle) { ToggleGlobal(); return; }
                if (id == HkSwitch) { NextProfile(); return; }
                if (id == HkEvacuation) { StartCountdown(0); return; }
                if (id == HkRocket) { StartCountdown(1); return; }
                if (id == HkFreeCountdown) { StartCountdown(2); return; }
            }
            base.WndProc(ref m);
        }

        private void StartTick()
        {
            _tick = new Timer();
            _tick.Interval = 500;
            _tick.Tick += TickCheck;
            _tick.Start();
        }

        /// <summary>
        /// 两件事：前台程序变了要重算自动显隐；鼠标换到别的屏了，
        /// 「跟随鼠标所在屏」的准星要搬过去。
        /// </summary>
        private void TickCheck(object sender, EventArgs e)
        {
            TickCountdowns();
            if (_cfg.AutoHide)
            {
                string fg = ForegroundExeName();
                if (fg != _lastFg)
                {
                    _lastFg = fg;
                    ApplyVisibility();
                }
            }

            bool follow = false;
            for (int i = 0; i < _ovl.Count; i++)
            {
                if (string.IsNullOrEmpty(_ovl[i].Item.ScreenName)) { follow = true; break; }
            }
            if (!follow) return;

            try
            {
                Screen s = Screen.FromPoint(Control.MousePosition);
                if (s.DeviceName == _lastScreen) return;
                _lastScreen = s.DeviceName;
                for (int i = 0; i < _ovl.Count; i++)
                {
                    OverlayForm f = _ovl[i];
                    if (!f.Visible || !string.IsNullOrEmpty(f.Item.ScreenName)) continue;
                    f.RefreshPosition();
                    f.Redraw();
                }
                UpdatePreview();
            }
            catch { }
        }
    }
}
