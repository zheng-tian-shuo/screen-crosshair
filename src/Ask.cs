using System;
using System.Drawing;
using System.Windows.Forms;

namespace ScreenCrosshair
{
    /// <summary>深色小输入框，替代 WinForms 里缺失的 InputBox</summary>
    public class AskForm : Form
    {
        private readonly TextBox _tb;

        public string Value
        {
            get { return _tb.Text.Trim(); }
        }

        public AskForm(string title, string prompt, string init)
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(400, 168);
            BackColor = Theme.Card;
            ShowInTaskbar = false;
            KeyPreview = true;

            Ui.L(this, title, Theme.SectionHead, Theme.Text, 22, 20, 356, 24);
            Ui.L(this, prompt, Theme.Small, Theme.TextMuted, 22, 48, 356, 34);

            _tb = Ui.Box(this, 22, 88, 356);
            _tb.Text = init == null ? "" : init;
            _tb.SelectAll();

            FlatBtn ok = new FlatBtn();
            ok.Kind = 1;
            ok.Text = "确定";
            ok.Bounds = new Rectangle(206, 124, 82, 28);
            ok.Click += delegate { Accept(); };
            Controls.Add(ok);

            FlatBtn cancel = new FlatBtn();
            cancel.Text = "取消";
            cancel.Bounds = new Rectangle(296, 124, 82, 28);
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(cancel);

            // 和主窗口一样，高 DPI 下整体放大一次
            if (Theme.K > 1.001f)
            {
                Scale(new SizeF(Theme.K, Theme.K));
                ClientSize = new Size(Theme.S(400), Theme.S(168));
            }

            // 输入框现在套在一层边框面板里，不再是窗口的直接子控件，焦点自己点一下更稳
            Shown += delegate { _tb.Focus(); _tb.SelectAll(); };
        }

        private void Accept()
        {
            if (Value.Length == 0) return;
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter) { Accept(); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); }
            base.OnKeyDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Pen p = new Pen(Theme.BorderLit, 1f))
                e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
        }

        /// <summary>返回 null 表示用户取消</summary>
        public static string Ask(IWin32Window owner, string title, string prompt, string init)
        {
            using (AskForm f = new AskForm(title, prompt, init))
            {
                if (f.ShowDialog(owner) != DialogResult.OK) return null;
                return f.Value;
            }
        }
    }
}
