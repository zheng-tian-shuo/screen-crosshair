using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace ScreenCrosshair
{
    public partial class MainForm
    {
        private static void CheckFov(bool ok, string message)
        {
            if (!ok) throw new Exception(message);
            Console.WriteLine("PASS " + message);
        }

        private Point FovPoint() { return new Point(Cur().X, Cur().Y); }

        private void ConfigureFov(string fov, string aspect, int width, int height,
            ProjectionDisplayMode mode, int x, int y)
        {
            _tbFovTarget.Text = fov;
            _tbTargetAspect.Text = aspect;
            _tbTargetWidth.Text = width.ToString();
            _tbTargetHeight.Text = height.ToString();
            SetProjectionMode(mode);
            _tbWindowX.Text = x.ToString();
            _tbWindowY.Text = y.ToString();
        }

        private static void CheckProjectionMath()
        {
            Point p;
            Size reference = new Size(1920, 1080);
            Point template = new Point(953, 975);
            Rectangle full = new Rectangle(Point.Empty, reference);
            CheckFov(ProjectionMath.TryConvertHorizontalFov(template, reference, 90, full,
                90, 16.0 / 9.0, out p) && p == template, "reference projection is unchanged");
            CheckFov(!ProjectionMath.TryConvertHorizontalFov(template, reference, 90, full,
                60, 16.0 / 9.0, out p), "low FOV rejects off-screen projection");
            CheckFov(!ProjectionMath.TryConvertHorizontalFov(template, reference, 90, full,
                90, 21.0 / 9.0, out p), "ultrawide aspect rejects off-screen projection");
            foreach (double invalid in new double[] { double.NaN, double.PositiveInfinity, 1, 179 })
                CheckFov(!ProjectionMath.TryConvertHorizontalFov(template, reference, 90, full,
                    invalid, 16.0 / 9.0, out p) &&
                    !ProjectionMath.TryConvertHorizontalFov(template, reference, invalid, full,
                    90, 16.0 / 9.0, out p), "invalid reference/target FOV rejected: " + invalid);

            Rectangle viewport;
            CheckFov(ProjectionMath.TryGetViewport(new Size(2560, 1600), reference, 16.0 / 9.0,
                ProjectionDisplayMode.Fit, Point.Empty, out viewport) &&
                viewport == new Rectangle(0, 80, 2560, 1440) &&
                ProjectionMath.TryConvertHorizontalFov(template, reference, 90, viewport, 90,
                    16.0 / 9.0, out p) && p == new Point(1271, 1380),
                "letterbox mapping includes the 80-pixel top bar");
            CheckFov(ProjectionMath.TryGetViewport(reference, new Size(1440, 1080), 4.0 / 3.0,
                ProjectionDisplayMode.Fit, Point.Empty, out viewport) &&
                viewport == new Rectangle(240, 0, 1440, 1080) &&
                ProjectionMath.TryConvertHorizontalFov(template, reference, 90, viewport, 90,
                    4.0 / 3.0, out p) && p == new Point(955, 866),
                "pillarbox mapping includes the 240-pixel left bar");
            CheckFov(ProjectionMath.TryGetViewport(reference, new Size(1280, 720), 16.0 / 9.0,
                ProjectionDisplayMode.Window, new Point(320, 180), out viewport) &&
                ProjectionMath.TryConvertHorizontalFov(template, reference, 90, viewport, 90,
                    16.0 / 9.0, out p) && p == new Point(955, 830),
                "window mapping uses actual game image size and origin");
            CheckFov(!ProjectionMath.TryGetViewport(reference, new Size(1280, 720), 16.0 / 9.0,
                ProjectionDisplayMode.Window, new Point(1000, 0), out viewport),
                "window extending beyond the selected screen is rejected");
            CheckFov(ProjectionMath.TryConvertHorizontalFov(template, reference, 90,
                new Rectangle(0, 0, 3840, 2160), 90, 16.0 / 9.0, out p) &&
                p == new Point(1906, 1950), "final screen coordinates round only once");
        }

        private void CheckFovFlow(string output)
        {
            Cur().ScreenName = Screen.PrimaryScreen.DeviceName;
            PushToUi();
            ConfigureFov("90", "16:9", 1920, 1080, ProjectionDisplayMode.Stretch, 0, 0);
            GenerateTemplatePoint();
            Size desktop = ScreenOf(Cur()).Bounds.Size;
            CheckFov(_lblFovResult.ForeColor == Theme.Green && FovPoint() == new Point(
                (int)Math.Round(953.0 * desktop.Width / 1920),
                (int)Math.Round(975.0 * desktop.Height / 1080)), "button applies full-screen mapping");
            Point original = FovPoint();
            _tbFovTarget.Text = "60";
            CheckFov(_lblFovResult.ForeColor != Theme.Green, "editing a parameter clears stale success");
            GenerateTemplatePoint();
            CheckFov(FovPoint() == original && _lblFovResult.ForeColor == Theme.Danger,
                "out-of-view generation leaves existing coordinates unchanged");
            BackToCenter();
            GenerateTemplatePoint();
            CheckFov(Cur().Centered, "out-of-view generation also preserves centered mode");
            _tbFovTarget.Text = "90";
            GenerateTemplatePoint();
            original = FovPoint();
            _tbFovTarget.Text = "abc";
            GenerateTemplatePoint();
            CheckFov(FovPoint() == original && _lblFovResult.ForeColor == Theme.Danger &&
                !_lblFovResult.Text.StartsWith("已生成"), "invalid input replaces the old success result");
            _tbFovTarget.Text = "100";
            _tbTargetAspect.Text = "garbage";
            GenerateTemplatePoint();
            CheckFov(FovPoint() == original && _lblFovResult.ForeColor == Theme.Danger,
                "invalid aspect leaves coordinates unchanged");
            _tbTargetAspect.Text = "4:3";
            _tbTargetHeight.Text = "0";
            GenerateTemplatePoint();
            CheckFov(FovPoint() == original && _lblFovResult.ForeColor == Theme.Danger,
                "invalid resolution leaves coordinates unchanged");

            ConfigureFov("100", "4:3", 640, 360, ProjectionDisplayMode.Window, 37, 53);
            GenerateTemplatePoint();
            CheckFov(_lblFovResult.ForeColor == Theme.Green, "valid custom window can be generated");
            Point savedPoint = FovPoint();
            _cfg.SaveToDisk();
            AppSettings saved = AppSettings.Load();
            CheckFov(saved.Items[0].TargetFov == 100 && saved.Items[0].TargetAspect == "4:3" &&
                saved.Items[0].TargetWidth == 640 && saved.Items[0].TargetHeight == 360 &&
                saved.Items[0].DisplayMode == ProjectionDisplayMode.Window &&
                saved.Items[0].WindowOriginX == 37 && saved.Items[0].WindowOriginY == 53,
                "all generation parameters survive writing and reloading settings");
            using (MainForm reopened = new MainForm())
            {
                reopened._cfg = saved;
                reopened.PushToUi();
                CheckFov(reopened._tbFovTarget.Text == "100" && reopened._tbTargetAspect.Text == "4:3" &&
                    reopened._tbTargetWidth.Text == "640" && reopened._tbTargetHeight.Text == "360" &&
                    reopened.SelectedProjectionMode() == ProjectionDisplayMode.Window && reopened._tbWindowX.Text == "37" &&
                    reopened._tbWindowY.Text == "53", "restart restores every generation input");
                reopened.GenerateTemplatePoint();
                CheckFov(reopened.FovPoint() == savedPoint, "regeneration after restart keeps the same point");
                reopened.DisposeFovFixture();
            }
            CrosshairItemSettings first = Cur();
            CrosshairItemSettings clone = first.Clone();
            CheckFov(clone.TargetFov == 100 && clone.DisplayMode == ProjectionDisplayMode.Window &&
                clone.WindowOriginX == 37, "duplicating a crosshair retains its generation parameters");
            AddItem();
            CheckFov(_tbFovTarget.Text == "90" && _tbTargetAspect.Text == "16:9" &&
                _lblFovResult.ForeColor != Theme.Green, "new crosshair has its own inputs and no stale result");
            _tbFovTarget.Text = "110";
            _cbItem.SelectedIndex = 0;
            CheckFov(_tbFovTarget.Text == "100" && _tbTargetAspect.Text == "4:3" &&
                SelectedProjectionMode() == ProjectionDisplayMode.Window, "switching crosshairs restores each one's parameters");
            _tbFovTarget.Text = "101";
            CheckFov(first.TargetFov == 101, "valid edits are saved before clicking generate");
            _tbFovTarget.Text = "100";
            GenerateTemplatePoint();
            _cbItem.SelectedIndex = 1;
            CheckFov(_tbFovTarget.Text == "110" && _lblFovResult.ForeColor != Theme.Green,
                "switching away from a successful generation clears its status");
            _cbItem.SelectedIndex = 0;
            GenerateTemplatePoint();
            _tbWindowX.Text = "32767";
            GenerateTemplatePoint();
            CheckFov(FovPoint() == savedPoint && _lblFovResult.ForeColor == Theme.Danger,
                "invalid window region preserves the existing point");
            _tbWindowX.Text = "37";
            _tbWindowY.Text = "invalid";
            GenerateTemplatePoint();
            CheckFov(FovPoint() == savedPoint && _lblFovResult.ForeColor == Theme.Danger,
                "invalid window origin preserves the existing point");
            _tbWindowY.Text = "53";

            Opacity = 0;
            ShowInTaskbar = false;
            Show();
            foreach (bool light in new bool[] { false, true })
            {
                SwitchTheme(light);
                foreach (ProjectionDisplayMode mode in new ProjectionDisplayMode[] {
                    ProjectionDisplayMode.Stretch, ProjectionDisplayMode.Fit, ProjectionDisplayMode.Window, ProjectionDisplayMode.WindowFit })
                {
                    SetProjectionMode(mode);
                    GenerateTemplatePoint();
                    Control card = _lblFovResult.Parent;
                    _pages[PageCrosshair].ScrollControlIntoView(card);
                    Application.DoEvents();
                    using (Bitmap bitmap = new Bitmap(card.Width, card.Height))
                    {
                        card.DrawToBitmap(bitmap, card.ClientRectangle);
                        bitmap.Save(Path.Combine(output, (light ? "light-" : "dark-") + mode + ".png"), ImageFormat.Png);
                    }
                }
            }
            Console.WriteLine("PASS rendered all four display modes in both themes at scale " + Theme.K);
            _cfg.ReadFrom(new string[] { "[app]", "ProfileCount=1", "[profile0]", "Count=1",
                "0.TargetFov=NaN", "0.TargetAspect=Infinity", "0.TargetWidth=1", "0.TargetHeight=1080" });
            CheckFov(Cur().TargetFov == 90 && Cur().TargetAspect == "16:9" && Cur().TargetWidth == 0,
                "invalid saved generation inputs fall back to safe defaults");
        }

        private void CheckAdaptivePosition()
        {
            Rectangle viewport;
            Point p;
            CheckFov(ProjectionMath.TryGetViewport(new Size(1920, 1080), new Size(1280, 800),
                16.0 / 9.0, ProjectionDisplayMode.WindowFit, new Point(91, 105), out viewport) &&
                viewport == new Rectangle(91, 145, 1280, 720) &&
                ProjectionMath.TryConvertHorizontalFov(new Point(953, 975), new Size(1920, 1080),
                    90, viewport, 90, 16.0 / 9.0, out p) && p == new Point(726, 795),
                "window letterbox projection includes client origin and both top/bottom bars");
            CheckFov(ProjectionMath.TryGetViewport(new Size(1920, 1080), new Size(1280, 720),
                4.0 / 3.0, ProjectionDisplayMode.WindowFit, new Point(91, 105), out viewport) &&
                viewport == new Rectangle(251, 105, 960, 720),
                "window pillarbox projection stays inside captured client area");
            foreach (ProjectionDisplayMode mode in new ProjectionDisplayMode[] {
                ProjectionDisplayMode.Stretch, ProjectionDisplayMode.Fit })
            {
                CrosshairItemSettings item = new CrosshairItemSettings();
                item.Centered = false;
                item.AutoScreenPosition = true;
                item.AppliedDisplayMode = mode;
                item.RefreshGeneratedPosition(new Size(2560, 1600));
                Point desktop = new Point(item.X, item.Y);
                CheckFov(desktop == new Point(1271, mode == ProjectionDisplayMode.Fit ? 1380 : 1444),
                    mode + " starts at the expected desktop point");
                using (OverlayForm overlay = new OverlayForm(item))
                {
                    item.RefreshGeneratedPosition(new Size(1920, 1080));
                    overlay.RefreshPosition(new Rectangle(-1920, 80, 1920, 1080));
                    CheckFov(new Point(item.X, item.Y) == new Point(953, 975) &&
                        overlay.Left + overlay.Width / 2 == -967 &&
                        overlay.Top + overlay.Height / 2 == 1055,
                        mode + " output change maps to the new monitor geometry");
                    overlay.Location = new Point(0, 0);
                    CheckFov(item.AutoScreenPosition && item.X == 953 && item.Y == 975,
                        "OS relocation cannot overwrite the generated point");
                    overlay.SetMoveMode(true);
                    overlay.Location = new Point(5, 5);
                    CheckFov(item.AutoScreenPosition, "enabling drag alone does not mistake OS movement for user input");
                }
                for (int i = 0; i < 3; i++)
                {
                    item.RefreshGeneratedPosition(new Size(2560, 1600));
                    CheckFov(new Point(item.X, item.Y) == desktop, mode + " returns without rounding drift");
                    item.RefreshGeneratedPosition(new Size(1920, 1080));
                }
            }

            _cfg = new AppSettings();
            _cfg.GlobalVisible = false;
            RebuildOverlays();
            PushToUi();
            ConfigureFov("90", "16:9", 1920, 1080, ProjectionDisplayMode.Stretch, 0, 0);
            GenerateTemplatePoint();
            CrosshairItemSettings current = Cur();
            CheckFov(current.AutoScreenPosition, "generation enables resolution tracking for borderless/fullscreen");
            _tbFovTarget.Text = "60";
            GenerateTemplatePoint();
            CheckFov(current.AutoScreenPosition && current.AppliedFov == 90 && current.TargetFov == 60,
                "failed generation preserves the applied snapshot separately from draft inputs");
            _cfg.SaveToDisk();
            CrosshairItemSettings restored = AppSettings.Load().Items[0];
            restored.RefreshGeneratedPosition(new Size(1920, 1080));
            CheckFov(restored.AutoScreenPosition && restored.X == 953 && restored.Y == 975 && restored.TargetFov == 60,
                "restart restores last successful projection rather than pending draft parameters");
            foreach (string screenName in new string[] { "", Screen.PrimaryScreen.DeviceName })
            {
                current.ScreenName = screenName;
                current.X = -100;
                current.Y = -100;
                RefreshScreenPositions();
                CheckFov(current.X >= 0 && current.Y >= 0 && _tbFovTarget.Text == "60" && !_ovl[0].Visible,
                    "hidden fixed/follow-screen items refresh without changing draft text");
            }
            PushToUi();
            CommitXy();
            CheckFov(current.AutoScreenPosition, "leaving untouched XY boxes keeps automatic positioning");
            _sSize.Value = Math.Min(100, current.Size + 1);
            UiChanged(_sSize, EventArgs.Empty);
            CheckFov(current.AutoScreenPosition, "appearance changes retain automatic positioning");
            _tbX.Text = "123";
            CommitXy();
            CheckFov(!current.AutoScreenPosition && current.X == 123, "manual XY input disables automatic positioning");
            _tbFovTarget.Text = "90";
            GenerateTemplatePoint();
            using (Button nudge = new Button())
            {
                nudge.Tag = new Point(1, 0);
                NudgeClick(nudge, EventArgs.Empty);
            }
            CheckFov(!current.AutoScreenPosition, "nudge disables automatic positioning");
            GenerateTemplatePoint();
            BackToCenter();
            CheckFov(!current.AutoScreenPosition && current.Centered, "centering disables automatic positioning");
            ConfigureFov("90", "16:9", 640, 400, ProjectionDisplayMode.Window, 10, 20);
            _cbDisplayMode.SelectedIndex = 1;
            GenerateTemplatePoint();
            CheckFov(SelectedProjectionMode() == ProjectionDisplayMode.WindowFit &&
                !current.AutoScreenPosition && current.X < 650 && current.Y < 420,
                "changing window scaling fits within the window and disables screen tracking");
            _cfg.SaveToDisk();
            CheckFov(AppSettings.Load().Items[0].DisplayMode == ProjectionDisplayMode.WindowFit,
                "window plus black bars survives settings reload");
        }

        private void DisposeFovFixture()
        {
            foreach (OverlayForm overlay in _ovl) overlay.Dispose();
            _ovl.Clear();
            if (_toastTimer != null) _toastTimer.Dispose();
        }

        public static void RunFovRegression(string output)
        {
            typeof(AppSettings).GetField("_path", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.ini"));
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) Theme.K = Math.Max(1f, g.DpiX / 96f);
            CheckProjectionMath();
            using (MainForm form = new MainForm())
            {
                try { form.CheckFovFlow(output); form.CheckAdaptivePosition(); form.CheckWindowGrabFlow(); }
                finally { form.DisposeFovFixture(); }
            }
        }
    }
}

public class FovRegressionEntry
{
    [STAThread] public static void Main(string[] args)
    {
        if (args[0] == "--window-fixture") ScreenCrosshair.WindowGrabFixture.Run(args[1]);
        else ScreenCrosshair.MainForm.RunFovRegression(args[0]);
    }
}
