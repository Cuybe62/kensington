using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swpublished;
using TrackballCAD.Navigation;
namespace TrackballCAD.SolidWorks
{
    [ComVisible(true), Guid("52273221-553B-4A82-AE18-6A4A7E123EBB"), ProgId("TrackballCAD.SolidWorksAddin"), ClassInterface(ClassInterfaceType.None)]
    public sealed class SolidWorksAddin : ISwAddin
    {
        private System.Windows.Forms.Timer timer;
        private NavigationServer server;
        private SolidWorksAdapter adapter;
        private NavigationEngine engine;
        private bool updating;
        private bool requireRelease = true;
        private long lastFrame;
        private int pid;
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
        public bool ConnectToSW(object ThisSW, int Cookie)
        {
            try
            {
                if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA) throw new InvalidOperationException("Expected SolidWorks STA.");
                pid = Process.GetCurrentProcess().Id;
                adapter = new SolidWorksAdapter((ISldWorks)ThisSW); engine = new NavigationEngine(adapter);
                server = new NavigationServer(pid);
                timer = new System.Windows.Forms.Timer { Interval = 16 };
                timer.Tick += Tick; timer.Start(); return true;
            }
            catch (Exception ex) { Trace.WriteLine("TrackballCAD startup: " + ex); DisconnectFromSW(); return false; }
        }
        private void Tick(object sender, EventArgs args)
        {
            if (updating) return;
            long now = Stopwatch.GetTimestamp();
            // WM_TIMER is best effort; never catch up missed frames in bursts.
            if ((now - lastFrame) / (double)Stopwatch.Frequency < 1.0 / 120) return;
            lastFrame = now; updating = true;
            try
            {
                uint foreground; GetWindowThreadProcessId(GetForegroundWindow(), out foreground);
                bool changed = adapter.Refresh();
                server.Available = adapter.IsAvailable && foreground == pid;
                if (changed || !server.Available) { requireRelease = true; server.Pending.Reset(); return; }
                var frame = server.Pending.Consume();
                if (!frame.IsFresh(DateTime.UtcNow.Ticks)) { requireRelease = true; server.Pending.Reset(); return; }
                if (!frame.Active) requireRelease = false;
                if (requireRelease) return;
                if (engine.Update(frame)) server.AppliedFrames++;
            }
            catch (COMException ex) { requireRelease = true; server.Available = false; server.Pending.Reset(); adapter.Clear(); Trace.WriteLine("TrackballCAD COM: " + ex); }
            finally { updating = false; }
        }
        public bool DisconnectFromSW()
        {
            if (timer != null) { timer.Stop(); timer.Tick -= Tick; timer.Dispose(); timer = null; }
            server?.Dispose(); server = null; adapter?.Clear(); adapter = null; engine = null; return true;
        }
        [ComRegisterFunction] public static void Register(Type type)
        {
            using (var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\SolidWorks\Addins\" + type.GUID.ToString("B")))
            { key.SetValue(null, 0, RegistryValueKind.DWord); key.SetValue("Title", "TrackballCAD"); key.SetValue("Description", "Experimental independent trackball rotation"); }
        }
        [ComUnregisterFunction] public static void Unregister(Type type)
        { Registry.LocalMachine.DeleteSubKeyTree(@"SOFTWARE\SolidWorks\Addins\" + type.GUID.ToString("B"), false); }
    }
}
