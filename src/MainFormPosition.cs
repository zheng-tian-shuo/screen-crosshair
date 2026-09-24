using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace ScreenCrosshair
{
    public partial class MainForm
    {
        private Chk _chkCentered;
        private TextBox _tbX, _tbY;
        private FlatBtn _btnDrag;
        private ComboBox _cbScreen;
        private List<string> _screenNames = new List<string>();
        private TextBox _tbFovTarget;
        private TextBox _tbTargetWidth, _tbTargetHeight;
        private TextBox _tbTargetAspect;
        private TextBox _tbWindowX, _tbWindowY;
        private ComboBox _cbDisplayMode;
        private Label _lblFovResult, _lblFovHint, _lblTargetSize;
        private bool _syncingFov;

        private void BuildPositionCards()
        {
            Panel pg = _pages[PageCrosshair];

            Card c1 = NewCard(pg, "位置", 622, 282);
            _pgCards.Add(c1);

            _chkCentered = new Chk();
            _chkCentered.Text = "屏幕居中（先居中，再用下面的方式微调到落点）";
            _chkCentered.Bounds = new Rectangle(14, 40, 420, 22);
            _chkCentered.CheckedChanged += UiChanged;
            c1.Controls.Add(_chkCentered);

            Ui.L(c1, "X", Theme.Body, Theme.TextMuted, 14, 76, 18, 20);
            _tbX = Ui.NumBox(c1, 34, 74, 70);
            Ui.L(c1, "Y", Theme.Body, Theme.TextMuted, 116, 76, 18, 20);
            _tbY = Ui.NumBox(c1, 136, 74, 70);
            _tbX.Leave += XyCommit;
            _tbY.Leave += XyCommit;
            _tbX.KeyDown += XyKey;
            _tbY.KeyDown += XyKey;

            FlatBtn apply = new FlatBtn();
            apply.Text = "应用坐标";
            apply.Bounds = new Rectangle(216, 73, 84, 26);
            apply.Click += delegate { CommitXy(); };
            c1.Controls.Add(apply);

            Label xyHint = Ui.L(c1, "相对目标屏幕左上角", Theme.Small, Theme.TextFaint,
                310, 73, 130, 26);
            xyHint.TextAlign = ContentAlignment.MiddleLeft;

            FlatBtn pick = new FlatBtn();
            pick.Kind = 1;
            pick.Text = "屏幕取点";
            pick.Bounds = new Rectangle(14, 108, 96, 28);
            pick.Click += delegate { PickOnScreen(); };
            c1.Controls.Add(pick);

            _btnDrag = new FlatBtn();
            _btnDrag.Text = "拖动定位";
            _btnDrag.Bounds = new Rectangle(118, 108, 96, 28);
            _btnDrag.Click += delegate { ToggleDrag(); };
            c1.Controls.Add(_btnDrag);

            FlatBtn center = new FlatBtn();
            center.Text = "回到中心";
            center.Bounds = new Rectangle(222, 108, 96, 28);
            center.Click += delegate { BackToCenter(); };
            c1.Controls.Add(center);

            Ui.L(c1, "1 像素微调", Theme.Body, Theme.TextMuted, 14, 154, 76, 20);
            FlatBtn btnUp = new FlatBtn();
            btnUp.Text = "↑";
            btnUp.Bounds = new Rectangle(136, 146, 32, 26);
            btnUp.Tag = new Point(0, -1);
            btnUp.Click += NudgeClick;
            c1.Controls.Add(btnUp);

            FlatBtn btnLeft = new FlatBtn();
            btnLeft.Text = "←";
            btnLeft.Bounds = new Rectangle(100, 176, 32, 26);
            btnLeft.Tag = new Point(-1, 0);
            btnLeft.Click += NudgeClick;
            c1.Controls.Add(btnLeft);

            FlatBtn btnDown = new FlatBtn();
            btnDown.Text = "↓";
            btnDown.Bounds = new Rectangle(136, 176, 32, 26);
            btnDown.Tag = new Point(0, 1);
            btnDown.Click += NudgeClick;
            c1.Controls.Add(btnDown);

            FlatBtn btnRight = new FlatBtn();
            btnRight.Text = "→";
            btnRight.Bounds = new Rectangle(172, 176, 32, 26);
            btnRight.Tag = new Point(1, 0);
            btnRight.Click += NudgeClick;
            c1.Controls.Add(btnRight);

            Label nudgeHint = Ui.L(c1, "点击方向键单像素微调落点\n按一下动 1 像素，立刻生效", Theme.Small,
                Theme.TextFaint, 218, 154, 216, 44);
            nudgeHint.TextAlign = ContentAlignment.MiddleLeft;

            // ---- 目标屏幕 ----
            Panel sep = new Panel();
            sep.Bounds = new Rectangle(14, 212, 426, 1);
            sep.BackColor = Theme.Border;
            c1.Controls.Add(sep);

            Ui.L(c1, "目标屏幕", Theme.Body, Theme.TextMuted, 14, 224, 60, 20);
            _cbScreen = Ui.Combo(c1, 78, 221, 260);
            _cbScreen.SelectedIndexChanged += UiChanged;
            FillScreens();
            Ui.L(c1, "选「跟随鼠标所在屏」时，准星会跟着你正在用的那块屏走",
                Theme.Small, Theme.TextFaint, 14, 252, 426, 20);

            // ---- FOV 坐标换算 ----
            Card c4 = NewCard(pg, "FOV / 分辨率坐标换算", 920, 332);
            _pgCards.Add(c4);
            Ui.L(c4, "水平 FOV", Theme.Body, Theme.TextMuted, 14, 42, 72, 20);
            _tbFovTarget = Ui.NumBox(c4, 96, 39, 64);
            Ui.L(c4, "°", Theme.Body, Theme.TextFaint, 164, 42, 18, 20);

            _lblTargetSize = Ui.L(c4, "目标分辨率", Theme.Body, Theme.TextMuted, 14, 76, 76, 20);
            _tbTargetWidth = Ui.NumBox(c4, 96, 73, 64);
            Ui.L(c4, "×", Theme.Body, Theme.TextFaint, 162, 76, 12, 20);
            _tbTargetHeight = Ui.NumBox(c4, 178, 73, 64);
            Ui.L(c4, "宽高比", Theme.Body, Theme.TextMuted, 14, 110, 54, 20);
            _tbTargetAspect = Ui.Box(c4, 78, 107, 64);
            _tbTargetAspect.Text = "16:9";
            _tbTargetAspect.TextAlign = HorizontalAlignment.Center;
            Ui.L(c4, "游戏画面比例（默认 16:9）", Theme.Small, Theme.TextFaint, 150, 110, 290, 20);
            SetTargetResolutionFromScreen();
            FlatBtn currentSize = new FlatBtn();
            currentSize.Text = "使用屏幕尺寸";
            currentSize.Bounds = new Rectangle(260, 73, 110, 26);
            currentSize.Click += delegate { SetTargetResolutionFromScreen(); };
            c4.Controls.Add(currentSize);

            Ui.L(c4, "显示方式", Theme.Body, Theme.TextMuted, 14, 144, 72, 20);
            _cbDisplayMode = Ui.Combo(c4, 96, 141, 272);
            _cbDisplayMode.Items.AddRange(new object[] {
                "全屏拉伸", "保持比例（黑边）", "窗口（手填画面区域）" });
            _cbDisplayMode.SelectedIndex = 0;
            Ui.L(c4, "窗口左上角", Theme.Body, Theme.TextMuted, 14, 178, 76, 20);
            Ui.L(c4, "X", Theme.Body, Theme.TextMuted, 96, 178, 18, 20);
            _tbWindowX = Ui.NumBox(c4, 116, 175, 64);
            Ui.L(c4, "Y", Theme.Body, Theme.TextMuted, 196, 178, 18, 20);
            _tbWindowY = Ui.NumBox(c4, 216, 175, 64);
            _lblFovHint = Ui.L(c4, "", Theme.Small, Theme.TextMuted, 14, 210, 426, 36);

            FlatBtn convert = new FlatBtn();
            convert.Kind = 1;
            convert.Text = "自动生成准星位置";
            convert.Bounds = new Rectangle(14, 258, 136, 30);
            convert.Click += delegate { GenerateTemplatePoint(); };
            c4.Controls.Add(convert);

            _lblFovResult = Ui.L(c4, "确认参数后生成",
                Theme.Small, Theme.TextMuted, 170, 250, 270, 44);
            _lblFovResult.TextAlign = ContentAlignment.MiddleLeft;

            Ui.L(c4, "模板基准：FOV 90 · 1920×1080 · X953 Y975",
                Theme.Small, Theme.TextFaint, 14, 304, 426, 20);
            foreach (TextBox box in new TextBox[] { _tbFovTarget, _tbTargetWidth,
                _tbTargetHeight, _tbTargetAspect, _tbWindowX, _tbWindowY })
                box.TextChanged += FovInputChanged;
            _cbDisplayMode.SelectedIndexChanged += FovInputChanged;
            UpdateFovModeUi();
        }

        private void SetTargetResolutionFromScreen()
        {
            if (_tbTargetWidth == null || _tbTargetHeight == null) return;
            Screen screen = ScreenOf(Cur());
            if (screen == null) screen = Screen.PrimaryScreen;
            if (screen == null) return;
            _syncingFov = true;
            try
            {
                _tbTargetWidth.Text = screen.Bounds.Width.ToString();
                _tbTargetHeight.Text = screen.Bounds.Height.ToString();
            }
            finally { _syncingFov = false; }
            if (_cbDisplayMode != null) FovInputChanged(null, EventArgs.Empty);
        }

        private void FillScreens()
        {
            _screenNames.Clear();
            _cbScreen.Items.Clear();
            _screenNames.Add("");
            _cbScreen.Items.Add("跟随鼠标所在屏");
            Screen[] all = Screen.AllScreens;
            for (int i = 0; i < all.Length; i++)
            {
                _screenNames.Add(all[i].DeviceName);
                _cbScreen.Items.Add("屏幕 " + (i + 1) + "   " +
                    all[i].Bounds.Width + "×" + all[i].Bounds.Height +
                    (all[i].Primary ? "   (主屏)" : ""));
            }
        }

        private static bool TryAspectRatio(TextBox textBox, out double ratio)
        {
            return ProjectionMath.TryAspectRatio(textBox == null ? "" : textBox.Text, out ratio);
        }

        private void SyncFovUi(CrosshairItemSettings it)
        {
            if (_tbFovTarget == null || it == null) return;
            _syncingFov = true;
            try
            {
                _tbFovTarget.Text = it.TargetFov.ToString("R", CultureInfo.InvariantCulture);
                _tbTargetAspect.Text = it.TargetAspect;
                Size size = new Size(it.TargetWidth, it.TargetHeight);
                if (size.Width < 2 || size.Height < 2)
                {
                    Screen screen = ScreenOf(it);
                    if (screen != null) size = screen.Bounds.Size;
                }
                _tbTargetWidth.Text = size.Width.ToString();
                _tbTargetHeight.Text = size.Height.ToString();
                _cbDisplayMode.SelectedIndex = (int)it.DisplayMode;
                _tbWindowX.Text = it.WindowOriginX.ToString();
                _tbWindowY.Text = it.WindowOriginY.ToString();
            }
            finally { _syncingFov = false; }
            UpdateFovModeUi();
            ResetFovResult();
        }

        private void UpdateFovModeUi()
        {
            bool window = _cbDisplayMode.SelectedIndex == (int)ProjectionDisplayMode.Window;
            _tbWindowX.Enabled = _tbWindowY.Enabled = window;
            _lblTargetSize.Text = window ? "画面大小" : "目标分辨率";
            _lblFovHint.Text = window
                ? "填写游戏画面的实际像素大小和左上角位置（不含边框）。\n左上角相对目标屏幕；移动或缩放窗口后需重新填写。"
                : (_cbDisplayMode.SelectedIndex == (int)ProjectionDisplayMode.Fit
                    ? "按所填宽高比居中显示，自动扣除上下或左右黑边。"
                    : "游戏画面拉伸并铺满目标屏幕时使用。\n有黑边请选择「保持比例」，窗口模式请填写画面区域。");
        }

        private void FovInputChanged(object sender, EventArgs e)
        {
            if (_loading || _syncingFov) return;
            UpdateFovModeUi();
            ResetFovResult("参数已修改，请重新生成");
            SaveFovInputs();
        }

        private void SaveFovInputs()
        {
            CrosshairItemSettings it = Cur();
            if (it == null) return;
            double value;
            int width, height, x, y;
            if (TryFov(_tbFovTarget, out value)) it.TargetFov = value;
            if (TryAspectRatio(_tbTargetAspect, out value)) it.TargetAspect = _tbTargetAspect.Text.Trim();
            if (TryResolution(_tbTargetWidth.Text, _tbTargetHeight.Text, out width, out height))
            {
                it.TargetWidth = width;
                it.TargetHeight = height;
            }
            if (_cbDisplayMode.SelectedIndex >= 0)
                it.DisplayMode = (ProjectionDisplayMode)_cbDisplayMode.SelectedIndex;
            if (TryWindowOrigin(out x, out y))
            {
                it.WindowOriginX = x;
                it.WindowOriginY = y;
            }
            SaveSoon();
        }

        private bool TryWindowOrigin(out int x, out int y)
        {
            x = y = 0;
            return int.TryParse(_tbWindowX.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out x) &&
                int.TryParse(_tbWindowY.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out y) &&
                x >= 0 && x <= 32767 && y >= 0 && y <= 32767;
        }

        private void ResetFovResult(string message = "确认参数后生成")
        {
            if (_lblFovResult == null) return;
            _lblFovResult.Text = message;
            _lblFovResult.ForeColor = Theme.TextMuted;
            if (_toast != null && _toast.Text.StartsWith("已生成准星位置")) _toast.Visible = false;
        }

        private void FovFailure(string message)
        {
            _lblFovResult.Text = message;
            _lblFovResult.ForeColor = Theme.Danger;
            Toast(message);
        }

        private void GenerateTemplatePoint()
        {
            CrosshairItemSettings it = Cur();
            if (it == null) return;
            ResetFovResult();
            double targetFov, aspect;
            if (!TryFov(_tbFovTarget, out targetFov))
            {
                FovFailure("水平 FOV 要大于 1 且小于 179");
                return;
            }
            if (!TryAspectRatio(_tbTargetAspect, out aspect))
            {
                FovFailure("宽高比格式应为 16:9 或 1.777");
                return;
            }
            int targetWidth, targetHeight;
            if (!TryResolution(_tbTargetWidth.Text, _tbTargetHeight.Text, out targetWidth, out targetHeight))
            {
                FovFailure("画面宽高要填 2 到 32767 之间的整数");
                return;
            }
            Screen screen = ScreenOf(it);
            if (screen == null) screen = Screen.PrimaryScreen;
            if (screen == null) { FovFailure("无法识别当前屏幕"); return; }
            ProjectionDisplayMode mode = (ProjectionDisplayMode)_cbDisplayMode.SelectedIndex;
            int originX = 0, originY = 0;
            if (mode == ProjectionDisplayMode.Window && !TryWindowOrigin(out originX, out originY))
            {
                FovFailure("窗口左上角要填 0 到 32767 之间的整数");
                return;
            }
            Rectangle viewport;
            if (!ProjectionMath.TryGetViewport(screen.Bounds.Size, new Size(targetWidth, targetHeight),
                aspect, mode, new Point(originX, originY), out viewport))
            {
                FovFailure("画面区域必须完整位于目标屏幕内");
                return;
            }
            Point generated;
            if (!ProjectionMath.TryConvertHorizontalFov(new Point(953, 975), new Size(1920, 1080),
                90.0, viewport, targetFov, aspect, out generated))
            {
                FovFailure("模板点超出当前视野，已保留原位置");
                return;
            }
            SaveFovInputs();
            it.Centered = false;
            it.X = generated.X;
            it.Y = generated.Y;
            AfterEdit();
            _lblFovResult.Text = "\u5df2\u751f\u6210 X " + generated.X + "\uff0cY " + generated.Y;
            _lblFovResult.ForeColor = Theme.Green;
            Toast("\u5df2\u751f\u6210\u51c6\u661f\u4f4d\u7f6e X " + generated.X + " Y " + generated.Y);
        }

        private void NudgeClick(object sender, EventArgs e)
        {
            Control c = sender as Control;
            if (c == null || !(c.Tag is Point)) return;
            Point d = (Point)c.Tag;
            CrosshairItemSettings it = Cur();
            if (it == null) return;
            if (it.Centered)
            {
                // 从居中切到自由坐标时，先把当前中心点算出来当起点
                Rectangle b = ScreenOf(it).Bounds;
                it.X = b.Width / 2;
                it.Y = b.Height / 2;
                it.Centered = false;
            }
            it.X += d.X;
            it.Y += d.Y;
            AfterEdit();
        }

        private void XyKey(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter) { CommitXy(); e.SuppressKeyPress = true; }
        }

        private void XyCommit(object sender, EventArgs e)
        {
            CommitXy();
        }

        private void CommitXy()
        {
            if (_loading) return;
            CrosshairItemSettings it = Cur();
            if (it == null) return;
            it.X = ParseInt(_tbX, it.X, -32768, 32767);
            it.Y = ParseInt(_tbY, it.Y, -32768, 32767);
            it.Centered = false;
            AfterEdit();
        }

        private static bool TryFov(TextBox t, out double fov)
        {
            fov = 0;
            if (t == null || !double.TryParse(t.Text.Trim(), NumberStyles.Float,
                CultureInfo.InvariantCulture, out fov)) return false;
            return fov > 1.0 && fov < 179.0;
        }

        internal static bool TryResolution(string width, string height, out int w, out int h)
        {
            w = h = 0;
            return int.TryParse(width, NumberStyles.Integer, CultureInfo.InvariantCulture, out w) &&
                int.TryParse(height, NumberStyles.Integer, CultureInfo.InvariantCulture, out h) &&
                w >= 2 && w <= 32767 && h >= 2 && h <= 32767;
        }

    }
}
