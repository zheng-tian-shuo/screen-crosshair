using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace ScreenCrosshair
{
    internal static class Program
    {
        internal static bool RestartAsAdminRequested;
        // 与 v1/v2 区分，避免新旧版本同时驻留导致屏幕上叠两套准星
        private const string MutexName =
            "ScreenCrosshair_v5_9F2C41A8_6B7D_4E15_9A83_2C5E70D4B1F6";

        [STAThread]
        private static void Main()
        {
            bool createdNew;
            bool restartAsAdmin = false;
            using (Mutex mutex = new Mutex(true, MutexName, out createdNew))
            {
                if (!createdNew)
                {
                    MessageBox.Show(HelpText.AppName + "已在运行，请查看系统托盘图标。",
                        HelpText.AppName,
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                // 先量一次屏幕 DPI，界面按它整体放大；覆盖层用物理像素，不受影响
                try
                {
                    using (System.Drawing.Graphics g =
                        System.Drawing.Graphics.FromHwnd(IntPtr.Zero))
                    {
                        float k = g.DpiX / 96f;
                        if (k > 1f) Theme.K = k;
                    }
                }
                catch { }
                Application.ThreadException += OnThreadException;
                AppDomain.CurrentDomain.UnhandledException += OnUnhandled;

                try
                {
                    Application.Run(new MainForm());
                }
                catch (Exception ex)
                {
                    Report("启动失败", ex);
                }
                finally
                {
                    GC.KeepAlive(mutex);
                }
                restartAsAdmin = RestartAsAdminRequested;
            }

            if (restartAsAdmin)
            {
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo(Application.ExecutablePath);
                    psi.Verb = "runas";
                    psi.UseShellExecute = true;
                    psi.WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory;
                    Process.Start(psi);
                }
                catch (Exception ex) { AppLog.Write("管理员模式重启失败", ex); }
            }
        }

        private static void OnThreadException(object sender, ThreadExceptionEventArgs e)
        {
            Report("运行出错", e.Exception);
        }

        private static void OnUnhandled(object sender, UnhandledExceptionEventArgs e)
        {
            Report("运行出错", e.ExceptionObject as Exception);
        }

        private static void Report(string title, Exception ex)
        {
            AppLog.Write(title, ex);
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(title + "。可以把下面的信息截图反馈：");
            sb.AppendLine();
            sb.AppendLine(ex != null ? ex.ToString() : "未知错误");
            MessageBox.Show(sb.ToString(), HelpText.AppName,
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
