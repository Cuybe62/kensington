using System;
using System.IO;
namespace TrackballCAD.Navigation
{
    public struct MotionFrame
    {
        public double X, Y;
        public bool Active;
        public long UtcTicks;
        public bool IsFresh(long now) => UtcTicks > 0 && UtcTicks <= now && now - UtcTicks <= TimeSpan.TicksPerMillisecond * 150;
        public void Write(BinaryWriter writer)
        {
            writer.Write(0x54424344); writer.Write(1); writer.Write(UtcTicks);
            writer.Write(X); writer.Write(Y); writer.Write(Active); writer.Flush();
        }
        public static MotionFrame Read(BinaryReader reader)
        {
            if (reader.ReadInt32() != 0x54424344 || reader.ReadInt32() != 1) throw new InvalidDataException("Invalid TrackballCAD protocol.");
            var frame = new MotionFrame { UtcTicks = reader.ReadInt64(), X = reader.ReadDouble(), Y = reader.ReadDouble(), Active = reader.ReadBoolean() };
            if (double.IsNaN(frame.X) || double.IsNaN(frame.Y) || double.IsInfinity(frame.X) || double.IsInfinity(frame.Y) || Math.Abs(frame.X) > 100 || Math.Abs(frame.Y) > 100)
                throw new InvalidDataException("Invalid rotation delta.");
            return frame;
        }
    }
    public sealed class MotionAccumulator
    {
        private readonly object sync = new object();
        private double x, y;
        private bool active;
        private long timestamp;
        public void Add(MotionFrame frame)
        {
            lock (sync)
            {
                if (!frame.Active) { x = y = 0; active = false; timestamp = frame.UtcTicks; return; }
                if (frame.UtcTicks < timestamp || frame.UtcTicks - timestamp > TimeSpan.TicksPerMillisecond * 150) x = y = 0;
                x = Math.Max(-100, Math.Min(100, x + frame.X)); y = Math.Max(-100, Math.Min(100, y + frame.Y));
                active = true; timestamp = frame.UtcTicks;
            }
        }
        public MotionFrame Consume()
        {
            lock (sync)
            {
                var frame = new MotionFrame { X = x, Y = y, Active = active, UtcTicks = timestamp };
                x = y = 0; return frame;
            }
        }
        public void Reset() { lock (sync) { x = y = 0; active = false; timestamp = 0; } }
    }
}
