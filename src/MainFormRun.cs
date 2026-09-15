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
        private string _lastScreen;

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
            }
            catch { }
        }
    }
}
