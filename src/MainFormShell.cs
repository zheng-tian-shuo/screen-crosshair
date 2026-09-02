using System;
using System.Drawing;
using System.Windows.Forms;

namespace ScreenCrosshair
{
    public partial class MainForm
    {
        private Label _lblPrevInfo;

        // ---------------- 左侧导航 ----------------
        private void BuildSide()
        {
            Panel side = new Panel();
            side.Bounds = new Rectangle(0, BarH, SideW, WinH - BarH);
            side.BackColor = Theme.Window;
            Controls.Add(side);

            Panel sep = new Panel();
            sep.Bounds = new Rectangle(SideW - 1, 0, 1, side.Height);
            sep.BackColor = Theme.Border;
            side.Controls.Add(sep);

            _tabs = new SideTab[TabNames.Length];
            for (int i = 0; i < TabNames.Length; i++)
            {
                SideTab t = new SideTab();
                t.Text = TabNames[i];
                t.Bounds = new Rectangle(10, 12 + i * 42, 130, 38);
                t.Tag = i;
                t.Click += TabClick;
                side.Controls.Add(t);
                _tabs[i] = t;
            }

            int y = 12 + TabNames.Length * 42 + 12;
            Panel sep2 = new Panel();
            sep2.Bounds = new Rectangle(18, y, 112, 1);
            sep2.BackColor = Theme.Border;
            side.Controls.Add(sep2);

            // 高度给宽松些：这块有 10 行，热键停用时最后一行是「已停用」，
            // 中文字比「Ctrl + F8」这种英文占得高，按刚好够的高度算会被切掉半截。
            // 下面到侧栏底部还有一大片空白，多留点不影响别的东西。
            _lblSideHint = Ui.L(side, "", Theme.Small, Theme.TextFaint,
                16, y + 12, 126, 210);
        }

        private void TabClick(object sender, EventArgs e)
        {
            Control c = sender as Control;
            if (c == null || !(c.Tag is int)) return;
            SelectPage((int)c.Tag);
        }

        private void SelectPage(int i)
        {
            if (_pages == null || i < 0 || i >= _pages.Length) return;
            _page = i;
            for (int k = 0; k < _pages.Length; k++)
            {
                _pages[k].Visible = (k == i);
                _tabs[k].SetActive(k == i);
            }
            _previewWrap.Visible = (i != PageHelp);
            if (i == PageHelp) _pages[PageHelp].BringToFront();
            else _previewWrap.BringToFront();
        }

        // ---------------- 内容区 + 预览 ----------------
        private void BuildHost()
        {
            _host = new Panel();
            _host.Bounds = new Rectangle(SideW, BarH, WinW - SideW, WinH - BarH);
            _host.BackColor = Theme.Window;
            Controls.Add(_host);

            _pageW = _host.Width - PrevW;

            _previewWrap = new Panel();
            _previewWrap.Bounds = new Rectangle(_pageW + 6, 14, PrevW - 20, _host.Height - 28);
            _previewWrap.BackColor = Theme.Window;
            _host.Controls.Add(_previewWrap);
            BuildPreview();

            _pages = new Panel[PageCount];
            for (int i = 0; i < PageCount; i++)
            {
                Panel p = new Panel();
                p.Bounds = new Rectangle(0, 0,
                    (i == PageHelp ? _host.Width : _pageW), _host.Height);
                p.BackColor = Theme.Window;
                p.AutoScroll = true;
                p.Visible = false;
                _host.Controls.Add(p);
                _pages[i] = p;
            }
        }

        private void BuildPreview()
        {
            Card c = new Card();
            c.Caption = "实时预览";
            c.Bounds = new Rectangle(0, 0, _previewWrap.Width, _previewWrap.Height);
            _previewWrap.Controls.Add(c);

            int inner = c.Width - 24;
            _preview = new PreviewBox();
            _preview.Bounds = new Rectangle(12, 38, inner, inner);
            c.Controls.Add(_preview);

            string[] bg = { "棋盘", "夜间", "雪地", "草地", "沙地" };
            int bw = (inner - 16) / 5;
            for (int i = 0; i < bg.Length; i++)
            {
                FlatBtn b = new FlatBtn();
                b.Text = bg[i];
                b.Font = Theme.Small;
                b.Radius = 5;
                b.Bounds = new Rectangle(12 + i * (bw + 4), 38 + inner + 10, bw, 26);
                b.Tag = i;
                b.Click += BgClick;
                c.Controls.Add(b);
            }

            int y = 38 + inner + 46;
            Ui.L(c, "换个背景看看在草地、雪地这种杂色场景里够不够显眼",
                Theme.Small, Theme.TextFaint, 12, y, inner, 34);

            _lblPrevInfo = Ui.L(c, "", Theme.Small, Theme.TextMuted, 12, y + 40, inner, 120);
        }

        private void BgClick(object sender, EventArgs e)
        {
            Control c = sender as Control;
            if (c == null || !(c.Tag is int)) return;
            _preview.BgKind = (int)c.Tag;
            _preview.Invalidate();
        }

        /// <summary>造一行「标签 + 滑块 + 数值」</summary>
        private Slider SliderRow(Card c, string caption, int y, int min, int max,
            EventHandler onChange, out Label valLabel)
        {
            Ui.L(c, caption, Theme.Body, Theme.TextMuted, 14, y + 2, 60, 20);
            Slider s = new Slider();
            s.Min = min;
            s.Max = max;
            s.Bounds = new Rectangle(76, y, 278, 22);
            s.ValueChanged += onChange;
            c.Controls.Add(s);
            valLabel = Ui.L(c, "", Theme.Mono, Theme.Accent, 362, y + 2, 90, 20);
            return s;
        }

        private Card NewCard(Panel page, string caption, int y, int h)
        {
            Card c = new Card();
            c.Caption = caption;
            c.Bounds = new Rectangle(14, y, _pageW - 42, h);
            page.Controls.Add(c);
            return c;
        }
    }
}
