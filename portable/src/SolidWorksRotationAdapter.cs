using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

namespace TrackballCAD.Portable
{
    internal static class NavigationWindows
    {
        [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
        [DllImport("user32.dll")] internal static extern bool IsWindowEnabled(IntPtr window);
        [DllImport("user32.dll")] internal static extern bool IsWindow(IntPtr window);
        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
        internal static bool Down(int key) { return (GetAsyncKeyState(key) & 0x8000) != 0; }
    }

    // All RCWs are owned by the caller's STA. No COM calls from Raw Input handlers.
    public sealed class SolidWorksRotationAdapter : IDisposable
    {
        private readonly int owner = Thread.CurrentThread.ManagedThreadId;
        private object application, document, view;
        private long gesture = -1;
        private int processId;
        private IntPtr mainWindow;
        public string Revision { get; private set; }
        public string DocumentTitle { get; private set; }
        public string Stage { get; private set; }
        public string CleanupError { get; private set; }
        public SolidWorksRotationAdapter() { Stage = "Not connected"; CleanupError = ""; }
        private void CheckThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != owner || Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
                throw new InvalidOperationException("SolidWorks calls must stay on the owning STA.");
        }
        private static object Call(object target, string name, params object[] args)
        { return target.GetType().InvokeMember(name, BindingFlags.InvokeMethod, null, target, args); }
        private static object Read(object target, string name)
        { return target.GetType().InvokeMember(name, BindingFlags.GetProperty, null, target, new object[0]); }
        public void Connect()
        {
            CheckThread(); Dispose();
            try
            {
                Stage = "GetActiveObject";
                application = Marshal.GetActiveObject("SldWorks.Application");
                Stage = "RevisionNumber";
                Revision = Convert.ToString(Call(application, "RevisionNumber"));
                if (!Revision.StartsWith("33.", StringComparison.Ordinal)) throw new InvalidOperationException("This prototype has been tested only with revision 33.x.");
                Stage = "GetProcessID";
                processId = Convert.ToInt32(Call(application, "GetProcessID"));
                using (Process process = Process.GetProcessById(processId))
                {
                    mainWindow = process.MainWindowHandle;
                    if (mainWindow == IntPtr.Zero) throw new InvalidOperationException("No SolidWorks main window found.");
                }
                Stage = "Connected";
            }
            catch { Dispose(); throw; }
        }
        // Win32 only; safe to query while receiving raw packets. Strict main-window check rejects modals/detached views.
        public bool IsForeground
        {
            get
            {
                if (application == null || mainWindow == IntPtr.Zero || !NavigationWindows.IsWindow(mainWindow)) return false;
                uint actual;
                IntPtr foreground = NavigationWindows.GetForegroundWindow();
                NavigationWindows.GetWindowThreadProcessId(foreground, out actual);
                return actual == processId && foreground == mainWindow && NavigationWindows.IsWindowEnabled(mainWindow);
            }
        }
        public bool IsAlive { get { return application != null && NavigationWindows.IsWindow(mainWindow); } }
        private void BeginGesture(long currentGesture)
        {
            EndGesture();
            Stage = "ActiveDoc";
            document = Read(application, "ActiveDoc");
            if (document == null) throw new InvalidOperationException("Open a test part or assembly, then enable navigation again.");
            Stage = "Document.GetType";
            int type = Convert.ToInt32(Call(document, "GetType"));
            if (type != 1 && type != 2) throw new InvalidOperationException("Rotation requires a part or assembly, not a drawing.");
            Stage = "IsOpenedViewOnly";
            if (Convert.ToBoolean(Call(document, "IsOpenedViewOnly"))) throw new InvalidOperationException("Document is view-only or still loading.");
            Stage = "ActiveView";
            view = Read(document, "ActiveView");
            if (view == null) throw new InvalidOperationException("No active model view.");
            Stage = "GetTitle";
            DocumentTitle = Convert.ToString(Call(document, "GetTitle"));
            gesture = currentGesture;
        }
        public bool Rotate(RotationCommand command, Func<bool> stillAllowed)
        {
            CheckThread();
            if (!command.HasMotion || !IsForeground || !stillAllowed()) return false;
            if (application == null) throw new InvalidOperationException("Connect to SolidWorks first.");
            if (gesture != command.Gesture) BeginGesture(command.Gesture);
            object currentDocument = null, currentView = null;
            try
            {
                Stage = "Context check";
                currentDocument = Read(application, "ActiveDoc");
                if (!object.ReferenceEquals(document, currentDocument)) throw new InvalidOperationException("Active document changed. Re-enable navigation explicitly.");
                currentView = Read(document, "ActiveView");
                if (!object.ReferenceEquals(view, currentView)) throw new InvalidOperationException("Active view changed. Re-enable navigation explicitly.");
                if (!IsForeground || !stillAllowed() || !RotationEngine.IsFresh(command.MessageTick, Environment.TickCount)) return false;
                Stage = "RotateAboutCenter";
                Call(view, "RotateAboutCenter", command.Vertical, command.Horizontal);
                Stage = "GraphicsRedraw2 (rotation already applied)";
                Call(document, "GraphicsRedraw2");
                Stage = "Ready";
                return true;
            }
            finally { Release(currentView); Release(currentDocument); }
        }
        private void Release(object value)
        {
            if (value == null || !Marshal.IsComObject(value)) return;
            try { Marshal.ReleaseComObject(value); }
            catch (Exception ex) { CleanupError = ex.Message; }
        }
        public void EndGesture()
        {
            CheckThread();
            Release(view); view = null; Release(document); document = null; gesture = -1;
        }
        public void Dispose()
        {
            CheckThread(); EndGesture(); Release(application); application = null; mainWindow = IntPtr.Zero;
        }
    }
}
