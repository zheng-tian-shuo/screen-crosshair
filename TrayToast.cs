using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ScreenCrosshair
{
    /// <summary>
    /// 自绘的托盘提示。系统的 ShowBalloonTip 在 Win10/11 会被转成通知中心的卡片：
    /// 标题固定写 exe 文件名，左上角那个图标由资源管理器的图标缓存决定，换了图标也刷不掉。
    /// 所以这里自己画一张，跟软件同一套配色和头像，不经系统那一层。
    /// </summary>
    internal class TrayToast : Form
    {
        private const int W = 340;
        private const int H = 96;
        private const int HoldMs = 2600;

        private readonly string _title;
        private readonly string _body;
        private readonly Timer _life = new Timer();
        private int _elapsed;

        public TrayToast(string title, string body)
        {
            _title = title;
            _body = body;

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = Theme.Card;
            ForeColor = Theme.Text;
            DoubleBuffered = true;
            ClientSize = new Size(Theme.S(W), Theme.S(H));

            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            int pad = Theme.S(14);
            Location = new Point(wa.Right - Width - pad, wa.Bottom - Height - pad);

            try
            {
                using (GraphicsPath p = Ui.Round(new Rectangle(0, 0, Width, Height), Theme.S(10)))
                    Region = new Region(p);
            }
            catch { }

            _life.Interval = 40;
            _life.Tick += Beat;
            _life.Start();
        }

        /// <summary>不抢焦点、不进 Alt+Tab：游戏里弹出来也不会把人切出去</summary>
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= Native.WS_EX_NOACTIVATE | Native.WS_EX_TOOLWINDOW;
                return cp;
            }
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        private void Beat(object sender, EventArgs e)
        {
            _elapsed += _life.Interval;
            if (_elapsed <= HoldMs) return;

            double left = 1.0 - (_elapsed - HoldMs) / 420.0;
            if (left <= 0.02) { Close(); return; }
            Opacity = left;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            Close();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _life.Stop();
            _life.Dispose();
            base.OnFormClosed(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            Rectangle all = new Rectangle(0, 0, Width, Height);
            Ui.FillRound(g, all, Theme.S(10), Theme.Card);

            // 左侧一道金条，跟主界面的强调色对上
            using (GraphicsPath clip = Ui.Round(all, Theme.S(10)))
            {
                Region old = g.Clip;
                g.SetClip(clip);
                using (SolidBrush b = new SolidBrush(Theme.Accent))
                    g.FillRectangle(b, 0, 0, Theme.S(4), Height);
                g.Clip = old;
            }

            Ui.DrawRound(g, new Rectangle(0, 0, Width - 1, Height - 1),
                Theme.S(10), Theme.BorderLit, 1f);

            int ax = Theme.S(20), ay = (Height - Theme.S(40)) / 2;
            Brand.Draw(g, new Rectangle(ax, ay, Theme.S(40), Theme.S(40)));

            int tx = ax + Theme.S(52);
            int tw = Width - tx - Theme.S(16);
            TextRenderer.DrawText(g, _title, Theme.BodyBold,
                new Rectangle(tx, Theme.S(18), tw, Theme.S(20)), Theme.Text,
                TextFormatFlags.Left | TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, _body, Theme.Small,
                new Rectangle(tx, Theme.S(40), tw, Theme.S(40)), Theme.TextMuted,
                TextFormatFlags.Left | TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
        }

        /// <summary>弹一张提示，自己会淡出关掉</summary>
        public static void Pop(string title, string body)
        {
            try
            {
                TrayToast t = new TrayToast(title, body);
                t.Show();
                t.TopMost = true;
            }
            catch { }
        }
    }
}
