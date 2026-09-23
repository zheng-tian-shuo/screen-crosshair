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
        private Label _lblFovResult;

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
            Card c4 = NewCard(pg, "FOV / 分辨率坐标换算", 920, 220);
            _pgCards.Add(c4);
            Ui.L(c4, "目标 FOV", Theme.Body, Theme.TextMuted, 14, 42, 72, 20);
            _tbFovTarget = Ui.NumBox(c4, 96, 39, 64);
            Ui.L(c4, "°", Theme.Body, Theme.TextFaint, 164, 42, 18, 20);

            Ui.L(c4, "目标分辨率", Theme.Body, Theme.TextMuted, 14, 76, 68, 20);
            _tbTargetWidth = Ui.NumBox(c4, 96, 73, 64);
            Ui.L(c4, "×", Theme.Body, Theme.TextFaint, 162, 76, 12, 20);
            _tbTargetHeight = Ui.NumBox(c4, 178, 73, 64);
            Ui.L(c4, "宽高比", Theme.Body, Theme.TextMuted, 14, 110, 54, 20);
            _tbTargetAspect = Ui.Box(c4, 78, 107, 64);
            _tbTargetAspect.Text = "16:9";
            _tbTargetAspect.TextAlign = HorizontalAlignment.Center;
            Ui.L(c4, "默认 16:9", Theme.Small, Theme.TextFaint, 150, 110, 80, 20);
            SetTargetResolutionFromScreen();
            FlatBtn currentSize = new FlatBtn();
            currentSize.Text = "识别分辨率";
            currentSize.Bounds = new Rectangle(260, 73, 90, 26);
            currentSize.Click += delegate { SetTargetResolutionFromScreen(); };
            c4.Controls.Add(currentSize);

            FlatBtn convert = new FlatBtn();
            convert.Kind = 1;
            convert.Text = "自动生成准星位置";
            convert.Bounds = new Rectangle(14, 144, 136, 30);
            convert.Click += delegate { GenerateTemplatePoint(); };
            c4.Controls.Add(convert);

            _lblFovResult = Ui.L(c4, "输入目标 FOV 和宽高比后生成",
                Theme.Small, Theme.TextMuted, 170, 144, 270, 32);
            _lblFovResult.TextAlign = ContentAlignment.MiddleLeft;

            Ui.L(c4, "模板基准：FOV 90 · 1920×1080 · X953 Y975",
                Theme.Small, Theme.TextFaint, 14, 182, 426, 20);

        }

        private void SetTargetResolutionFromScreen()
        {
            if (_tbTargetWidth == null || _tbTargetHeight == null) return;
            Screen screen = ScreenOf(Cur());
            if (screen == null) screen = Screen.PrimaryScreen;
            if (screen == null) return;
            _tbTargetWidth.Text = screen.Bounds.Width.ToString();
            _tbTargetHeight.Text = screen.Bounds.Height.ToString();
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
            ratio = 0;
            string text = textBox == null ? "" : textBox.Text.Trim().Replace('\uFF1A', ':');
            string[] parts = text.Split(':');
            double width, height;
            if (parts.Length == 2)
            {
                if (!double.TryParse(parts[0].Trim(), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out width) ||
                    !double.TryParse(parts[1].Trim(), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out height) || height <= 0)
                    return false;
                ratio = width / height;
            }
            else if (!double.TryParse(text, NumberStyles.Float,
                CultureInfo.InvariantCulture, out ratio)) return false;
            return ratio > 0.1 && ratio < 10.0 &&
                !double.IsNaN(ratio) && !double.IsInfinity(ratio);
        }

        private void GenerateTemplatePoint()
        {
            CrosshairItemSettings it = Cur();
            if (it == null) return;
            double targetFov, aspect;
            if (!TryFov(_tbFovTarget, out targetFov))
            {
                Toast("\u76ee\u6807 FOV \u8981\u586b 1 \u5230 179 \u4e4b\u95f4\u7684\u6570\u5b57");
                return;
            }
            if (!TryAspectRatio(_tbTargetAspect, out aspect))
            {
                Toast("\u5bbd\u9ad8\u6bd4\u683c\u5f0f\u5e94\u4e3a 16:9 \u6216 1.777");
                return;
            }
            SetTargetResolutionFromScreen();
            int targetWidth = ParseInt(_tbTargetWidth, 0, 2, 32767);
            int targetHeight = ParseInt(_tbTargetHeight, 0, 2, 32767);
            if (targetWidth < 2 || targetHeight < 2)
            {
                Toast("\u65e0\u6cd5\u8bc6\u522b\u5f53\u524d\u5c4f\u5e55\u5206\u8fa8\u7387");
                return;
            }
            int projectionHeight = (int)Math.Round(targetWidth / aspect);
            if (projectionHeight < 2 || projectionHeight > 32767)
            {
                Toast("\u8be5\u5bbd\u9ad8\u6bd4\u4e0b\u76ee\u6807\u753b\u9762\u9ad8\u5ea6\u65e0\u6548");
                return;
            }
            Point projected = ProjectionMath.ConvertHorizontalFov(
                new Point(953, 975), new Size(1920, 1080), 90.0,
                new Size(targetWidth, projectionHeight), targetFov, aspect);
            Screen screen = ScreenOf(it);
            if (screen == null) screen = Screen.PrimaryScreen;
            if (screen == null) { Toast("\u65e0\u6cd5\u8bc6\u522b\u5f53\u524d\u5c4f\u5e55"); return; }
            Rectangle desktop = screen.Bounds;
            Point generated = new Point(
                (int)Math.Round((double)projected.X * desktop.Width / targetWidth),
                ClampPixel((int)Math.Round((double)projected.Y * desktop.Height / projectionHeight)));
            it.FovReferenceSet = true;
            it.FovReference = 90.0;
            it.FovReferenceX = 953;
            it.FovReferenceY = 975;
            it.FovReferenceWidth = 1920;
            it.FovReferenceHeight = 1080;
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

        private static int ClampPixel(int n)
        {
            if (n < 0) return 0;
            if (n > 32767) return 32767;
            return n;
        }

    }
}
