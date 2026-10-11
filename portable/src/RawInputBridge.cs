// C# 5 syntax: compiled by Windows PowerShell 5.1 Add-Type, without an SDK.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace TrackballCAD.Portable
{
    internal static class Native
    {
        [StructLayout(LayoutKind.Sequential)] internal struct Registration
        { public ushort Page, Usage; public uint Flags; public IntPtr Target; }
        [StructLayout(LayoutKind.Sequential)] internal struct DeviceEntry
        { public IntPtr Handle; public uint Type; }
        [StructLayout(LayoutKind.Sequential)] internal struct Header
        { public uint Type, Size; public IntPtr Device, WParam; }
        // The ULONG union is aligned at offset 4, not at offset 2.
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
        internal static extern uint GetRawInputDeviceList([In, Out] DeviceEntry[] devices, ref uint count, uint size);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern uint GetRawInputDeviceInfo(IntPtr device, uint command, StringBuilder name, ref uint size);
        [DllImport("user32.dll", SetLastError = true)]
        internal static extern uint GetRawInputData(IntPtr input, uint command, IntPtr data, ref uint size, uint headerSize);
        [DllImport("user32.dll", SetLastError = true)]
        internal static extern uint GetRegisteredRawInputDevices([In, Out] Registration[] devices, ref uint count, uint size);
        [DllImport("user32.dll")] internal static extern int GetMessageTime();
    }

    public sealed class DeviceInfo
    {
        public long Handle { get; private set; }
        public string Path { get; private set; }
        public string Vid { get; private set; }
        public string Pid { get; private set; }
        public string Label { get; private set; }
        internal DeviceInfo(IntPtr handle, string path)
        {
            Handle = handle.ToInt64(); Path = path;
            Vid = Extract(path, "VID"); Pid = Extract(path, "PID");
            Label = "HID mouse " + (Vid.Length == 0 ? "unknown VID" : Vid) + ":" + (Pid.Length == 0 ? "unknown PID" : Pid);
        }
        private static string Extract(string path, string key)
        {
            Match match = Regex.Match(path, key + "_([0-9A-F]{4})", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value.ToUpperInvariant() : "";
        }
        public override string ToString() { return Label + " | " + Path; }
    }

    public sealed class InputSnapshot
    {
        public DeviceInfo Device { get; internal set; }
        public long Events { get; internal set; }
        public long IntervalEvents { get; internal set; }
        public long DeltaX { get; internal set; }
        public long DeltaY { get; internal set; }
        public int LastX { get; internal set; }
        public int LastY { get; internal set; }
        public bool Absolute { get; internal set; }
        public long Wheel { get; internal set; }
        public long HorizontalWheel { get; internal set; }
        public bool WheelObserved { get; internal set; }
        public bool HorizontalWheelObserved { get; internal set; }
        public ushort HeldButtons { get; internal set; }
        public ushort LastButtonFlags { get; internal set; }
        public ushort IntervalButtonFlags { get; internal set; }
    }

    public struct RawMousePacket
    {
        public long Device;
        public int X, Y, MessageTick;
        public bool Absolute;
    }

    // Pure managed accumulator. No PowerShell callback, UI operation or COM call here.
    public sealed class MouseAccumulator
    {
        private readonly object sync = new object();
        private long events, intervalEvents, x, y, wheel, horizontalWheel;
        private int lastX, lastY;
        private bool absolute, wheelObserved, horizontalWheelObserved;
        private ushort held, lastFlags, intervalFlags;
        public void Add(int dx, int dy, ushort flags, short buttonData, bool isAbsolute)
        {
            lock (sync)
            {
                events++; intervalEvents++; lastX = dx; lastY = dy; absolute = isAbsolute;
                // Absolute positions must never become relative navigation deltas.
                if (!isAbsolute) { x += dx; y += dy; }
                lastFlags = flags; intervalFlags |= flags;
                for (int b = 0; b < 5; b++)
                {
                    if ((flags & (1 << (b * 2))) != 0) held |= (ushort)(1 << b);
                    if ((flags & (2 << (b * 2))) != 0) held &= (ushort)~(1 << b);
                }
                if ((flags & 0x400) != 0) { wheel += buttonData; wheelObserved = true; }
                if ((flags & 0x800) != 0) { horizontalWheel += buttonData; horizontalWheelObserved = true; }
            }
        }
        public InputSnapshot Consume()
        {
            lock (sync)
            {
                InputSnapshot snapshot = new InputSnapshot {
                    Events = events, IntervalEvents = intervalEvents, DeltaX = x, DeltaY = y,
                    LastX = lastX, LastY = lastY, Absolute = absolute, Wheel = wheel,
                    HorizontalWheel = horizontalWheel, HeldButtons = held, LastButtonFlags = lastFlags,
                    IntervalButtonFlags = intervalFlags, WheelObserved = wheelObserved,
                    HorizontalWheelObserved = horizontalWheelObserved
                };
                intervalEvents = x = y = wheel = horizontalWheel = 0; intervalFlags = 0;
                return snapshot;
            }
        }
    }

    // Construct, pump and dispose on the same STA. Snapshots contain no native resources.
    public sealed class RawInputBridge : NativeWindow, IDisposable
    {
        private readonly int ownerThread;
        private readonly Dictionary<long, MouseAccumulator> accumulators = new Dictionary<long, MouseAccumulator>();
        private DeviceInfo[] devices = new DeviceInfo[0];
        private IntPtr buffer;
        private uint capacity;
        private bool disposed, registered;
        public bool DevicesChanged { get; private set; }
        public string LastError { get; private set; }
        public long UnidentifiedEvents { get; private set; }
        // Subscribe only from managed C#; never attach a PowerShell script block.
        public event Action<RawMousePacket> MouseInput;

        public static DeviceInfo[] EnumerateDevices()
        {
            uint size = (uint)Marshal.SizeOf(typeof(Native.DeviceEntry));
            for (int attempt = 0; attempt < 4; attempt++)
            {
                uint count = 0;
                if (Native.GetRawInputDeviceList(null, ref count, size) == uint.MaxValue) throw new Win32Exception();
                if (count == 0) return new DeviceInfo[0];
                Native.DeviceEntry[] list = new Native.DeviceEntry[count];
                uint read = Native.GetRawInputDeviceList(list, ref count, size);
                if (read == uint.MaxValue) continue;
                List<DeviceInfo> result = new List<DeviceInfo>();
                for (int i = 0; i < read; i++)
                {
                    if (list[i].Type != 0) continue;
                    uint chars = 0;
                    if (Native.GetRawInputDeviceInfo(list[i].Handle, 0x20000007, null, ref chars) == uint.MaxValue || chars == 0) continue;
                    StringBuilder path = new StringBuilder((int)chars);
                    if (Native.GetRawInputDeviceInfo(list[i].Handle, 0x20000007, path, ref chars) == uint.MaxValue) continue;
                    result.Add(new DeviceInfo(list[i].Handle, path.ToString()));
                }
                return result.ToArray();
            }
            throw new InvalidOperationException("Raw Input device list kept changing. Retry with USB devices stable.");
        }

        private static void CheckExistingRegistration()
        {
            uint count = 0, size = (uint)Marshal.SizeOf(typeof(Native.Registration));
            if (Native.GetRegisteredRawInputDevices(null, ref count, size) == uint.MaxValue) throw new Win32Exception();
            if (count == 0) return;
            Native.Registration[] entries = new Native.Registration[count];
            uint read = Native.GetRegisteredRawInputDevices(entries, ref count, size);
            if (read == uint.MaxValue) throw new Win32Exception();
            for (int i = 0; i < read; i++)
                if (entries[i].Page == 1 && (entries[i].Usage == 2 || entries[i].Usage == 0))
                    throw new InvalidOperationException("This process already registers mouse Raw Input. Use a fresh powershell.exe -STA -File process, not an embedded host.");
        }

        public RawInputBridge()
        {
            if (IntPtr.Size != 8 || Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
                throw new InvalidOperationException("Windows PowerShell x64 STA required.");
            ownerThread = Thread.CurrentThread.ManagedThreadId;
            LastError = "";
            CheckExistingRegistration();
            CreateHandle(new CreateParams { Caption = "TrackballCAD Portable Raw Input", Parent = new IntPtr(-3) });
            try
            {
                Native.Registration registration = new Native.Registration { Page = 1, Usage = 2, Flags = 0x2100, Target = Handle };
                if (!Native.RegisterRawInputDevices(new[] { registration }, 1, (uint)Marshal.SizeOf(typeof(Native.Registration)))) throw new Win32Exception();
                registered = true;
                RefreshDevices();
            }
            catch { Dispose(); throw; }
        }
        private void CheckThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != ownerThread) throw new InvalidOperationException("Capture must run on its owning STA.");
            if (disposed) throw new ObjectDisposedException("RawInputBridge");
        }
        public DeviceInfo[] RefreshDevices()
        {
            CheckThread();
            devices = EnumerateDevices(); accumulators.Clear();
            foreach (DeviceInfo device in devices) accumulators.Add(device.Handle, new MouseAccumulator());
            DevicesChanged = false;
            return (DeviceInfo[])devices.Clone();
        }
        public InputSnapshot[] ConsumeSnapshots()
        {
            CheckThread();
            List<InputSnapshot> snapshots = new List<InputSnapshot>();
            foreach (DeviceInfo device in devices)
            {
                InputSnapshot snapshot = accumulators[device.Handle].Consume();
                snapshot.Device = device; snapshots.Add(snapshot);
            }
            return snapshots.ToArray();
        }
        protected override void WndProc(ref Message message)
        {
            try
            {
                if (message.Msg == 0x00FF && !disposed) Read(message.LParam);
                if (message.Msg == 0x00FE && !disposed)
                {
                    DevicesChanged = true;
                    // Drop old handles immediately. A handle can be reused after hotplug.
                    accumulators.Clear(); devices = new DeviceInfo[0];
                }
            }
            catch (Exception ex) { LastError = ex.GetType().Name + ": " + ex.Message; }
            finally { base.WndProc(ref message); } // Calls DefWindowProc for WM_INPUT cleanup.
        }
        private void Read(IntPtr input)
        {
            uint size = 0, headerSize = (uint)Marshal.SizeOf(typeof(Native.Header));
            if (Native.GetRawInputData(input, 0x10000003, IntPtr.Zero, ref size, headerSize) == uint.MaxValue) throw new Win32Exception();
            if (size < headerSize + 24 || size > 65536) return;
            if (size > capacity)
            {
                if (buffer != IntPtr.Zero) Marshal.FreeHGlobal(buffer);
                buffer = IntPtr.Zero; capacity = 0;
                buffer = Marshal.AllocHGlobal((int)size); capacity = size;
            }
            uint read = Native.GetRawInputData(input, 0x10000003, buffer, ref size, headerSize);
            if (read == uint.MaxValue) throw new Win32Exception();
            if (read < headerSize + 24) return;
            Native.Header header = (Native.Header)Marshal.PtrToStructure(buffer, typeof(Native.Header));
            if (header.Type != 0) return;
            MouseAccumulator accumulator;
            if (header.Device == IntPtr.Zero || !accumulators.TryGetValue(header.Device.ToInt64(), out accumulator))
            { UnidentifiedEvents++; return; }
            Native.Mouse mouse = (Native.Mouse)Marshal.PtrToStructure(IntPtr.Add(buffer, (int)headerSize), typeof(Native.Mouse));
            accumulator.Add(mouse.X, mouse.Y, mouse.ButtonFlags, mouse.ButtonData, (mouse.Flags & 1) != 0);
            Action<RawMousePacket> handler = MouseInput;
            if (handler != null) handler(new RawMousePacket {
                Device = header.Device.ToInt64(), X = mouse.X, Y = mouse.Y,
                Absolute = (mouse.Flags & 1) != 0, MessageTick = Native.GetMessageTime()
            });
        }
        public void Dispose()
        {
            if (disposed) return;
            CheckThread(); disposed = true;
            if (registered)
            {
                Native.Registration registration = new Native.Registration { Page = 1, Usage = 2, Flags = 1, Target = IntPtr.Zero };
                if (!Native.RegisterRawInputDevices(new[] { registration }, 1, (uint)Marshal.SizeOf(typeof(Native.Registration))))
                    LastError = "Raw Input unregister failed: " + new Win32Exception().Message;
                registered = false;
            }
            DestroyHandle();
            if (buffer != IntPtr.Zero) Marshal.FreeHGlobal(buffer);
            buffer = IntPtr.Zero;
        }
    }
}
