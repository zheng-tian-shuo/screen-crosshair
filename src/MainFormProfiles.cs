using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace ScreenCrosshair
{
    public partial class MainForm
    {
        private ListBox _lstProfile;

        private void BuildPageProfiles()
        {
            Panel pg = _pages[PageProfiles];

            Card c1 = NewCard(pg, "预设方案（一张图一套准星）", 14, 248);

            Panel wrap = new Panel();
            wrap.Bounds = new Rectangle(14, 40, 254, 194);
            wrap.BackColor = Theme.Border;
            wrap.Padding = new Padding(1);
            c1.Controls.Add(wrap);

            _lstProfile = new ListBox();
            _lstProfile.BorderStyle = BorderStyle.None;
            _lstProfile.BackColor = Theme.CardAlt;
            _lstProfile.ForeColor = Theme.Text;
            _lstProfile.Font = Theme.Body;
            // ItemHeight 不在 Form.Scale 的管辖范围里，字号却会随 DPI 变大，这里得自己换算
            _lstProfile.ItemHeight = Theme.S(22);
            _lstProfile.IntegralHeight = false;
            _lstProfile.DrawMode = DrawMode.OwnerDrawFixed;
            _lstProfile.DrawItem += ProfileDrawItem;
            _lstProfile.Dock = DockStyle.Fill;
            _lstProfile.DoubleClick += delegate { UseSelectedProfile(); };
            wrap.Controls.Add(_lstProfile);

            string[] names = { "新建", "重命名", "复制", "删除" };
            EventHandler[] acts =
            {
                delegate { NewProfile(); },
                delegate { RenameProfile(); },
                delegate { DupProfile(); },
                delegate { DelProfile(); }
            };
            for (int i = 0; i < names.Length; i++)
            {
                FlatBtn b = new FlatBtn();
                b.Text = names[i];
                b.Kind = (i == 3) ? 2 : 0;
                b.Bounds = new Rectangle(280, 40 + i * 34, 158, 28);
                b.Click += acts[i];
                c1.Controls.Add(b);
            }

            FlatBtn use = new FlatBtn();
            use.Kind = 1;
            use.Text = "切换到选中的预设";
            use.Bounds = new Rectangle(280, 182, 158, 30);
            use.Click += delegate { UseSelectedProfile(); };
            c1.Controls.Add(use);

            // ---- 配置文件 ----
            Card c2 = NewCard(pg, "配置文件", 274, 156);
            Ui.L(c2, AppSettings.ConfigPath, Theme.Small, Theme.TextMuted, 14, 40, 424, 34);

            FlatBtn open = new FlatBtn();
            open.Text = "打开所在文件夹";
            open.Bounds = new Rectangle(14, 80, 130, 28);
            open.Click += delegate { OpenConfigDir(); };
            c2.Controls.Add(open);

            FlatBtn exp = new FlatBtn();
            exp.Text = "导出…";
            exp.Bounds = new Rectangle(152, 80, 90, 28);
            exp.Click += delegate { ExportCfg(); };
            c2.Controls.Add(exp);

            FlatBtn imp = new FlatBtn();
            imp.Text = "导入…";
            imp.Bounds = new Rectangle(250, 80, 90, 28);
            imp.Click += delegate { ImportCfg(); };
            c2.Controls.Add(imp);

            Ui.L(c2, "连配置一共就两个文件，拷到别的机器直接能用，ini 也能发给别人",
                Theme.Small, Theme.TextFaint, 14, 116, 424, 32);
        }

        /// <summary>
        /// 列表的选中行默认是系统那条亮蓝，跟深色配色打架得厉害，所以自己画：
        /// 选中行用深金底 + 金字，其余跟卡片同色。
        /// </summary>
        private void ProfileDrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= _lstProfile.Items.Count) return;
            bool sel = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            using (SolidBrush b = new SolidBrush(sel ? Theme.AccentDeep : Theme.CardAlt))
                e.Graphics.FillRectangle(b, e.Bounds);
            Rectangle t = new Rectangle(e.Bounds.X + Theme.S(6), e.Bounds.Y,
                e.Bounds.Width - Theme.S(6), e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, _lstProfile.Items[e.Index].ToString(),
                Theme.Body, t, sel ? Theme.Accent : Theme.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        private void FillProfileList()
        {
            if (_lstProfile == null) return;
            int keep = _lstProfile.SelectedIndex;
            _lstProfile.BeginUpdate();
            _lstProfile.Items.Clear();
            for (int i = 0; i < _cfg.Profiles.Count; i++)
            {
                Profile p = _cfg.Profiles[i];
                string mark = (i == _cfg.ActiveIndex) ? "● " : "   ";
                _lstProfile.Items.Add(mark + p.Name + "   (" + p.Items.Count + " 个准星)");
            }
            _lstProfile.EndUpdate();
            if (keep >= 0 && keep < _lstProfile.Items.Count) _lstProfile.SelectedIndex = keep;
            else if (_lstProfile.Items.Count > 0) _lstProfile.SelectedIndex = _cfg.ActiveIndex;
        }

        private void OpenConfigDir()
        {
            try
            {
                string dir = Path.GetDirectoryName(AppSettings.ConfigPath);
                Process.Start("explorer.exe", "\"" + dir + "\"");
            }
            catch { }
        }

        private void ExportCfg()
        {
            using (SaveFileDialog d = new SaveFileDialog())
            {
                d.Filter = "配置文件 (*.ini)|*.ini";
                d.FileName = HelpText.AppName + "配置.ini";
                if (d.ShowDialog(this) != DialogResult.OK) return;
                Toast(_cfg.ExportTo(d.FileName) ? "已导出" : "导出失败，检查目标位置能不能写");
            }
        }

        private void ImportCfg()
        {
            using (OpenFileDialog d = new OpenFileDialog())
            {
                d.Filter = "配置文件 (*.ini)|*.ini|所有文件 (*.*)|*.*";
                if (d.ShowDialog(this) != DialogResult.OK) return;
                if (!_cfg.ImportFrom(d.FileName)) { Toast("导入失败，文件读不出来"); return; }
                RebuildOverlays();
                PushToUi();
                RegisterHotkeys();
                _cfg.Save();
                Toast("已导入");
            }
        }
    }
}
