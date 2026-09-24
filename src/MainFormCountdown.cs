using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace ScreenCrosshair
{
    public partial class MainForm
    {
        private const int HkEvacuation = 0x0A73;
        private const int HkRocket = 0x0A74;
        private const int HkFreeCountdown = 0x0A75;

        // 三个倒计时的热键已经并到「热键」页统一管理，这里只留时长和显示样式。
        private TextBox _tbFreeMinutes, _tbFreeSeconds;
        private Slider _sCountdownFontSize;
        private Label _vCountdownFontSize;
        private TextBox _tbCountdownRight, _tbCountdownTop;
        private FlatBtn _btnCountdownDrag;
        private bool _countdownDragMode;
        private Chk _chkClock, _chkClockSeconds;
        private Label _lblClockNow;
        private Slider _sClockFontSize;
        private Label _vClockFontSize;
        private TextBox _tbClockRight, _tbClockTop;
        private FlatBtn _btnClockDrag;
        private bool _clockDragMode;
        private Slider _sHudOpacity;
        private Label _vHudOpacity;
        private Chk _chkHudPlate, _chkHudMarkers;
        private HudPreviewBox _hudPreview;
        private readonly double[] _countdownEndedUntil = new double[3];

        private void BuildPageCountdown()
        {
            Panel pg = _pages[PageCountdown];

            Card len = NewCard(pg, "倒计时时长", 14, 200);
            // 手动断行，自动换行会把最后几个字挤到第二行显得零碎
            Ui.L(len, "撤离点固定 05:00、火箭固定 04:30，这两个不用设；\n只有自由倒计时要自己填时长。",
                Theme.Small, Theme.TextFaint, 14, 40, 426, 34);

            Ui.L(len, "自由倒计时", Theme.Body, Theme.TextMuted, 14, 82, 74, 20);
            _tbFreeMinutes = Ui.NumBox(len, 94, 80, 54);
            Ui.L(len, "分", Theme.Body, Theme.TextMuted, 152, 82, 24, 20);
            _tbFreeSeconds = Ui.NumBox(len, 178, 80, 54);
            Ui.L(len, "秒（0-59）", Theme.Small, Theme.TextFaint, 236, 82, 92, 20);

            FlatBtn apply = new FlatBtn();
            apply.Kind = 1;
            apply.Text = "应用时长";
            apply.Bounds = new Rectangle(14, 116, 100, 28);
            apply.Click += delegate { ApplyCountdownSettings(); };
            len.Controls.Add(apply);

            Ui.L(len, "三个倒计时的热键都在「热键」页设置。\n"
                    + "按下热键开始或重新开始计时，计时结束后屏幕上的面板自动隐藏。",
                Theme.Small, Theme.TextFaint, 14, 152, 426, 34);

            Card display = NewCard(pg, "倒计时显示", 226, 184);
            _sCountdownFontSize = CenteredSliderRow(display, "字号", 42, 14, 72,
                CountdownDisplayChanged, out _vCountdownFontSize);
            Ui.L(display, "位置", Theme.Body, Theme.TextMuted, 68, 80, 50, 20);
            Ui.L(display, "距右侧", Theme.Small, Theme.TextMuted, 124, 82, 52, 20);
            _tbCountdownRight = Ui.NumBox(display, 178, 78, 58);
            Ui.L(display, "px", Theme.Small, Theme.TextFaint, 242, 82, 24, 20);
            Ui.L(display, "距顶部", Theme.Small, Theme.TextMuted, 274, 82, 52, 20);
            _tbCountdownTop = Ui.NumBox(display, 328, 78, 58);
            _tbCountdownRight.Leave += CountdownDisplayChanged;
            _tbCountdownTop.Leave += CountdownDisplayChanged;
            _tbCountdownRight.KeyDown += CountdownDisplayKey;
            _tbCountdownTop.KeyDown += CountdownDisplayKey;

            FlatBtn reset = new FlatBtn();
            reset.Text = "恢复右上角";
            reset.Bounds = new Rectangle(78, 116, 106, 28);
            reset.Click += delegate { ResetCountdownPosition(); };
            display.Controls.Add(reset);
            _btnCountdownDrag = new FlatBtn();
            _btnCountdownDrag.Text = "拖动定位";
            _btnCountdownDrag.Bounds = new Rectangle(194, 116, 106, 28);
            _btnCountdownDrag.Click += delegate { ToggleCountdownDrag(); };
            display.Controls.Add(_btnCountdownDrag);
            Ui.L(display, "HUD 面板会自动按剩余时间变色；拖动定位可直观看到屏幕上的实际效果。",
                Theme.Small, Theme.TextFaint, 22, 150, 410, 20).TextAlign = ContentAlignment.MiddleCenter;

            BuildHudStyleCard(pg);
            BuildHudPreviewCard(pg);
            BuildClockCard(pg);
        }

        private void BuildHudStyleCard(Panel pg)
        {
            Card style = NewCard(pg, "HUD 样式", 404, 146);
            _sHudOpacity = CenteredSliderRow(style, "底板透明度", 40, 20, 100,
                CountdownStyleChanged, out _vHudOpacity);
            _vHudOpacity.Width = 84;

            _chkHudPlate = new Chk();
            _chkHudPlate.Text = "显示半透明底板";
            _chkHudPlate.Bounds = new Rectangle(56, 78, 170, 22);
            _chkHudPlate.CheckedChanged += CountdownStyleChanged;
            style.Controls.Add(_chkHudPlate);

            _chkHudMarkers = new Chk();
            _chkHudMarkers.Text = "显示左侧状态条";
            _chkHudMarkers.Bounds = new Rectangle(246, 78, 170, 22);
            _chkHudMarkers.CheckedChanged += CountdownStyleChanged;
            style.Controls.Add(_chkHudMarkers);

            Ui.L(style, "透明度越低越不挡背景；关闭底板后仍保留文字描边，适合极简显示。",
                Theme.Small, Theme.TextFaint, 14, 114, 426, 20).TextAlign = ContentAlignment.MiddleCenter;
        }

        private void BuildHudPreviewCard(Panel pg)
        {
            Card preview = NewCard(pg, "HUD 效果预览", 564, 244);
            _hudPreview = new HudPreviewBox();
            _hudPreview.Bounds = new Rectangle(14, 38, preview.Width - 28, 192);
            preview.Controls.Add(_hudPreview);
        }

        /// <summary>
        /// 时钟常显。游戏无边框全屏时任务栏的钟看不见，这行就顶上去了。
        /// 它挂在倒计时面板最上面一行，倒计时来去都不会把它挤走。
        /// </summary>
        private void BuildClockCard(Panel pg)
        {
            Card clock = NewCard(pg, "北京时间", 822, 228);

            _chkClock = new Chk();
            _chkClock.Text = "在屏幕上常显北京时间";
            _chkClock.Bounds = new Rectangle(14, 40, 300, 22);
            _chkClock.CheckedChanged += ClockChanged;
            clock.Controls.Add(_chkClock);

            _chkClockSeconds = new Chk();
            _chkClockSeconds.Text = "显示秒";
            _chkClockSeconds.Bounds = new Rectangle(14, 72, 110, 22);
            _chkClockSeconds.CheckedChanged += ClockChanged;
            clock.Controls.Add(_chkClockSeconds);

            _lblClockNow = Ui.L(clock, "", Theme.Mono, Theme.Accent, 132, 74, 300, 20);

            _sClockFontSize = CenteredSliderRow(clock, "时间字号", 100, 14, 72,
                ClockDisplayChanged, out _vClockFontSize);

            Ui.L(clock, "位置", Theme.Body, Theme.TextMuted, 14, 134, 50, 20);
            Ui.L(clock, "距右侧", Theme.Small, Theme.TextMuted, 74, 136, 52, 20);
            _tbClockRight = Ui.NumBox(clock, 128, 132, 58);
            Ui.L(clock, "px", Theme.Small, Theme.TextFaint, 192, 136, 24, 20);
            Ui.L(clock, "距顶部", Theme.Small, Theme.TextMuted, 224, 136, 52, 20);
            _tbClockTop = Ui.NumBox(clock, 278, 132, 58);
            _tbClockRight.Leave += ClockDisplayChanged;
            _tbClockTop.Leave += ClockDisplayChanged;
            _tbClockRight.KeyDown += ClockDisplayKey;
            _tbClockTop.KeyDown += ClockDisplayKey;

            FlatBtn reset = new FlatBtn();
            reset.Text = "恢复右上角";
            reset.Bounds = new Rectangle(78, 166, 106, 28);
            reset.Click += delegate { ResetClockPosition(); };
            clock.Controls.Add(reset);
            _btnClockDrag = new FlatBtn();
            _btnClockDrag.Text = "拖动定位时间";
            _btnClockDrag.Bounds = new Rectangle(194, 166, 122, 28);
            _btnClockDrag.Click += delegate { ToggleClockDrag(); };
            clock.Controls.Add(_btnClockDrag);

            Ui.L(clock, "时间已独立，可单独拖动；字号只影响时间，不影响倒计时。",
                Theme.Small, Theme.TextFaint, 14, 202, 426, 20);
        }

        private Slider CenteredSliderRow(Card c, string caption, int y, int min, int max,
            EventHandler onChange, out Label valLabel)
        {
            int x = (c.Width - 410) / 2;
            Ui.L(c, caption, Theme.Body, Theme.TextMuted, x, y + 2, 86, 20);
            Slider s = new Slider();
            s.Min = min;
            s.Max = max;
            s.Bounds = new Rectangle(x + 94, y, 226, 22);
            s.ValueChanged += onChange;
            c.Controls.Add(s);
            valLabel = Ui.L(c, "", Theme.Mono, Theme.Accent, x + 326, y + 2, 84, 20);
            return s;
        }

        private void PushCountdownToUi()
        {
            if (_tbFreeMinutes == null) return;
            int minutes = _cfg.FreeCountdownSeconds / 60;
            int seconds = _cfg.FreeCountdownSeconds % 60;
            _tbFreeMinutes.Text = minutes.ToString();
            _tbFreeSeconds.Text = seconds.ToString();
            _sCountdownFontSize.SetSilent(_cfg.CountdownFontSize);
            _sClockFontSize.SetSilent(_cfg.ClockFontSize);
            _tbCountdownRight.Text = _cfg.CountdownRightOffset.ToString();
            _tbCountdownTop.Text = _cfg.CountdownTopOffset.ToString();
            _tbClockRight.Text = _cfg.ClockRightOffset.ToString();
            _tbClockTop.Text = _cfg.ClockTopOffset.ToString();
            _vCountdownFontSize.Text = _cfg.CountdownFontSize + " px";
            _vClockFontSize.Text = _cfg.ClockFontSize + " px";
            _chkClock.SetSilent(_cfg.ShowClock);
            _chkClockSeconds.SetSilent(_cfg.ClockSeconds);
            _sHudOpacity.SetSilent(_cfg.HudOpacity);
            _vHudOpacity.Text = _cfg.HudOpacity + "%";
            _chkHudPlate.SetSilent(_cfg.HudShowPlate);
            _chkHudMarkers.SetSilent(_cfg.HudShowMarkers);
            RefreshClockLabel();
            RefreshHudPreview();
        }

        /// <summary>北京时间 = UTC+8。不看本机时区，出国或时区设错也照样对。</summary>
        private static DateTime BeijingNow()
        {
            return DateTime.UtcNow.AddHours(8);
        }

        private string ClockText()
        {
            return BeijingNow().ToString(_cfg.ClockSeconds ? "HH:mm:ss" : "HH:mm");
        }

        /// <summary>设置页上那行小字预览，让人不用去看屏幕就知道显示成什么样</summary>
        private void RefreshClockLabel()
        {
            if (_lblClockNow == null) return;
            _lblClockNow.Text = _cfg.ShowClock ? "现在 " + ClockText() : "已关闭";
            _lblClockNow.ForeColor = _cfg.ShowClock ? Theme.Accent : Theme.TextFaint;
        }

        private void ClockChanged(object sender, EventArgs e)
        {
            if (_loading || _chkClock == null) return;
            _cfg.ShowClock = _chkClock.Checked;
            _cfg.ClockSeconds = _chkClockSeconds.Checked;
            RefreshClockLabel();
            if (!_cfg.ShowClock && _clockDragMode) ToggleClockDrag();
            UpdateClockOverlay();
            RefreshHudPreview();
            SaveSoon();
        }

        private void ClockDisplayChanged(object sender, EventArgs e)
        {
            if (_loading || _tbClockRight == null) return;
            _cfg.ClockRightOffset = ParseInt(_tbClockRight, _cfg.ClockRightOffset, 0, 9999);
            _cfg.ClockTopOffset = ParseInt(_tbClockTop, _cfg.ClockTopOffset, 0, 9999);
            _cfg.ClockFontSize = _sClockFontSize.Value;
            UpdateClockOverlay();
            RefreshHudPreview();
            SaveSoon();
        }

        private void ClockDisplayKey(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                ClockDisplayChanged(sender, EventArgs.Empty);
                e.SuppressKeyPress = true;
            }
        }

        private void ResetClockPosition()
        {
            _tbClockRight.Text = "18";
            _tbClockTop.Text = "24";
            ClockDisplayChanged(null, EventArgs.Empty);
            Toast("时间已恢复到右上角");
        }

        private void CountdownDisplayChanged(object sender, EventArgs e)
        {
            if (_loading || _sCountdownFontSize == null) return;
            _cfg.CountdownFontSize = _sCountdownFontSize.Value;
            _cfg.CountdownRightOffset = ParseInt(_tbCountdownRight, _cfg.CountdownRightOffset, 0, 9999);
            _cfg.CountdownTopOffset = ParseInt(_tbCountdownTop, _cfg.CountdownTopOffset, 0, 9999);
            if (_countdownOverlay != null && _countdownOverlay.Visible)
                UpdateCountdownOverlay();
            RefreshHudPreview();
            SaveSoon();
        }

        private void CountdownStyleChanged(object sender, EventArgs e)
        {
            if (_loading || _sHudOpacity == null) return;
            _cfg.HudOpacity = _sHudOpacity.Value;
            _cfg.HudShowPlate = _chkHudPlate.Checked;
            _cfg.HudShowMarkers = _chkHudMarkers.Checked;
            _vHudOpacity.Text = _cfg.HudOpacity + "%";
            if (_countdownOverlay != null)
                _countdownOverlay.SetStyle(_cfg.HudOpacity, _cfg.HudShowPlate, _cfg.HudShowMarkers);
            if (_clockOverlay != null)
                _clockOverlay.SetStyle(_cfg.HudOpacity, _cfg.HudShowPlate, _cfg.HudShowMarkers);
            RefreshHudPreview();
            SaveSoon();
        }

        private void RefreshHudPreview()
        {
            if (_hudPreview == null) return;
            _hudPreview.SetStyle(_cfg.ClockFontSize, _cfg.CountdownFontSize,
                _cfg.HudOpacity, _cfg.HudShowPlate, _cfg.HudShowMarkers,
                _cfg.ClockSeconds, _cfg.ShowClock, _cfg.FreeCountdownSeconds);
        }

        private void CountdownDisplayKey(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                CountdownDisplayChanged(sender, EventArgs.Empty);
                e.SuppressKeyPress = true;
            }
        }

        private void ResetCountdownPosition()
        {
            _tbCountdownRight.Text = "18";
            _tbCountdownTop.Text = "76";
            CountdownDisplayChanged(null, EventArgs.Empty);
            Toast("倒计时已恢复到右上角");
        }

        private void ToggleCountdownDrag()
        {
            if (_countdownOverlay == null)
            {
                _countdownOverlay = new CountdownOverlayForm();
                _countdownOverlay.PositionChangedByUser += CountdownMoved;
            }
            if (!_countdownDragMode)
            {
                _countdownDragMode = true;
                try { _countdownScreen = Screen.FromPoint(Control.MousePosition); }
                catch { _countdownScreen = Screen.PrimaryScreen; }
                _countdownOverlay.SetMoveMode(true);
                UpdateCountdownOverlay();
                _btnCountdownDrag.Text = "完成定位";
                _btnCountdownDrag.Kind = 1;
                _btnCountdownDrag.Invalidate();
                Toast("拖动屏幕上的倒计时面板，放好后点击「完成定位」");
            }
            else
            {
                _countdownDragMode = false;
                _countdownOverlay.SetMoveMode(false);
                _btnCountdownDrag.Text = "拖动定位";
                _btnCountdownDrag.Kind = 0;
                _btnCountdownDrag.Invalidate();
                _cfg.Save();
                UpdateCountdownOverlay();
                Toast("倒计时位置已应用，将自动保存");
            }
        }

        private void CountdownMoved(object sender, EventArgs e)
        {
            if (!_countdownDragMode || _countdownOverlay == null) return;
            Point center = new Point(_countdownOverlay.Left + _countdownOverlay.Width / 2,
                _countdownOverlay.Top + _countdownOverlay.Height / 2);
            try { _countdownScreen = Screen.FromPoint(center); }
            catch { _countdownScreen = Screen.PrimaryScreen; }
            if (_countdownScreen == null) return;
            Rectangle b = _countdownScreen.WorkingArea;
            _cfg.CountdownRightOffset = Math.Max(0, b.Right - _countdownOverlay.Right);
            _cfg.CountdownTopOffset = Math.Max(0, _countdownOverlay.Top - b.Top);
            _cfg.CountdownScreenName = _countdownScreen.DeviceName;
            _loading = true;
            try
            {
                _tbCountdownRight.Text = _cfg.CountdownRightOffset.ToString();
                _tbCountdownTop.Text = _cfg.CountdownTopOffset.ToString();
            }
            finally { _loading = false; }
            SaveSoon();
        }

        private void ToggleClockDrag()
        {
            if (!_cfg.ShowClock)
            {
                Toast("请先开启屏幕时间");
                return;
            }
            if (_clockOverlay == null)
            {
                _clockOverlay = new CountdownOverlayForm();
                _clockOverlay.PositionChangedByUser += ClockMoved;
            }
            if (!_clockDragMode)
            {
                _clockDragMode = true;
                try { _clockScreen = Screen.FromPoint(Control.MousePosition); }
                catch { _clockScreen = Screen.PrimaryScreen; }
                _clockOverlay.SetMoveMode(true);
                UpdateClockOverlay();
                _btnClockDrag.Text = "完成定位";
                _btnClockDrag.Kind = 1;
                _btnClockDrag.Invalidate();
                Toast("拖动屏幕上的时间，放好后点击「完成定位」");
            }
            else
            {
                _clockDragMode = false;
                _clockOverlay.SetMoveMode(false);
                _btnClockDrag.Text = "拖动定位时间";
                _btnClockDrag.Kind = 0;
                _btnClockDrag.Invalidate();
                _cfg.Save();
                UpdateClockOverlay();
                Toast("时间位置已应用，将自动保存");
            }
        }

        private void ClockMoved(object sender, EventArgs e)
        {
            if (!_clockDragMode || _clockOverlay == null) return;
            Point center = new Point(_clockOverlay.Left + _clockOverlay.Width / 2,
                _clockOverlay.Top + _clockOverlay.Height / 2);
            try { _clockScreen = Screen.FromPoint(center); }
            catch { _clockScreen = Screen.PrimaryScreen; }
            if (_clockScreen == null) return;
            Rectangle b = _clockScreen.WorkingArea;
            _cfg.ClockRightOffset = Math.Max(0, b.Right - _clockOverlay.Right);
            _cfg.ClockTopOffset = Math.Max(0, _clockOverlay.Top - b.Top);
            _cfg.ClockScreenName = _clockScreen.DeviceName;
            _loading = true;
            try
            {
                _tbClockRight.Text = _cfg.ClockRightOffset.ToString();
                _tbClockTop.Text = _cfg.ClockTopOffset.ToString();
            }
            finally { _loading = false; }
            SaveSoon();
        }

        private List<CountdownDisplay> CountdownPreviewItems()
        {
            List<CountdownDisplay> list = new List<CountdownDisplay>();
            list.Add(new CountdownDisplay { Name = "撤离点", RemainingSeconds = 300 });
            list.Add(new CountdownDisplay { Name = "火箭", RemainingSeconds = 270 });
            list.Add(new CountdownDisplay { Name = "自由", RemainingSeconds = _cfg.FreeCountdownSeconds });
            return list;
        }

        private void ApplyCountdownSettings()
        {
            int minutes = ParseInt(_tbFreeMinutes, 0, 0, 999);
            int seconds = ParseInt(_tbFreeSeconds, 0, 0, 59);
            int total = minutes * 60 + seconds;
            if (total < 1)
            {
                Toast("自由倒计时至少设置 1 秒");
                return;
            }

            _cfg.FreeCountdownSeconds = total;
            RefreshHudPreview();
            _cfg.Save();
            Toast("自由倒计时时长已应用");
        }

        private void StartCountdown(int index)
        {
            if (index < 0 || index > 2) return;
            if (_countdownDragMode) ToggleCountdownDrag();
            int seconds = index == 0 ? 300 : (index == 1 ? 270 : _cfg.FreeCountdownSeconds);
            if (seconds < 1) seconds = 1;
            _countdownEnds[index] = MonotonicTime.Seconds + seconds;
            _countdownRunning[index] = true;
            _countdownEndedUntil[index] = 0;
            _countdownScreen = FindScreen(_cfg.CountdownScreenName);
            if (_countdownScreen == null) try { _countdownScreen = Screen.FromPoint(Control.MousePosition); }
            catch { _countdownScreen = Screen.PrimaryScreen; }
            UpdateCountdownOverlay();
        }

        private void StopAllCountdowns()
        {
            for (int i = 0; i < _countdownRunning.Length; i++)
            {
                _countdownRunning[i] = false;
                _countdownEndedUntil[i] = 0;
            }
            UpdateCountdownOverlay();
        }

        private void TickCountdowns()
        {
            bool any = false;
            bool ended = false;
            double now = MonotonicTime.Seconds;
            for (int i = 0; i < _countdownRunning.Length; i++)
            {
                if (!_countdownRunning[i]) continue;
                if (_countdownEnds[i] <= now)
                {
                    _countdownRunning[i] = false;
                    _countdownEndedUntil[i] = now + 1;
                    ended = true;
                    Toast(CountdownName(i) + "已结束");
                }
                else any = true;
            }

            for (int i = 0; i < _countdownEndedUntil.Length; i++)
                if (_countdownEndedUntil[i] > now) { ended = true; break; }
            if (any || ended) UpdateCountdownOverlay();
            else if (_countdownOverlay != null && !_countdownDragMode) _countdownOverlay.Hide();
            UpdateClockOverlay();
            if (_cfg.ShowClock && _page == PageCountdown) RefreshClockLabel();
        }

        private void UpdateCountdownOverlay()
        {
            if (_countdownOverlay == null)
            {
                _countdownOverlay = new CountdownOverlayForm();
                _countdownOverlay.PositionChangedByUser += CountdownMoved;
            }
            _countdownOverlay.SetFontSize(_cfg.CountdownFontSize);
            _countdownOverlay.SetStyle(_cfg.HudOpacity, _cfg.HudShowPlate, _cfg.HudShowMarkers);

            if (_countdownDragMode)
            {
                _countdownOverlay.SetItems(CountdownPreviewItems());
                _countdownOverlay.PlaceOnScreen(_countdownScreen,
                    _cfg.CountdownRightOffset, _cfg.CountdownTopOffset);
                _countdownOverlay.ShowTopNoActivate();
                return;
            }

            List<CountdownDisplay> items = new List<CountdownDisplay>();
            double now = MonotonicTime.Seconds;
            for (int i = 0; i < _countdownRunning.Length; i++)
            {
                if (_countdownRunning[i])
                {
                    int left = MonotonicTime.Remaining(_countdownEnds[i], now);
                    if (left < 1) left = 1;
                    items.Add(new CountdownDisplay { Name = CountdownName(i), RemainingSeconds = left });
                }
                else if (_countdownEndedUntil[i] > now)
                {
                    items.Add(new CountdownDisplay { Name = CountdownName(i), RemainingSeconds = 0, Ended = true });
                }
            }

            if (items.Count == 0)
            {
                _countdownOverlay.Hide();
                return;
            }
            _countdownOverlay.SetItems(items);
            _countdownOverlay.PlaceOnScreen(_countdownScreen,
                _cfg.CountdownRightOffset, _cfg.CountdownTopOffset);
            _countdownOverlay.ShowTopNoActivate();
        }

        private void UpdateClockOverlay()
        {
            if (!_cfg.ShowClock)
            {
                if (_clockOverlay != null && !_clockDragMode) _clockOverlay.Hide();
                return;
            }
            if (_clockOverlay == null)
            {
                _clockOverlay = new CountdownOverlayForm();
                _clockOverlay.PositionChangedByUser += ClockMoved;
            }
            _clockOverlay.SetFontSize(_cfg.ClockFontSize);
            _clockOverlay.SetStyle(_cfg.HudOpacity, _cfg.HudShowPlate, _cfg.HudShowMarkers);
            _clockOverlay.SetItems(new List<CountdownDisplay> {
                new CountdownDisplay { Name = "", Text = ClockText() }
            });
            if (_clockScreen == null)
            {
                _clockScreen = FindScreen(_cfg.ClockScreenName);
                if (_clockScreen != null) { }
                else try { _clockScreen = Screen.FromPoint(Control.MousePosition); }
                catch { _clockScreen = Screen.PrimaryScreen; }
            }
            _clockOverlay.PlaceOnScreen(_clockScreen, _cfg.ClockRightOffset, _cfg.ClockTopOffset);
            _clockOverlay.ShowTopNoActivate();
        }

        private static Screen FindScreen(string deviceName)
        {
            if (string.IsNullOrEmpty(deviceName)) return null;
            Screen[] all = Screen.AllScreens;
            for (int i = 0; i < all.Length; i++)
                if (string.Equals(all[i].DeviceName, deviceName, StringComparison.OrdinalIgnoreCase))
                    return all[i];
            return null;
        }

        private static string CountdownName(int index)
        {
            if (index == 0) return "撤离点";
            if (index == 1) return "火箭";
            return "自由";
        }

    }
}
