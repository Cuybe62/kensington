using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
namespace TrackballCAD.Input
{
    public static class DeviceManager
    {
        public static IReadOnlyList<RawInputDevice> Enumerate()
        {
            uint size = (uint)Marshal.SizeOf(typeof(NativeMethods.DeviceEntry));
            // Devices may arrive between the count and data calls.
            for (int retry = 0; retry < 4; retry++)
            {
                uint count = 0;
                if (NativeMethods.GetRawInputDeviceList(null, ref count, size) == uint.MaxValue) throw new Win32Exception();
                var entries = new NativeMethods.DeviceEntry[count];
                uint read = NativeMethods.GetRawInputDeviceList(entries, ref count, size);
                if (read == uint.MaxValue) continue;
                var result = new List<RawInputDevice>();
                for (int i = 0; i < read; i++)
                {
                    if (entries[i].Type != 0) continue;
                    uint chars = 0;
                    if (NativeMethods.GetRawInputDeviceInfo(entries[i].Handle, 0x20000007, null, ref chars) == uint.MaxValue || chars == 0) continue;
                    var path = new StringBuilder((int)chars);
                    if (NativeMethods.GetRawInputDeviceInfo(entries[i].Handle, 0x20000007, path, ref chars) == uint.MaxValue) continue;
                    result.Add(new RawInputDevice(entries[i].Handle, path.ToString()));
                }
                return result;
            }
            throw new Win32Exception("Device enumeration changed repeatedly; retry refresh.");
        }
    }
}
