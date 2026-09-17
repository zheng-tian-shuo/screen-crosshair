using System.Drawing;
using System.Windows.Forms;

namespace ScreenCrosshair
{
    public partial class MainForm
    {
        private FlatBtn _themeDark, _themeLight;

        private void SwitchTheme(bool light)
        {
            if (Theme.IsLight == light) return;
            Color[] oldColors = Theme.Palette();
            Theme.SetLight(light);
            Color[] newColors = Theme.Palette();
            SuspendLayout();
            try
            {
                RecolorControls(this, oldColors, newColors);
                if (_tray != null && _tray.ContextMenuStrip != null)
                {
                    ContextMenuStrip menu = _tray.ContextMenuStrip;
                    menu.BackColor = Theme.Card;
                    menu.ForeColor = Theme.Text;
                    foreach (ToolStripItem item in menu.Items)
                    {
                        item.BackColor = Theme.Card;
                        item.ForeColor = Theme.Text;
                    }
                }
                _cfg.LightTheme = light;
                _cfg.Save();
                UpdateThemeButtons();
            }
            finally { ResumeLayout(false); }
            Invalidate(true);
        }

        private void UpdateThemeButtons()
        {
            _themeDark.Kind = Theme.IsLight ? 0 : 1;
            _themeLight.Kind = Theme.IsLight ? 1 : 0;
            _themeDark.Invalidate();
            _themeLight.Invalidate();
        }

        private static Color RemapColor(Color color, Color[] before, Color[] after)
        {
            for (int i = 0; i < before.Length; i++)
                if (color.ToArgb() == before[i].ToArgb()) return after[i];
            return color;
        }

        private static void RecolorControls(Control control, Color[] before, Color[] after)
        {
            // Children first: inherited colors must be read before their parent changes.
            foreach (Control child in control.Controls) RecolorControls(child, before, after);
            // Swatches are user colors, including white, rather than theme surfaces.
            if (!(control.Tag is Color))
                control.BackColor = RemapColor(control.BackColor, before, after);
            control.ForeColor = RemapColor(control.ForeColor, before, after);
            control.Invalidate();
        }
    }
}
