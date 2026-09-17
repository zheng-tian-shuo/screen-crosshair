using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;

namespace ScreenCrosshair
{
    /// <summary>一套准星组合（比如「零号大坝·西撤离」），可用热键整套切换</summary>
    public class Profile
    {
        public string Name;
        public List<CrosshairItemSettings> Items;

        public Profile()
        {
            Name = "默认";
            Items = new List<CrosshairItemSettings>();
        }

        public Profile Clone()
        {
            Profile p = new Profile();
            p.Name = Name;
            for (int i = 0; i < Items.Count; i++) p.Items.Add(Items[i].Clone());
            return p;
        }

        public static Profile NewDefault(string name)
        {
            Profile p = new Profile();
            p.Name = name;
            CrosshairItemSettings it = new CrosshairItemSettings();
            it.Name = "准星 1";
            p.Items.Add(it);
            return p;
        }
    }

    /// <summary>
    /// 极简 ini 容器。解析全程不抛异常：认不出的行直接跳过，
    /// 单独一行写坏不会让整个配置作废（旧版 v2 就死在这一点上）。
    /// </summary>
    internal class IniBag
    {
        private readonly Dictionary<string, Dictionary<string, string>> _secs =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        public void Load(string[] lines)
        {
            Dictionary<string, string> cur = Sec("app");
            for (int i = 0; i < lines.Length; i++)
            {
                string s = lines[i] == null ? "" : lines[i].Trim();
                if (s.Length == 0 || s[0] == '#' || s[0] == ';') continue;
                if (s[0] == '[' && s[s.Length - 1] == ']')
                {
                    cur = Sec(s.Substring(1, s.Length - 2).Trim());
                    continue;
                }
                int eq = s.IndexOf('=');
                if (eq <= 0) continue;
                string k = s.Substring(0, eq).Trim();
                string v = s.Substring(eq + 1).Trim();
                if (k.Length > 0) cur[k] = v;
            }
        }

        public Dictionary<string, string> Sec(string name)
        {
            Dictionary<string, string> d;
            if (!_secs.TryGetValue(name, out d))
            {
                d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                _secs[name] = d;
            }
            return d;
        }

        public bool Has(string name)
        {
            return _secs.ContainsKey(name);
        }

        // ---- 容错取值 ----
        public static int I(Dictionary<string, string> s, string k, int def, int lo, int hi)
        {
            string v;
            int n;
            if (s.TryGetValue(k, out v) &&
                int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out n))
            {
                if (n < lo) n = lo;
                if (n > hi) n = hi;
                return n;
            }
            return def;
        }

        public static double D(Dictionary<string, string> s, string k, double def, double lo, double hi)
        {
            string v;
            double n;
            if (s.TryGetValue(k, out v) &&
                double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out n) &&
                !double.IsNaN(n) && !double.IsInfinity(n))
            {
                if (n < lo) n = lo;
                if (n > hi) n = hi;
                return n;
            }
            return def;
        }

        public static bool B(Dictionary<string, string> s, string k, bool def)
        {
            string v;
            if (!s.TryGetValue(k, out v)) return def;
            v = v.Trim();
            if (v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
            if (v == "0" || v.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;
            return def;
        }

        public static string S(Dictionary<string, string> s, string k, string def)
        {
            string v;
            if (s.TryGetValue(k, out v) && v.Length > 0) return v;
            return def;
        }

        public static Color C(Dictionary<string, string> s, string k, Color def)
        {
            string v;
            if (!s.TryGetValue(k, out v)) return def;
            v = v.Trim().TrimStart('#');
            int n;
            if (v.Length == 6 &&
                int.TryParse(v, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out n))
                return Color.FromArgb((n >> 16) & 0xFF, (n >> 8) & 0xFF, n & 0xFF);
            // 兼容旧版本存的十进制 ARGB
            if (int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out n))
                return Color.FromArgb(255, Color.FromArgb(n));
            return def;
        }
    }
}
