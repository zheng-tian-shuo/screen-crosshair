using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Globalization;

namespace ScreenCrosshair
{
    public partial class AppSettings
    {
        internal static CrosshairItemSettings ReadItem(Dictionary<string, string> d, string pre)
        {
            CrosshairItemSettings it = new CrosshairItemSettings();
            it.Name = IniBag.S(d, pre + "Name", "准星");
            // 缺项时的兜底值跟构造函数里的默认值保持一致：红色圆点 50 px
            it.Shape = (CrosshairShape)IniBag.I(d, pre + "Shape", 0, 0, 8);
            it.Color = IniBag.C(d, pre + "Color", it.Color);
            it.Size = IniBag.I(d, pre + "Size", 50, 4, 400);
            it.Thickness = IniBag.I(d, pre + "Thickness", 2, 1, 12);
            it.Opacity = IniBag.I(d, pre + "Opacity", 100, 10, 100);
            it.Glow = IniBag.B(d, pre + "Glow", true);
            it.Visible = IniBag.B(d, pre + "Visible", true);
            it.Centered = IniBag.B(d, pre + "Centered", true);
            it.X = IniBag.I(d, pre + "X", 0, -32768, 32767);
            it.Y = IniBag.I(d, pre + "Y", 0, -32768, 32767);
            it.ScreenName = IniBag.S(d, pre + "Screen", "");
            it.TargetFov = IniBag.D(d, pre + "TargetFov", 90.0, 1.0, 179.0);
            if (it.TargetFov <= 1.0 || it.TargetFov >= 179.0) it.TargetFov = 90.0;
            it.TargetWidth = IniBag.I(d, pre + "TargetWidth", 0, 0, 32767);
            it.TargetHeight = IniBag.I(d, pre + "TargetHeight", 0, 0, 32767);
            if (it.TargetWidth < 2 || it.TargetHeight < 2) it.TargetWidth = it.TargetHeight = 0;
            it.TargetAspect = IniBag.S(d, pre + "TargetAspect", "16:9");
            double aspect;
            if (!ProjectionMath.TryAspectRatio(it.TargetAspect, out aspect)) it.TargetAspect = "16:9";
            it.DisplayMode = (ProjectionDisplayMode)IniBag.I(d, pre + "DisplayMode", 0, 0, 3);
            it.WindowOriginX = IniBag.I(d, pre + "WindowOriginX", 0, 0, 32767);
            it.WindowOriginY = IniBag.I(d, pre + "WindowOriginY", 0, 0, 32767);
            it.AppliedFov = IniBag.D(d, pre + "AppliedFov", 90.0, 1.0, 179.0);
            it.AppliedAspect = IniBag.D(d, pre + "AppliedAspect", 16.0 / 9.0, 0.1, 10.0);
            it.AppliedDisplayMode = (ProjectionDisplayMode)IniBag.I(d, pre + "AppliedDisplayMode", 0, 0, 1);
            it.AutoScreenPosition = IniBag.B(d, pre + "AutoScreenPosition", false) && !it.Centered &&
                it.AppliedFov > 1 && it.AppliedFov < 179 && it.AppliedAspect > 0.1 && it.AppliedAspect < 10;
            it.FovReferenceSet = IniBag.B(d, pre + "FovReferenceSet", false);
            it.FovReference = IniBag.D(d, pre + "FovReference", 90.0, 1.0, 179.0);
            it.FovReferenceX = IniBag.I(d, pre + "FovReferenceX", 0, -32768, 32767);
            it.FovReferenceY = IniBag.I(d, pre + "FovReferenceY", 0, -32768, 32767);
            it.FovReferenceWidth = IniBag.I(d, pre + "FovReferenceWidth", 0, 0, 32767);
            it.FovReferenceHeight = IniBag.I(d, pre + "FovReferenceHeight", 0, 0, 32767);
            it.TickCount = IniBag.I(d, pre + "TickCount", 5, 1, 20);
            it.TickSpacing = IniBag.I(d, pre + "TickSpacing", 26, 4, 200);
            it.TickLength = IniBag.I(d, pre + "TickLength", 11, 4, 120);
            it.ShowLabels = IniBag.B(d, pre + "ShowLabels", true);
            it.LabelStart = IniBag.I(d, pre + "LabelStart", 50, 0, 99999);
            it.LabelStep = IniBag.I(d, pre + "LabelStep", 25, 0, 99999);
            return it;
        }

        private bool _savePending;

        /// <summary>先标记配置已修改，实际写盘统一放到程序退出时。</summary>
        public void Save()
        {
            _savePending = true;
        }

        /// <summary>退出时先写临时文件再替换配置，不生成额外备份文件。</summary>
        public void SaveToDisk()
        {
            if (!_savePending) return;
            try
            {
                string path = ConfigPath;
                string tmp = path + ".tmp";
                File.WriteAllLines(tmp, BuildLines().ToArray(), Encoding.UTF8);
                if (File.Exists(path))
                {
                    // Replace the destination in one filesystem operation where possible.
                    // A plain Copy briefly leaves a truncated settings.ini if the process is
                    // interrupted halfway through the write.
                    try
                    {
                        File.Replace(tmp, path, null);
                    }
                    catch (PlatformNotSupportedException)
                    {
                        File.Copy(tmp, path, true);
                        File.Delete(tmp);
                    }
                    catch (IOException)
                    {
                        // File.Replace can fail on filesystems that do not support it (for
                        // example some removable drives); retain the safe fallback.
                        File.Copy(tmp, path, true);
                        File.Delete(tmp);
                    }
                }
                else File.Move(tmp, path);
                _savePending = false;
            }
            catch (Exception ex) { AppLog.Write("保存配置失败", ex); }
        }

        public bool ExportTo(string file)
        {
            try
            {
                File.WriteAllLines(file, BuildLines().ToArray(), Encoding.UTF8);
                return true;
            }
            catch { return false; }
        }

        public bool ImportFrom(string file)
        {
            // Recovery belongs to this machine, never to an imported preset.
            bool weakActive = WeakActive;
            string weakPolicy = WeakPolicyName;
            string weakRecords = WeakMtuRecords;
            try
            {
                string[] lines = File.ReadAllLines(file, Encoding.UTF8);
                ReadFrom(lines);
                // Import changes the live settings just like editing a field in the UI.
                // Mark it dirty so closing the app persists the imported configuration.
                _savePending = true;
                return true;
            }
            catch { return false; }
            finally
            {
                WeakActive = weakActive;
                WeakPolicyName = weakPolicy;
                WeakMtuRecords = weakRecords;
            }
        }

        public List<string> BuildLines()
        {
            List<string> L = new List<string>();
            L.Add("# " + AppInfo.AppName + " " + AppInfo.Version + " 配置文件");
            L.Add("# 纯文本，可以直接用记事本改；改坏某一行只会丢那一项，不影响其他设置。");
            L.Add("");
            L.Add("[app]");
            L.Add("LightTheme=" + (LightTheme ? "1" : "0"));
            L.Add("GlobalVisible=" + (GlobalVisible ? "1" : "0"));
            L.Add("WindowX=" + WindowX);
            L.Add("WindowY=" + WindowY);
            L.Add("ActiveProfile=" + ActiveIndex);
            L.Add("SelectedItem=" + SelectedItem);
            L.Add("ToggleMod=" + ToggleMod);
            L.Add("ToggleKey=" + ToggleKey);
            L.Add("ToggleOn=" + (ToggleOn ? "1" : "0"));
            L.Add("SwitchMod=" + SwitchMod);
            L.Add("SwitchKey=" + SwitchKey);
            L.Add("SwitchOn=" + (SwitchOn ? "1" : "0"));
            L.Add("EvacuationMod=" + EvacuationMod);
            L.Add("EvacuationKey=" + EvacuationKey);
            L.Add("EvacuationOn=" + (EvacuationOn ? "1" : "0"));
            L.Add("RocketMod=" + RocketMod);
            L.Add("RocketKey=" + RocketKey);
            L.Add("RocketOn=" + (RocketOn ? "1" : "0"));
            L.Add("FreeMod=" + FreeMod);
            L.Add("FreeKey=" + FreeKey);
            L.Add("FreeOn=" + (FreeOn ? "1" : "0"));
            L.Add("WeakMod=" + WeakMod);
            L.Add("WeakKey=" + WeakKey);
            L.Add("WeakOn=" + (WeakOn ? "1" : "0"));
            L.Add("FreeCountdownSeconds=" + FreeCountdownSeconds);
            L.Add("CountdownFontSize=" + CountdownFontSize);
            L.Add("CountdownRightOffset=" + CountdownRightOffset);
            L.Add("CountdownTopOffset=" + CountdownTopOffset);
            L.Add("CountdownScreen=" + (CountdownScreenName == null ? "" : CountdownScreenName));
            L.Add("ClockRightOffset=" + ClockRightOffset);
            L.Add("ClockTopOffset=" + ClockTopOffset);
            L.Add("ClockScreen=" + (ClockScreenName == null ? "" : ClockScreenName));
            L.Add("ClockFontSize=" + ClockFontSize);
            L.Add("HudOpacity=" + HudOpacity);
            L.Add("HudShowPlate=" + (HudShowPlate ? "1" : "0"));
            L.Add("HudShowMarkers=" + (HudShowMarkers ? "1" : "0"));
            L.Add("ShowClock=" + (ShowClock ? "1" : "0"));
            L.Add("ClockSeconds=" + (ClockSeconds ? "1" : "0"));
            L.Add("AutoHide=" + (AutoHide ? "1" : "0"));
            L.Add("GameExe=" + (GameExe == null ? "" : GameExe));
            L.Add("WeakGameExe=" + (WeakGameExe == null ? "" : WeakGameExe));
            L.Add("WeakLevel=" + WeakLevel);
            L.Add("WeakUseMtu=" + (WeakUseMtu ? "1" : "0"));
            L.Add("WeakShowIndicator=" + (WeakShowIndicator ? "1" : "0"));
            L.Add("WeakIndicatorOpacity=" + WeakIndicatorOpacity);
            L.Add("WeakIndicatorX=" + WeakIndicatorX);
            L.Add("WeakIndicatorY=" + WeakIndicatorY);
            L.Add("WeakActive=" + (WeakActive ? "1" : "0"));
            L.Add("WeakPolicyName=" + (WeakPolicyName == null ? "" : WeakPolicyName));
            L.Add("WeakMtuRecords=" + (WeakMtuRecords == null ? "" : WeakMtuRecords));
            L.Add("ProfileCount=" + Profiles.Count);

            for (int i = 0; i < Profiles.Count; i++)
            {
                Profile pr = Profiles[i];
                L.Add("");
                L.Add("[profile" + i + "]");
                L.Add("Name=" + pr.Name);
                L.Add("Count=" + pr.Items.Count);
                for (int k = 0; k < pr.Items.Count; k++)
                    WriteItem(L, k + ".", pr.Items[k]);
            }
            return L;
        }

        private static void WriteItem(List<string> L, string pre, CrosshairItemSettings it)
        {
            L.Add(pre + "Name=" + it.Name);
            L.Add(pre + "Shape=" + (int)it.Shape);
            L.Add(pre + "Color=" + Theme.HexOf(it.Color));
            L.Add(pre + "Size=" + it.Size);
            L.Add(pre + "Thickness=" + it.Thickness);
            L.Add(pre + "Opacity=" + it.Opacity);
            L.Add(pre + "Glow=" + (it.Glow ? "1" : "0"));
            L.Add(pre + "Visible=" + (it.Visible ? "1" : "0"));
            L.Add(pre + "Centered=" + (it.Centered ? "1" : "0"));
            L.Add(pre + "X=" + it.X);
            L.Add(pre + "Y=" + it.Y);
            L.Add(pre + "Screen=" + (it.ScreenName == null ? "" : it.ScreenName));
            L.Add(pre + "TargetFov=" + it.TargetFov.ToString("R", CultureInfo.InvariantCulture));
            L.Add(pre + "TargetWidth=" + it.TargetWidth);
            L.Add(pre + "TargetHeight=" + it.TargetHeight);
            L.Add(pre + "TargetAspect=" + it.TargetAspect);
            L.Add(pre + "DisplayMode=" + (int)it.DisplayMode);
            L.Add(pre + "WindowOriginX=" + it.WindowOriginX);
            L.Add(pre + "WindowOriginY=" + it.WindowOriginY);
            L.Add(pre + "AutoScreenPosition=" + (it.AutoScreenPosition ? "1" : "0"));
            L.Add(pre + "AppliedFov=" + it.AppliedFov.ToString("R", CultureInfo.InvariantCulture));
            L.Add(pre + "AppliedAspect=" + it.AppliedAspect.ToString("R", CultureInfo.InvariantCulture));
            L.Add(pre + "AppliedDisplayMode=" + (int)it.AppliedDisplayMode);
            // Keep custom ticks when temporarily using another shape.
            {
                L.Add(pre + "TickCount=" + it.TickCount);
                L.Add(pre + "TickSpacing=" + it.TickSpacing);
                L.Add(pre + "TickLength=" + it.TickLength);
                L.Add(pre + "ShowLabels=" + (it.ShowLabels ? "1" : "0"));
                L.Add(pre + "LabelStart=" + it.LabelStart);
                L.Add(pre + "LabelStep=" + it.LabelStep);
            }
        }
    }
}
