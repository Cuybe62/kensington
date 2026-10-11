using System;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using TrackballCAD.Navigation;
namespace TrackballCAD.SolidWorks
{
    internal sealed class NavigationServer : IDisposable
    {
        public readonly MotionAccumulator Pending = new MotionAccumulator();
        public volatile bool Available;
        public volatile int AppliedFrames;
        private readonly object sync = new object();
        private readonly Thread worker;
        private NamedPipeServerStream pipe;
        private volatile bool stopping;
        public NavigationServer(int pid)
        { worker = new Thread(() => Run(pid)) { IsBackground = true, Name = "TrackballCAD pipe receiver" }; worker.Start(); }
        private void Run(int pid)
        {
            var security = new PipeSecurity();
            security.SetAccessRuleProtection(true, false);
            security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.NetworkSid, null), PipeAccessRights.FullControl, AccessControlType.Deny));
            security.AddAccessRule(new PipeAccessRule(WindowsIdentity.GetCurrent().User, PipeAccessRights.FullControl, AccessControlType.Allow));
            while (!stopping)
            {
                try
                {
                    using (var connection = new NamedPipeServerStream(PipeAddress.ForProcess(pid), PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 1024, 1024, security))
                    {
                        lock (sync) { if (stopping) return; pipe = connection; }
                        connection.WaitForConnection(); Pending.Reset();
                        using (var reader = new BinaryReader(connection, System.Text.Encoding.UTF8, true))
                        using (var writer = new BinaryWriter(connection, System.Text.Encoding.UTF8, true))
                        {
                            while (!stopping)
                            {
                                var frame = MotionFrame.Read(reader);
                                if (frame.IsFresh(DateTime.UtcNow.Ticks)) Pending.Add(frame); else Pending.Reset();
                                writer.Write(Available); writer.Write(AppliedFrames); writer.Flush();
                            }
                        }
                    }
                }
                catch (Exception ex) when (ex is IOException || ex is ObjectDisposedException || ex is UnauthorizedAccessException)
                { System.Diagnostics.Trace.WriteLine("TrackballCAD pipe: " + ex.Message); }
                finally { lock (sync) { pipe = null; } Pending.Reset(); }
                if (!stopping) Thread.Sleep(100);
            }
        }
        public void Dispose()
        { stopping = true; lock (sync) { pipe?.Dispose(); } worker.Join(1000); Pending.Reset(); }
    }
}
