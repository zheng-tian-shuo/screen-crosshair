# Run after build/build.ps1. Creates only an invisible test window; no app startup or settings writes.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$exe = Get-ChildItem (Join-Path $root 'build') -Filter '* v6.*.exe' | Select-Object -First 1
$assembly = [Reflection.Assembly]::LoadFile($exe.FullName)
$source = @'
using System;
using System.Drawing;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows.Forms;
using ScreenCrosshair;

public class UiRegression
{
    class TestSlider : Slider
    {
        public void Wheel(int delta)
        {
            OnMouseWheel(new HandledMouseEventArgs(MouseButtons.None, 0, 0, 0, delta));
        }
    }
    static void Check(bool ok, string message)
    {
        if (!ok) throw new Exception(message);
        Console.WriteLine("PASS " + message);
    }
    public static void Run()
    {
        Check(Ui.KeepWindowVisible(new Rectangle(1900, -600, 916, 624),
            new Rectangle(0, 0, 1920, 1040)) == new Point(1004, 0), "window title remains reachable");
        Check(Ui.KeepWindowVisible(new Rectangle(-2000, 900, 916, 624),
            new Rectangle(-1920, 0, 1920, 1040)) == new Point(-1920, 416), "negative monitor coordinates");
        Check(Ui.KeepWindowVisible(new Rectangle(100, -200, 1832, 1248),
            new Rectangle(0, 0, 1366, 728)) == Point.Empty, "oversized window keeps title on screen");

        MainForm main = (MainForm)FormatterServices.GetUninitializedObject(typeof(MainForm));
        AppSettings cfg = new AppSettings();
        cfg.Items[0].Name = "Custom & renamed";
        cfg.Items[0].Visible = false;
        typeof(MainForm).GetField("_cfg", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(main, cfg);
        string label = (string)typeof(MainForm).GetMethod("ItemLabel", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(main, new object[] { 0 });
        Check(label.Contains("Custom & renamed") && label.Length > "1. Custom & renamed".Length,
            "renamed item keeps its name and visibility marker");

        using (Form form = new Form())
        using (Panel page = new Panel())
        using (Card card = new Card())
        using (TestSlider slider = new TestSlider())
        {
            form.Opacity = 0;
            form.ShowInTaskbar = false;
            form.ClientSize = new Size(400, 300);
            page.Dock = DockStyle.Fill;
            page.AutoScroll = true;
            card.Bounds = new Rectangle(10, 10, 300, 1400);
            slider.Bounds = new Rectangle(10, 10, 250, 22);
            card.Controls.Add(slider);
            page.Controls.Add(card);
            form.Controls.Add(page);
            form.Show();
            Application.DoEvents();
            int value = slider.Value;
            slider.Wheel(-120);
            Check(page.AutoScrollPosition.Y < 0, "wheel over slider scrolls enclosing page");
            Check(slider.Value == value, "wheel does not change slider value");
            slider.Wheel(12000);
            Check(page.AutoScrollPosition.Y == 0, "scroll clamps at top");
            ComboBox combo = Ui.Combo(card, 10, 50, 180);
            combo.Items.AddRange(new object[] { "One", "Two" });
            combo.SelectedIndex = 0;
            Message wheel = Message.Create(combo.Handle, 0x020A, new IntPtr(-120 << 16), IntPtr.Zero);
            object[] args = { wheel };
            combo.GetType().GetMethod("WndProc", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(combo, args);
            Check(page.AutoScrollPosition.Y < 0 && combo.SelectedIndex == 0,
                "collapsed combo scrolls page without changing selection");
            slider.Wheel(-12000);
            int bottom = page.AutoScrollPosition.Y;
            slider.Wheel(-120);
            Check(page.AutoScrollPosition.Y == bottom, "scroll clamps at bottom");
            form.Close();
        }
    }
}
'@
$compiler = New-Object Microsoft.CSharp.CSharpCodeProvider
$parameters = New-Object System.CodeDom.Compiler.CompilerParameters
$parameters.GenerateInMemory = $true
$parameters.ReferencedAssemblies.AddRange(@($exe.FullName, 'System.dll', 'System.Windows.Forms.dll', 'System.Drawing.dll'))
try {
    $result = $compiler.CompileAssemblyFromSource($parameters, $source)
    if ($result.Errors.HasErrors) { throw ($result.Errors | Out-String) }
    $result.CompiledAssembly.GetType('UiRegression').GetMethod('Run').Invoke($null, @())
} finally { $compiler.Dispose() }
