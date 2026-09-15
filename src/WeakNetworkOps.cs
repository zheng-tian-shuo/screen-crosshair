using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;

namespace ScreenCrosshair
{
    /// <summary>Windows QoS/MTU 弱网操作。所有改动都使用 ActiveStore，重启不会遗留。</summary>
    internal static class WeakNetworkOps
    {
        internal struct Profile
        {
            public string Name;
            public long BitsPerSecond;
            public int Mtu;
            public Profile(string name, long rate, int mtu)
            { Name = name; BitsPerSecond = rate; Mtu = mtu; }
        }

        internal static readonly Profile[] Profiles =
        {
            new Profile("轻微 · 256 Kbps", 256000, 1400),
            new Profile("中等 · 96 Kbps", 96000, 1100),
            new Profile("严重 · 32 Kbps", 32000, 800),
            new Profile("极端 · 8 Kbps", 8000, 576)
        };

        internal static bool IsAdministrator()
        {
            try
            {
                WindowsPrincipal p = new WindowsPrincipal(WindowsIdentity.GetCurrent());
                return p.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch { return false; }
        }

        internal static bool IsSafeExeName(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 180 || !value.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) return false;
            if (value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || value.IndexOf('\\') >= 0 || value.IndexOf('/') >= 0) return false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (!(char.IsLetterOrDigit(c) || c == ' ' || c == '.' || c == '_' || c == '-')) return false;
            }
            return true;
        }

        internal static bool Apply(string exeName, int level, bool useMtu,
            out string policyName, out string mtuRecords, out string log)
        {
            policyName = PolicyName(exeName);
            mtuRecords = "";
            level = Math.Max(0, Math.Min(Profiles.Length - 1, level));
            Profile profile = Profiles[level];
            List<string> lines = new List<string>();
            string output = "";

            // Put QoS and MTU work in one PowerShell process. Cold-starting one per operation was the main toggle delay.
            string script = "$n='" + policyName + "';try {" +
                "Get-NetQosPolicy -Name $n -PolicyStore ActiveStore -ErrorAction SilentlyContinue | Remove-NetQosPolicy -Confirm:$false -ErrorAction SilentlyContinue;" +
                "New-NetQosPolicy -Name $n -AppPathNameMatchCondition '" + exeName + "' -IPProtocolMatchCondition Both -NetworkProfile All -ThrottleRateActionBitsPerSecond " +
                profile.BitsPerSecond.ToString(CultureInfo.InvariantCulture) +
                " -PolicyStore ActiveStore -Confirm:$false -ErrorAction Stop | Out-Null;" +
                "if (Get-NetQosPolicy -Name $n -PolicyStore ActiveStore -ErrorAction SilentlyContinue) {'QOS|OK'} else {'QOS|MISSING'}" +
                "} catch {'QOS|ERR|' + $_.Exception.Message};";
            if (useMtu)
                script += "$r=Get-NetIPInterface -AddressFamily IPv4 -ErrorAction SilentlyContinue | Where-Object {$_.ConnectionState -eq 'Connected' -and $_.InterfaceIndex -ne 1};" +
                    "foreach($i in $r){if($i.NlMtu -gt " + profile.Mtu + "){$old=$i.NlMtu;try {Set-NetIPInterface -InterfaceIndex $i.InterfaceIndex -AddressFamily IPv4 -NlMtuBytes " + profile.Mtu + " -PolicyStore ActiveStore -ErrorAction Stop; 'MTU|'+$i.InterfaceIndex+'|'+$old} catch {'MTUERR|'+$i.InterfaceIndex}}}";
            bool started = RunPowerShell(script, out output);
            bool qosOk = started && output.IndexOf("QOS|OK", StringComparison.Ordinal) >= 0;
            lines.Add(qosOk
                ? "已对 " + exeName + " 启用出站限速：" + (profile.BitsPerSecond / 1000) + " Kbps。"
                : "QoS 限速策略创建失败：" + Short(output));

            if (useMtu)
            {
                List<string> records = new List<string>();
                string[] raw = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < raw.Length; i++)
                {
                    string[] part = raw[i].Trim().Split('|');
                    if (part.Length != 3 || part[0] != "MTU") continue;
                    int index, original;
                    if (int.TryParse(part[1], out index) && int.TryParse(part[2], out original) && index > 0 && original > 0)
                        records.Add(index + "|" + original);
                }
                mtuRecords = string.Join(";", records.ToArray());
                lines.Add(mtuRecords.Length > 0
                    ? "已临时降低 " + records.Count + " 个活动网卡的 MTU，关闭时会还原。"
                    : (started ? "没有需要降低 MTU 的活动网卡。" : "MTU 调整失败：" + Short(output)));
            }
            else lines.Add("未调整 MTU。");

            log = string.Join(Environment.NewLine, lines.ToArray());
            return qosOk || mtuRecords.Length > 0;
        }

        internal static void Restore(string policyName, string mtuRecords, out string log)
        {
            List<string> lines = new List<string>();
            string output = "";
            StringBuilder script = new StringBuilder();
            bool hasPolicy = !string.IsNullOrEmpty(policyName);
            if (!string.IsNullOrEmpty(policyName))
            {
                script.Append("try {$n='").Append(policyName).Append("';Get-NetQosPolicy -Name $n -PolicyStore ActiveStore -ErrorAction SilentlyContinue | Remove-NetQosPolicy -Confirm:$false -ErrorAction SilentlyContinue;'QOSOK'} catch {'QOSERR'}; ");
            }
            bool hasMtu = !string.IsNullOrEmpty(mtuRecords);
            if (!string.IsNullOrEmpty(mtuRecords))
            {
                string[] all = mtuRecords.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < all.Length; i++)
                {
                    string[] part = all[i].Split('|');
                    int index, mtu;
                    if (part.Length != 2 || !int.TryParse(part[0], out index) || !int.TryParse(part[1], out mtu)) continue;
                    script.Append("try { Set-NetIPInterface -InterfaceIndex ").Append(index)
                        .Append(" -AddressFamily IPv4 -NlMtuBytes ").Append(mtu)
                        .Append(" -PolicyStore ActiveStore -ErrorAction Stop; 'MTUOK' } catch { 'MTUERR' }; ");
                }
            }
            bool ran = script.Length > 0 && RunPowerShell(script.ToString(), out output);
            if (hasPolicy)
                lines.Add(ran && output.IndexOf("QOSOK", StringComparison.Ordinal) >= 0
                    ? "已清除临时 QoS 限速策略。" : "QoS 策略清除失败：" + Short(output));
            if (hasMtu)
            {
                int restored = ran ? output.Split(new[] { "MTUOK" }, StringSplitOptions.None).Length - 1 : 0;
                lines.Add("已还原 " + restored + " 个网卡的 MTU。");
            }
            if (lines.Count == 0) lines.Add("没有检测到需要恢复的弱网状态。");
            log = string.Join(Environment.NewLine, lines.ToArray());
        }

        private static string PolicyName(string exeName)
        {
            using (MD5 md5 = MD5.Create())
            {
                byte[] bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(exeName.ToLowerInvariant()));
                return "ScreenCrosshairWeak_" + BitConverter.ToString(bytes).Replace("-", "").Substring(0, 12);
            }
        }

        private static bool RunPowerShell(string script, out string output)
        {
            output = "";
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "powershell.exe";
                psi.Arguments = "-NoProfile -NonInteractive -Command \"" + script.Replace("\"", "\\\"") + "\"";
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                using (Process p = Process.Start(psi))
                {
                    string stdout = p.StandardOutput.ReadToEnd();
                    string stderr = p.StandardError.ReadToEnd();
                    if (!p.WaitForExit(30000)) { try { p.Kill(); } catch { } return false; }
                    output = (stdout + " " + stderr).Trim();
                    return p.ExitCode == 0;
                }
            }
            catch (Exception ex) { output = ex.Message; return false; }
        }

        private static string Short(string text)
        {
            if (string.IsNullOrEmpty(text)) return "未知原因（请确认以管理员运行）。";
            text = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
            return text.Length > 150 ? text.Substring(0, 150) + "…" : text;
        }
    }
}
