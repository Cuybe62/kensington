using System;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace TrackballCAD.Portable
{
    public sealed class ViewTestResult
    {
        public string Stage { get; internal set; }
        public string Revision { get; internal set; }
        public string DocumentTitle { get; internal set; }
        public bool RotationRequested { get; internal set; }
        public bool RotationApplied { get; internal set; }
        public bool RedrawCompleted { get; internal set; }
        public string Error { get; internal set; }
        public string HResult { get; internal set; }
        public string CleanupError { get; internal set; }
    }

    public static class SolidWorksViewTest
    {
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
        [DllImport("user32.dll")] private static extern bool IsWindowEnabled(IntPtr window);
        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
        public static bool EscapePressed { get { return (GetAsyncKeyState(0x1B) & 0x8000) != 0; } }
        private static object Call(object target, string name, params object[] args)
        { return target.GetType().InvokeMember(name, BindingFlags.InvokeMethod, null, target, args); }
        private static object Read(object target, string name)
        { return target.GetType().InvokeMember(name, BindingFlags.GetProperty, null, target, new object[0]); }
        private static void CheckForeground(Process process)
        {
            if (EscapePressed) throw new InvalidOperationException("Cancelled with Escape.");
            process.Refresh();
            if (process.HasExited) throw new InvalidOperationException("SolidWorks closed.");
            IntPtr window = process.MainWindowHandle;
            uint foregroundPid;
            IntPtr foreground = GetForegroundWindow();
            GetWindowThreadProcessId(foreground, out foregroundPid);
            if (window == IntPtr.Zero || foregroundPid != process.Id || foreground != window || !IsWindowEnabled(window))
                throw new InvalidOperationException("Use the main SolidWorks window in the foreground, without a modal dialog or detached document window.");
        }
        private static void Release(object value, ViewTestResult result)
        {
            if (value == null || !Marshal.IsComObject(value)) return;
            try { Marshal.ReleaseComObject(value); }
            catch (Exception ex) { result.CleanupError += ex.Message + " "; }
        }
        public static ViewTestResult RotateOnce()
        {
            ViewTestResult result = new ViewTestResult { Stage = "Host", Error = "", CleanupError = "" };
            object application = null, document = null, view = null, currentDocument = null;
            Process[] processes = new Process[0];
            Stopwatch deadline = Stopwatch.StartNew();
            try
            {
                if (IntPtr.Size != 8 || Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
                    throw new InvalidOperationException("Windows PowerShell x64 STA required.");
                result.Stage = "Foreground";
                processes = Process.GetProcessesByName("SLDWORKS");
                if (processes.Length != 1) throw new InvalidOperationException("Keep exactly one SolidWorks instance open for this test.");
                Process process = processes[0];
                CheckForeground(process);
                result.Stage = "GetActiveObject";
                application = Marshal.GetActiveObject("SldWorks.Application");
                result.Stage = "GetProcessID";
                if (Convert.ToInt32(Call(application, "GetProcessID")) != process.Id)
                    throw new InvalidOperationException("The COM instance does not match the foreground process.");
                result.Stage = "RevisionNumber";
                result.Revision = Convert.ToString(Call(application, "RevisionNumber"));
                if (!result.Revision.StartsWith("33.", StringComparison.Ordinal))
                    throw new InvalidOperationException("This first test targets revision 33.x. Report the detected revision before testing another release.");
                result.Stage = "ActiveDoc";
                document = Read(application, "ActiveDoc");
                if (document == null) throw new InvalidOperationException("Open a test part or assembly first.");
                result.Stage = "Document.GetType";
                int type = Convert.ToInt32(Call(document, "GetType"));
                if (type != 1 && type != 2) throw new InvalidOperationException("Only parts and assemblies are supported; drawings are excluded.");
                result.Stage = "IsOpenedViewOnly";
                if (Convert.ToBoolean(Call(document, "IsOpenedViewOnly")))
                    throw new InvalidOperationException("View-only or loading document: wait for a fully loaded editable model.");
                result.Stage = "GetTitle";
                result.DocumentTitle = Convert.ToString(Call(document, "GetTitle"));
                result.Stage = "ActiveView";
                view = Read(document, "ActiveView");
                if (view == null) throw new InvalidOperationException("No active model view.");
                result.Stage = "FinalContextCheck";
                currentDocument = Read(application, "ActiveDoc");
                if (!object.ReferenceEquals(document, currentDocument))
                    throw new InvalidOperationException("Active document changed during the test. Nothing will be replayed.");
                CheckForeground(process);
                if (deadline.Elapsed.TotalSeconds > 2)
                    throw new InvalidOperationException("Preparation took too long. Cancelled instead of applying a delayed rotation.");
                result.Stage = "RotateAboutCenter";
                result.RotationRequested = true;
                Call(view, "RotateAboutCenter", 0.0, Math.PI / 36.0); // Single horizontal 5 degree rotation.
                result.RotationApplied = true;
                result.Stage = "GraphicsRedraw2";
                Call(document, "GraphicsRedraw2");
                result.RedrawCompleted = true;
                result.Stage = "Complete";
            }
            catch (Exception ex)
            {
                while (ex.InnerException != null) ex = ex.InnerException;
                result.HResult = "0x" + ex.HResult.ToString("X8");
                result.Error = ex.GetType().Name + ": " + ex.Message;
            }
            finally
            {
                // The two ActiveDoc queries each acquired a reference, even if RCWs match.
                Release(currentDocument, result); Release(view, result); Release(document, result); Release(application, result);
                foreach (Process process in processes) process.Dispose();
            }
            return result;
        }
    }

    public sealed class ViewTestWindow : Form
    {
        private readonly System.Windows.Forms.Timer timer;
        private readonly Stopwatch countdown = new Stopwatch();
        private readonly TextBox output;
        private readonly Button arm;
        private bool disposed;
        public ViewTestResult Result { get; private set; }
        public ViewTestWindow()
        {
            Text = "TrackballCAD Portable - explicit view test";
            ClientSize = new Size(750, 320);
            output = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
                Text = "Open a test part or assembly in SolidWorks. Close dialogs and finish active commands.\r\nClick Arm, then switch to the main SolidWorks window within 5 seconds.\r\nOne 5 degree view rotation only. No geometry changes or saving.\r\nEscape cancels the countdown even in SolidWorks. No trackball input is consumed." };
            FlowLayoutPanel buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 42 };
            arm = new Button { Text = "Arm one 5 degree rotation (5 s)", AutoSize = true };
            Button cancel = new Button { Text = "Cancel / close", AutoSize = true };
            buttons.Controls.Add(arm); buttons.Controls.Add(cancel);
            Controls.Add(output); Controls.Add(buttons);
            timer = new System.Windows.Forms.Timer { Interval = 100 };
            arm.Click += delegate { Result = null; arm.Enabled = false; countdown.Restart(); timer.Start(); };
            cancel.Click += delegate { timer.Stop(); Close(); };
            timer.Tick += delegate {
                if (SolidWorksViewTest.EscapePressed) { timer.Stop(); arm.Enabled = true; output.Text = "Cancelled. No rotation requested."; return; }
                double remaining = 5 - countdown.Elapsed.TotalSeconds;
                if (remaining > 0) { output.Text = "Switch to SolidWorks. Rotation in " + Math.Ceiling(remaining) + " s. Escape cancels."; return; }
                timer.Stop();
                // A suspended or busy UI must never execute an old armed request.
                if (countdown.Elapsed.TotalSeconds > 7) { output.Text = "Countdown expired while the UI was busy. Cancelled."; arm.Enabled = true; return; }
                Result = SolidWorksViewTest.RotateOnce();
                output.Text = "Stage: " + Result.Stage + "\r\nRevision: " + Result.Revision + "\r\nDocument: " + Result.DocumentTitle +
                    "\r\nRotation requested: " + Result.RotationRequested + "\r\nRotation API returned: " + Result.RotationApplied +
                    "\r\nRedraw completed: " + Result.RedrawCompleted + "\r\nHRESULT: " + Result.HResult + "\r\n" + Result.Error +
                    "\r\nCleanup: " + Result.CleanupError + "\r\nNo automatic retry. Confirm the visible result in SolidWorks.";
                arm.Enabled = true;
            };
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing && !disposed) { disposed = true; timer.Stop(); timer.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
