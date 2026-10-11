using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
namespace TrackballCAD.UI
{
    internal static class WindowsContext
    {
        [StructLayout(LayoutKind.Sequential)] private struct Point { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);
        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll", SetLastError = true)] private static extern bool GetCursorPos(out Point point);
        [DllImport("user32.dll", SetLastError = true)] private static extern bool ClipCursor(ref Rect rect);
        [DllImport("user32.dll", SetLastError = true)] private static extern bool ClipCursor(IntPtr rect);
        private static bool clipped;
        public static bool IsForeground(int pid)
        { uint actual; GetWindowThreadProcessId(GetForegroundWindow(), out actual); return pid > 0 && actual == pid; }
        public static bool EscapePressed => (GetAsyncKeyState(0x1B) & 0x8000) != 0;
        public static void Constrain()
        {
            if (clipped) return;
            Point p; if (!GetCursorPos(out p)) throw new Win32Exception();
            var rect = new Rect { Left = p.X, Top = p.Y, Right = p.X + 1, Bottom = p.Y + 1 };
            if (!ClipCursor(ref rect)) throw new Win32Exception();
            clipped = true;
        }
        public static void Release()
        {
            if (!clipped) return;
            if (!ClipCursor(IntPtr.Zero)) throw new Win32Exception();
            clipped = false;
        }
    }
}
