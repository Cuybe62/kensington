using System;
using System.Runtime.InteropServices;

namespace TrackballCAD.Portable
{
    [StructLayout(LayoutKind.Sequential)]
    public struct CursorPoint { public int X, Y; }

    internal static class CursorWindows
    {
        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool GetCursorPos(out CursorPoint point);
        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool SetCursorPos(int x, int y);
    }

    // Owned by the UI STA. Pure state so one-shot behavior can be tested without Win32.
    public sealed class CursorReturnState
    {
        private CursorPoint origin;
        private bool captured;
        private int requestedAt;
        public bool Pending { get; private set; }
        public void Begin(CursorPoint point)
        {
            origin = point; captured = true; Pending = false;
        }
        public void End(bool restore, int now)
        {
            if (!restore) { Cancel(); return; }
            if (!captured) return; // Repeated Escape must not refresh or duplicate a request.
            captured = false; Pending = true; requestedAt = now;
        }
        public void Cancel() { captured = false; Pending = false; }
        public bool TryTake(bool foreground, int now, out CursorPoint point)
        {
            point = origin;
            bool restore = Pending && foreground && unchecked((uint)(now - requestedAt)) <= 1000;
            Pending = false; // Consume even on refusal or native failure; never retry later.
            return restore;
        }
    }
}
