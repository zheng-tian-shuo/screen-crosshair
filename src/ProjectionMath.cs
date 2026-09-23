using System;
using System.Drawing;

namespace ScreenCrosshair
{
    public static class ProjectionMath
    {
        public static Point ConvertHorizontalFov(Point referencePoint, Size referenceSize,
            double referenceFov, Size targetSize, double targetFov)
        {
            return ConvertHorizontalFov(referencePoint, referenceSize, referenceFov,
                targetSize, targetFov, (double)targetSize.Width / targetSize.Height);
        }

        public static Point ConvertHorizontalFov(Point referencePoint, Size referenceSize,
            double referenceFov, Size targetSize, double targetFov, double targetAspectRatio)
        {
            if (referenceSize.Width < 2 || referenceSize.Height < 2 ||
                targetSize.Width < 2 || targetSize.Height < 2)
                return new Point(0, 0);
            if (referenceFov <= 1.0 || referenceFov >= 179.0 ||
                targetFov <= 1.0 || targetFov >= 179.0 ||
                targetAspectRatio <= 0.1 || targetAspectRatio >= 10.0 ||
                double.IsNaN(targetAspectRatio) || double.IsInfinity(targetAspectRatio))
                return new Point(targetSize.Width / 2, targetSize.Height / 2);

            double referenceHalfH = HalfAngle(referenceFov);
            double targetHalfH = HalfAngle(targetFov);
            double referenceHalfV = referenceHalfH * referenceSize.Height / referenceSize.Width;
            double targetHalfV = targetHalfH / targetAspectRatio;
            double referenceCenterX = referenceSize.Width / 2.0;
            double referenceCenterY = referenceSize.Height / 2.0;
            double targetCenterX = targetSize.Width / 2.0;
            double targetCenterY = targetSize.Height / 2.0;
            double cameraX = (referencePoint.X - referenceCenterX) /
                (referenceSize.Width / 2.0) * referenceHalfH;
            double cameraY = (referencePoint.Y - referenceCenterY) *
                (2.0 / referenceSize.Width) * HalfAngle(referenceFov);

            int x = Clamp((int)Math.Round(targetCenterX + cameraX / targetHalfH *
                (targetSize.Width / 2.0)), targetSize.Width);
            int y = Clamp((int)Math.Round(targetCenterY + cameraY / targetHalfV *
                (targetSize.Height / 2.0)), targetSize.Height);
            return new Point(x, y);
        }

        private static double HalfAngle(double fov)
        {
            return Math.Tan(fov * Math.PI / 360.0);
        }

        private static int Clamp(int value, int size)
        {
            if (value < 0) return 0;
            if (value >= size) return size - 1;
            return value;
        }
    }
}
