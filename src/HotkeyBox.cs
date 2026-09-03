using System;
using System.Drawing;
using System.Windows.Forms;

namespace ScreenCrosshair
{
    /// <summary>点击后直接录入键盘按键，避免从列表里猜按键名称。</summary>
    public class HotkeyBox : TextBox
    {
        public uint VirtualKey { get; private set; }
        public uint Modifiers { get; private set; }
        public event EventHandler HotkeyChanged;

        public HotkeyBox()
        {
            BorderStyle = BorderStyle.None;
            BackColor = Theme.CardAlt;
            ForeColor = Theme.Text;
            Font = Theme.Body;
            ReadOnly = true;
            TabStop = true;
            ShortcutsEnabled = false;
            TextAlign = HorizontalAlignment.Center;
            Cursor = Cursors.Hand;
        }

        public void SetHotkey(uint modifiers, uint virtualKey)
        {
            Modifiers = modifiers;
            VirtualKey = virtualKey;
            Text = KeyTable.NameOfVk(virtualKey);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            // Modifier keys are collected with the next key, so Ctrl/Alt/Shift
            // alone do not accidentally become the main hotkey.
            if (e.KeyCode == Keys.ControlKey || e.KeyCode == Keys.Control ||
                e.KeyCode == Keys.LControlKey || e.KeyCode == Keys.RControlKey ||
                e.KeyCode == Keys.ShiftKey || e.KeyCode == Keys.Shift ||
                e.KeyCode == Keys.LShiftKey || e.KeyCode == Keys.RShiftKey ||
                e.KeyCode == Keys.Menu || e.KeyCode == Keys.Alt ||
                e.KeyCode == Keys.LMenu || e.KeyCode == Keys.RMenu)
            {
                base.OnKeyDown(e);
                return;
            }

            uint modifiers = 0;
            if (e.Control) modifiers |= Native.MOD_CONTROL;
            if (e.Alt) modifiers |= Native.MOD_ALT;
            if (e.Shift) modifiers |= Native.MOD_SHIFT;
            SetHotkey(modifiers, (uint)e.KeyValue);
            if (HotkeyChanged != null) HotkeyChanged(this, EventArgs.Empty);
            e.SuppressKeyPress = true;
            e.Handled = true;
            base.OnKeyDown(e);
        }
    }
}
