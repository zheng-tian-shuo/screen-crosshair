using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace ScreenCrosshair
{
    public partial class MainForm
    {
        private TextBox _tbWeakExe;
        private ComboBox _cbWeakLevel;
        private Chk _chkWeakMtu;
        private Chk _chkWeakIndicator;
        private Slider _sWeakOpacity;
        private Label _vWeakOpacity;
        private FlatBtn _btnWeakOn, _btnWeakOff, _btnWeakGrab;
        private Label _lblWeakState;
        private bool _weakBusy;
        // The worker can still be changing Windows networking after the UI starts closing.
        // Cleanup waits for it so a late apply cannot leave an untracked policy behind.
        private readonly ManualResetEvent _weakOperationDone = new ManualResetEvent(true);
        private System.Windows.Forms.Timer _weakGrabTimer;
        private int _weakGrabLeft;

        private void BuildPageWeakNetwork()
        {
            Panel pg = _pages[PageWeakNetwork];
            Card c = NewCard(pg, "灵魂出窍（仅作用于指定应用）", 14, 366);

            Ui.L(c, "应用进程", Theme.Body, Theme.TextMuted, 14, 44, 62, 20);
            _tbWeakExe = Ui.Box(c, 78, 40, 214);
            _tbWeakExe.TextChanged += WeakSettingsChanged;
            _btnWeakGrab = new FlatBtn();
            _btnWeakGrab.Text = "3 秒后抓取前台程序";
            _btnWeakGrab.Bounds = new Rectangle(300, 40, 128, 28);
            _btnWeakGrab.Font = Theme.Small;
            _btnWeakGrab.Click += delegate { StartWeakProcessGrab(); };
            c.Controls.Add(_btnWeakGrab);
            Ui.L(c, "可填任意 .exe 文件名，或点右侧按钮切到目标程序后自动抓取。", Theme.Small, Theme.TextFaint,
                78, 68, 350, 24);

            Ui.L(c, "弱网档位", Theme.Body, Theme.TextMuted, 14, 100, 62, 20);
            _cbWeakLevel = Ui.Combo(c, 78, 97, 200);
            for (int i = 0; i < WeakNetworkOps.Profiles.Length; i++) _cbWeakLevel.Items.Add(WeakNetworkOps.Profiles[i].Name);
            _cbWeakLevel.SelectedIndexChanged += WeakSettingsChanged;

            _chkWeakMtu = new Chk();
            _chkWeakMtu.Text = "同时降低活动网卡 MTU（关闭时自动还原）";
            _chkWeakMtu.Bounds = new Rectangle(14, 138, 414, 24);
            _chkWeakMtu.CheckedChanged += WeakSettingsChanged;
            c.Controls.Add(_chkWeakMtu);

            _chkWeakIndicator = new Chk();
            _chkWeakIndicator.Text = "在桌面显示状态牌（可拖动移动位置）";
            _chkWeakIndicator.Bounds = new Rectangle(14, 162, 414, 24);
            _chkWeakIndicator.CheckedChanged += WeakSettingsChanged;
            c.Controls.Add(_chkWeakIndicator);

            _sWeakOpacity = SliderRow(c, "不透明度", 190, 20, 100, WeakSettingsChanged, out _vWeakOpacity);

            _btnWeakOn = new FlatBtn();
            _btnWeakOn.Kind = 1;
            _btnWeakOn.Text = "开启弱网";
            _btnWeakOn.Bounds = new Rectangle(14, 226, 112, 30);
            _btnWeakOn.Click += delegate { StartWeakNetwork(false); };
            c.Controls.Add(_btnWeakOn);
            _btnWeakOff = new FlatBtn();
            _btnWeakOff.Text = "关闭并还原";
            _btnWeakOff.Bounds = new Rectangle(136, 226, 124, 30);
            _btnWeakOff.Click += delegate { StopWeakNetwork(false); };
            c.Controls.Add(_btnWeakOff);

            _lblWeakState = Ui.L(c, "", Theme.Small, Theme.TextMuted, 14, 266, 414, 54);
            Ui.L(c, "使用 Windows 临时 QoS 策略限制出站带宽。需要管理员权限；\n程序退出或下次启动会尝试恢复网络。",
                Theme.Small, Theme.TextFaint, 14, 332, 414, 40);
        }

        private void PushWeakNetworkToUi()
        {
            if (_tbWeakExe == null) return;
            _tbWeakExe.Text = _cfg.WeakGameExe;
            _cbWeakLevel.SelectedIndex = Math.Max(0, Math.Min(WeakNetworkOps.Profiles.Length - 1, _cfg.WeakLevel));
            _chkWeakMtu.SetSilent(_cfg.WeakUseMtu);
            _chkWeakIndicator.SetSilent(_cfg.WeakShowIndicator);
            _sWeakOpacity.SetSilent(_cfg.WeakIndicatorOpacity);
            _vWeakOpacity.Text = _cfg.WeakIndicatorOpacity + "%";
            UpdateWeakControls();
            SyncWeakIndicator();
            UpdateWeakState(HasWeakRecoveryState() ? "● 弱网已开启或等待恢复。" : "○ 弱网未开启。");
        }

        private void WeakSettingsChanged(object sender, EventArgs e)
        {
            if (_loading) return;
            _cfg.WeakGameExe = _tbWeakExe.Text.Trim();
            _cfg.WeakLevel = Math.Max(0, _cbWeakLevel.SelectedIndex);
            _cfg.WeakUseMtu = _chkWeakMtu.Checked;
            _cfg.WeakShowIndicator = _chkWeakIndicator.Checked;
            _cfg.WeakIndicatorOpacity = _sWeakOpacity.Value;
            _vWeakOpacity.Text = _cfg.WeakIndicatorOpacity + "%";
            SaveSoon();
            SyncWeakIndicator();
        }

        private void UpdateWeakControls()
        {
            // Changing these after Apply only changes the saved UI setting; it cannot
            // change an already-created Windows policy. Lock them until restoration.
            bool locked = _weakBusy || HasWeakRecoveryState();
            if (_tbWeakExe != null) _tbWeakExe.Enabled = !locked;
            if (_cbWeakLevel != null) _cbWeakLevel.Enabled = !locked;
            if (_chkWeakMtu != null) _chkWeakMtu.Enabled = !locked;
            if (_btnWeakGrab != null) _btnWeakGrab.Enabled = !locked;
        }

        private void StartWeakProcessGrab()
        {
            if (_weakGrabTimer == null)
            {
                _weakGrabTimer = new System.Windows.Forms.Timer();
                _weakGrabTimer.Interval = 1000;
                _weakGrabTimer.Tick += WeakProcessGrabTick;
            }
            _weakGrabLeft = 3;
            _btnWeakGrab.Text = "3…";
            _weakGrabTimer.Start();
        }

        private void WeakProcessGrabTick(object sender, EventArgs e)
        {
            _weakGrabLeft--;
            if (_weakGrabLeft > 0) { _btnWeakGrab.Text = _weakGrabLeft + "…"; return; }
            _weakGrabTimer.Stop();
            _btnWeakGrab.Text = "3 秒后抓取前台程序";
            string exe = ForegroundExeName();
            if (exe.Length == 0) { Toast("没抓到前台程序，再试一次"); return; }
            _tbWeakExe.Text = exe + ".exe";
            Toast("已填入 " + exe + ".exe");
        }

        private void ToggleWeakNetworkFromHotkey()
        {
            if (_weakBusy) return;
            if (HasWeakRecoveryState()) StopWeakNetwork(true);
            else StartWeakNetwork(true);
        }

        private bool HasWeakRecoveryState()
        {
            return _cfg.WeakActive || !string.IsNullOrEmpty(_cfg.WeakPolicyName) || !string.IsNullOrEmpty(_cfg.WeakMtuRecords);
        }

        private void SaveWeakRecoveryState()
        {
            // Recovery records must survive an unexpected exit, unlike ordinary UI preferences.
            _cfg.Save();
            _cfg.SaveToDisk();
        }

        private void StartWeakNetwork(bool fromHotkey)
        {
            if (_weakBusy || HasWeakRecoveryState()) return;
            string exe = _tbWeakExe == null ? _cfg.WeakGameExe : _tbWeakExe.Text.Trim();
            if (!WeakNetworkOps.IsSafeExeName(exe))
            {
                if (!fromHotkey) Toast("请填写有效的 .exe 文件名");
                return;
            }
            if (!WeakNetworkOps.IsAdministrator())
            {
                UpdateWeakState("需要管理员权限。请退出后右键以管理员身份运行。");
                if (!fromHotkey) Toast("弱网需要管理员权限");
                return;
            }
            _cfg.WeakGameExe = exe;
            _cfg.WeakLevel = Math.Max(0, _cbWeakLevel == null ? _cfg.WeakLevel : _cbWeakLevel.SelectedIndex);
            _cfg.WeakUseMtu = _chkWeakMtu == null ? _cfg.WeakUseMtu : _chkWeakMtu.Checked;
            int level = _cfg.WeakLevel;
            bool useMtu = _cfg.WeakUseMtu;

            // Persist the policy name before the worker starts. If the app is closed or
            // crashes during Apply, the next launch can still remove this policy.
            _cfg.WeakActive = true;
            _cfg.WeakPolicyName = WeakNetworkOps.PolicyName(exe);
            _cfg.WeakMtuRecords = "";
            SaveWeakRecoveryState();
            SetWeakBusy(true, "正在开启弱网…");
            _weakOperationDone.Reset();
            ThreadPool.QueueUserWorkItem(delegate
            {
                string policy, records, log;
                bool ok = WeakNetworkOps.Apply(exe, level, useMtu, out policy, out records, out log);
                _weakOperationDone.Set();
                if (IsDisposed || Disposing || !IsHandleCreated) return;
                BeginInvoke((MethodInvoker)delegate
                {
                    if (ok)
                    {
                        _cfg.WeakActive = true;
                        _cfg.WeakPolicyName = policy;
                        _cfg.WeakMtuRecords = records;
                        SaveWeakRecoveryState();
                        if (!fromHotkey) Toast("弱网已开启");
                    }
                    else
                    {
                        _cfg.WeakActive = false;
                        _cfg.WeakPolicyName = "";
                        _cfg.WeakMtuRecords = "";
                        SaveWeakRecoveryState();
                    }
                    SetWeakBusy(false, ok ? "弱网已开启。\n" + log : "弱网开启失败。\n" + log);
                });
            });
        }

        private void StopWeakNetwork(bool fromHotkey)
        {
            if (_weakBusy) return;
            if (!_cfg.WeakActive && string.IsNullOrEmpty(_cfg.WeakPolicyName) && string.IsNullOrEmpty(_cfg.WeakMtuRecords)) return;
            SetWeakBusy(true, "正在恢复网络…");
            _weakOperationDone.Reset();
            ThreadPool.QueueUserWorkItem(delegate
            {
                string log;
                bool ok = WeakNetworkOps.Restore(_cfg.WeakPolicyName, _cfg.WeakMtuRecords, out log);
                _weakOperationDone.Set();
                if (IsDisposed || Disposing || !IsHandleCreated) return;
                BeginInvoke((MethodInvoker)delegate
                {
                    if (ok)
                    {
                        _cfg.WeakActive = false;
                        _cfg.WeakPolicyName = "";
                        _cfg.WeakMtuRecords = "";
                        SaveWeakRecoveryState();
                        if (!fromHotkey) Toast("网络已还原");
                    }
                    SetWeakBusy(false, ok ? "弱网已关闭。\n" + log : "恢复失败，弱网仍可能生效。\n" + log);
                });
            });
        }

        private void RecoverWeakNetworkOnStart()
        {
            if (!_cfg.WeakActive && string.IsNullOrEmpty(_cfg.WeakPolicyName) && string.IsNullOrEmpty(_cfg.WeakMtuRecords)) return;
            if (!WeakNetworkOps.IsAdministrator()) return;
            SetWeakBusy(true, "正在恢复上次残留的弱网状态…");
            _weakOperationDone.Reset();
            ThreadPool.QueueUserWorkItem(delegate
            {
                string log;
                bool ok = WeakNetworkOps.Restore(_cfg.WeakPolicyName, _cfg.WeakMtuRecords, out log);
                _weakOperationDone.Set();
                if (IsDisposed || Disposing || !IsHandleCreated) return;
                BeginInvoke((MethodInvoker)delegate
                {
                    if (ok)
                    {
                        _cfg.WeakActive = false; _cfg.WeakPolicyName = ""; _cfg.WeakMtuRecords = ""; SaveWeakRecoveryState();
                        SetWeakBusy(false, "已恢复上次残留的弱网状态。\n" + log);
                    }
                    else SetWeakBusy(false, "上次弱网状态恢复失败，请手动关闭并还原。\n" + log);
                });
            });
        }

        private void RestoreWeakNetworkOnExit()
        {
            // Apply/Restore have a 30-second PowerShell timeout. Wait a little longer
            // before the final synchronous restore so a late Apply cannot win the race.
            if (_weakBusy) _weakOperationDone.WaitOne(35000);
            if (!_cfg.WeakActive && string.IsNullOrEmpty(_cfg.WeakPolicyName) && string.IsNullOrEmpty(_cfg.WeakMtuRecords)) return;
            string log;
            bool ok = WeakNetworkOps.Restore(_cfg.WeakPolicyName, _cfg.WeakMtuRecords, out log);
            if (ok)
            {
                _cfg.WeakActive = false; _cfg.WeakPolicyName = ""; _cfg.WeakMtuRecords = ""; _cfg.Save();
            }
        }

        private void SetWeakBusy(bool busy, string state)
        {
            _weakBusy = busy;
            if (_btnWeakOn != null) _btnWeakOn.Enabled = !busy;
            if (_btnWeakOff != null) _btnWeakOff.Enabled = !busy;
            UpdateWeakControls();
            SyncWeakIndicator();
            UpdateWeakState(state);
        }

        private void SyncWeakIndicator()
        {
            if (!_cfg.WeakShowIndicator)
            {
                if (_weakIndicator != null) _weakIndicator.Hide();
                return;
            }
            if (_weakIndicator == null)
            {
                _weakIndicator = new WeakStatusOverlayForm();
                _weakIndicator.PositionChangedByUser += WeakIndicatorMoved;
                if (_cfg.WeakIndicatorX != -32768 && _cfg.WeakIndicatorY != -32768)
                    _weakIndicator.Location = new Point(_cfg.WeakIndicatorX, _cfg.WeakIndicatorY);
            }
            _weakIndicator.SetOpacityPercent(_cfg.WeakIndicatorOpacity);
            _weakIndicator.SetState(HasWeakRecoveryState(), _weakBusy);
            _weakIndicator.ShowTopNoActivate();
        }

        private void WeakIndicatorMoved(object sender, EventArgs e)
        {
            if (_weakIndicator == null) return;
            _cfg.WeakIndicatorX = _weakIndicator.Left;
            _cfg.WeakIndicatorY = _weakIndicator.Top;
            SaveSoon();
        }

        private void UpdateWeakState(string state)
        {
            if (_lblWeakState == null) return;
            _lblWeakState.Text = state;
            _lblWeakState.ForeColor = state.Contains("弱网已开启") ? Theme.Green : Theme.TextMuted;
        }
    }
}
