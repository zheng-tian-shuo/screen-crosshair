using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ScreenCrosshair
{
    /// <summary>设置主窗口。无边框 + 自绘标题栏 + 左侧导航 + 右侧常驻预览。</summary>
    public partial class MainForm : Form
    {
        private AppSettings _cfg;
        private readonly List<OverlayForm> _ovl = new List<OverlayForm>();
        private bool _loading;      // 界面回填中，忽略控件事件
        private bool _dragMode;     // 拖动定位状态
        private bool _reallyExit;
        private Timer _pickTimer;
        private int _page;

        // 「位置」原来是独立一页，可它调的还是当前准星，和「准星」页一直分不清，
        // 现在整块并到「准星」页下半部分了
        private static readonly string[] TabNames = { "准星", "倒计时", "预设", "热键", "灵魂出窍" };

        // 页序号统一走这几个常量，别再往代码里散落 0/1/2——之前删一页就得满地改数字
        private const int PageCrosshair = 0;
        private const int PageCountdown = 1;
        private const int PageProfiles = 2;
        private const int PageHotkeys = 3;
        private const int PageWeakNetwork = 4;
        private const int PageCount = 5;

        private const int WinW = 728;
        private const int WinH = 660;
        private const int BarH = 56;
        private const int SideW = 168;

        private FlatBtn _pillGlobal;
        private SideTab[] _tabs;
        private Label _lblSideHint;
        private Panel _host;
        private Panel[] _pages;
        private int _pageW;

        private CountdownOverlayForm _countdownOverlay;
        private CountdownOverlayForm _clockOverlay;
        private WeakStatusOverlayForm _weakIndicator;
        private readonly double[] _countdownEnds = new double[3];
        private readonly bool[] _countdownRunning = new bool[3];
        private Screen _countdownScreen;
        private Screen _clockScreen;

        public MainForm()
        {
            _cfg = AppSettings.Load();
            Theme.SetLight(_cfg.LightTheme);
            InitWindow();
            BuildTitleBar();
            BuildSide();
            BuildHost();
            BuildPageCrosshair();
            BuildPositionCards();     // 接着往「准星」页下面摆位置相关的卡片
            BuildPageCountdown();
            BuildPageProfiles();
            BuildPageHotkeys();
            BuildPageWeakNetwork();
            BuildTray();

            ApplyScale();
            RebuildOverlays();
            PushToUi();
            SelectPage(0);
            RegisterHotkeys();
            RecoverWeakNetworkOnStart();
            StartTick();
        }

        private void InitWindow()
        {
            Text = AppInfo.AppName + " " + AppInfo.Version;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(WinW, WinH);
            MinimumSize = Size;
            MaximumSize = Size;
            BackColor = Theme.Window;
            ForeColor = Theme.Text;
            Font = Theme.Body;
            DoubleBuffered = true;
            KeyPreview = true;
            try { Icon = Brand.AppIcon; }
            catch { }
            ApplyRegion();

            if (_cfg.WindowX != -32768 && _cfg.WindowY != -32768)
            {
                Rectangle want = new Rectangle(_cfg.WindowX, _cfg.WindowY, Width, Height);
                for (int i = 0; i < Screen.AllScreens.Length; i++)
                {
                    if (!Screen.AllScreens[i].WorkingArea.IntersectsWith(want)) continue;
                    StartPosition = FormStartPosition.Manual;
                    Location = want.Location;
                    break;
                }
            }
        }

        protected override void OnLocationChanged(EventArgs e)
        {
            base.OnLocationChanged(e);
            if (_cfg == null || WindowState != FormWindowState.Normal || !Visible) return;
            _cfg.WindowX = Left;
            _cfg.WindowY = Top;
            SaveSoon();
        }

        private void ApplyRegion()
        {
            try
            {
                using (GraphicsPath p = Ui.Round(new Rectangle(0, 0, Width, Height), Theme.S(9)))
                    Region = new Region(p);
            }
            catch { }
        }

        protected override void OnShown(EventArgs e)
        {
            EnsureWindowVisible();
            base.OnShown(e);
        }

        private void EnsureWindowVisible()
        {
            Location = Ui.KeepWindowVisible(Bounds, Screen.FromRectangle(Bounds).WorkingArea);
        }

        /// <summary>
        /// 布局坐标全是按 96 DPI 写死的，而字体是 pt 单位、会随系统缩放自动变大。
        /// 所以高 DPI 屏上要把整套控件按同一系数放大一遍，不然文字会撑破控件。
        /// </summary>
        private void ApplyScale()
        {
            float k = Theme.K;
            if (k <= 1.001f) return;

            MinimumSize = Size.Empty;
            MaximumSize = Size.Empty;
            Scale(new SizeF(k, k));
            ClientSize = new Size(Theme.S(WinW), Theme.S(WinH));
            MinimumSize = Size;
            MaximumSize = Size;

            ApplyRegion();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Ui.DrawRound(e.Graphics, new Rectangle(0, 0, Width - 1, Height - 1), 9, Theme.Border, 1f);
        }

        // ---------------- 标题栏 ----------------
        private void BuildTitleBar()
        {
            Panel bar = new Panel();
            bar.Bounds = new Rectangle(0, 0, WinW, BarH);
            bar.BackColor = Theme.Window;
            Controls.Add(bar);
            bar.MouseDown += BarDrag;

            Panel logo = new Panel();
            logo.Bounds = new Rectangle(18, 9, 38, 38);
            logo.BackColor = Color.Transparent;
            logo.Paint += LogoPaint;
            logo.MouseDown += BarDrag;
            bar.Controls.Add(logo);

            Ui.L(bar, AppInfo.AppName, Theme.Title, Theme.Text, 64, 9, 140, 24).MouseDown += BarDrag;
            Ui.L(bar, "屏幕准星与计时工具", Theme.Sub, Theme.TextFaint,
                66, 33, 230, 16).MouseDown += BarDrag;

            _pillGlobal = new FlatBtn();
            _themeDark = new FlatBtn();
            _themeDark.Text = "深色";
            _themeDark.Bounds = new Rectangle(WinW - 396, 14, 52, 26);
            _themeDark.Click += delegate { SwitchTheme(false); };
            bar.Controls.Add(_themeDark);
            _themeLight = new FlatBtn();
            _themeLight.Text = "浅色";
            _themeLight.Bounds = new Rectangle(WinW - 344, 14, 52, 26);
            _themeLight.Click += delegate { SwitchTheme(true); };
            bar.Controls.Add(_themeLight);
            UpdateThemeButtons();
            _pillGlobal.Bounds = new Rectangle(WinW - 280, 14, 168, 26);
            _pillGlobal.Click += delegate { ToggleGlobal(); };
            bar.Controls.Add(_pillGlobal);

            FlatBtn min = new FlatBtn();
            min.Kind = 3;
            min.Text = "—";
            min.Bounds = new Rectangle(WinW - 98, 14, 34, 26);
            min.Click += delegate { HideToTray(); };
            bar.Controls.Add(min);

            FlatBtn cls = new FlatBtn();
            cls.Kind = 3;
            cls.IsDangerClose = true;
            cls.Text = "✕";
            cls.Bounds = new Rectangle(WinW - 56, 14, 34, 26);
            cls.Click += delegate { HideToTray(); };
            bar.Controls.Add(cls);

            Panel line = new Panel();
            line.Bounds = new Rectangle(0, BarH - 1, WinW, 1);
            line.BackColor = Theme.Border;
            bar.Controls.Add(line);
        }

        private void BarDrag(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            Native.ReleaseCapture();
            Native.SendMessage(Handle, Native.WM_NCLBUTTONDOWN,
                (IntPtr)Native.HTCAPTION, IntPtr.Zero);
        }

        private void LogoPaint(object sender, PaintEventArgs e)
        {
            Control c = sender as Control;
            if (c == null) return;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Brand.Draw(e.Graphics, new Rectangle(0, 0, c.Width, c.Height));
        }
    }
}
