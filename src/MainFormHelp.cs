using System;
using System.Drawing;
using System.Windows.Forms;

namespace ScreenCrosshair
{
    public partial class MainForm
    {
        private Card _helpCard;
        private HelpView _helpView;

        private void BuildPageHelp()
        {
            Panel pg = _pages[PageHelp];
            int cardW = _host.Width - 42;

            _helpView = new HelpView();
            _helpView.Bounds = new Rectangle(16, 40, cardW - 32, 10);
            _helpView.SetMarkdown(HelpText.Readme);

            _helpCard = new Card();
            _helpCard.Caption = "使用说明（" + HelpText.Version + "，内置，不需要外部文档）";
            _helpCard.Bounds = new Rectangle(14, 14, cardW, _helpView.Height + 56);
            _helpCard.Controls.Add(_helpView);
            pg.Controls.Add(_helpCard);
        }

        /// <summary>
        /// 说明文字的高度是按实际 DPI 量出来的，整体缩放会把它再乘一遍，
        /// 所以缩放之后要用新宽度重排一次，卡片高度也跟着重算。
        /// </summary>
        private void RelayoutHelp()
        {
            if (_helpView == null || _helpCard == null) return;
            _helpView.SetMarkdown(HelpText.Readme);
            _helpCard.Height = _helpView.Height + Theme.S(56);
        }
    }
}
