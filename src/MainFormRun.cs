using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ScreenCrosshair
{
    public partial class MainForm
    {
        private const int HkToggle = 0x0A71;
        private const int HkSwitch = 0x0A72;
        private const int HkWeak = 0x0A76;

        private Timer _tick;
        private string _lastFg = "\x01";

        private static readonly int[] HkIds =
            { HkToggle, HkSwitch, HkEvacuation, HkRocket, HkFreeCountdown, HkWeak };

        private void RegisterHotkeys()
        {
            for (int i = 0; i < HkIds.Length; i++) Native.UnregisterHotKey(Handle, HkIds[i]);

            int okCount = 0;
            int onCount = 0;
            string bad = "";
            for (int i = 0; i < AppSettings.HotCount; i++)
            {
                if (!_cfg.HotEnabled(i)) continue;
                onCount++;
                uint key = _cfg.HotKey(i);
                uint mod = _cfg.HotMod(i);
                int duplicate = FindDuplicateHotkey(i, mod, key);
                if (duplicate >= 0)
                {
                    bad += (bad.Length > 0 ? "\uFF1B" : "") + AppSettings.HotShort[i]
                        + "\uFF08\u4E0E " + AppSettings.HotShort[duplicate] + " \u91CD\u590D\uFF09";
                    continue;
                }

                bool ok = key != 0 && Native.RegisterHotKey(Handle, HkIds[i],
                    mod | Native.MOD_NOREPEAT, key);
                if (ok) okCount++;
                else
                {
                    int error = Marshal.GetLastWin32Error();
                    string reason = error == 1409
                        ? HotkeyText(mod, key) + " \u88AB\u5176\u4ED6\u7A0B\u5E8F\u6216\u7CFB\u7EDF\u5360\u7528"
                        : "\u9519\u8BEF " + error;
                    bad += (bad.Length > 0 ? "\uFF1B" : "") + AppSettings.HotShort[i]
                        + "\uFF08" + reason + "\uFF09";
                }
            }

            if (_lblHotState != null)
            {
                if (onCount == 0)
                {
                    _lblHotState.ForeColor = Theme.TextFaint;
                    _lblHotState.Text = AppSettings.HotCount + " \u6761\u70ED\u952E\u5168\u90E8\u505C\u7528";
                }
                else if (bad.Length == 0)
                {
                    _lblHotState.ForeColor = Theme.Green;
                    _lblHotState.Text = "\u5DF2\u751F\u6548 " + okCount + " \u6761\uFF0C\u505C\u7528 "
                        + (AppSettings.HotCount - onCount) + " \u6761";
                }
                else
                {
                    _lblHotState.ForeColor = Theme.Warn;
                    _lblHotState.Text = "\u6CE8\u518C\u5931\u8D25\uFF1A" + bad;
                }
            }
            UpdateSideHint();
        }

        private int FindDuplicateHotkey(int slot, uint mod, uint key)
        {
            if (key == 0) return -1;
            for (int i = 0; i < slot; i++)
                if (_cfg.HotEnabled(i) && _cfg.HotMod(i) == mod && _cfg.HotKey(i) == key)
                    return i;
            return -1;
        }

        private static string HotkeyText(uint mod, uint key)
        {
            string text = "";
            if ((mod & Native.MOD_CONTROL) != 0) text += "Ctrl + ";
            if ((mod & Native.MOD_ALT) != 0) text += "Alt + ";
            if ((mod & Native.MOD_SHIFT) != 0) text += "Shift + ";
            return text + KeyTable.NameOfVk(key);
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
                if (id == HkWeak) { ToggleWeakNetworkFromHotkey(); return; }
            }
            base.WndProc(ref m);
            if (m.Msg == Native.WM_DISPLAYCHANGE && IsHandleCreated && !IsDisposed)
                BeginInvoke((MethodInvoker)delegate { if (!IsDisposed) RefreshScreenPositions(); });
        }

        private void StartTick()
        {
            _tick = new Timer();
            _tick.Interval = 500;
            _tick.Tick += TickCheck;
            _tick.Start();
        }

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

            RefreshScreenPositions();
        }

        private void RefreshScreenPositions()
        {
            if (_cfg == null) return;
            bool changed = false;
            foreach (CrosshairItemSettings it in _cfg.Items)
            {
                Screen screen = ScreenOf(it);
                if (screen == null || !it.RefreshGeneratedPosition(screen.Bounds.Size)) continue;
                changed = true;
                if (it == Cur() && _tbX != null && _tbY != null)
                {
                    bool loading = _loading;
                    _loading = true;
                    try
                    {
                        if (!_tbX.Focused) _tbX.Text = it.X.ToString();
                        if (!_tbY.Focused) _tbY.Text = it.Y.ToString();
                    }
                    finally { _loading = loading; }
                    if (_lblFovResult != null && _lblFovResult.ForeColor == Theme.Green)
                        ResetFovResult("已按上次生成参数更新位置");
                }
            }
            if (changed) SaveSoon();
            foreach (OverlayForm overlay in _ovl)
                if (overlay.RefreshPosition() && overlay.Visible) overlay.Redraw();
        }
    }
}
