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
        private TextBox _tbFovBase, _tbFovTarget;
        private Label _lblFovBasePoint, _lblFovResult;

        private void BuildPositionCards()
        {
            Panel pg = _pages[PageCrosshair];

            Card c1 = NewCard(pg, "位置", 622, 258);
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

            // 卡片内容区只有 440 px 宽，这行提示挤在「应用坐标」右边，写长了会被截掉。
            // 上下范围跟按钮取齐并垂直居中，视觉上才是一行。
            Label xyHint = Ui.L(c1, "相对目标屏幕左上角", Theme.Small, Theme.TextFaint,
                310, 73, 130, 26);
            xyHint.TextAlign = ContentAlignment.MiddleLeft;

            FlatBtn pick = new FlatBtn();
            pick.Kind = 1;
            pick.Text = "屏幕取点";
            pick.Bounds = new Rectangle(14, 110, 100, 30);
            pick.Click += delegate { PickOnScreen(); };
            c1.Controls.Add(pick);

            _btnDrag = new FlatBtn();
            _btnDrag.Text = "拖动定位";
            _btnDrag.Bounds = new Rectangle(122, 110, 100, 30);
            _btnDrag.Click += delegate { ToggleDrag(); };
            c1.Controls.Add(_btnDrag);

            FlatBtn center = new FlatBtn();
            center.Text = "回到中心";
            center.Bounds = new Rectangle(230, 110, 100, 30);
            center.Click += delegate { BackToCenter(); };
            c1.Controls.Add(center);

            Ui.L(c1, "1 像素微调", Theme.Body, Theme.TextMuted, 14, 156, 74, 20);
            string[] arrows = { "←", "→", "↑", "↓" };
            int[] dx = { -1, 1, 0, 0 };
            int[] dy = { 0, 0, -1, 1 };
            for (int i = 0; i < 4; i++)
            {
                FlatBtn b = new FlatBtn();
                b.Text = arrows[i];
                b.Bounds = new Rectangle(92 + i * 38, 152, 34, 28);
                b.Tag = new Point(dx[i], dy[i]);
                b.Click += NudgeClick;
                c1.Controls.Add(b);
            }
            // 原来这里写的是「滚轮也能拖准星」，其实覆盖层只接了拖拽（WM_NCHITTEST 返回
            // HTCAPTION），滚轮没有任何处理，说法不对，换成按一下动一像素这句实话
            Label nudgeHint = Ui.L(c1, "按一下动 1 像素，立刻生效", Theme.Small,
                Theme.TextFaint, 250, 152, 190, 28);
            nudgeHint.TextAlign = ContentAlignment.MiddleLeft;

            // ---- 目标屏幕 ----
            // 原来这是单独一张卡片，就为了一个下拉框；并页之后页面已经够长了，
            // 而「在哪块屏」本来就属于位置，直接接在微调下面，中间拉一道分隔线
            Panel sep = new Panel();
            sep.Bounds = new Rectangle(14, 192, 426, 1);
            sep.BackColor = Theme.Border;
            c1.Controls.Add(sep);

            Ui.L(c1, "目标屏幕", Theme.Body, Theme.TextMuted, 14, 206, 60, 20);
            _cbScreen = Ui.Combo(c1, 78, 203, 260);
            _cbScreen.SelectedIndexChanged += UiChanged;
            FillScreens();
            Ui.L(c1, "选「跟随鼠标所在屏」时，准星会跟着你正在用的那块屏走",
                Theme.Small, Theme.TextFaint, 14, 232, 426, 20);

            // ---- 炸点标定流程 ----
            Card c3 = NewCard(pg, "炸点标定流程", 892, 148);
            _pgCards.Add(c3);
            Ui.L(c3, "1. 固定站位，用要标的武器打一发，记住爆点在屏幕上的位置。",
                Theme.Body, Theme.TextMuted, 14, 42, 424, 20);
            Ui.L(c3, "2. 回到这里点「屏幕取点」，画面会冻结成截图，在爆点上左键点一下。",
                Theme.Body, Theme.TextMuted, 14, 66, 424, 20);
            Ui.L(c3, "3. 用上面的 1 像素微调对齐，然后去「预设」页存成这张图的方案。",
                Theme.Body, Theme.TextMuted, 14, 90, 424, 20);
            Ui.L(c3, "4. 要标一串距离就把形状换成「分划板」，量出每格多少米后填进去。",
                Theme.Body, Theme.TextMuted, 14, 114, 424, 20);

            // ---- FOV 坐标换算 ----
            Card c4 = NewCard(pg, "FOV 坐标换算（水平 FOV）", 1052, 230);
            _pgCards.Add(c4);
            Ui.L(c4, "基准 FOV", Theme.Body, Theme.TextMuted, 14, 42, 62, 20);
            _tbFovBase = Ui.NumBox(c4, 78, 39, 64);
            Ui.L(c4, "°", Theme.Body, Theme.TextFaint, 146, 42, 18, 20);
            Ui.L(c4, "目标 FOV", Theme.Body, Theme.TextMuted, 178, 42, 62, 20);
            _tbFovTarget = Ui.NumBox(c4, 242, 39, 64);
            Ui.L(c4, "°", Theme.Body, Theme.TextFaint, 310, 42, 18, 20);

            FlatBtn record = new FlatBtn();
            record.Text = "记录当前点为基准";
            record.Bounds = new Rectangle(14, 78, 136, 28);
            record.Click += delegate { RecordFovReference(); };
            c4.Controls.Add(record);

            _lblFovBasePoint = Ui.L(c4, "未记录基准点", Theme.Small, Theme.TextFaint,
                170, 78, 270, 28);
            _lblFovBasePoint.TextAlign = ContentAlignment.MiddleLeft;

            FlatBtn convert = new FlatBtn();
            convert.Kind = 1;
            convert.Text = "计算并应用目标点";
            convert.Bounds = new Rectangle(14, 120, 136, 30);
            convert.Click += delegate { ConvertFovPoint(); };
            c4.Controls.Add(convert);

            _lblFovResult = Ui.L(c4, "先在基准 FOV 下记录一个点",
                Theme.Small, Theme.TextMuted, 170, 120, 270, 32);
            _lblFovResult.TextAlign = ContentAlignment.MiddleLeft;

            FlatBtn keep = new FlatBtn();
            keep.Text = "新建原本准星";
            keep.Bounds = new Rectangle(14, 162, 136, 28);
            keep.Click += delegate { CreateOriginalCrosshair(); };
            c4.Controls.Add(keep);

            Label keepHint = Ui.L(c4, "校准后新建一个准星，位置为原本准星",
                Theme.Small, Theme.TextFaint, 170, 162, 270, 28);
            keepHint.TextAlign = ContentAlignment.MiddleLeft;

            Ui.L(c4, "同宽高比有效，分辨率会按基准记录自动换算；开镜倍率需另设基准。",
                Theme.Small, Theme.TextFaint, 14, 202, 426, 20);

            // FOV 换算是标定流程的前置步骤，放在流程卡片上方更符合使用顺序。
            _pgCards.Remove(c4);
            _pgCards.Insert(_pgCards.IndexOf(c3), c4);
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

        private Point CurrentPoint(CrosshairItemSettings it)
        {
            Rectangle b = ScreenOf(it).Bounds;
            if (it.Centered) return new Point(b.Width / 2, b.Height / 2);
            return new Point(it.X, it.Y);
        }

        private void RecordFovReference()
        {
            CrosshairItemSettings it = Cur();
            if (it == null) return;

            double fov;
            if (!TryFov(_tbFovBase, out fov))
            {
                Toast("基准 FOV 要填 1 到 179 之间的数字");
                return;
            }

            Point p = CurrentPoint(it);
            it.FovReferenceSet = true;
            it.FovReference = fov;
            it.FovReferenceX = p.X;
            it.FovReferenceY = p.Y;
            Rectangle screenBounds = ScreenOf(it).Bounds;
            it.FovReferenceWidth = screenBounds.Width;
            it.FovReferenceHeight = screenBounds.Height;
            SyncFovUi(it);
            _lblFovResult.Text = "基准点已记录，可以切换游戏 FOV 后换算";
            _lblFovResult.ForeColor = Theme.Green;
            _cfg.Save();
            Toast("已记录基准点 X " + p.X + "   Y " + p.Y);
        }

        private void ConvertFovPoint()
        {
            CrosshairItemSettings it = Cur();
            if (it == null) return;

            double target;
            if (!TryFov(_tbFovTarget, out target))
            {
                Toast("目标 FOV 要填 1 到 179 之间的数字");
                return;
            }
            if (!it.FovReferenceSet)
            {
                Toast("请先记录基准点");
                return;
            }

            Rectangle b = ScreenOf(it).Bounds;
            Point targetPoint = ConvertFovCoordinates(it, b.Size, target);
            it.Centered = false;
            it.X = targetPoint.X;
            it.Y = targetPoint.Y;
            AfterEdit();
            _lblFovResult.Text = "已应用目标点  X " + it.X + "   Y " + it.Y;
            _lblFovResult.ForeColor = Theme.Green;
            Toast("FOV " + target.ToString("0.##", CultureInfo.InvariantCulture) +
                "° 坐标已应用");
        }

        internal static Point ConvertFovCoordinates(CrosshairItemSettings it, Size size, double target)
        {
            // 水平 FOV 下，像素偏移还要随基准屏幕宽度换算；旧配置没有记录
            // 基准分辨率时退回当前宽度，保持与旧版行为兼容。
            int refWidth = it.FovReferenceWidth > 0 ? it.FovReferenceWidth : size.Width;
            int refHeight = it.FovReferenceHeight > 0 ? it.FovReferenceHeight : size.Height;
            double ratio = ((double)size.Width / refWidth) *
                Math.Tan(it.FovReference * Math.PI / 360.0) /
                Math.Tan(target * Math.PI / 360.0);
            int x = ClampPixel((int)Math.Round(size.Width / 2 + (it.FovReferenceX - refWidth / 2) * ratio));
            int y = ClampPixel((int)Math.Round(size.Height / 2 + (it.FovReferenceY - refHeight / 2) * ratio));
            return new Point(x, y);
        }

        private void CreateOriginalCrosshair()
        {
            CrosshairItemSettings it = Cur();
            if (it == null) return;
            if (!it.FovReferenceSet)
            {
                Toast("请先记录基准点并完成 FOV 校准");
                return;
            }
            if (_cfg.Items.Count >= 16)
            {
                Toast("一套预设最多 16 个准星");
                return;
            }

            int index = _cfg.SelectedItem;
            CrosshairItemSettings copy = it.Clone();
            copy.Name = it.Name + " · 原本位置";
            copy.Centered = false;
            Point original = ConvertFovCoordinates(it, ScreenOf(it).Bounds.Size, it.FovReference);
            copy.X = original.X;
            copy.Y = original.Y;
            copy.FovReferenceSet = false;
            copy.FovReference = 90.0;
            copy.FovReferenceX = 0;
            copy.FovReferenceY = 0;
            copy.FovReferenceWidth = 0;
            copy.FovReferenceHeight = 0;
            _cfg.Items.Insert(index + 1, copy);
            _cfg.SelectedItem = index;
            RebuildOverlays();
            PushToUi();
            _cfg.Save();
            _lblFovResult.Text = "已新建原本位置准星";
            _lblFovResult.ForeColor = Theme.Green;
            Toast("已新建原本位置准星");
        }

        private static int ClampPixel(int n)
        {
            if (n < 0) return 0;
            if (n > 32767) return 32767;
            return n;
        }

        private void SyncFovUi(CrosshairItemSettings it)
        {
            if (_tbFovBase == null || it == null) return;
            _tbFovBase.Text = it.FovReference.ToString("0.###", CultureInfo.InvariantCulture);
            if (it.FovReferenceSet)
            {
                string size = it.FovReferenceWidth > 0
                    ? "   " + it.FovReferenceWidth + "×" + it.FovReferenceHeight
                    : "";
                _lblFovBasePoint.Text = "基准点  X " + it.FovReferenceX + "   Y " + it.FovReferenceY + size;
                _lblFovBasePoint.ForeColor = Theme.TextMuted;
            }
            else
            {
                _lblFovBasePoint.Text = "未记录基准点";
                _lblFovBasePoint.ForeColor = Theme.TextFaint;
            }
        }
    }
}
