using System;
using System.Drawing;
using System.Globalization;

namespace ScreenCrosshair
{
    public enum ProjectionDisplayMode { Stretch, Fit, Window }

    public static class ProjectionMath
    {
        public static bool TryAspectRatio(string text, out double ratio)
        {
            ratio = 0;
            string[] parts = (text ?? "").Trim().Replace('\uFF1A', ':').Split(':');
            double width, height;
            if (parts.Length == 2)
            {
                if (!double.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out width) ||
                    !double.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out height) ||
                    height <= 0) return false;
                ratio = width / height;
            }
            else if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out ratio)) return false;
            return ratio > 0.1 && ratio < 10.0;
        }

        public static Point ConvertHorizontalFov(Point referencePoint, Size referenceSize,
            double referenceFov, Size targetSize, double targetFov)
        {
            return ConvertHorizontalFov(referencePoint, referenceSize, referenceFov,
                targetSize, targetFov, (double)targetSize.Width / targetSize.Height);
        }

        public static Point ConvertHorizontalFov(Point referencePoint, Size referenceSize,
            double referenceFov, Size targetSize, double targetFov, double targetAspectRatio)
        {
            Point point;
            if (!TryConvertHorizontalFov(referencePoint, referenceSize, referenceFov,
                new Rectangle(Point.Empty, targetSize), targetFov, targetAspectRatio, out point))
                throw new ArgumentOutOfRangeException("targetFov", "参数无效或投影超出画面");
            return point;
        }

        // Keep subpixel precision until mapping into the displayed game rectangle.
        // Failure must be handled by the caller, never turned into a screen-edge point.
        public static bool TryConvertHorizontalFov(Point referencePoint, Size referenceSize,
            double referenceFov, Rectangle viewport, double targetFov,
            double targetAspectRatio, out Point point)
        {
            point = Point.Empty;
            if (referenceSize.Width < 2 || referenceSize.Height < 2 ||
                viewport.Width < 2 || viewport.Height < 2 ||
                !ValidFov(referenceFov) || !ValidFov(targetFov) ||
                !(targetAspectRatio > 0.1 && targetAspectRatio < 10.0)) return false;

            double scale = Math.Tan(referenceFov * Math.PI / 360.0) /
                Math.Tan(targetFov * Math.PI / 360.0);
            double x = 0.5 + (referencePoint.X - referenceSize.Width / 2.0) /
                referenceSize.Width * scale;
            double y = 0.5 + (referencePoint.Y - referenceSize.Height / 2.0) /
                referenceSize.Width * scale * targetAspectRatio;
            if (!(x >= 0 && x < 1 && y >= 0 && y < 1)) return false;
            Point mapped = new Point((int)Math.Round(viewport.Left + x * viewport.Width),
                (int)Math.Round(viewport.Top + y * viewport.Height));
            if (!viewport.Contains(mapped)) return false;
            point = mapped;
            return true;
        }

        public static bool TryGetViewport(Size screenSize, Size targetSize, double aspect,
            ProjectionDisplayMode mode, Point windowOrigin, out Rectangle viewport)
        {
            viewport = Rectangle.Empty;
            if (screenSize.Width < 2 || screenSize.Height < 2 ||
                targetSize.Width < 2 || targetSize.Height < 2 ||
                !(aspect > 0.1 && aspect < 10.0)) return false;
            Rectangle screen = new Rectangle(Point.Empty, screenSize);
            if (mode == ProjectionDisplayMode.Stretch) viewport = screen;
            else if (mode == ProjectionDisplayMode.Fit)
            {
                int width = screenSize.Width;
                int height = (int)Math.Round(width / aspect);
                if (height > screenSize.Height)
                {
                    height = screenSize.Height;
                    width = (int)Math.Round(height * aspect);
                }
                viewport = new Rectangle((screenSize.Width - width) / 2,
                    (screenSize.Height - height) / 2, width, height);
            }
            else if (mode == ProjectionDisplayMode.Window)
                viewport = new Rectangle(windowOrigin, targetSize);
            else return false;
            return viewport.Width >= 2 && viewport.Height >= 2 && screen.Contains(viewport);
        }

        private static bool ValidFov(double fov)
        {
            return fov > 1.0 && fov < 179.0;
        }
    }
}
