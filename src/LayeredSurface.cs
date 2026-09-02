using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace ScreenCrosshair
{
    /// <summary>
    /// 分层窗口的画布：一块 32 位预乘 alpha 的 DIB，画好之后直接交给 UpdateLayeredWindow。
    /// 准星和倒计时面板都要逐像素半透明（洋红抠色会啃掉抗锯齿边缘），
    /// 申请 / 释放 DIB 这套活儿两边一模一样，所以抽出来共用。
    /// </summary>
    internal sealed class LayeredSurface : IDisposable
    {
        private IntPtr _hBitmap = IntPtr.Zero;
        private IntPtr _hOldBmp = IntPtr.Zero;
        private IntPtr _memDc = IntPtr.Zero;
        private Bitmap _bmp;              // 直接架在 DIB 内存上，省一次拷贝
        private int _w, _h;

        /// <summary>要一块 w×h 的画布。内容不保留，调用方自己 Clear。拿不到就返回 null。</summary>
        public Bitmap Begin(int w, int h)
        {
            if (w <= 0 || h <= 0) return null;
            if (_bmp != null && _w == w && _h == h) return _bmp;
            Release();

            Native.BITMAPINFOHEADER bi = new Native.BITMAPINFOHEADER();
            bi.biSize = (uint)Marshal.SizeOf(typeof(Native.BITMAPINFOHEADER));
            bi.biWidth = w;
            bi.biHeight = -h;              // 负数 = 自上而下，和 GDI+ 的行序一致
            bi.biPlanes = 1;
            bi.biBitCount = 32;
            bi.biCompression = Native.BI_RGB;

            IntPtr screenDc = Native.GetDC(IntPtr.Zero);
            try
            {
                IntPtr bits;
                _hBitmap = Native.CreateDIBSection(screenDc, ref bi,
                    Native.DIB_RGB_COLORS, out bits, IntPtr.Zero, 0);
                if (_hBitmap == IntPtr.Zero || bits == IntPtr.Zero) return null;

                _memDc = Native.CreateCompatibleDC(screenDc);
                _hOldBmp = Native.SelectObject(_memDc, _hBitmap);
                // PArgb = 预乘 alpha，UpdateLayeredWindow 要求的就是这个格式
                _bmp = new Bitmap(w, h, w * 4, PixelFormat.Format32bppPArgb, bits);
                _w = w;
                _h = h;
                return _bmp;
            }
            catch { Release(); return null; }
            finally { Native.ReleaseDC(IntPtr.Zero, screenDc); }
        }

        /// <summary>把画好的内容推给系统，同时告诉它窗口该在哪</summary>
        public void Push(IntPtr hwnd, int x, int y)
        {
            if (_bmp == null || _memDc == IntPtr.Zero) return;
            IntPtr screenDc = Native.GetDC(IntPtr.Zero);
            try
            {
                Native.POINT dst = new Native.POINT(x, y);
                Native.SIZE size = new Native.SIZE(_w, _h);
                Native.POINT src = new Native.POINT(0, 0);
                Native.BLENDFUNCTION bf = new Native.BLENDFUNCTION();
                bf.BlendOp = Native.AC_SRC_OVER;
                bf.BlendFlags = 0;
                bf.SourceConstantAlpha = 255;
                bf.AlphaFormat = Native.AC_SRC_ALPHA;
                Native.UpdateLayeredWindow(hwnd, screenDc, ref dst, ref size,
                    _memDc, ref src, 0, ref bf, Native.ULW_ALPHA);
            }
            finally { Native.ReleaseDC(IntPtr.Zero, screenDc); }
        }

        private void Release()
        {
            if (_bmp != null) { _bmp.Dispose(); _bmp = null; }
            if (_memDc != IntPtr.Zero)
            {
                if (_hOldBmp != IntPtr.Zero) Native.SelectObject(_memDc, _hOldBmp);
                Native.DeleteDC(_memDc);
                _memDc = IntPtr.Zero;
                _hOldBmp = IntPtr.Zero;
            }
            if (_hBitmap != IntPtr.Zero)
            {
                Native.DeleteObject(_hBitmap);
                _hBitmap = IntPtr.Zero;
            }
            _w = 0;
            _h = 0;
        }

        public void Dispose()
        {
            Release();
        }
    }
}
