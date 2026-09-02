using System;
using System.Windows.Forms;

namespace ScreenCrosshair
{
    public partial class MainForm
    {
        private int SelProfileIndex()
        {
            if (_lstProfile == null || _lstProfile.SelectedIndex < 0) return _cfg.ActiveIndex;
            return _lstProfile.SelectedIndex;
        }

        private void NewProfile()
        {
            string n = AskForm.Ask(this, "新建预设",
                "给这套准星起个名字，建议写地图 + 撤离点，比如「零号大坝 · 西门」", "新预设");
            if (n == null) return;
            _cfg.Profiles.Add(Profile.NewDefault(n));
            _cfg.ActiveIndex = _cfg.Profiles.Count - 1;
            _cfg.SelectedItem = 0;
            RebuildOverlays();
            PushToUi();
            _cfg.Save();
            Toast("已新建并切换到「" + n + "」");
        }

        private void RenameProfile()
        {
            int i = SelProfileIndex();
            if (i < 0 || i >= _cfg.Profiles.Count) return;
            string n = AskForm.Ask(this, "重命名预设", "改成什么名字？", _cfg.Profiles[i].Name);
            if (n == null) return;
            _cfg.Profiles[i].Name = n;
            FillProfileList();
            UpdateSideHint();
            _cfg.Save();
        }

        private void DupProfile()
        {
            int i = SelProfileIndex();
            if (i < 0 || i >= _cfg.Profiles.Count) return;
            if (_cfg.Profiles.Count >= 64) { Toast("预设太多了"); return; }
            Profile p = _cfg.Profiles[i].Clone();
            p.Name = p.Name + " 副本";
            _cfg.Profiles.Insert(i + 1, p);
            if (_cfg.ActiveIndex > i) _cfg.ActiveIndex++;
            FillProfileList();
            _cfg.Save();
            Toast("已复制为「" + p.Name + "」");
        }

        private void DelProfile()
        {
            if (_cfg.Profiles.Count <= 1) { Toast("至少要留一套预设"); return; }
            int i = SelProfileIndex();
            if (i < 0 || i >= _cfg.Profiles.Count) return;

            string name = _cfg.Profiles[i].Name;
            if (MessageBox.Show(this, "删除预设「" + name + "」？这套准星会一起删掉。",
                "确认删除", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            _cfg.Profiles.RemoveAt(i);
            if (_cfg.ActiveIndex >= _cfg.Profiles.Count) _cfg.ActiveIndex = _cfg.Profiles.Count - 1;
            if (_cfg.ActiveIndex > i) _cfg.ActiveIndex--;
            _cfg.SelectedItem = 0;
            RebuildOverlays();
            PushToUi();
            _cfg.Save();
            Toast("已删除「" + name + "」");
        }

        private void UseSelectedProfile()
        {
            int i = SelProfileIndex();
            if (i < 0 || i >= _cfg.Profiles.Count) return;
            if (i == _cfg.ActiveIndex) { Toast("这已经是当前预设了"); return; }
            SwitchProfile(i);
        }

        /// <summary>热键和托盘也走这里</summary>
        private void SwitchProfile(int i)
        {
            if (i < 0 || i >= _cfg.Profiles.Count) return;
            _cfg.ActiveIndex = i;
            _cfg.SelectedItem = 0;
            if (_dragMode) ToggleDrag();      // 换预设就退出拖动，避免残留窗口吃点击
            RebuildOverlays();
            PushToUi();
            _cfg.Save();
            Toast("已切换到「" + _cfg.Active.Name + "」");
        }

        private void NextProfile()
        {
            if (_cfg.Profiles.Count <= 1) { Toast("只有一套预设"); return; }
            SwitchProfile((_cfg.ActiveIndex + 1) % _cfg.Profiles.Count);
        }
    }
}
