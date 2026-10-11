using System;
namespace TrackballCAD.Input
{
    public sealed class TrackballState : EventArgs
    {
        public IntPtr Device { get; }
        public int Dx { get; }
        public int Dy { get; }
        public short Wheel { get; }
        public short HorizontalWheel { get; }
        public ushort ButtonFlags { get; }
        public bool IsAbsolute { get; }
        public TrackballState(IntPtr device, int dx, int dy, short wheel, short horizontalWheel, ushort buttons, bool absolute)
        { Device = device; Dx = dx; Dy = dy; Wheel = wheel; HorizontalWheel = horizontalWheel; ButtonFlags = buttons; IsAbsolute = absolute; }
    }
}
