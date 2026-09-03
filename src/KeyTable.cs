using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace ScreenCrosshair
{
    /// <summary>热键可选的按键表。名字给界面看，虚拟键码给 RegisterHotKey 用。</summary>
    internal static class KeyTable
    {
        public static readonly string[] Names;
        public static readonly uint[] Vks;

        static KeyTable()
        {
            List<string> n = new List<string>();
            List<uint> v = new List<uint>();

            for (int i = 0; i < 12; i++) { n.Add("F" + (i + 1)); v.Add((uint)Keys.F1 + (uint)i); }
            for (char c = 'A'; c <= 'Z'; c++) { n.Add(c.ToString()); v.Add((uint)c); }
            for (char c = '0'; c <= '9'; c++) { n.Add("数字 " + c); v.Add((uint)c); }

            n.Add("`"); v.Add((uint)Keys.Oemtilde);
            n.Add("-"); v.Add((uint)Keys.OemMinus);
            n.Add("="); v.Add((uint)Keys.Oemplus);
            n.Add("["); v.Add((uint)Keys.OemOpenBrackets);
            n.Add("]"); v.Add((uint)Keys.OemCloseBrackets);
            n.Add("\\"); v.Add((uint)Keys.OemPipe);
            n.Add(";"); v.Add((uint)Keys.OemSemicolon);
            n.Add("'"); v.Add((uint)Keys.OemQuotes);
            n.Add(","); v.Add((uint)Keys.Oemcomma);
            n.Add("."); v.Add((uint)Keys.OemPeriod);
            n.Add("/"); v.Add((uint)Keys.OemQuestion);
            n.Add("空格"); v.Add((uint)Keys.Space);
            n.Add("Insert"); v.Add((uint)Keys.Insert);
            n.Add("Delete"); v.Add((uint)Keys.Delete);
            n.Add("Home"); v.Add((uint)Keys.Home);
            n.Add("End"); v.Add((uint)Keys.End);
            n.Add("PageUp"); v.Add((uint)Keys.PageUp);
            n.Add("PageDown"); v.Add((uint)Keys.PageDown);
            n.Add("Pause"); v.Add((uint)Keys.Pause);
            n.Add("ScrollLock"); v.Add((uint)Keys.Scroll);

            for (int i = 0; i < 10; i++) { n.Add("小键盘 " + i); v.Add((uint)Keys.NumPad0 + (uint)i); }
            n.Add("小键盘 *"); v.Add((uint)Keys.Multiply);
            n.Add("小键盘 -"); v.Add((uint)Keys.Subtract);
            n.Add("小键盘 +"); v.Add((uint)Keys.Add);
            n.Add("小键盘 /"); v.Add((uint)Keys.Divide);
            n.Add("小键盘 ."); v.Add((uint)Keys.Decimal);

            Names = n.ToArray();
            Vks = v.ToArray();
        }

        /// <summary>找不到就退回 F8，保证下拉框总有个合法选中项</summary>
        public static int IndexOfVk(uint vk)
        {
            for (int i = 0; i < Vks.Length; i++) if (Vks[i] == vk) return i;
            for (int i = 0; i < Vks.Length; i++) if (Vks[i] == (uint)Keys.F8) return i;
            return 0;
        }

        public static string NameOfVk(uint vk)
        {
            for (int i = 0; i < Vks.Length; i++) if (Vks[i] == vk) return Names[i];

            switch (vk)
            {
                case (uint)Keys.Back: return "Backspace";
                case (uint)Keys.Tab: return "Tab";
                case (uint)Keys.Return: return "Enter";
                case (uint)Keys.Escape: return "Esc";
                case (uint)Keys.CapsLock: return "CapsLock";
                case (uint)Keys.NumLock: return "NumLock";
                case (uint)Keys.PrintScreen: return "PrintScreen";
                case (uint)Keys.Up: return "Up";
                case (uint)Keys.Down: return "Down";
                case (uint)Keys.Left: return "Left";
                case (uint)Keys.Right: return "Right";
                case (uint)Keys.LWin: return "Left Win";
                case (uint)Keys.RWin: return "Right Win";
                case (uint)Keys.Apps: return "Menu";
            }

            string name = new KeysConverter().ConvertToString((Keys)vk);
            if (!string.IsNullOrEmpty(name)) return name;
            return "键 " + vk;
        }

        /// <summary>拼成 "Ctrl + Shift + F8" 这种给人看的写法</summary>
        public static string Describe(uint mod, uint vk)
        {
            string s = "";
            if ((mod & Native.MOD_CONTROL) != 0) s += "Ctrl + ";
            if ((mod & Native.MOD_ALT) != 0) s += "Alt + ";
            if ((mod & Native.MOD_SHIFT) != 0) s += "Shift + ";
            return s + NameOfVk(vk);
        }
    }
}
