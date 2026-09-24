using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ScreenCrosshair
{
    // Independent of settings autosave: the PowerShell worker writes this journal
    // before changing any adapter, so even a killed UI process can recover later.
    internal static class WeakRecoveryStore
    {
        internal static string Path { get { return AppSettings.ConfigPath + ".weak-mtu"; } }
        internal static bool Exists { get { return File.Exists(Path); } }

        internal static string ReadRecords(string saved)
        {
            string journal = Exists ? File.ReadAllText(Path, Encoding.UTF8).Trim() : "";
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            List<string> records = new List<string>();
            foreach (string record in ((saved ?? "") + ";" + journal).Split(
                new[] { ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string value = record.Trim();
                if (value.Length > 0 && seen.Add(value)) records.Add(value);
            }
            return string.Join(";", records.ToArray());
        }

        internal static void Clear()
        {
            File.Delete(Path);
        }
    }
}
