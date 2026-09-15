using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace ScreenCrosshair
{
    /// <summary>全局设置。配置文件放在 exe 同目录，做到真正便携。</summary>
    public partial class AppSettings
    {
        public List<Profile> Profiles;
        public int ActiveIndex;    // 当前预设
        public int SelectedItem;   // 界面上选中的准星
        public bool GlobalVisible;

        public uint ToggleMod;     // 显示/隐藏全部
        public uint ToggleKey;
        public bool ToggleOn;
        public uint SwitchMod;     // 切换到下一套预设
        public uint SwitchKey;
        public bool SwitchOn;

        public uint EvacuationMod; // 撤离点倒计时
        public uint EvacuationKey;
        public bool EvacuationOn;
        public uint RocketMod;     // 火箭倒计时
        public uint RocketKey;
        public bool RocketOn;
        public uint FreeMod;       // 自由倒计时
        public uint FreeKey;
        public bool FreeOn;
        public uint WeakMod;       // 弱网开关
        public uint WeakKey;
        public bool WeakOn;
        public int FreeCountdownSeconds;
        public int CountdownFontSize;
        public int CountdownRightOffset;
        public int CountdownTopOffset;
        public string CountdownScreenName;
        public int ClockRightOffset;
        public int ClockTopOffset;
        public string ClockScreenName;
        public int ClockFontSize;
        public int HudOpacity;
        public bool HudShowPlate;
        public bool HudShowMarkers;
        public bool ShowClock;      // 面板顶上常显北京时间
        public bool ClockSeconds;   // 时钟带不带秒

        public bool AutoHide;      // 前台窗口不是游戏时自动隐藏
        public string GameExe;     // 目标进程名，不含 .exe
        public string WeakGameExe; // QoS 限速匹配的进程文件名
        public int WeakLevel;
        public bool WeakUseMtu;
        public bool WeakShowIndicator;
        public int WeakIndicatorX;
        public int WeakIndicatorY;
        public bool WeakActive;
        public string WeakPolicyName;
        public string WeakMtuRecords;
        public int WindowX;
        public int WindowY;

        public AppSettings()
        {
            Profiles = new List<Profile>();
            Profiles.Add(Profile.NewDefault("默认"));
            ActiveIndex = 0;
            SelectedItem = 0;
            GlobalVisible = true;
            ToggleMod = 0;
            ToggleKey = (uint)Keys.F8;
            ToggleOn = true;
            SwitchMod = Native.MOD_CONTROL;
            SwitchKey = (uint)Keys.F8;
            SwitchOn = true;
            EvacuationMod = Native.MOD_ALT;
            EvacuationKey = (uint)Keys.F1;
            EvacuationOn = true;
            RocketMod = Native.MOD_ALT;
            RocketKey = (uint)Keys.F2;
            RocketOn = true;
            FreeMod = Native.MOD_ALT;
            FreeKey = (uint)Keys.F3;
            FreeOn = true;
            WeakMod = Native.MOD_CONTROL | Native.MOD_SHIFT;
            WeakKey = (uint)Keys.W;
            WeakOn = true;
            FreeCountdownSeconds = 60;
            CountdownFontSize = 28;
            CountdownRightOffset = 18;
            CountdownTopOffset = 76;
            CountdownScreenName = "";
            ClockRightOffset = 18;
            ClockTopOffset = 24;
            ClockScreenName = "";
            ClockFontSize = 22;
            HudOpacity = 78;
            HudShowPlate = true;
            HudShowMarkers = true;
            ShowClock = false;
            ClockSeconds = true;
            AutoHide = false;
            GameExe = "";
            WeakGameExe = "";
            WeakLevel = 3;
            WeakUseMtu = true;
            WeakShowIndicator = false;
            WeakIndicatorX = -32768;
            WeakIndicatorY = -32768;
            WeakActive = false;
            WeakPolicyName = "";
            WeakMtuRecords = "";
            WindowX = -32768;
            WindowY = -32768;
        }

        public Profile Active
        {
            get
            {
                if (Profiles.Count == 0) Profiles.Add(Profile.NewDefault("默认"));
                if (ActiveIndex < 0) ActiveIndex = 0;
                if (ActiveIndex >= Profiles.Count) ActiveIndex = Profiles.Count - 1;
                return Profiles[ActiveIndex];
            }
        }

        public List<CrosshairItemSettings> Items
        {
            get { return Active.Items; }
        }

        // ---------------- 热键槽位 ----------------
        // 界面和注册逻辑都按这个顺序循环。字段本身还是分开写的，好让 ini 里保持
        // ToggleKey / RocketOn 这种一眼能看懂的键名；按序号访问统一走下面几个方法。
        public const int HotToggle = 0;
        public const int HotSwitch = 1;
        public const int HotEvacuation = 2;
        public const int HotRocket = 3;
        public const int HotFree = 4;
        public const int HotWeak = 5;
        public const int HotCount = 6;

        /// <summary>「热键」页每一行左边的名字</summary>
        public static readonly string[] HotNames =
            { "显示 / 隐藏全部", "切换下一预设", "撤离点 5:00", "火箭 4:30", "自由倒计时", "弱网开关" };

        /// <summary>状态行里列举注册失败的项时用的短名</summary>
        public static readonly string[] HotShort =
            { "显隐", "切预设", "撤离点", "火箭", "自由", "灵魂出窍" };

        public uint HotMod(int i)
        {
            switch (i)
            {
                case HotToggle: return ToggleMod;
                case HotSwitch: return SwitchMod;
                case HotEvacuation: return EvacuationMod;
                case HotRocket: return RocketMod;
                case HotFree: return FreeMod;
                default: return WeakMod;
            }
        }

        public uint HotKey(int i)
        {
            switch (i)
            {
                case HotToggle: return ToggleKey;
                case HotSwitch: return SwitchKey;
                case HotEvacuation: return EvacuationKey;
                case HotRocket: return RocketKey;
                case HotFree: return FreeKey;
                default: return WeakKey;
            }
        }

        public bool HotEnabled(int i)
        {
            switch (i)
            {
                case HotToggle: return ToggleOn;
                case HotSwitch: return SwitchOn;
                case HotEvacuation: return EvacuationOn;
                case HotRocket: return RocketOn;
                case HotFree: return FreeOn;
                default: return WeakOn;
            }
        }

        public void SetHot(int i, uint mod, uint key)
        {
            switch (i)
            {
                case HotToggle: ToggleMod = mod; ToggleKey = key; break;
                case HotSwitch: SwitchMod = mod; SwitchKey = key; break;
                case HotEvacuation: EvacuationMod = mod; EvacuationKey = key; break;
                case HotRocket: RocketMod = mod; RocketKey = key; break;
                case HotFree: FreeMod = mod; FreeKey = key; break;
                default: WeakMod = mod; WeakKey = key; break;
            }
        }

        public void SetHotEnabled(int i, bool on)
        {
            switch (i)
            {
                case HotToggle: ToggleOn = on; break;
                case HotSwitch: SwitchOn = on; break;
                case HotEvacuation: EvacuationOn = on; break;
                case HotRocket: RocketOn = on; break;
                case HotFree: FreeOn = on; break;
                default: WeakOn = on; break;
            }
        }

        private static string _path;

        /// <summary>优先 exe 同目录（便携）；目录不可写时退回 %AppData%</summary>
        public static string ConfigPath
        {
            get
            {
                if (_path != null) return _path;
                try
                {
                    string dir = Path.GetDirectoryName(Application.ExecutablePath);
                    string probe = Path.Combine(dir, "settings.ini");
                    if (!File.Exists(probe))
                    {
                        // 试写一下确认有权限，随后删掉，不留垃圾文件
                        using (FileStream fs = File.Open(probe, FileMode.CreateNew, FileAccess.Write)) { }
                        File.Delete(probe);
                    }
                    _path = probe;
                    return _path;
                }
                catch { }
                try
                {
                    string ad = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "ScreenCrosshair");
                    Directory.CreateDirectory(ad);
                    _path = Path.Combine(ad, "settings.ini");
                }
                catch { _path = "settings.ini"; }
                return _path;
            }
        }

        public static AppSettings Load()
        {
            AppSettings s = new AppSettings();
            try
            {
                string p = ConfigPath;
                if (File.Exists(p)) s.ReadFrom(File.ReadAllLines(p, Encoding.UTF8));
            }
            catch { }
            return s;
        }

        internal void ReadFrom(string[] lines)
        {
            IniBag ini = new IniBag();
            ini.Load(lines);
            Dictionary<string, string> app = ini.Sec("app");

            GlobalVisible = IniBag.B(app, "GlobalVisible", true);
            ToggleMod = (uint)IniBag.I(app, "ToggleMod", 0, 0, 15);
            ToggleKey = (uint)IniBag.I(app, "ToggleKey", (int)Keys.F8, 1, 255);
            ToggleOn = IniBag.B(app, "ToggleOn", true);
            SwitchMod = (uint)IniBag.I(app, "SwitchMod", (int)Native.MOD_CONTROL, 0, 15);
            SwitchKey = (uint)IniBag.I(app, "SwitchKey", (int)Keys.F8, 1, 255);
            SwitchOn = IniBag.B(app, "SwitchOn", true);
            EvacuationMod = (uint)IniBag.I(app, "EvacuationMod", (int)Native.MOD_ALT, 0, 15);
            EvacuationKey = (uint)IniBag.I(app, "EvacuationKey", (int)Keys.F1, 1, 255);
            EvacuationOn = IniBag.B(app, "EvacuationOn", true);
            RocketMod = (uint)IniBag.I(app, "RocketMod", (int)Native.MOD_ALT, 0, 15);
            RocketKey = (uint)IniBag.I(app, "RocketKey", (int)Keys.F2, 1, 255);
            RocketOn = IniBag.B(app, "RocketOn", true);
            FreeMod = (uint)IniBag.I(app, "FreeMod", (int)Native.MOD_ALT, 0, 15);
            FreeKey = (uint)IniBag.I(app, "FreeKey", (int)Keys.F3, 1, 255);
            FreeOn = IniBag.B(app, "FreeOn", true);
            WeakMod = (uint)IniBag.I(app, "WeakMod", (int)(Native.MOD_CONTROL | Native.MOD_SHIFT), 0, 15);
            WeakKey = (uint)IniBag.I(app, "WeakKey", (int)Keys.W, 1, 255);
            WeakOn = IniBag.B(app, "WeakOn", true);
            FreeCountdownSeconds = IniBag.I(app, "FreeCountdownSeconds", 60, 1, 59999);
            CountdownFontSize = IniBag.I(app, "CountdownFontSize", 28, 14, 72);
            CountdownRightOffset = IniBag.I(app, "CountdownRightOffset", 18, 0, 9999);
            CountdownTopOffset = IniBag.I(app, "CountdownTopOffset", 76, 0, 9999);
            CountdownScreenName = IniBag.S(app, "CountdownScreen", "");
            ClockRightOffset = IniBag.I(app, "ClockRightOffset", 18, 0, 9999);
            ClockTopOffset = IniBag.I(app, "ClockTopOffset", 24, 0, 9999);
            ClockScreenName = IniBag.S(app, "ClockScreen", "");
            ClockFontSize = IniBag.I(app, "ClockFontSize", 22, 14, 72);
            HudOpacity = IniBag.I(app, "HudOpacity", 78, 20, 100);
            HudShowPlate = IniBag.B(app, "HudShowPlate", true);
            HudShowMarkers = IniBag.B(app, "HudShowMarkers", true);
            ShowClock = IniBag.B(app, "ShowClock", false);
            ClockSeconds = IniBag.B(app, "ClockSeconds", true);
            AutoHide = IniBag.B(app, "AutoHide", false);
            GameExe = IniBag.S(app, "GameExe", "");
            WeakGameExe = IniBag.S(app, "WeakGameExe", "");
            WeakLevel = IniBag.I(app, "WeakLevel", 3, 0, 3);
            WeakUseMtu = IniBag.B(app, "WeakUseMtu", true);
            WeakShowIndicator = IniBag.B(app, "WeakShowIndicator", false);
            WeakIndicatorX = IniBag.I(app, "WeakIndicatorX", -32768, -32768, 32767);
            WeakIndicatorY = IniBag.I(app, "WeakIndicatorY", -32768, -32768, 32767);
            WeakActive = IniBag.B(app, "WeakActive", false);
            WeakPolicyName = IniBag.S(app, "WeakPolicyName", "");
            WeakMtuRecords = IniBag.S(app, "WeakMtuRecords", "");
            WindowX = IniBag.I(app, "WindowX", -32768, -32768, 32767);
            WindowY = IniBag.I(app, "WindowY", -32768, -32768, 32767);

            int pc = IniBag.I(app, "ProfileCount", 0, 0, 64);
            List<Profile> list = new List<Profile>();
            for (int i = 0; i < pc; i++)
            {
                string sn = "profile" + i;
                if (!ini.Has(sn)) continue;
                Dictionary<string, string> d = ini.Sec(sn);
                Profile pr = new Profile();
                pr.Name = IniBag.S(d, "Name", "预设 " + (i + 1));
                int cnt = IniBag.I(d, "Count", 0, 0, 32);
                for (int k = 0; k < cnt; k++) pr.Items.Add(ReadItem(d, k + "."));
                if (pr.Items.Count == 0) pr.Items.Add(new CrosshairItemSettings());
                list.Add(pr);
            }
            if (list.Count > 0) Profiles = list;

            ActiveIndex = IniBag.I(app, "ActiveProfile", 0, 0, Profiles.Count - 1);
            SelectedItem = IniBag.I(app, "SelectedItem", 0, 0, Math.Max(0, Items.Count - 1));
        }
    }
}
