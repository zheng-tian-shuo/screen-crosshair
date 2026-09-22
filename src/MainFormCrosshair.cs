using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace ScreenCrosshair
{
    public partial class MainForm
    {
        private ComboBox _cbItem;
        private Chk _chkVisible;
        private Label _lblItemCount;
        private ComboBox _cbShape;
        private FlatBtn _btnColor;
        private Label _lblHex;
        private Slider _sSize, _sThick, _sOpacity;
        private Label _vSize, _vThick, _vOpacity;
        private Chk _chkGlow;

        /// <summary>
        /// 「准星」页从上到下的卡片。分划板那张会随形状显隐，下面的卡片得跟着上下挪，
        /// 所以纵坐标不能像别的页那样写死，统一由 ReflowCrosshairPage 算。
        /// </summary>
        private readonly List<Card> _pgCards = new List<Card>();
        private bool _tickShown = true;

        private static readonly Color[] Presets =
        {
            Color.FromArgb(0, 229, 255), Color.FromArgb(57, 255, 20),
            Color.FromArgb(255, 0, 229), Color.FromArgb(255, 225, 0),
            Color.FromArgb(255, 59, 48), Color.FromArgb(255, 255, 255),
            Color.FromArgb(255, 138, 0), Color.FromArgb(179, 136, 255)
        };

        private void BuildPageCrosshair()
        {
            Panel pg = _pages[PageCrosshair];

            // ---- 准星列表 ----
            Card c1 = NewCard(pg, "准星", 14, 106);
            _pgCards.Add(c1);
            Ui.L(c1, "当前", Theme.Body, Theme.TextMuted, 14, 42, 50, 20);
            _cbItem = Ui.Combo(c1, 68, 39, 148);
            _cbItem.DropDownWidth = Theme.S(320);
            _cbItem.SelectedIndexChanged += ItemPicked;

            FlatBtn add = new FlatBtn();
            add.Text = "新建";
            add.Bounds = new Rectangle(220, 39, 50, 26);
            add.Click += delegate { AddItem(); };
            c1.Controls.Add(add);

            FlatBtn dup = new FlatBtn();
            dup.Text = "复制";
            dup.Bounds = new Rectangle(274, 39, 50, 26);
            dup.Click += delegate { DupItem(); };
            c1.Controls.Add(dup);

            FlatBtn rename = new FlatBtn();
            rename.Text = "改名";
            rename.Bounds = new Rectangle(328, 39, 50, 26);
            rename.Click += delegate { RenameItem(); };
            c1.Controls.Add(rename);

            FlatBtn del = new FlatBtn();
            del.Kind = 2;
            del.Text = "删除";
            del.Bounds = new Rectangle(382, 39, 58, 26);
            del.Click += delegate { DelItem(); };
            c1.Controls.Add(del);

            _chkVisible = new Chk();
            _chkVisible.Text = "显示这个准星";
            _chkVisible.Bounds = new Rectangle(14, 72, 160, 22);
            _chkVisible.CheckedChanged += UiChanged;
            c1.Controls.Add(_chkVisible);

            _lblItemCount = Ui.L(c1, "", Theme.Small, Theme.TextFaint, 190, 74, 250, 20);

            // ---- 外观 ----
            Card c2 = NewCard(pg, "外观", 132, 252);
            _pgCards.Add(c2);
            Ui.L(c2, "形状", Theme.Body, Theme.TextMuted, 14, 44, 50, 20);
            _cbShape = Ui.Combo(c2, 68, 41, 190);
            for (int i = 0; i < Shapes.Names.Length; i++) _cbShape.Items.Add(Shapes.Names[i]);
            _cbShape.SelectedIndexChanged += UiChanged;

            Ui.L(c2, "颜色", Theme.Body, Theme.TextMuted, 14, 82, 50, 20);
            _btnColor = new FlatBtn();
            _btnColor.Text = "取色";
            _btnColor.Bounds = new Rectangle(68, 79, 54, 24);
            _btnColor.Click += delegate { PickColor(); };
            c2.Controls.Add(_btnColor);

            for (int i = 0; i < Presets.Length; i++)
            {
                Panel sw = new Panel();
                sw.Bounds = new Rectangle(132 + i * 26, 80, 22, 22);
                sw.BackColor = Presets[i];
                sw.Cursor = Cursors.Hand;
                sw.Tag = Presets[i];
                sw.Paint += delegate(object sender, PaintEventArgs e)
                {
                    if (!Theme.IsLight) return;
                    Control chip = (Control)sender;
                    using (Pen border = new Pen(Theme.BorderLit))
                        e.Graphics.DrawRectangle(border, 0, 0, chip.Width - 1, chip.Height - 1);
                };
                sw.Click += SwatchClick;
                c2.Controls.Add(sw);
            }
            _lblHex = Ui.L(c2, "", Theme.Mono, Theme.TextMuted, 348, 82, 92, 20);

            _sSize = SliderRow(c2, "大小", 118, 4, 300, UiChanged, out _vSize);
            _sThick = SliderRow(c2, "线宽", 152, 1, 12, UiChanged, out _vThick);
            _sOpacity = SliderRow(c2, "透明度", 186, 10, 100, UiChanged, out _vOpacity);

            _chkGlow = new Chk();
            _chkGlow.Text = "暗色外发光（亮背景上更清楚，推荐开）";
            _chkGlow.Bounds = new Rectangle(14, 218, 420, 22);
            _chkGlow.CheckedChanged += UiChanged;
            c2.Controls.Add(_chkGlow);

            BuildTickCard(pg);
        }

        /// <summary>
        /// 按 _pgCards 的顺序从上往下重排「准星」页的卡片，跳过当前不该显示的那张。
        /// 这里不能拿 Card.Visible 当判据：页面自己没显示时，子控件的 Visible 一律
        /// 读回 false，会把整页卡片全叠到顶上。所以显隐状态另存一个 _tickShown。
        ///
        /// 还得先把滚动条归零再摆：AutoScroll 面板里子控件的 Top 是「滚动之后」的坐标，
        /// 页面往下滚了 300 px 时写 Top = 21，卡片的真实位置其实是 321。等用户滚回顶部，
        /// 这 300 px 就成了页面上方一大块空白——改个形状就能触发，很容易撞上。
        /// </summary>
        private void ReflowCrosshairPage()
        {
            Panel pg = _pages[PageCrosshair];
            Point keep = pg.AutoScrollPosition;   // 读出来是负值，回填时要取反
            pg.AutoScrollPosition = Point.Empty;

            int y = Theme.S(14);
            int gap = Theme.S(16);
            for (int i = 0; i < _pgCards.Count; i++)
            {
                Card c = _pgCards[i];
                if (c == _cardTicks && !_tickShown) continue;
                if (c.Top != y) c.Top = y;
                y += c.Height + gap;
            }

            pg.AutoScrollPosition = new Point(-keep.X, -keep.Y);
        }

        private void SwatchClick(object sender, EventArgs e)
        {
            Control c = sender as Control;
            if (c == null || !(c.Tag is Color)) return;
            CrosshairItemSettings it = Cur();
            if (it == null) return;
            it.Color = (Color)c.Tag;
            AfterEdit();
        }

        private void PickColor()
        {
            CrosshairItemSettings it = Cur();
            if (it == null) return;
            using (ColorDialog d = new ColorDialog())
            {
                d.Color = it.Color;
                d.FullOpen = true;
                if (d.ShowDialog(this) != DialogResult.OK) return;
                it.Color = d.Color;
            }
            AfterEdit();
        }
    }
}
