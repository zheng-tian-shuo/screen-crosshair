# Builds icon.ico and brand.png from brand_src.jpg (the artwork the user supplied).
# The source is a white-background JPEG, so the white is flood-filled away from the borders
# instead of being thresholded globally -- the eyes and the helmet are pure white too and a
# global threshold would punch holes right through them.
# ASCII only (BOM-less files are read as ANSI by PowerShell 5.1).

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$buildDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$assetsDir = Join-Path (Split-Path $buildDir -Parent) 'assets'
$src = Join-Path $assetsDir 'brand_src.jpg'
$icoPath = Join-Path $assetsDir 'icon.ico'
$appIcoPath = Join-Path $assetsDir 'app.ico'
$brandPath = Join-Path $assetsDir 'brand.png'

Add-Type -ReferencedAssemblies 'System.Drawing' -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
public class Keyer {
  // Flood fill the white paper from every border pixel; only what the outside can reach
  // becomes transparent, so enclosed white (eyes, helmet) survives.
  public static Bitmap KeyOut(string path, int th) {
    Bitmap src = new Bitmap(path);
    Bitmap bmp = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb);
    using (Graphics g = Graphics.FromImage(bmp)) g.DrawImage(src, 0, 0, src.Width, src.Height);
    src.Dispose();

    int w = bmp.Width, h = bmp.Height;
    Rectangle rc = new Rectangle(0, 0, w, h);
    BitmapData bd = bmp.LockBits(rc, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
    int stride = bd.Stride;
    byte[] px = new byte[stride * h];
    Marshal.Copy(bd.Scan0, px, 0, px.Length);

    bool[] seen = new bool[w * h];
    Stack<int> st = new Stack<int>();
    for (int x = 0; x < w; x++) { st.Push(x); st.Push((h - 1) * w + x); }
    for (int y = 0; y < h; y++) { st.Push(y * w); st.Push(y * w + w - 1); }

    while (st.Count > 0) {
      int id = st.Pop();
      if (id < 0 || id >= w * h || seen[id]) continue;
      int x = id % w, y = id / w;
      int o = y * stride + x * 4;
      if (px[o] < th || px[o + 1] < th || px[o + 2] < th) continue;  // B,G,R
      seen[id] = true;
      px[o + 3] = 0;
      if (x > 0) st.Push(id - 1);
      if (x < w - 1) st.Push(id + 1);
      if (y > 0) st.Push(id - w);
      if (y < h - 1) st.Push(id + w);
    }

    // The JPEG edge is a white-ish gradient; fade the pixels that still touch transparency
    // so the cut-out does not keep a white halo on a dark background.
    byte[] copy = (byte[])px.Clone();
    for (int y = 1; y < h - 1; y++) {
      for (int x = 1; x < w - 1; x++) {
        int id = y * w + x;
        if (seen[id]) continue;
        if (!(seen[id - 1] || seen[id + 1] || seen[id - w] || seen[id + w])) continue;
        int o = y * stride + x * 4;
        int lum = (copy[o] * 114 + copy[o + 1] * 587 + copy[o + 2] * 299) / 1000;
        if (lum > 205) px[o + 3] = (byte)(255 - Math.Min(255, (lum - 205) * 5));
      }
    }

    Marshal.Copy(px, 0, bd.Scan0, px.Length);
    bmp.UnlockBits(bd);
    return bmp;
  }

  // Tight bounding box of everything still opaque, so the mark can be centred properly.
  public static Rectangle Solid(Bitmap b, int aMin) {
    int x0 = b.Width, y0 = b.Height, x1 = -1, y1 = -1;
    BitmapData bd = b.LockBits(new Rectangle(0, 0, b.Width, b.Height),
      ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
    byte[] px = new byte[bd.Stride * b.Height];
    Marshal.Copy(bd.Scan0, px, 0, px.Length);
    b.UnlockBits(bd);
    for (int y = 0; y < b.Height; y++)
      for (int x = 0; x < b.Width; x++)
        if (px[y * bd.Stride + x * 4 + 3] > aMin) {
          if (x < x0) x0 = x;
          if (y < y0) y0 = y;
          if (x > x1) x1 = x;
          if (y > y1) y1 = y;
        }
    if (x1 < 0) return new Rectangle(0, 0, b.Width, b.Height);
    return Rectangle.FromLTRB(x0, y0, x1 + 1, y1 + 1);
  }

  // The artwork is drawn with a heavy black outline, which disappears against a dark
  // taskbar. Dilating the alpha into a white rim keeps the silhouette readable on any
  // background and matches the sticker look of the original.
  public static Bitmap Rim(Bitmap src, int r) {
    int w = src.Width, h = src.Height;
    Rectangle rc = new Rectangle(0, 0, w, h);
    BitmapData sd = src.LockBits(rc, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
    int stride = sd.Stride;
    byte[] sp = new byte[stride * h];
    Marshal.Copy(sd.Scan0, sp, 0, sp.Length);
    src.UnlockBits(sd);

    byte[] dp = new byte[stride * h];
    for (int y = 0; y < h; y++) {
      for (int x = 0; x < w; x++) {
        int o = y * stride + x * 4;
        if (sp[o + 3] > 24) continue;
        int best = 0;
        for (int dy = -r; dy <= r && best < 255; dy++) {
          int yy = y + dy;
          if (yy < 0 || yy >= h) continue;
          for (int dx = -r; dx <= r; dx++) {
            int xx = x + dx;
            if (xx < 0 || xx >= w) continue;
            if (dx * dx + dy * dy > r * r) continue;
            int a = sp[yy * stride + xx * 4 + 3];
            if (a > best) best = a;
          }
        }
        if (best > 24) {
          dp[o] = 255; dp[o + 1] = 255; dp[o + 2] = 255;
          dp[o + 3] = (byte)best;
        }
      }
    }

    Bitmap outb = new Bitmap(w, h, PixelFormat.Format32bppArgb);
    BitmapData od = outb.LockBits(rc, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
    Marshal.Copy(dp, 0, od.Scan0, dp.Length);
    outb.UnlockBits(od);
    using (Graphics g = Graphics.FromImage(outb)) g.DrawImage(src, 0, 0);
    return outb;
  }

  // One ICO entry as a 32bpp bottom-up DIB: BITMAPINFOHEADER with a doubled height,
  // BGRA rows, then an all-zero AND mask (4-byte aligned rows).
  // PNG-compressed entries are deliberately not used: System.Drawing.Icon cannot decode
  // them and renders colour noise instead, and the app itself loads its icon through it.
  public static byte[] Dib(Bitmap b) {
    int w = b.Width, h = b.Height;
    BitmapData bd = b.LockBits(new Rectangle(0, 0, w, h),
      ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
    byte[] px = new byte[bd.Stride * h];
    Marshal.Copy(bd.Scan0, px, 0, px.Length);
    b.UnlockBits(bd);

    int maskStride = ((w + 31) / 32) * 4;
    byte[] outb = new byte[40 + w * h * 4 + maskStride * h];
    int o = 0;
    Put32(outb, ref o, 40);
    Put32(outb, ref o, w);
    Put32(outb, ref o, h * 2);
    Put16(outb, ref o, 1);
    Put16(outb, ref o, 32);
    for (int i = 0; i < 6; i++) Put32(outb, ref o, 0);
    for (int y = h - 1; y >= 0; y--) {
      int src = y * bd.Stride;
      Array.Copy(px, src, outb, o, w * 4);
      o += w * 4;
    }
    return outb;
  }

  static void Put16(byte[] a, ref int o, int v) {
    a[o++] = (byte)(v & 0xFF); a[o++] = (byte)((v >> 8) & 0xFF);
  }
  static void Put32(byte[] a, ref int o, int v) {
    a[o++] = (byte)(v & 0xFF); a[o++] = (byte)((v >> 8) & 0xFF);
    a[o++] = (byte)((v >> 16) & 0xFF); a[o++] = (byte)((v >> 24) & 0xFF);
  }
}
'@

Write-Host 'keying out the white background...'
$art = [Keyer]::KeyOut($src, 238)
$box = [Keyer]::Solid($art, 8)
Write-Host ('art ' + $art.Width + 'x' + $art.Height + '  solid ' + $box.Width + 'x' + $box.Height + ' at ' + $box.X + ',' + $box.Y)

# Square canvas of side n, the given source rect scaled to fill 'fill' of it, centred.
function Get-Square {
    param([System.Drawing.Bitmap]$Src, [System.Drawing.Rectangle]$Box, [int]$N, [double]$Fill)
    $bmp = New-Object System.Drawing.Bitmap($N, $N, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $side = [Math]::Max($Box.Width, $Box.Height)
    $scale = ($N * $Fill) / $side
    $w = [int][Math]::Round($Box.Width * $scale)
    $h = [int][Math]::Round($Box.Height * $scale)
    $dst = New-Object System.Drawing.Rectangle([int](($N - $w) / 2), [int](($N - $h) / 2), $w, $h)
    $g.DrawImage($Src, $dst, $Box.X, $Box.Y, $Box.Width, $Box.Height,
        [System.Drawing.GraphicsUnit]::Pixel)
    $g.Dispose()
    return $bmp
}

# Head crop: at icon sizes the full figure turns to mush, so small entries show the face only.
# Numbers are read off the 1256x1238 artwork.
$head = New-Object System.Drawing.Rectangle(275, 150, 745, 745)
# 16-20px cannot carry the hood and the ear cups at all; those sizes get just the gold skull.
$face = New-Object System.Drawing.Rectangle(290, 300, 520, 520)

# The in-app logo sits on a dark title bar, so it gets the same white rim as the icon.
# It is only ever drawn at 36-40px (title bar, tray toast), so it uses the head crop too --
# the whole figure at that size is an unreadable pale smudge.
$flatBrand = Get-Square -Src $art -Box $head -N 256 -Fill 0.94
$brand = [Keyer]::Rim($flatBrand, 10)
$flatBrand.Dispose()
$brand.Save($brandPath, [System.Drawing.Imaging.ImageFormat]::Png)
Write-Host ('brand.png written')

# Downscale with a good filter; used to render each entry at up to 4x and shrink it down.
function Get-Shrunk {
    param([System.Drawing.Bitmap]$Src, [int]$N)
    if ($Src.Width -eq $N) { return $Src }
    $bmp = New-Object System.Drawing.Bitmap($N, $N, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $g.DrawImage($Src, (New-Object System.Drawing.Rectangle(0, 0, $N, $N)), 0, 0,
        $Src.Width, $Src.Height, [System.Drawing.GraphicsUnit]::Pixel)
    $g.Dispose()
    $Src.Dispose()
    return $bmp
}

# Every entry is a 32bpp DIB, never a PNG-compressed one: System.Drawing.Icon cannot decode
# PNG entries and the app loads this very file for its window and tray icon.
function Write-Ico {
    param([int[]]$Sizes, [string]$Path)
    $payloads = @()
    foreach ($n in $Sizes) {
        $srcBox = $box
        $fill = 0.96
        if ($n -le 48) { $srcBox = $head; $fill = 0.92 }
        if ($n -le 20) { $srcBox = $face; $fill = 0.94 }
        # Rimming straight at the target size turns small entries into a grey blob: render at
        # up to 4x, dilate the rim there, then shrink -- edges come out cleanly antialiased.
        # 256 is already big enough to rim in place (the dilation loop is O(m^2 * r^2)).
        $ss = [Math]::Max(1, [Math]::Min(4, [int][Math]::Floor(256.0 / $n)))
        $m = $n * $ss
        $r = [int][Math]::Max(1, [Math]::Round($m / 26.0))
        Write-Host ('  entry ' + $n + ' (render ' + $m + ', rim ' + $r + ')')
        $flat = Get-Square -Src $art -Box $srcBox -N $m -Fill $fill
        $ring = [Keyer]::Rim($flat, $r)
        $flat.Dispose()
        $ring = Get-Shrunk -Src $ring -N $n
        $payloads += , ([byte[]][Keyer]::Dib($ring))
        $ring.Dispose()
    }

    $fs = New-Object System.IO.FileStream($Path, [System.IO.FileMode]::Create)
    $bw = New-Object System.IO.BinaryWriter($fs)
    $bw.Write([uint16]0)
    $bw.Write([uint16]1)
    $bw.Write([uint16]$Sizes.Count)
    $offset = 6 + 16 * $Sizes.Count
    for ($i = 0; $i -lt $Sizes.Count; $i++) {
        $n = $Sizes[$i]
        $dim = $n
        if ($n -ge 256) { $dim = 0 }
        $bw.Write([byte]$dim); $bw.Write([byte]$dim)
        $bw.Write([byte]0); $bw.Write([byte]0)
        $bw.Write([uint16]1); $bw.Write([uint16]32)
        $bw.Write([uint32]$payloads[$i].Length)
        $bw.Write([uint32]$offset)
        $offset += $payloads[$i].Length
    }
    foreach ($p in $payloads) { $bw.Write($p) }
    $bw.Flush(); $bw.Dispose(); $fs.Dispose()
    Write-Host ((Split-Path $Path -Leaf) + ' written, ' + $Sizes.Count + ' sizes, ' +
        [Math]::Round((Get-Item $Path).Length / 1024.0, 1) + ' KB')
}

# The shell icon: all the way up to 256 for large views in Explorer.
Write-Ico -Sizes @(16, 20, 24, 32, 48, 64, 96, 128, 256) -Path $icoPath
# Embedded copy for Form.Icon / NotifyIcon.Icon. Kept to the sizes those two actually ask for
# (16..64 covers 100%-400% DPI) so the exe does not carry the 256px entry twice.
Write-Ico -Sizes @(16, 20, 24, 32, 48, 64) -Path $appIcoPath

$brand.Dispose()
$art.Dispose()

