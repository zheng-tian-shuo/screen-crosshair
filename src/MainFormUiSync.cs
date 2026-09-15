using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace ScreenCrosshair
{
    public partial class MainForm
    {
        private Label _toast;
        private Timer _toastTimer;

        /// <summary>把配置整体回填到界面。_loading 期间所有控件事件都被忽略。</summary>
        private void PushToUi()
        {
            _loading = true;
            try
            {
                FillItemCombo();

                CrosshairItemSettings it = Cur();
                if (it != null)
                {
                    _chkVisible.SetSilent(it.Visible);
                    _cbShape.SelectedIndex = (int)it.Shape;
                    _sSize.SetSilent(it.Size);
                    _sThick.SetSilent(it.Thickness);
                    _sOpacity.SetSilent(it.Opacity);
                    _chkGlow.SetSilent(it.Glow);

                    _chkCentered.SetSilent(it.Centered);
                    _tbX.Text = it.X.ToString();
                    _tbY.Text = it.Y.ToString();
                    _cbScreen.SelectedIndex = ScreenIndexOf(it.ScreenName);
                    SyncFovUi(it);
                    if (_tbFovTarget != null && string.IsNullOrEmpty(_tbFovTarget.Text))
                        _tbFovTarget.Text = "100";

                    _sTickCount.SetSilent(it.TickCount);
                    _sTickSpace.SetSilent(it.TickSpacing);
                    _sTickLen.SetSilent(it.TickLength);
                    _chkLabels.SetSilent(it.ShowLabels);
                    _tbLabelStart.Text = it.LabelStart.ToString();
                    _tbLabelStep.Text = it.LabelStep.ToString();
                    ShowTickCard(Shapes.HasTicks(it.Shape));
                }

                PushHotkeysToUi();
                _chkAuto.SetSilent(_cfg.AutoHide);
                _tbGameExe.Text = _cfg.GameExe;
                UpdateAdminModeButton();
                PushWeakNetworkToUi();

                FillProfileList();
                PushCountdownToUi();
            }
            finally { _loading = false; }

            SyncLabels();
            UpdatePill();
            UpdateSideHint();
        }

        private int ScreenIndexOf(string dev)
        {
            if (string.IsNullOrEmpty(dev)) return 0;
            for (int i = 0; i < _screenNames.Count; i++)
                if (_screenNames[i] == dev) return i;
            return 0;
        }

        private string ItemLabel(int i)
        {
            CrosshairItemSettings it = _cfg.Items[i];
            return (i + 1) + ". " + Shapes.NameOf(it.Shape) + (it.Visible ? "" : "（已关）");
        }

        private void FillItemCombo()
        {
            bool old = _loading;
            _loading = true;
            try
            {
                int sel = _cfg.SelectedItem;
                _cbItem.Items.Clear();
                for (int i = 0; i < _cfg.Items.Count; i++) _cbItem.Items.Add(ItemLabel(i));
                if (sel < 0) sel = 0;
                if (sel >= _cbItem.Items.Count) sel = _cbItem.Items.Count - 1;
                if (sel >= 0) _cbItem.SelectedIndex = sel;
            }
            finally { _loading = old; }
        }

        /// <summary>只更新数值/文字这类只读显示，不碰用户正在编辑的输入框</summary>
        private void SyncLabels()
        {
            CrosshairItemSettings it = Cur();
            if (it == null) return;

            _vSize.Text = it.Size + " px";
            _vThick.Text = it.Thickness + " px";
            _vOpacity.Text = it.Opacity + " %";
            _vTickCount.Text = it.TickCount + " 格";
            _vTickSpace.Text = it.TickSpacing + " px";
            _vTickLen.Text = it.TickLength + " px";
            _lblHex.Text = Theme.HexOf(it.Color);
            _lblItemCount.Text = "本预设 " + _cfg.Items.Count + " 个准星 · 画布 " +
                it.CanvasSize() + " px";

            int i = _cfg.SelectedItem;
            if (i >= 0 && i < _cbItem.Items.Count)
            {
                string s = ItemLabel(i);
                if (!string.Equals(_cbItem.Items[i] as string, s))
                {
                    bool old = _loading;
                    _loading = true;
                    try { _cbItem.Items[i] = s; _cbItem.SelectedIndex = i; }
                    finally { _loading = old; }
                }
            }
        }

        private void UpdatePill()
        {
            if (_pillGlobal == null) return;
            _pillGlobal.Kind = _cfg.GlobalVisible ? 1 : 0;
            _pillGlobal.Text = _cfg.GlobalVisible ? "已显示 · 点击隐藏" : "已隐藏 · 点击显示";
            _pillGlobal.Invalidate();
        }

        private void UpdateSideHint()
        {
            if (_lblSideHint == null) return;
            _lblSideHint.Text =
                "当前预设\n" + _cfg.Active.Name + "\n\n" +
                "显示 / 隐藏\n" + HotHint(AppSettings.HotToggle) + "\n\n" +
                "切换预设\n" + HotHint(AppSettings.HotSwitch);
        }

        /// <summary>停用的热键在侧栏直接写「已停用」，免得看着有键其实按不动</summary>
        private string HotHint(int slot)
        {
            return _cfg.HotEnabled(slot)
                ? KeyTable.Describe(_cfg.HotMod(slot), _cfg.HotKey(slot))
                : "已停用";
        }

        /// <summary>底部一闪而过的小提示</summary>
        private void Toast(string msg)
        {
            if (_toast == null)
            {
                _toast = new Label();
                _toast.AutoSize = false;
                _toast.TextAlign = ContentAlignment.MiddleCenter;
                _toast.Font = Theme.Body;
                _toast.BackColor = Theme.CardAlt;
                _toast.ForeColor = Theme.Accent;
                _toast.Bounds = new Rectangle((ClientSize.Width - Theme.S(320)) / 2,
                    ClientSize.Height - Theme.S(52), Theme.S(320), Theme.S(30));
                Controls.Add(_toast);

                _toastTimer = new Timer();
                _toastTimer.Interval = 1900;
                _toastTimer.Tick += delegate
                {
                    _toastTimer.Stop();
                    if (_toast != null) _toast.Visible = false;
                };
            }
            _toast.Text = msg;
            _toast.Visible = true;
            _toast.BringToFront();
            _toastTimer.Stop();
            _toastTimer.Start();
        }
    }
}
