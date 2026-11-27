using System;
using System.Diagnostics;

namespace IGIRebuilt
{
    public static class NavigationDiagnostics
    {
        public static bool Enabled;
        public static long GridCalls, AuthoredCalls, ClearanceChecks, ExpandedNodes, DisconnectedRejects;
        public static double GridMilliseconds, AuthoredMilliseconds, LongestGridMilliseconds;
        public static Timing Grid() => new Timing(false);
        public static Timing Authored() => new Timing(true);
        public static void Reset()
        {
            GridCalls = AuthoredCalls = ClearanceChecks = ExpandedNodes = 0;
            DisconnectedRejects = 0;
            GridMilliseconds = AuthoredMilliseconds = LongestGridMilliseconds = 0;
        }
        public readonly struct Timing : IDisposable
        {
            private readonly long start;
            private readonly bool authored;
            public Timing(bool authored) { this.authored = authored; start = Enabled ? Stopwatch.GetTimestamp() : 0; }
            public void Dispose()
            {
                if (start == 0) return;
                double ms = (Stopwatch.GetTimestamp() - start) * 1000d / Stopwatch.Frequency;
                if (authored) { AuthoredCalls++; AuthoredMilliseconds += ms; }
                else { GridCalls++; GridMilliseconds += ms; LongestGridMilliseconds = Math.Max(LongestGridMilliseconds, ms); }
            }
        }
    }
}
