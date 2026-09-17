using System;
using System.Windows.Forms;

namespace ScreenCrosshair
{
    /// <summary>
    /// 深色下拉框。使用原生绘制，避免鼠标悬停时在 WM_PAINT 后再次直接画窗口导致闪烁。
    /// </summary>
    internal class FlatCombo : ComboBox
    {
        private const int WM_MOUSEWHEEL = 0x020A;

        /// <summary>
        /// 选择框只允许鼠标点击改变选项，滚轮在收起或展开状态都不参与选择。
        /// </summary>
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            return;
        }

        protected override void WndProc(ref Message m)
        {
            // ComboBox 可能在进入 OnMouseWheel 前就由原生控件处理滚轮，
            // 所以这里也要拦截 Windows 消息，确保展开下拉列表时同样只能点击选择。
            if (m.Msg == WM_MOUSEWHEEL)
            {
                if (!DroppedDown) Ui.ScrollPage(this, (short)((m.WParam.ToInt64() >> 16) & 0xffff));
                return;
            }
            base.WndProc(ref m);
        }
    }
}
