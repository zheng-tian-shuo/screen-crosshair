using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading;

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
                "if (Get-NetQosPolicy -PolicyStore ActiveStore -ErrorAction Stop | Where-Object {$_.Name -eq $n}) {Remove-NetQosPolicy -Name $n -PolicyStore ActiveStore -Confirm:$false -ErrorAction Stop};" +
                "New-NetQosPolicy -Name $n -AppPathNameMatchCondition '" + exeName + "' -IPProtocolMatchCondition Both -NetworkProfile All -ThrottleRateActionBitsPerSecond " +
                profile.BitsPerSecond.ToString(CultureInfo.InvariantCulture) +
                " -PolicyStore ActiveStore -Confirm:$false -ErrorAction Stop | Out-Null;" +
                "if (Get-NetQosPolicy -Name $n -PolicyStore ActiveStore -ErrorAction SilentlyContinue) {'QOS|OK'} else {'QOS|MISSING'}" +
                "} catch {'QOS|ERR|' + $_.Exception.Message};";
            if (useMtu)
                script += BuildMtuScript(profile.Mtu, WeakRecoveryStore.Path);
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
                    {
                        records.Add(index + "|" + original + "|" + profile.Mtu);
                    }
                }
                mtuRecords = string.Join(";", records.ToArray());
                bool mtuErrors = output.IndexOf("MTUERR|", StringComparison.Ordinal) >= 0;
                lines.Add(mtuRecords.Length > 0
                    ? "已临时降低 " + records.Count + " 个活动网卡的 MTU，关闭时会还原。"
                    : (mtuErrors ? "MTU 调整失败：" + Short(output)
                    : (started ? "没有需要降低 MTU 的活动网卡。" : "MTU 调整失败：" + Short(output))));
            }
            else lines.Add("未调整 MTU。");

            if (mtuRecords.Length > 0)
            {
                List<string> adapterIds = new List<string>();
                string[] saved = mtuRecords.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < saved.Length; i++)
                {
                    string[] fields = saved[i].Split('|');
                    if (fields.Length > 0) adapterIds.Add("index " + fields[0]);
                }
                lines.Add("本次调整的网卡接口索引：" + string.Join("、", adapterIds.ToArray()) + "。关闭时会检查后恢复。");
            }
            if (!qosOk && mtuRecords.Length > 0)
                lines.Add("部分成功：QoS 限速未启用，但 MTU 已调整；关闭弱网时仍会尝试恢复。");

            bool appliedAny = mtuRecords.Length > 0;
            try { mtuRecords = WeakRecoveryStore.ReadRecords(mtuRecords); }
            catch (Exception ex) { lines.Add("恢复记录读取失败，关闭时将重试：" + ex.Message); }
            log = string.Join(Environment.NewLine, lines.ToArray());
            return qosOk || appliedAny;
        }

        internal static string BuildMtuScript(int mtu, string journalPath)
        {
            string target = mtu.ToString(CultureInfo.InvariantCulture);
            return "try {$r=@(Get-NetIPInterface -AddressFamily IPv4 -PolicyStore ActiveStore -ErrorAction Stop | " +
                "Where-Object {$_.ConnectionState -eq 'Connected' -and $_.InterfaceIndex -ne 1 -and $_.NlMtu -gt " + target + "});" +
                "$records=@($r | ForEach-Object {''+$_.InterfaceIndex+'|'+$_.NlMtu+'|" + target + "'});" +
                "[IO.File]::WriteAllText('" + journalPath.Replace("'", "''") + "',($records -join ';'),[Text.Encoding]::UTF8);" +
                "foreach($i in $r){$old=$i.NlMtu;try {" +
                "Set-NetIPInterface -InterfaceIndex $i.InterfaceIndex -AddressFamily IPv4 -NlMtuBytes " + target + " -PolicyStore ActiveStore -ErrorAction Stop;" +
                "$now=Get-NetIPInterface -InterfaceIndex $i.InterfaceIndex -AddressFamily IPv4 -PolicyStore ActiveStore -ErrorAction Stop;" +
                "if(!$now -or $now.NlMtu -ne " + target + "){throw 'MTU verification failed'};" +
                "'MTU|'+$i.InterfaceIndex+'|'+$old} catch {'MTUERR|'+$i.InterfaceIndex+'|'+$_.Exception.Message}}" +
                "} catch {'MTUERR|'+$_.Exception.Message};";
        }

        internal static bool Restore(string policyName, string mtuRecords, out string log)
        {
            try { mtuRecords = WeakRecoveryStore.ReadRecords(mtuRecords); }
            catch (Exception ex) { log = "无法读取恢复记录：" + ex.Message; return false; }
            string output;
            bool ran = RunPowerShell(BuildRestoreScript(policyName, mtuRecords), out output);
            bool ok = ran && Array.IndexOf(output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries), "RESTORE|OK") >= 0;
            bool conflict = output.IndexOf("MTUSKIP|", StringComparison.Ordinal) >= 0;
            log = ok ? "已确认临时 QoS 策略已清除，记录的网卡 MTU 已还原。"
                : "网络尚未完全恢复，请再次点击关闭并还原。\n" + Short(output);
            if (!ok && conflict)
                log = "检测到网卡 MTU 在弱网期间被外部修改，软件未覆盖这些改动。\n" + Short(output);
            if (ok)
            {
                try { WeakRecoveryStore.Clear(); }
                catch (Exception ex) { log = "网络已还原，但恢复记录尚未清理：" + ex.Message; return false; }
            }
            return ok;
        }

        internal static string BuildRestoreScript(string policyName, string mtuRecords)
        {
            StringBuilder script = new StringBuilder("$ok=$true;");
            if (!string.IsNullOrEmpty(policyName))
            {
                // Query the store, not a missing name: absence is success, query failure is not.
                script.Append("try {$n='").Append(policyName.Replace("'", "''")).Append("';")
                    .Append("if (Get-NetQosPolicy -PolicyStore ActiveStore -ErrorAction Stop | Where-Object {$_.Name -eq $n}) {")
                    .Append("Remove-NetQosPolicy -Name $n -PolicyStore ActiveStore -Confirm:$false -ErrorAction Stop;};")
                    .Append("if (Get-NetQosPolicy -PolicyStore ActiveStore -ErrorAction Stop | Where-Object {$_.Name -eq $n}) {throw 'QoS policy still exists'}")
                    .Append("} catch {$ok=$false; 'QOSERR|'+$_.Exception.Message};");
            }
            if (!string.IsNullOrEmpty(mtuRecords))
            {
                string[] all = mtuRecords.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < all.Length; i++)
                {
                    string[] part = all[i].Split('|');
                    int index, mtu, applied = 0;
                    bool hasApplied = part.Length == 3 && int.TryParse(part[2], out applied) && applied > 0;
                    if ((part.Length != 2 && part.Length != 3) || (part.Length == 3 && !hasApplied) ||
                        !int.TryParse(part[0], out index) || !int.TryParse(part[1], out mtu) || index <= 0 || mtu <= 0)
                    {
                        script.Append("$ok=$false; 'MTUERR|Invalid recovery record';");
                        continue;
                    }
                    script.Append("try {");
                    if (hasApplied)
                    {
                        script.Append("$i=Get-NetIPInterface -InterfaceIndex ").Append(index)
                            .Append(" -AddressFamily IPv4 -PolicyStore ActiveStore -ErrorAction Stop;")
                            .Append("if (!$i) {throw 'Interface not found'};")
                            .Append("if ($i.NlMtu -eq ").Append(mtu).Append(") {} elseif ($i.NlMtu -ne ")
                            .Append(applied).Append(") {$ok=$false; 'MTUSKIP|'+").Append(index).Append("+'|'+$i.NlMtu+'|'+").Append(applied).Append("} else {");
                    }
                    script.Append("Set-NetIPInterface -InterfaceIndex ").Append(index)
                        .Append(" -AddressFamily IPv4 -NlMtuBytes ").Append(mtu)
                        .Append(" -PolicyStore ActiveStore -ErrorAction Stop;")
                        .Append("$i=Get-NetIPInterface -InterfaceIndex ").Append(index)
                        .Append(" -AddressFamily IPv4 -PolicyStore ActiveStore -ErrorAction Stop;")
                        .Append("if (!$i -or $i.NlMtu -ne ").Append(mtu).Append(") {throw 'MTU verification failed'}");
                    if (hasApplied) script.Append("}");
                    script.Append("} catch {$ok=$false; 'MTUERR|'+$_.Exception.Message};");
                }
            }
            script.Append("if ($ok) {'RESTORE|OK'}");
            return script.ToString();
        }

        internal static string PolicyName(string exeName)
        {
            using (MD5 md5 = MD5.Create())
            {
                byte[] bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(exeName.ToLowerInvariant()));
                return "ScreenCrosshairWeak_" + BitConverter.ToString(bytes).Replace("-", "").Substring(0, 12);
            }
        }

        private static bool RunPowerShell(string script, out string output)
        {
            ProcessStartInfo psi = new ProcessStartInfo("powershell.exe");
            psi.Arguments = "-NoProfile -NonInteractive -EncodedCommand " +
                Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
            return RunProcess(psi, 30000, out output);
        }

        internal static bool RunProcess(ProcessStartInfo psi, int timeoutMs, out string output)
        {
            StringBuilder captured = new StringBuilder();
            object gate = new object();
            bool accepting = true;
            using (ManualResetEvent stdoutDone = new ManualResetEvent(false))
            using (ManualResetEvent stderrDone = new ManualResetEvent(false))
            using (Process p = new Process())
            {
                try
                {
                    psi.CreateNoWindow = true;
                    psi.UseShellExecute = false;
                    psi.RedirectStandardOutput = true;
                    psi.RedirectStandardError = true;
                    p.StartInfo = psi;
                    p.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
                    {
                        lock (gate)
                        {
                            if (!accepting) return;
                            if (e.Data == null) stdoutDone.Set();
                            else captured.AppendLine(e.Data);
                        }
                    };
                    p.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
                    {
                        lock (gate)
                        {
                            if (!accepting) return;
                            if (e.Data == null) stderrDone.Set();
                            else captured.AppendLine(e.Data);
                        }
                    };
                    p.Start();
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                    bool exited = p.WaitForExit(timeoutMs);
                    if (!exited)
                    {
                        try { p.Kill(); p.WaitForExit(2000); }
                        catch (Exception ex) { lock (gate) captured.AppendLine(ex.Message); }
                    }
                    // Bound stream draining too: inherited pipes must not defeat the timeout.
                    bool drainedOut = stdoutDone.WaitOne(1000);
                    bool drainedErr = stderrDone.WaitOne(1000);
                    lock (gate)
                    {
                        if (!exited) captured.AppendLine("操作超时，已请求终止后台命令。");
                        if (!drainedOut || !drainedErr) captured.AppendLine("后台命令输出未完整结束。");
                        output = captured.ToString().Trim();
                    }
                    return exited && drainedOut && drainedErr && p.ExitCode == 0;
                }
                catch (Exception ex)
                {
                    try { if (!p.HasExited) p.Kill(); } catch { }
                    lock (gate) output = captured.ToString() + ex.Message;
                    return false;
                }
                finally { lock (gate) accepting = false; }
            }
        }

        private static string Short(string text)
        {
            if (string.IsNullOrEmpty(text)) return "未知原因（请确认以管理员运行）。";
            text = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
            return text.Length > 150 ? text.Substring(0, 150) + "…" : text;
        }
    }
}
