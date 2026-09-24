using System;
using System.Diagnostics;

namespace ScreenCrosshair
{
    internal static class MonotonicTime
    {
        internal static double Seconds
        {
            get { return (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency; }
        }

        internal static int Remaining(double deadline, double now)
        {
            return (int)Math.Max(0, Math.Ceiling(deadline - now));
        }
    }
}
