using System;
using System.Drawing;

namespace ScreenCrosshair
{
    /// <summary>准星形状。0-4 与旧版本编号保持一致，5 以后为新增。</summary>
    public enum CrosshairShape
    {
        Dot = 0,          // 圆点
        Circle = 1,       // 圆环
        Cross = 2,        // 十字
        X = 3,            // 叉形
        CircleCross = 4,  // 圆环十字
        TShape = 5,       // T 形：只有上半竖线，不挡落点视野
        HLine = 6,        // 水平线：标定水平面/楼层高度
        DotRing = 7,      // 点 + 外圈：杂色背景上最容易找到
        Mildot = 8        // 分划板：竖刻度尺，炸点标距离用
    }

    public static class Shapes
    {
        public static readonly string[] Names =
        {
            "圆点", "圆环", "十字", "叉形", "圆环十字",
            "T 形", "水平线", "点 + 外圈", "分划板（刻度）"
        };

        public static string NameOf(CrosshairShape s)
        {
            int i = (int)s;
            return (i >= 0 && i < Names.Length) ? Names[i] : Names[4];
        }

        /// <summary>分划板有额外的刻度参数，界面上要多显示一组控件</summary>
        public static bool HasTicks(CrosshairShape s)
        {
            return s == CrosshairShape.Mildot;
        }
    }

    /// <summary>单个准星的全部设置</summary>
    public class CrosshairItemSettings
    {
        public string Name;
        public CrosshairShape Shape;
        public Color Color;
        public int Size;         // 外框像素尺寸
        public int Thickness;    // 线宽
        public int Opacity;      // 不透明度 10-100，per-pixel alpha 才有意义
        public bool Glow;        // 暗色外发光，替代旧版的硬黑描边
        public bool Visible;
        public bool Centered;    // true=屏幕居中，false=自由坐标
        public int X;
        public int Y;
        public bool FovReferenceSet;
        public double FovReference;
        public int FovReferenceX;
        public int FovReferenceY;
        public int FovReferenceWidth;
        public int FovReferenceHeight;
        public string ScreenName; // 目标屏幕 DeviceName，空=鼠标/游戏所在屏

        public double TargetFov = 90.0;
        public int TargetWidth, TargetHeight; // 0 = first use, fill from target screen
        public string TargetAspect = "16:9";
        public ProjectionDisplayMode DisplayMode = ProjectionDisplayMode.Stretch;
        public int WindowOriginX, WindowOriginY; // game image origin relative to target screen

        // 分划板参数
        public int TickCount;    // 刻度数量
        public int TickSpacing;  // 刻度间距（像素）
        public int TickLength;   // 刻度横线长度
        public bool ShowLabels;  // 是否画距离数字
        public int LabelStart;   // 第一格代表的距离
        public int LabelStep;    // 每格递增的距离

        public CrosshairItemSettings()
        {
            Name = "准星";
            // 默认就是炸点标定最常用的那一套：红色圆点、50 px
            Shape = CrosshairShape.Dot;
            Color = Color.FromArgb(255, 59, 48);
            Size = 50;
            Thickness = 2;
            Opacity = 100;
            Glow = true;
            Visible = true;
            Centered = true;
            X = 0;
            Y = 0;
            ScreenName = "";
            FovReferenceSet = false;
            FovReference = 90.0;
            FovReferenceX = 0;
            FovReferenceY = 0;
            FovReferenceWidth = 0;
            FovReferenceHeight = 0;
            TickCount = 5;
            TickSpacing = 26;
            TickLength = 11;
            ShowLabels = true;
            LabelStart = 50;
            LabelStep = 25;
        }

        public CrosshairItemSettings Clone()
        {
            return (CrosshairItemSettings)MemberwiseClone();
        }

        /// <summary>绘制这个准星需要的画布边长（含发光余量），保证为奇数以有唯一中心像素</summary>
        public int CanvasSize()
        {
            int core = Math.Max(8, Size) + Math.Max(1, Thickness) * 2;
            if (Shape == CrosshairShape.Mildot)
            {
                int h = Math.Max(1, TickCount) * Math.Max(2, TickSpacing) + Math.Max(8, Size);
                core = Math.Max(core, h * 2);
                core = Math.Max(core, (Math.Max(4, TickLength) + 46) * 2);
            }
            int pad = Glow ? 14 : 6;
            int n = core + pad * 2;
            return (n % 2 == 0) ? n + 1 : n;
        }
    }
}
