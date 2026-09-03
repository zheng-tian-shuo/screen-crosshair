using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ScreenCrosshair
{
    /// <summary>界面绘制与布局的小工具</summary>
    public static class Ui
    {
        public static GraphicsPath Round(Rectangle r, int rad)
        {
            GraphicsPath p = new GraphicsPath();
            if (rad <= 0 || r.Width <= 0 || r.Height <= 0)
            {
                p.AddRectangle(r);
                return p;
            }
            int d = rad * 2;
            if (d > r.Width) d = r.Width;
            if (d > r.Height) d = r.Height;
            p.AddArc(r.Left, r.Top, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void FillRound(Graphics g, Rectangle r, int rad, Color c)
        {
            using (GraphicsPath p = Round(r, rad))
            using (SolidBrush b = new SolidBrush(c))
                g.FillPath(b, p);
        }

        public static void DrawRound(Graphics g, Rectangle r, int rad, Color c, float w)
        {
            using (GraphicsPath p = Round(r, rad))
            using (Pen pen = new Pen(c, w))
                g.DrawPath(pen, p);
        }

        /// <summary>快速造一个标签并挂到 parent 上</summary>
        public static Label L(Control parent, string text, Font f, Color c,
            int x, int y, int w, int h)
        {
            Label l = new Label();
            l.Text = text;
            l.Font = f;
            l.ForeColor = c;
            l.BackColor = Color.Transparent;
            l.AutoSize = false;
            l.Bounds = new Rectangle(x, y, w, h);
            parent.Controls.Add(l);
            return l;
        }

        /// <summary>
        /// 深色文本框。BorderStyle.FixedSingle 那圈边是系统按浅色主题画的亮灰，
        /// 在深色卡片上比下拉框亮一截，所以跟下拉框一样：外面套一层 1px 的边框面板，
        /// 里面放一个无边框文本框。
        /// </summary>
        public static TextBox Box(Control parent, int x, int y, int w)
        {
            Panel wrap = new Panel();
            wrap.Bounds = new Rectangle(x, y, w, 24);
            wrap.BackColor = Theme.CardAlt;
            wrap.Paint += BoxFrame;
            parent.Controls.Add(wrap);

            TextBox t = new TextBox();
            t.BorderStyle = BorderStyle.None;
            t.BackColor = Theme.CardAlt;
            t.ForeColor = Theme.Text;
            t.Font = Theme.Body;
            wrap.Controls.Add(t);

            // 单行文本框的高度只认字体，Dock / Height 都按不动，只能自己摆到正中间。
            // 挂在 Resize 上是因为 Form.Scale 之后面板会变大，那时候要重新算一次。
            //
            // 还要挂 SizeChanged：Form.Scale 是先放大外框（触发 Resize，这时按新宽度摆好了里面），
            // 之后才去放大子控件，于是文本框又被乘了一遍，宽度反而超出外框。
            // 左对齐时超出的部分在右边看不见，居中之后就会把数字顶到框的右边缘。
            EventHandler fit = delegate { FitBox(wrap, t); };
            wrap.Resize += fit;
            t.SizeChanged += fit;
            fit(null, EventArgs.Empty);
            return t;
        }

        /// <summary>
        /// 填数字用的窄输入框：内容居中。左对齐时「1」会贴在框最左边，
        /// 离后面的「分」「px」隔着一大片空白，看着像没填对。
        /// </summary>
        public static TextBox NumBox(Control parent, int x, int y, int w)
        {
            TextBox t = Box(parent, x, y, w);
            t.TextAlign = HorizontalAlignment.Center;
            return t;
        }

        public static HotkeyBox Hotkey(Control parent, int x, int y, int w)
        {
            Panel wrap = new Panel();
            wrap.Bounds = new Rectangle(x, y, w, 24);
            wrap.BackColor = Theme.CardAlt;
            wrap.Paint += BoxFrame;
            parent.Controls.Add(wrap);

            HotkeyBox t = new HotkeyBox();
            wrap.Controls.Add(t);
            EventHandler fit = delegate { FitBox(wrap, t); };
            wrap.Resize += fit;
            t.SizeChanged += fit;
            fit(null, EventArgs.Empty);
            return t;
        }

        private static void BoxFrame(object sender, PaintEventArgs e)
        {
            Control c = sender as Control;
            if (c == null) return;
            using (Pen p = new Pen(Theme.Border, 1f))
                e.Graphics.DrawRectangle(p, 0, 0, c.Width - 1, c.Height - 1);
        }

        private static void FitBox(Panel wrap, TextBox t)
        {
            int pad = Theme.S(3);
            int h = t.Height;
            int top = (wrap.ClientSize.Height - h) / 2;
            if (top < 1) top = 1;
            int iw = wrap.ClientSize.Width - pad * 2;
            if (iw < 1) iw = 1;
            Rectangle want = new Rectangle(pad, top, iw, h);
            // 位置已经对了就别再赋值，不然 SizeChanged 会绕回这里死循环
            if (t.Bounds != want) t.Bounds = want;
        }

        /// <summary>深色下拉框（外面套一层边框，避开系统白边）</summary>
        public static ComboBox Combo(Control parent, int x, int y, int w)
        {
            Panel wrap = new Panel();
            wrap.Bounds = new Rectangle(x, y, w, 26);
            wrap.BackColor = Theme.Border;
            wrap.Padding = new Padding(1);
            parent.Controls.Add(wrap);

            ComboBox c = new FlatCombo();
            c.DropDownStyle = ComboBoxStyle.DropDownList;
            c.FlatStyle = FlatStyle.Flat;
            c.BackColor = Theme.CardAlt;
            c.ForeColor = Theme.Text;
            c.Font = Theme.Body;
            c.Dock = DockStyle.Fill;
            c.DrawMode = DrawMode.OwnerDrawFixed;
            c.ItemHeight = Theme.S(18);
            c.DrawItem += ComboDraw;
            wrap.Controls.Add(c);
            return c;
        }

        private static void ComboDraw(object sender, DrawItemEventArgs e)
        {
            ComboBox c = sender as ComboBox;
            if (c == null) return;
            // ComboBoxEdit 是「合起来那一格」，它拿到焦点时系统也会带 Selected 标记，
            // 照着涂金底会让没展开的下拉框整块发黄，所以只给展开后的列表行上色
            bool edit = (e.State & DrawItemState.ComboBoxEdit) == DrawItemState.ComboBoxEdit;
            bool sel = !edit &&
                (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            using (SolidBrush b = new SolidBrush(sel ? Theme.AccentDeep : Theme.CardAlt))
                e.Graphics.FillRectangle(b, e.Bounds);
            if (e.Index >= 0 && e.Index < c.Items.Count)
            {
                string s = c.Items[e.Index] == null ? "" : c.Items[e.Index].ToString();
                int pad = Theme.S(4);
                TextRenderer.DrawText(e.Graphics, s, Theme.Body,
                    new Rectangle(e.Bounds.X + pad, e.Bounds.Y, e.Bounds.Width - pad, e.Bounds.Height),
                    Theme.Text, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            }
        }
    }
}
