using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace ScreenCrosshair
{
    /// <summary>把内置说明书渲染出来：## 是小标题，- 和数字是列表，两空格开头算续行。</summary>
    public class HelpView : Control
    {
        private class Blk
        {
            public string T;
            public Font F;
            public Color C;
            public Rectangle R;
        }

        private readonly List<Blk> _bl = new List<Blk>();

        public HelpView()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint, true);
            BackColor = Theme.Card;
        }

        public void SetMarkdown(string src)
        {
            _bl.Clear();
            if (src == null) src = "";
            string[] lines = src.Replace("\r", "").Split('\n');
            int y = Theme.S(4);
            int full = Width - Theme.S(8);

            for (int i = 0; i < lines.Length; i++)
            {
                string raw = lines[i].TrimEnd();
                if (raw.Length == 0) { y += Theme.S(9); continue; }

                Font f = Theme.Body;
                Color c = Theme.TextMuted;
                int x = Theme.S(4);
                string t = raw;

                if (raw.StartsWith("## "))
                {
                    t = raw.Substring(3);
                    f = Theme.SectionHead;
                    c = Theme.Accent;
                    if (_bl.Count > 0) y += Theme.S(12);
                }
                else if (raw.StartsWith("- "))
                {
                    t = "· " + raw.Substring(2);
                    x = Theme.S(16);
                }
                else if (raw.Length > 2 && char.IsDigit(raw[0]) && raw[1] == '.')
                {
                    x = Theme.S(16);
                }
                else if (raw.StartsWith("  "))
                {
                    t = raw.Trim();
                    x = Theme.S(28);
                    c = Theme.TextFaint;
                }

                int w = full - x;
                if (w < Theme.S(40)) w = Theme.S(40);
                int h = TextRenderer.MeasureText(t, f, new Size(w, 0),
                    TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height;

                Blk b = new Blk();
                b.T = t;
                b.F = f;
                b.C = c;
                b.R = new Rectangle(x, y, w, h);
                _bl.Add(b);
                y += h + Theme.S(3);
            }

            Height = y + Theme.S(10);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            using (SolidBrush b = new SolidBrush(BackColor))
                e.Graphics.FillRectangle(b, ClientRectangle);

            for (int i = 0; i < _bl.Count; i++)
            {
                Blk k = _bl[i];
                TextRenderer.DrawText(e.Graphics, k.T, k.F, k.R, k.C,
                    TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            }
        }
    }
}
