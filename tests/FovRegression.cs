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
            _cbDisplayMode.SelectedIndex = (int)mode;
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
                    reopened._cbDisplayMode.SelectedIndex == 2 && reopened._tbWindowX.Text == "37" &&
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
                _cbDisplayMode.SelectedIndex == 2, "switching crosshairs restores each one's parameters");
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
                    ProjectionDisplayMode.Stretch, ProjectionDisplayMode.Fit, ProjectionDisplayMode.Window })
                {
                    _cbDisplayMode.SelectedIndex = (int)mode;
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
            Console.WriteLine("PASS rendered all three display modes in both themes at scale " + Theme.K);
            _cfg.ReadFrom(new string[] { "[app]", "ProfileCount=1", "[profile0]", "Count=1",
                "0.TargetFov=NaN", "0.TargetAspect=Infinity", "0.TargetWidth=1", "0.TargetHeight=1080" });
            CheckFov(Cur().TargetFov == 90 && Cur().TargetAspect == "16:9" && Cur().TargetWidth == 0,
                "invalid saved generation inputs fall back to safe defaults");
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
                try { form.CheckFovFlow(output); form.CheckWindowGrabFlow(); }
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
