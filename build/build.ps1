# 一键构建：画图标 → 编译 → 在上一层目录放一个便携 exe
# 直接双击 build.bat 也是走这个脚本。

$ErrorActionPreference = 'Stop'
$buildDir = $PSScriptRoot
$root = Split-Path $buildDir -Parent
$srcDir = Join-Path $root 'src'
$assetsDir = Join-Path $root 'assets'

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc))
{
    $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path $csc))
{
    throw '找不到 csc.exe，本机缺少 .NET Framework 4.x'
}

Write-Host '[1/3] 生成 icon.ico'
& (Join-Path $buildDir 'make_icon.ps1')

Write-Host '[2/3] 编译'
$exe = Join-Path $buildDir 'ScreenCrosshair.exe'
$src = @(Get-ChildItem -Path $srcDir -Filter '*.cs' | ForEach-Object { $_.FullName })
if ($src.Count -eq 0) { throw '目录里没有 .cs 源文件' }

# /codepage:65001 —— 源码是 UTF-8，不加这个中文字符串会被按 GBK 读成乱码
# brand.png / app.ico 作为嵌入资源打进 exe：标题栏头像、窗口图标、托盘图标都从 exe 内部取，
# 不额外带图片文件，也不受 Windows 图标缓存影响
$cscArgs = @(
    '/nologo'
    '/target:winexe'
    '/platform:anycpu'
    '/optimize+'
    '/codepage:65001'
    ('/win32icon:' + (Join-Path $assetsDir 'icon.ico'))
    ('/win32manifest:' + (Join-Path $buildDir 'app.manifest'))
    ('/resource:' + (Join-Path $assetsDir 'brand.png') + ',brand.png')
    ('/resource:' + (Join-Path $assetsDir 'app.ico') + ',app.ico')
    '/r:System.dll'
    '/r:System.Drawing.dll'
    '/r:System.Windows.Forms.dll'
    ('/out:' + $exe)
) + $src

& $csc $cscArgs
if ($LASTEXITCODE -ne 0) { throw '编译失败，看上面的报错' }

Write-Host '[3/3] 复制便携版'
# 文件名和窗口标题保持一致，都叫「得吃准星 v5.0」
$final = Join-Path (Split-Path $root -Parent) '得吃准星 v5.0.exe'
Copy-Item $exe $final -Force
# 改过名的旧包留着只会让人分不清哪个是新的（v1 的「屏幕准星.exe」不在这个清单里，不会被删）
$olds = @('屏幕准星 v3.exe', '屏幕准星 v3.0.exe', '得吃准星 v3.0.exe')
foreach ($o in $olds)
{
    $p = Join-Path (Split-Path $root -Parent) $o
    if (-not (Test-Path $p)) { continue }
    # 旧版还开着的时候删不掉，这不算编译失败，提一句就行
    try { Remove-Item $p -Force }
    catch { Write-Host ('  旧包删不掉（可能还在运行）：' + $o) }
}

$kb = [Math]::Round((Get-Item $final).Length / 1024.0, 1)
Write-Host ''
Write-Host ('完成：' + $final + '  (' + $kb + ' KB)')
Write-Host '单文件，拷走就能用，设置存在 exe 旁边的 settings.ini'
