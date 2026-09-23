# No app startup, live settings writes, or network changes.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$exe = Get-ChildItem (Join-Path $root 'build') -Filter '* v6.*.exe' |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
$assembly = [Reflection.Assembly]::LoadFile($exe.FullName)
$source = @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using ScreenCrosshair;

public class SettingsPositionRegression
{
    const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Check(bool ok, string message)
    {
        if (!ok) throw new Exception(message);
        Console.WriteLine("PASS " + message);
    }
    static Point Convert(CrosshairItemSettings item, Size size, double fov)
    {
        return (Point)typeof(MainForm).GetMethod("ConvertFovCoordinates",
            BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { item, size, fov });
    }
    static void Read(AppSettings cfg, string[] lines)
    {
        typeof(AppSettings).GetMethod("ReadFrom", Instance).Invoke(cfg, new object[] { lines });
    }
    public static void Run()
    {
        CrosshairItemSettings item = new CrosshairItemSettings();
        item.FovReferenceWidth = 1920;
        item.FovReferenceHeight = 1080;
        item.FovReferenceX = 1200;
        item.FovReferenceY = 600;
        Check(Convert(item, new Size(2560, 1440), 90) == new Point(1600, 800),
            "resolution change scales around the recorded center");
        Check(Convert(item, new Size(1920, 1080), 90) == new Point(1200, 600),
            "same FOV preserves the original point");
        Check(Convert(item, new Size(1920, 1080), 120) == new Point(1099, 575),
            "wider FOV moves the point toward center");
        Check(Convert(item, new Size(1920, 1200), 90) == new Point(1200, 660),
            "different aspect ratio converts vertical offset independently");
        Check(Convert(item, new Size(2560, 1080), 90) == new Point(1600, 620),
            "ultrawide aspect ratio preserves horizontal FOV projection");
        item.FovReferenceX = 960;
        item.FovReferenceY = 540;
        Check(Convert(item, new Size(2560, 1440), 120) == new Point(1280, 720),
            "center stays centered after resolution and FOV changes");
        item.FovReferenceWidth = item.FovReferenceHeight = 0;
        Check(Convert(item, new Size(1920, 1080), 90) == new Point(960, 540),
            "legacy reference without resolution remains compatible");

        AppSettings cfg = new AppSettings();
        cfg.Items[0].Shape = CrosshairShape.Dot;
        cfg.Items[0].TickCount = 17;
        cfg.Items[0].LabelStart = 99999;
        cfg.Items[0].LabelStep = 45678;
        cfg.WeakIndicatorOpacity = 42;
        AppSettings loaded = new AppSettings();
        Read(loaded, cfg.BuildLines().ToArray());
        Check(loaded.Items[0].TickCount == 17 && loaded.Items[0].LabelStart == 99999 &&
            loaded.Items[0].LabelStep == 45678, "hidden tick settings and five-digit labels survive reload");
        Check(loaded.WeakIndicatorOpacity == 42, "weak status indicator opacity survives reload");
        foreach (string invalid in new string[] { "NaN", "Infinity", "-Infinity" })
        {
            Read(loaded, new string[] { "[app]", "ProfileCount=1", "[profile0]", "Count=1",
                "0.FovReference=" + invalid });
            Check(loaded.Items[0].FovReference == 90, "invalid FOV falls back: " + invalid);
        }
        string file = Path.GetTempFileName();
        try
        {
            cfg.WeakActive = true;
            cfg.WeakPolicyName = "foreign-policy";
            cfg.WeakMtuRecords = "9|1500";
            Check(cfg.ExportTo(file), "export succeeds");
            Check(loaded.ImportFrom(file) && !loaded.WeakActive && loaded.WeakPolicyName == "" &&
                loaded.WeakMtuRecords == "", "import never adopts foreign network recovery state");
            loaded.WeakActive = true;
            loaded.WeakPolicyName = "local-policy";
            loaded.WeakMtuRecords = "7|1400";
            Check(loaded.ImportFrom(file) && loaded.WeakActive && loaded.WeakPolicyName == "local-policy" &&
                loaded.WeakMtuRecords == "7|1400", "import preserves local recovery state");
        }
        finally { File.Delete(file); }

        MainForm main = (MainForm)FormatterServices.GetUninitializedObject(typeof(MainForm));
        cfg = new AppSettings();
        cfg.GlobalVisible = false;
        cfg.Items.Add(new CrosshairItemSettings());
        List<OverlayForm> overlays = new List<OverlayForm>();
        typeof(MainForm).GetField("_cfg", Instance).SetValue(main, cfg);
        typeof(MainForm).GetField("_ovl", Instance).SetValue(main, overlays);
        typeof(MainForm).GetField("_dragMode", Instance).SetValue(main, true);
        try
        {
            typeof(MainForm).GetMethod("RebuildOverlays", Instance).Invoke(main, null);
            Check(overlays.Count == 2 && (bool)typeof(OverlayForm).GetField("_dragMode", Instance)
                .GetValue(overlays[1]), "rebuilt overlays retain drag mode");
            typeof(MainForm).GetMethod("OverlayMoved", Instance).Invoke(main,
                new object[] { overlays[1], EventArgs.Empty });
            Check((bool)typeof(AppSettings).GetField("_savePending", Instance).GetValue(cfg),
                "moving an unselected overlay marks settings dirty");
        }
        finally { foreach (OverlayForm overlay in overlays) overlay.Dispose(); }
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
    $result.CompiledAssembly.GetType('SettingsPositionRegression').GetMethod('Run').Invoke($null, @())
} finally { $compiler.Dispose() }
