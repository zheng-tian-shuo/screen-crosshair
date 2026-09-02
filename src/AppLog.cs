using System;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace ScreenCrosshair
{
    internal static class AppLog
    {
        private static readonly object Gate = new object();

        public static void Write(string message, Exception ex)
        {
            try
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ScreenCrosshair");
                Directory.CreateDirectory(dir);
                string file = Path.Combine(dir, "app.log");
                string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + message;
                if (ex != null) line += Environment.NewLine + ex;
                lock (Gate) File.AppendAllText(file, line + Environment.NewLine, Encoding.UTF8);
            }
            catch { }
        }

        public static void Write(string message)
        {
            Write(message, null);
        }
    }
}
