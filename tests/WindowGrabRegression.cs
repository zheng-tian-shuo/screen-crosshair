using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace ScreenCrosshair
{
    public partial class MainForm
    {
        private void CheckWindowGrabFlow()
        {
            _cfg.GlobalVisible = false;
            PushToUi();
            Rectangle relative;
            CheckFov(WindowGeometry.TryRelativeArea(new Rectangle(-1820, 140, 800, 600),
                new Rectangle(-1920, 0, 1920, 1080), out relative) &&
                relative == new Rectangle(100, 140, 800, 600),
                "captured client origin is relative to a monitor with negative desktop coordinates");
            CheckFov(!WindowGeometry.TryRelativeArea(new Rectangle(1800, 100, 800, 600),
                new Rectangle(0, 0, 1920, 1080), out relative), "cross-monitor client region is rejected");
            CheckFov(!WindowGeometry.TryGetClientArea(IntPtr.Zero, out relative),
                "missing window handle cannot become a captured region");

            string file = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "window-fixture.txt");
            ProcessStartInfo info = new ProcessStartInfo(Application.ExecutablePath,
                "--window-fixture \"" + file + "\"");
            info.UseShellExecute = false;
            info.CreateNoWindow = true;
            info.WindowStyle = ProcessWindowStyle.Hidden;
            using (Process helper = Process.Start(info))
            {
                try
                {
                    DateTime deadline = DateTime.UtcNow.AddSeconds(10);
                    while (!File.Exists(file) && !helper.HasExited && DateTime.UtcNow < deadline)
                        Thread.Sleep(50);
                    CheckFov(File.Exists(file), "separate hidden window fixture is ready");
                    string[] data = File.ReadAllLines(file);
                    IntPtr window = new IntPtr(long.Parse(data[0]));
                    Rectangle expected = new Rectangle(int.Parse(data[1]), int.Parse(data[2]),
                        int.Parse(data[3]), int.Parse(data[4]));
                    Rectangle actual;
                    CheckFov(WindowGeometry.TryGetClientArea(window, out actual) && actual == expected &&
                        actual.Size == new Size(640, 360) && actual.Top > int.Parse(data[5]),
                        "native window capture measures client pixels and excludes the title bar");
                    Screen screen = Screen.FromRectangle(expected);
                    _tbFovTarget.Text = "103";
                    _tbTargetAspect.Text = "4:3";
                    Point previous = FovPoint();
                    CaptureGameWindow(window);
                    CheckFov(_tbTargetWidth.Text == "640" && _tbTargetHeight.Text == "360" &&
                        _tbWindowX.Text == (expected.Left - screen.Bounds.Left).ToString() &&
                        _tbWindowY.Text == (expected.Top - screen.Bounds.Top).ToString(),
                        "capturing a foreign window fills its actual size and relative origin");
                    CheckFov(Cur().ScreenName == screen.DeviceName && _cbScreen.SelectedIndex > 0 &&
                        _cbDisplayMode.SelectedIndex == (int)ProjectionDisplayMode.Window,
                        "capture selects the game monitor and window display mode");
                    CheckFov(_tbFovTarget.Text == "103" && _tbTargetAspect.Text == "4:3" &&
                        FovPoint() == previous && _lblFovResult.ForeColor != Theme.Green,
                        "capture preserves FOV, aspect and position until generation is requested");
                    _cfg.SaveToDisk();
                    CrosshairItemSettings loaded = AppSettings.Load().Items[0];
                    CheckFov(loaded.TargetWidth == 640 && loaded.TargetHeight == 360 &&
                        loaded.WindowOriginX == expected.Left - screen.Bounds.Left &&
                        loaded.WindowOriginY == expected.Top - screen.Bounds.Top &&
                        loaded.ScreenName == screen.DeviceName,
                        "captured window dimensions, origin and monitor are persisted");
                    string snapshot = string.Join("\n", _cfg.BuildLines().ToArray());
                    CaptureGameWindow(Handle);
                    CheckFov(_lblFovResult.ForeColor == Theme.Danger &&
                        snapshot == string.Join("\n", _cfg.BuildLines().ToArray()),
                        "capturing the app itself leaves all settings unchanged");
                    CaptureGameWindow(IntPtr.Zero);
                    CheckFov(snapshot == string.Join("\n", _cfg.BuildLines().ToArray()),
                        "failed capture leaves all settings unchanged");

                    StartWindowGrab();
                    WindowGrabTick(null, EventArgs.Empty);
                    CheckFov(_windowGrabTimer.Enabled && _windowGrabLeft == 2,
                        "window capture waits for the three-second countdown");
                    StartWindowGrab();
                    CheckFov(!_windowGrabTimer.Enabled && _windowGrabItem == null &&
                        snapshot == string.Join("\n", _cfg.BuildLines().ToArray()),
                        "clicking capture again cancels without changing settings");
                    StartWindowGrab();
                    _tbFovTarget.Text = "104";
                    CheckFov(!_windowGrabTimer.Enabled && _windowGrabItem == null,
                        "editing parameters cancels a pending capture");
                    StartWindowGrab();
                    AddItem();
                    CheckFov(!_windowGrabTimer.Enabled && _windowGrabItem == null,
                        "switching crosshairs cancels a pending capture");
                }
                finally
                {
                    File.WriteAllText(file + ".close", "close");
                    if (!helper.WaitForExit(3000)) helper.Kill();
                }
            }
        }
    }

    // A normal top-level window in a different process, kept transparent and inactive.
    // It exercises the real Win32 APIs without capturing the user's active application.
    public class WindowGrabFixture : Form
    {
        protected override bool ShowWithoutActivation { get { return true; } }

        public static void Run(string file)
        {
            using (WindowGrabFixture window = new WindowGrabFixture())
            using (System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer())
            {
                window.AutoScaleMode = AutoScaleMode.None;
                window.ClientSize = new Size(640, 360);
                window.StartPosition = FormStartPosition.Manual;
                window.Location = new Point(Screen.PrimaryScreen.Bounds.Left + 80,
                    Screen.PrimaryScreen.Bounds.Top + 60);
                window.Opacity = 0;
                window.ShowInTaskbar = false;
                window.Shown += delegate
                {
                    Rectangle expected = window.RectangleToScreen(window.ClientRectangle);
                    File.WriteAllLines(file + ".tmp", new string[] {
                        window.Handle.ToInt64().ToString(), expected.X.ToString(), expected.Y.ToString(),
                        expected.Width.ToString(), expected.Height.ToString(), window.Top.ToString() });
                    File.Move(file + ".tmp", file);
                };
                DateTime deadline = DateTime.UtcNow.AddSeconds(20);
                timer.Interval = 100;
                timer.Tick += delegate
                {
                    if (File.Exists(file + ".close") || DateTime.UtcNow > deadline) window.Close();
                };
                timer.Start();
                Application.Run(window);
            }
        }
    }
}
