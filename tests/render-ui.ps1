param([string]$OutputDirectory = (Join-Path $PSScriptRoot '../build/ui-preview'))
# Compile a UI-only fixture: no tray, overlays, hotkey registration, networking or config writes.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$temp = Join-Path ([IO.Path]::GetTempPath()) ('crosshair-ui-' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($temp) | Out-Null
[IO.Directory]::CreateDirectory($OutputDirectory) | Out-Null
$source = [IO.File]::ReadAllText((Join-Path $root 'src/MainForm.cs'))
$source = $source.Replace('_cfg = AppSettings.Load();', '_cfg = new AppSettings();')
foreach ($call in @('BuildTray();', 'RebuildOverlays();', 'RegisterHotkeys();', 'RecoverWeakNetworkOnStart();', 'StartTick();', 'SaveSoon();')) {
    $source = $source.Replace($call, '')
}
$fixture = Join-Path $temp 'MainForm.cs'
[IO.File]::WriteAllText($fixture, $source, [Text.Encoding]::UTF8)
$entry = Join-Path $temp 'Preview.cs'
[IO.File]::WriteAllText($entry, @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
namespace ScreenCrosshair {
public partial class MainForm {
    [STAThread] public static void PreviewMain(string[] args) {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) Theme.K = Math.Max(1f, g.DpiX / 96f);
        using (MainForm f = new MainForm()) {
            f.Opacity = 0;
            f.ShowInTaskbar = false;
            f.Show();
            for (int theme = 0; theme < 2; theme++) {
              f.SwitchTheme(theme == 1);
              for (int page = 0; page < PageCount; page++) {
                f.SelectPage(page);
                Application.DoEvents();
                using (Bitmap bitmap = new Bitmap(f.Width, f.Height)) {
                    f.DrawToBitmap(bitmap, f.ClientRectangle);
                    bitmap.Save(Path.Combine(args[0], (theme == 1 ? "light-" : "dark-") + "page-" + page + ".png"), ImageFormat.Png);
                }
              }
            }
            f._tbFovTarget.Text = "107";
            int selected = f._cbShape.SelectedIndex;
            f.SwitchTheme(false);
            f.SwitchTheme(true);
            if (f._tbFovTarget.Text != "107" || f._cbShape.SelectedIndex != selected || f._page != PageWeakNetwork)
                throw new Exception("Theme switch changed editing state or active page");
            if (f.BackColor != Theme.Window || f._tbFovTarget.BackColor != Theme.CardAlt || f._tbFovTarget.ForeColor != Theme.Text)
                throw new Exception("Theme switch left stale control colors");
            foreach (Control c in f._pgCards[1].Controls)
                if (c.Tag is Color && c.BackColor != (Color)c.Tag)
                    throw new Exception("Theme switch changed a crosshair color swatch");
            if (!f._cfg.BuildLines().Contains("LightTheme=1"))
                throw new Exception("Theme preference was not serialized");
            for (int i = 0; i < f._hkKey.Length; i++) {
                if (f._hkCtrl[i].Right > f._hkAlt[i].Left ||
                    f._hkAlt[i].Right > f._hkShift[i].Left ||
                    f._hkShift[i].Right > f._hkKey[i].Parent.Left ||
                    f._hkOn[i].Right > f._hkOn[i].Parent.Width - Theme.S(12))
                    throw new Exception("Hotkey columns overlap or overflow at row " + i);
                if (f._hkKey[i].BorderStyle != BorderStyle.None)
                    throw new Exception("Hotkey input has a native border");
            }
            Console.WriteLine("PASS theme switch preserves edits, page, swatches and preference; rendered both themes across five pages at DPI scale " + Theme.K);
        }
    }
}}
public class PreviewEntry {
    [STAThread] public static void Main(string[] args) { ScreenCrosshair.MainForm.PreviewMain(args); }
}
'@, [Text.Encoding]::UTF8)
$csc = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$output = Join-Path $temp 'Preview.exe'
$files = @(Get-ChildItem (Join-Path $root 'src') -Filter '*.cs' | Where-Object Name -ne 'MainForm.cs' | ForEach-Object FullName)
& $csc /nologo /target:exe /main:PreviewEntry /codepage:65001 /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll "/out:$output" "/win32manifest:$root/build/app.manifest" "/resource:$root/assets/brand.png,brand.png" "/resource:$root/assets/app.ico,app.ico" $fixture $entry $files
if ($LASTEXITCODE -ne 0) { throw 'Preview fixture compilation failed' }
& $output ([IO.Path]::GetFullPath($OutputDirectory))
if ($LASTEXITCODE -ne 0) { throw 'UI layout verification failed' }
