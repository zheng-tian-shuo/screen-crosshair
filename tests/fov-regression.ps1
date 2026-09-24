param([string]$OutputDirectory = (Join-Path $PSScriptRoot '../build/ui-preview/fov-fixes'))
# Run the actual input/button flow with isolated settings and hidden overlays.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$temp = Join-Path ([IO.Path]::GetTempPath()) ('crosshair-fov-' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($temp) | Out-Null
[IO.Directory]::CreateDirectory($OutputDirectory) | Out-Null
$source = [IO.File]::ReadAllText((Join-Path $root 'src/MainForm.cs'))
$source = $source.Replace('_cfg = AppSettings.Load();', '_cfg = new AppSettings(); _cfg.GlobalVisible = false;')
foreach ($call in @('BuildTray();', 'RebuildOverlays();', 'RegisterHotkeys();', 'RecoverWeakNetworkOnStart();', 'StartTick();')) {
    $source = $source.Replace($call, '')
}
$fixture = Join-Path $temp 'MainForm.cs'
[IO.File]::WriteAllText($fixture, $source, [Text.Encoding]::UTF8)
$csc = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$output = Join-Path $temp 'FovRegression.exe'
$files = @(Get-ChildItem (Join-Path $root 'src') -Filter '*.cs' | Where-Object Name -ne 'MainForm.cs' | ForEach-Object FullName)
& $csc /nologo /target:exe /main:FovRegressionEntry /codepage:65001 /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll "/out:$output" "/win32manifest:$root/build/app.manifest" "/resource:$root/assets/brand.png,brand.png" "/resource:$root/assets/app.ico,app.ico" $fixture (Join-Path $PSScriptRoot 'FovRegression.cs') (Join-Path $PSScriptRoot 'WindowGrabRegression.cs') $files
if ($LASTEXITCODE -ne 0) { throw 'FOV fixture compilation failed' }
& $output ([IO.Path]::GetFullPath($OutputDirectory))
if ($LASTEXITCODE -ne 0) { throw 'FOV regression failed' }
