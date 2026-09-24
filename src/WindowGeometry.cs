using System;
using System.Drawing;

namespace ScreenCrosshair
{
    internal static class WindowGeometry
    {
        // The app is per-monitor DPI aware, so these coordinates are physical pixels.
        internal static bool TryGetClientArea(IntPtr window, out Rectangle area)
        {
            area = Rectangle.Empty;
            if (window == IntPtr.Zero || !Native.IsWindowVisible(window) || Native.IsIconic(window))
                return false;
            Native.RECT client;
            if (!Native.GetClientRect(window, out client)) return false;
            Native.POINT start = new Native.POINT(client.left, client.top);
            Native.POINT end = new Native.POINT(client.right, client.bottom);
            if (!Native.ClientToScreen(window, ref start) || !Native.ClientToScreen(window, ref end))
                return false;
            area = Rectangle.FromLTRB(Math.Min(start.x, end.x), Math.Min(start.y, end.y),
                Math.Max(start.x, end.x), Math.Max(start.y, end.y));
            return area.Width >= 2 && area.Height >= 2 && area.Width <= 32767 && area.Height <= 32767;
        }

        internal static bool TryRelativeArea(Rectangle area, Rectangle screen, out Rectangle relative)
        {
            relative = Rectangle.Empty;
            if (area.Width < 2 || area.Height < 2 || !screen.Contains(area)) return false;
            relative = new Rectangle(area.Left - screen.Left, area.Top - screen.Top, area.Width, area.Height);
            return relative.X <= 32767 && relative.Y <= 32767;
        }
    }
}
