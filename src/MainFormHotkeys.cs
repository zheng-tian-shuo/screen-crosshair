using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Security.Principal;
using System.Windows.Forms;

namespace ScreenCrosshair
{
    public partial class MainForm
    {
        // 五条热键（显隐 / 切预设 / 三个倒计时）现在全在这一页，每行一套控件。
        // 下标就是 AppSettings.HotToggle…HotFree，别单独排序。
        private Label[] _hkName;
        private Chk[] _hkCtrl, _hkAlt, _hkShift;
        private HotkeyBox[] _hkKey;
        private FlatBtn[] _hkOn;
        private Label _lblHotState;
        private Chk _chkAuto;
        private TextBox _tbGameExe;
        private FlatBtn _btnGrab;
        private FlatBtn _btnAdminMode;
        private Label _lblAdminHint;
        private Timer _grabTimer;
        private int _grabLeft;
        private ToolTip _hkTip;

        private void BuildPageHotkeys()
        {
            Panel pg = _pages[PageHotkeys];

            Card c1 = NewCard(pg, "全局热键", 14, 444);
            c1.Width = 560;
            c1.Left = (_pageW - c1.Width) / 2;

            const int GroupW = 560;
            int groupX = (c1.Width - GroupW) / 2;
            int n = AppSettings.HotCount;
            _hkName = new Label[n];
            _hkCtrl = new Chk[n];
            _hkAlt = new Chk[n];
            _hkShift = new Chk[n];
            _hkKey = new HotkeyBox[n];
            _hkOn = new FlatBtn[n];
            _hkTip = new ToolTip();

            for (int i = 0; i < n; i++)
            {
                int slot = i;           // 闭包要抓住当前值，直接用 i 的话所有按钮都指向最后一行
                int y = 48 + i * 38;

                _hkName[i] = Ui.L(c1, AppSettings.HotNames[i], Theme.Body, Theme.TextMuted,
                    groupX + 14, y + 2, 100, 20);
                _hkCtrl[i] = ModChk(c1, "Ctrl", groupX + 132, y);
                _hkAlt[i] = ModChk(c1, "Alt", groupX + 200, y);
                _hkShift[i] = ModChk(c1, "Shift", groupX + 260, y);
                int keyX = groupX + 336;
                _hkKey[i] = Ui.Hotkey(c1, keyX, y - 3, 94);
                _hkTip.SetToolTip(_hkKey[i], "点击后直接按下键盘按键");
                _hkKey[i].HotkeyChanged += delegate
                {
                    SetMod(_hkKey[slot].Modifiers, _hkCtrl[slot],
                        _hkAlt[slot], _hkShift[slot]);
                };

                FlatBtn on = new FlatBtn();
                on.Font = Theme.Small;
                on.Bounds = new Rectangle(groupX + 446, y - 3, 96, 28);
                on.Click += delegate { ToggleHotEnabled(slot); };
                c1.Controls.Add(on);
                _hkOn[i] = on;
            }

            int by = 48 + n * 38 + 8;

            FlatBtn ap = new FlatBtn();
            ap.Kind = 1;
            ap.Text = "应用热键";
            ap.Bounds = new Rectangle(groupX + 14, by, 100, 28);
            ap.Click += delegate { ApplyHotkeys(); };
            c1.Controls.Add(ap);

            _lblHotState = Ui.L(c1, "", Theme.Small, Theme.TextMuted,
                groupX + 124, by + 4, 418, 20);

            // 两句话手动断行：交给自动换行会把「说明别 / 的程序」这种词切成两截
            Ui.L(c1, "改完组合键要按「应用热键」；右边的开关按一下立刻生效。\n"
                    + "显示「被占用」说明这个组合被别的程序抢了，换一个或者把它停用。",
                Theme.Small, Theme.TextFaint, groupX + 14, by + 38, 528, 38);

            Panel permissionSep = new Panel();
            permissionSep.Bounds = new Rectangle(groupX + 14, by + 84, 528, 1);
            permissionSep.BackColor = Theme.Border;
            c1.Controls.Add(permissionSep);

            Ui.L(c1, "游戏权限", Theme.Body, Theme.TextMuted,
                groupX + 14, by + 100, 72, 28);
            _btnAdminMode = new FlatBtn();
            _btnAdminMode.Bounds = new Rectangle(groupX + 100, by + 100, 164, 28);
            _btnAdminMode.Click += delegate { RestartAsAdministrator(); };
            c1.Controls.Add(_btnAdminMode);
            _lblAdminHint = Ui.L(c1, "", Theme.Small, Theme.TextFaint,
                groupX + 276, by + 100, 266, 28);
            _lblAdminHint.TextAlign = ContentAlignment.MiddleLeft;

            // ---- 自动显隐 ----
            Card c2 = NewCard(pg, "跟着游戏自动显隐", 474, 150);
            c2.Width = c1.Width;
            c2.Left = c1.Left;

            _chkAuto = new Chk();
            _chkAuto.Text = "只在指定程序处于前台时显示准星";
            _chkAuto.Bounds = new Rectangle(14, 42, 400, 22);
            _chkAuto.CheckedChanged += UiChanged;
            c2.Controls.Add(_chkAuto);

            Ui.L(c2, "进程名", Theme.Body, Theme.TextMuted, 14, 78, 54, 20);
            _tbGameExe = Ui.Box(c2, 70, 76, 160);
            _tbGameExe.TextChanged += UiChanged;
            Ui.L(c2, ".exe 可以省略", Theme.Small, Theme.TextFaint, 238, 78, 100, 20);

            _btnGrab = new FlatBtn();
            _btnGrab.Text = "3 秒后抓取前台程序";
            _btnGrab.Bounds = new Rectangle(14, 108, 160, 28);
            _btnGrab.Click += delegate { StartGrab(); };
            c2.Controls.Add(_btnGrab);

            // 按钮右边只剩 256 px，这句原来写成一行会被卡片右边切掉。给它两行的高度，
            // 同时和按钮取同一个上下范围 + 垂直居中，免得文字贴着框顶跟按钮错开。
            Label grabHint = Ui.L(c2, "点一下再切到游戏，倒数结束自动填好。",
                Theme.Small, Theme.TextFaint, 182, 108, 256, 28);
            grabHint.TextAlign = ContentAlignment.MiddleLeft;

        }

        private void UpdateAdminModeButton()
        {
            if (_btnAdminMode == null) return;
            bool admin = IsAdministrator();
            _btnAdminMode.Text = admin ? "当前已是管理员模式" : "以管理员模式重启";
            _btnAdminMode.Enabled = !admin;
            _btnAdminMode.Kind = admin ? 0 : 1;
            _btnAdminMode.Invalidate();
            if (_lblAdminHint != null)
            {
                _lblAdminHint.Text = admin ? "管理员模式已启用" : "游戏中热键失效时，点此按钮";
                _lblAdminHint.ForeColor = admin ? Theme.Green : Theme.TextMuted;
            }
        }

        private void RestartAsAdministrator()
        {
            if (IsAdministrator()) return;
            try
            {
                // Close first so Program can release the single-instance mutex before UAC launch.
                Program.RestartAsAdminRequested = true;
                _reallyExit = true;
                Close();
            }
            catch (Win32Exception) { Toast("已取消管理员模式重启"); }
            catch (Exception ex) { AppLog.Write("管理员模式重启失败", ex); Toast("管理员模式重启失败"); }
        }

        private static bool IsAdministrator()
        {
            try
            {
                WindowsPrincipal p = new WindowsPrincipal(WindowsIdentity.GetCurrent());
                return p.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch { return false; }
        }

        /// <summary>
        /// 修饰键勾选框。这里不挂 UiChanged：热键的改动要按「应用热键」才生效，
        /// 顺手触发一次准星的保存流程纯属白费。
        /// </summary>
        private Chk ModChk(Card c, string text, int x, int y)
        {
            Chk k = new Chk();
            k.Text = text;
            // 字体是 pt 单位，高 DPI 下 MeasureText 给的是放大后的宽度，而 Bounds 之后还会被
            // Form.Scale 再乘一遍。这里先折回 96 DPI 的宽度，不然 150% 缩放时三个勾选框
            // 会各宽出一半，前一个直接盖住后一个的方框（Ctrl 压着 Alt、Alt 压着 Shift）。
            int tw = (int)Math.Ceiling(TextRenderer.MeasureText(text, Theme.Body).Width / Theme.K);
            k.Bounds = new Rectangle(x, y, tw + 26, 22);
            c.Controls.Add(k);
            return k;
        }

        private static uint ModOf(Chk c, Chk a, Chk s)
        {
            uint m = 0;
            if (c.Checked) m |= Native.MOD_CONTROL;
            if (a.Checked) m |= Native.MOD_ALT;
            if (s.Checked) m |= Native.MOD_SHIFT;
            return m;
        }

        private static void SetMod(uint m, Chk c, Chk a, Chk s)
        {
            c.SetSilent((m & Native.MOD_CONTROL) != 0);
            a.SetSilent((m & Native.MOD_ALT) != 0);
            s.SetSilent((m & Native.MOD_SHIFT) != 0);
        }

        private void PushHotkeysToUi()
        {
            if (_hkKey == null) return;
            for (int i = 0; i < AppSettings.HotCount; i++)
            {
                SetMod(_cfg.HotMod(i), _hkCtrl[i], _hkAlt[i], _hkShift[i]);
                _hkKey[i].SetHotkey(_cfg.HotMod(i), _cfg.HotKey(i));
                SyncHotRow(i);
            }
        }

        /// <summary>按启用状态刷新一行的开关按钮和名字颜色</summary>
        private void SyncHotRow(int i)
        {
            if (_hkOn == null || _hkOn[i] == null) return;
            bool on = _cfg.HotEnabled(i);
            _hkOn[i].Text = on ? "已启用" : "已停用";
            _hkOn[i].Kind = 0;
            _hkOn[i].ForeColor = on ? Theme.Green : Theme.TextFaint;
            _hkOn[i].Invalidate();
            _hkName[i].ForeColor = on ? Theme.TextMuted : Theme.TextFaint;
        }

        /// <summary>停用只是不去 RegisterHotKey，组合键本身留着，重新启用即回来</summary>
        private void ToggleHotEnabled(int slot)
        {
            _cfg.SetHotEnabled(slot, !_cfg.HotEnabled(slot));
            SyncHotRow(slot);
            RegisterHotkeys();
            _cfg.Save();
            Toast(AppSettings.HotNames[slot] + (_cfg.HotEnabled(slot) ? "：已启用" : "：已停用"));
        }

        private void ApplyHotkeys()
        {
            for (int i = 0; i < AppSettings.HotCount; i++)
            {
                uint key = _hkKey[i].VirtualKey;
                if (key == 0) key = _cfg.HotKey(i);
                _cfg.SetHot(i, ModOf(_hkCtrl[i], _hkAlt[i], _hkShift[i]), key);
            }
            RegisterHotkeys();
            _cfg.Save();
            Toast("热键已应用");
        }

        // ---- 倒数抓取前台进程 ----
        private void StartGrab()
        {
            if (_grabTimer == null)
            {
                _grabTimer = new Timer();
                _grabTimer.Interval = 1000;
                _grabTimer.Tick += GrabTick;
            }
            _grabLeft = 3;
            _btnGrab.Text = "3…";
            _grabTimer.Start();
        }

        private void GrabTick(object sender, EventArgs e)
        {
            _grabLeft--;
            if (_grabLeft > 0) { _btnGrab.Text = _grabLeft + "…"; return; }
            _grabTimer.Stop();
            _btnGrab.Text = "3 秒后抓取前台程序";
            string exe = ForegroundExeName();
            if (exe.Length == 0) { Toast("没抓到，再试一次"); return; }
            _tbGameExe.Text = exe;
            _chkAuto.Checked = true;
            Toast("已填入 " + exe);
        }
    }
}
