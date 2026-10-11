using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using TrackballCAD.Navigation;
namespace TrackballCAD.UI
{
    internal sealed class NavigationClient : IDisposable
    {
        public readonly MotionAccumulator Pending = new MotionAccumulator();
        private readonly Thread worker;
        private readonly object sync = new object();
        private NamedPipeClientStream pipe;
        private volatile bool stopping;
        private long lastAck;
        private volatile string status = "Connecting";
        public string Status => Connected ? status : "Disconnected / no recent response";
        public bool Connected => DateTime.UtcNow.Ticks - Interlocked.Read(ref lastAck) < TimeSpan.TicksPerMillisecond * 300;
        public volatile int AppliedFrames;
        public NavigationClient(int pid)
        {
            worker = new Thread(() => Run(pid)) { IsBackground = true, Name = "TrackballCAD pipe sender" }; worker.Start();
        }
        private void Run(int pid)
        {
            while (!stopping)
            {
                try
                {
                    using (var connection = new NamedPipeClientStream(".", PipeAddress.ForProcess(pid), PipeDirection.InOut))
                    {
                        lock (sync) { if (stopping) return; pipe = connection; }
                        connection.Connect(250); Pending.Reset();
                        using (var reader = new BinaryReader(connection, System.Text.Encoding.UTF8, true))
                        using (var writer = new BinaryWriter(connection, System.Text.Encoding.UTF8, true))
                        {
                            while (!stopping)
                            {
                                var frame = Pending.Consume();
                                if (!frame.IsFresh(DateTime.UtcNow.Ticks)) frame = new MotionFrame { UtcTicks = DateTime.UtcNow.Ticks };
                                frame.Write(writer);
                                bool available = reader.ReadBoolean(); AppliedFrames = reader.ReadInt32();
                                status = available ? "Connected / 3D view available" : "Connected / navigation unavailable";
                                Interlocked.Exchange(ref lastAck, DateTime.UtcNow.Ticks);
                                Thread.Sleep(16); // No UI or COM calls on this worker.
                            }
                        }
                    }
                }
                catch (Exception ex) when (ex is IOException || ex is TimeoutException || ex is ObjectDisposedException || ex is UnauthorizedAccessException)
                { status = ex.Message; }
                finally { lock (sync) { pipe = null; } Pending.Reset(); Interlocked.Exchange(ref lastAck, 0); }
                if (!stopping) Thread.Sleep(250);
            }
        }
        public void Dispose()
        {
            stopping = true;
            lock (sync) { pipe?.Dispose(); }
            worker.Join(1000); Pending.Reset();
        }
    }
}
