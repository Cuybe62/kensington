using System;
namespace TrackballCAD.Input
{
    public sealed class RawInputDevice
    {
        public IntPtr Handle { get; }
        public DeviceIdentifier Identifier { get; }
        public string Name { get; }
        public RawInputDevice(IntPtr handle, string path)
        { Handle = handle; Identifier = new DeviceIdentifier(path); Name = "HID mouse " + Identifier.Vid + ":" + Identifier.Pid; }
        public override string ToString() => Name + " — " + Identifier.DevicePath;
    }
}
