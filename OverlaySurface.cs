using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ScreenCrosshair
{
    public partial class OverlayForm
    {
        private readonly LayeredSurface _surf = new LayeredSurface();

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // 分层窗口的内容全部由 UpdateLayeredWindow 提供，这里什么都不用画
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Redraw();
        }

        /// <summary>重画准星并推给系统。每次设置变化 / 尺寸变化时调用。</summary>
        public void Redraw()
        {
            if (!IsHandleCreated || IsDisposed) return;
            int w = Width, h = Height;
            Bitmap bmp = _surf.Begin(w, h);
            if (bmp == null) return;

            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                if (_dragMode) DrawDragHint(g, w, h);
                Painter.Draw(g, _s, w / 2f, h / 2f);
            }
            _surf.Push(Handle, Left, Top);
        }

        /// <summary>拖动定位时给一层淡底 + 虚线框：既让用户看见可抓范围，
        /// 也保证像素 alpha 不为 0——分层窗口上全透明的像素是抓不住的。</summary>
        private void DrawDragHint(Graphics g, int w, int h)
        {
            using (SolidBrush b = new SolidBrush(Color.FromArgb(30, Theme.Accent)))
                g.FillRectangle(b, 0, 0, w, h);
            using (Pen p = new Pen(Color.FromArgb(170, Theme.Accent), 1f))
            {
                p.DashStyle = DashStyle.Dash;
                g.DrawRectangle(p, 0.5f, 0.5f, w - 1.5f, h - 1.5f);
            }
        }

        protected override void Dispose(bool disposing)
        {
            _surf.Dispose();
            base.Dispose(disposing);
        }
    }
}
