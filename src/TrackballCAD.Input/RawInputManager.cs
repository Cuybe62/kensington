using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Forms;
namespace TrackballCAD.Input
{
    // Own exclusively from the UI STA thread, in the separate capture process.
    public sealed class RawInputManager : NativeWindow, IDisposable
    {
        public event EventHandler<TrackballState> Input;
        public event EventHandler DevicesChanged;
        public event Action<Exception> Error;
        private IntPtr buffer;
        private uint capacity;
        private bool disposed;
        public RawInputManager()
        {
            CreateHandle(new CreateParams { Caption = "TrackballCAD Raw Input", Parent = new IntPtr(-3) });
            var registration = new NativeMethods.Registration { Page = 1, Usage = 2, Flags = 0x2100, Target = Handle }; // INPUTSINK | DEVNOTIFY
            if (!NativeMethods.RegisterRawInputDevices(new[] { registration }, 1, (uint)Marshal.SizeOf(typeof(NativeMethods.Registration))))
            { var error = new Win32Exception(); DestroyHandle(); throw error; }
        }
        protected override void WndProc(ref Message m)
        {
            try
            {
                if (m.Msg == 0x00FF) Read(m.LParam);
                else if (m.Msg == 0x00FE) DevicesChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex) { Error?.Invoke(ex); }
            finally { base.WndProc(ref m); } // DefWindowProc performs WM_INPUT cleanup.
        }
        private void Read(IntPtr input)
        {
            uint size = 0, headerSize = (uint)Marshal.SizeOf(typeof(NativeMethods.Header));
            if (NativeMethods.GetRawInputData(input, 0x10000003, IntPtr.Zero, ref size, headerSize) == uint.MaxValue) throw new Win32Exception();
            if (size < headerSize || size > 65536) return;
            if (size > capacity)
            {
                if (buffer != IntPtr.Zero) Marshal.FreeHGlobal(buffer);
                buffer = IntPtr.Zero; capacity = 0;
                buffer = Marshal.AllocHGlobal((int)size); capacity = size;
            }
            uint read = NativeMethods.GetRawInputData(input, 0x10000003, buffer, ref size, headerSize);
            if (read == uint.MaxValue) throw new Win32Exception();
            if (read < headerSize + 24) return;
            var header = Marshal.PtrToStructure<NativeMethods.Header>(buffer);
            if (header.Type != 0 || header.Device == IntPtr.Zero) return;
            var mouse = Marshal.PtrToStructure<NativeMethods.Mouse>(IntPtr.Add(buffer, (int)headerSize));
            Input?.Invoke(this, new TrackballState(header.Device, mouse.X, mouse.Y,
                (short)((mouse.ButtonFlags & 0x400) != 0 ? mouse.ButtonData : 0),
                (short)((mouse.ButtonFlags & 0x800) != 0 ? mouse.ButtonData : 0), mouse.ButtonFlags, (mouse.Flags & 1) != 0));
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            var registration = new NativeMethods.Registration { Page = 1, Usage = 2, Flags = 1 };
            if (!NativeMethods.RegisterRawInputDevices(new[] { registration }, 1, (uint)Marshal.SizeOf(typeof(NativeMethods.Registration))))
                Error?.Invoke(new Win32Exception());
            DestroyHandle();
            if (buffer != IntPtr.Zero) Marshal.FreeHGlobal(buffer);
            buffer = IntPtr.Zero;
        }
    }
}
