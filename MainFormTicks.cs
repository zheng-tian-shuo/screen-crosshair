using System;
using System.Drawing;
using System.Windows.Forms;

namespace ScreenCrosshair
{
    public partial class MainForm
    {
        private Card _cardTicks;
        private Slider _sTickCount, _sTickSpace, _sTickLen;
        private Label _vTickCount, _vTickSpace, _vTickLen;
        private Chk _chkLabels;
        private TextBox _tbLabelStart, _tbLabelStep;
        private Label _lblTickHint;

        /// <summary>分划板专用参数，只在形状选「分划板」时显示</summary>
        private void BuildTickCard(Panel pg)
        {
            _cardTicks = NewCard(pg, "分划板刻度", 396, 214);
            _pgCards.Add(_cardTicks);

            _sTickCount = SliderRow(_cardTicks, "刻度数", 40, 1, 20, UiChanged, out _vTickCount);
            _sTickSpace = SliderRow(_cardTicks, "间距", 74, 6, 160, UiChanged, out _vTickSpace);
            _sTickLen = SliderRow(_cardTicks, "刻度长", 108, 4, 80, UiChanged, out _vTickLen);

            _chkLabels = new Chk();
            _chkLabels.Text = "刻度旁显示距离";
            _chkLabels.Bounds = new Rectangle(14, 142, 150, 22);
            _chkLabels.CheckedChanged += UiChanged;
            _cardTicks.Controls.Add(_chkLabels);

            Ui.L(_cardTicks, "第一格", Theme.Body, Theme.TextMuted, 176, 144, 46, 20);
            _tbLabelStart = Ui.NumBox(_cardTicks, 224, 142, 54);
            _tbLabelStart.TextChanged += UiChanged;

            Ui.L(_cardTicks, "每格 +", Theme.Body, Theme.TextMuted, 290, 144, 46, 20);
            _tbLabelStep = Ui.NumBox(_cardTicks, 340, 142, 54);
            _tbLabelStep.TextChanged += UiChanged;

            _lblTickHint = Ui.L(_cardTicks, "用法：先在训练场量出每格对应多少米，之后按格数抬枪就行。",
                Theme.Small, Theme.TextFaint, 14, 174, 368, 32);
        }

        /// <summary>
        /// 这里不能拿 Visible 当短路条件：控件所在的页面在构造阶段是隐藏的，
        /// 此时 Visible 一律读回 false，会把「应该隐藏」当成已经隐藏跳过，
        /// 于是页面一显示，分划板卡片就跟着冒出来了。
        /// 卡片下面还压着位置那几张，显隐之后要重排一次，不然中间留一个大窟窿。
        /// </summary>
        private void ShowTickCard(bool on)
        {
            if (_cardTicks == null) return;
            _tickShown = on;
            _cardTicks.Visible = on;
            ReflowCrosshairPage();
        }

        private static int ParseInt(TextBox t, int def, int lo, int hi)
        {
            int n;
            if (t == null || !int.TryParse(t.Text.Trim(), out n)) return def;
            if (n < lo) n = lo;
            if (n > hi) n = hi;
            return n;
        }
    }
}
