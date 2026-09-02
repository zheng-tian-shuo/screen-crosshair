using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ScreenCrosshair
{
    /// <summary>
    /// 屏幕取点：先把当前画面截下来铺满全部显示器，用户在截图上点一下爆点，
    /// 就得到精确的屏幕坐标。比盲拖窗口准得多。
    /// </summary>
    public class PickerForm : Form
    {
        public Point Picked = Point.Empty;
        public bool Ok;

        private Bitmap _shot;
        private Point _m = new Point(-1, -1);

        public PickerForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = SystemInformation.VirtualScreen;
            ShowInTaskbar = false;
            TopMost = true;
            DoubleBuffered = true;
            KeyPreview = true;
            Cursor = Cursors.Cross;
            BackColor = Color.Black;

            try
            {
                _shot = new Bitmap(Width, Height);
                using (Graphics g = Graphics.FromImage(_shot))
                    g.CopyFromScreen(Bounds.Left, Bounds.Top, 0, 0,
                        new Size(Width, Height), CopyPixelOperation.SourceCopy);
            }
            catch
            {
                // 某些独占全屏抓不到画面，退回半透明蒙层，仍然能点
                if (_shot != null) { _shot.Dispose(); _shot = null; }
                Opacity = 0.35;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            _m = e.Location;
            Invalidate();
            base.OnMouseMove(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                Picked = PointToScreen(e.Location);
                Ok = true;
            }
            Close();
            base.OnMouseDown(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) Close();
            base.OnKeyDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            if (_shot != null)
            {
                g.DrawImageUnscaled(_shot, 0, 0);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(90, 0, 0, 0)))
                    g.FillRectangle(b, ClientRectangle);
            }

            if (_m.X < 0) return;

            // 全屏十字线 + 中心小方框，方便对准爆点
            using (Pen p = new Pen(Color.FromArgb(200, Theme.Accent), 1f))
            {
                p.DashStyle = DashStyle.Dash;
                g.DrawLine(p, 0, _m.Y, Width, _m.Y);
                g.DrawLine(p, _m.X, 0, _m.X, Height);
            }
            using (Pen p = new Pen(Color.FromArgb(230, Theme.Accent), 1f))
                g.DrawRectangle(p, _m.X - 6, _m.Y - 6, 12, 12);

            Point sp = PointToScreen(_m);
            Screen sc = Screen.FromPoint(sp);
            string txt = "屏幕内坐标  X " + (sp.X - sc.Bounds.Left) +
                         "   Y " + (sp.Y - sc.Bounds.Top) +
                         "        左键确定 · Esc 取消";
            Size sz = TextRenderer.MeasureText(txt, Theme.BodyBold);
            int bx = _m.X + 16, by = _m.Y + 16;
            if (bx + sz.Width + 20 > Width) bx = _m.X - sz.Width - 36;
            if (by + sz.Height + 16 > Height) by = _m.Y - sz.Height - 32;

            Rectangle box = new Rectangle(bx, by, sz.Width + 20, sz.Height + 14);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(235, 18, 20, 25)))
                g.FillRectangle(b, box);
            using (Pen p = new Pen(Theme.Accent, 1f))
                g.DrawRectangle(p, box);
            TextRenderer.DrawText(g, txt, Theme.BodyBold,
                new Point(box.X + 10, box.Y + 7), Theme.Text);
        }

        protected override void Dispose(bool disposing)
        {
            if (_shot != null) { _shot.Dispose(); _shot = null; }
            base.Dispose(disposing);
        }
    }
}
