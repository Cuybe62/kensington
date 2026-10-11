using System;
using System.Runtime.InteropServices;
using System.Text;

namespace TrackballCAD.Input
{
    internal static class NativeMethods
    {
        [StructLayout(LayoutKind.Sequential)] internal struct Registration
        { public ushort Page, Usage; public uint Flags; public IntPtr Target; }
        [StructLayout(LayoutKind.Sequential)] internal struct DeviceEntry
        { public IntPtr Handle; public uint Type; }
        [StructLayout(LayoutKind.Sequential)] internal struct Header
        { public uint Type, Size; public IntPtr Device, WParam; }
        // RAWMOUSE contains an aligned ULONG union at offset 4, NOT offset 2.
        [StructLayout(LayoutKind.Explicit, Size = 24)] internal struct Mouse
        {
            [FieldOffset(0)] public ushort Flags;
            [FieldOffset(4)] public ushort ButtonFlags;
            [FieldOffset(6)] public short ButtonData;
            [FieldOffset(12)] public int X;
            [FieldOffset(16)] public int Y;
        }
        [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool RegisterRawInputDevices([In] Registration[] devices, uint count, uint size);
        [DllImport("user32.dll", SetLastError = true)]
        internal static extern uint GetRawInputDeviceList([In, Out] DeviceEntry[] list, ref uint count, uint size);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern uint GetRawInputDeviceInfo(IntPtr device, uint command, StringBuilder data, ref uint size);
        [DllImport("user32.dll", SetLastError = true)]
        internal static extern uint GetRawInputData(IntPtr input, uint command, IntPtr data, ref uint size, uint headerSize);
    }
}
