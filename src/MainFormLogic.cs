using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace ScreenCrosshair
{
    public partial class MainForm
    {
        private Timer _saveTimer;

        private CrosshairItemSettings Cur()
        {
            List<CrosshairItemSettings> list = _cfg.Items;
            if (list.Count == 0) return null;
            int i = _cfg.SelectedItem;
            if (i < 0) i = 0;
            if (i >= list.Count) i = list.Count - 1;
            _cfg.SelectedItem = i;
            return list[i];
        }

        private Screen ScreenOf(CrosshairItemSettings it)
        {
            try
            {
                if (it != null && !string.IsNullOrEmpty(it.ScreenName))
                {
                    Screen[] all = Screen.AllScreens;
                    for (int i = 0; i < all.Length; i++)
                        if (all[i].DeviceName == it.ScreenName) return all[i];
                }
                return Screen.FromPoint(Control.MousePosition);
            }
            catch { return Screen.PrimaryScreen; }
        }

        /// <summary>所有控件共用的变更入口</summary>
        private void UiChanged(object sender, EventArgs e)
        {
            if (_loading) return;
            PullFromUi();
            AfterEdit();
        }

        private void ItemPicked(object sender, EventArgs e)
        {
            if (_loading || _cbItem.SelectedIndex < 0) return;
            _cfg.SelectedItem = _cbItem.SelectedIndex;
            PushToUi();
        }

        private void PullFromUi()
        {
            CrosshairItemSettings it = Cur();
            if (it != null)
            {
                if (_cbShape.SelectedIndex >= 0) it.Shape = (CrosshairShape)_cbShape.SelectedIndex;
                it.Size = _sSize.Value;
                it.Thickness = _sThick.Value;
                it.Opacity = _sOpacity.Value;
                it.Glow = _chkGlow.Checked;
                it.Visible = _chkVisible.Checked;
                it.Centered = _chkCentered.Checked;

                int si = _cbScreen.SelectedIndex;
                it.ScreenName = (si >= 0 && si < _screenNames.Count) ? _screenNames[si] : "";

                it.TickCount = _sTickCount.Value;
                it.TickSpacing = _sTickSpace.Value;
                it.TickLength = _sTickLen.Value;
                it.ShowLabels = _chkLabels.Checked;
                it.LabelStart = ParseInt(_tbLabelStart, it.LabelStart, 0, 99999);
                it.LabelStep = ParseInt(_tbLabelStep, it.LabelStep, 0, 99999);
            }
            _cfg.AutoHide = _chkAuto.Checked;
            _cfg.GameExe = _tbGameExe.Text.Trim();
        }

        /// <summary>改完设置统一走这里：同步只读显示、刷新覆盖层与预览、延迟落盘</summary>
        private void AfterEdit()
        {
            CrosshairItemSettings it = Cur();
            if (it != null)
            {
                _loading = true;
                try
                {
                    _chkCentered.SetSilent(it.Centered);
                    if (!_tbX.Focused) _tbX.Text = it.X.ToString();
                    if (!_tbY.Focused) _tbY.Text = it.Y.ToString();
                }
                finally { _loading = false; }
                ShowTickCard(Shapes.HasTicks(it.Shape));
            }
            SyncLabels();
            ApplyToOverlays();
            UpdatePreview();
            UpdateSideHint();
            SaveSoon();
        }

        private void ApplyToOverlays()
        {
            if (_ovl.Count != _cfg.Items.Count) { RebuildOverlays(); return; }
            for (int i = 0; i < _ovl.Count; i++) _ovl[i].Apply(_cfg.Items[i]);
            ApplyVisibility();
        }

        private void RebuildOverlays()
        {
            for (int i = 0; i < _ovl.Count; i++)
            {
                try { _ovl[i].Close(); _ovl[i].Dispose(); }
                catch { }
            }
            _ovl.Clear();

            List<CrosshairItemSettings> list = _cfg.Items;
            for (int i = 0; i < list.Count; i++)
            {
                OverlayForm f = new OverlayForm(list[i]);
                f.PositionChangedByUser += OverlayMoved;
                _ovl.Add(f);
            }
            ApplyVisibility();
        }

        /// <summary>按全局开关 / 单个开关 / 自动显隐决定谁该出现</summary>
        private void ApplyVisibility()
        {
            bool game = GameOk();
            for (int i = 0; i < _ovl.Count; i++)
            {
                OverlayForm f = _ovl[i];
                bool want = _cfg.GlobalVisible && f.Item.Visible && game;
                if (want)
                {
                    f.RefreshPosition();
                    if (!f.Visible) f.Show();
                    f.Redraw();
                }
                else if (f.Visible) f.Hide();
            }
            UpdatePill();
        }

        /// <summary>没开自动显隐就永远算「在游戏里」</summary>
        private bool GameOk()
        {
            if (!_cfg.AutoHide || _cfg.GameExe.Length == 0) return true;
            string want = _cfg.GameExe;
            if (want.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                want = want.Substring(0, want.Length - 4);
            return string.Equals(ForegroundExeName(), want, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>滑块连续拖动时别每帧写盘，攒 400ms 一起存</summary>
        private void SaveSoon()
        {
            if (_saveTimer == null)
            {
                _saveTimer = new Timer();
                _saveTimer.Interval = 400;
                _saveTimer.Tick += delegate { _saveTimer.Stop(); _cfg.Save(); };
            }
            _saveTimer.Stop();
            _saveTimer.Start();
        }
    }
}
