using System;

namespace TrackballCAD.Portable
{
    public sealed class RotationSettings
    {
        public int UpdateHz { get; set; }
        public string ActivationKey { get; set; }
        public string ActivationMode { get; set; }
        public bool ReturnCursorOnStop { get; set; }
        public double RadiansPerCount { get; set; }
        public bool InvertX { get; set; }
        public bool InvertY { get; set; }
        public RotationSettings() { ReturnCursorOnStop = true; ActivationMode = "Hold"; UpdateHz = 30; ActivationKey = "LeftShift"; RadiansPerCount = 0.002; }
        public void Validate()
        {
            if (ActivationMode != "Hold" && ActivationMode != "Toggle") throw new ArgumentException("Use Hold or Toggle.");
            if (UpdateHz < 30 || UpdateHz > 60) throw new ArgumentOutOfRangeException("UpdateHz", "Use 30 to 60 Hz.");
            if (double.IsNaN(RadiansPerCount) || double.IsInfinity(RadiansPerCount) || RadiansPerCount < 0.0001 || RadiansPerCount > 0.02)
                throw new ArgumentOutOfRangeException("RadiansPerCount", "Use 0.0001 to 0.02 rad/count.");
            if (ActivationKey != "LeftShift" && ActivationKey != "RightShift" && ActivationKey != "LeftControl" && ActivationKey != "RightControl")
                throw new ArgumentException("Use LeftShift, RightShift, LeftControl or RightControl.");
        }
        public int VirtualKey
        {
            get
            {
                Validate();
                switch (ActivationKey) { case "RightShift": return 0xA1; case "LeftControl": return 0xA2; case "RightControl": return 0xA3; default: return 0xA0; }
            }
        }
    }

    public struct RotationCommand
    {
        public double Horizontal, Vertical;
        public int MessageTick;
        public long Gesture;
        public bool HasMotion { get { return Horizontal != 0 || Vertical != 0; } }
    }

    // Pure input interpretation, independent from Win32/COM/UI. Timestamp arguments make it testable.
    public sealed class RotationEngine
    {
        private readonly object sync = new object();
        private readonly RotationSettings settings;
        private bool active, requireRelease = true;
        private long selected, gesture;
        private int began, firstPending;
        private double x, y;
        public RotationEngine(RotationSettings settings) { settings.Validate(); this.settings = settings; }
        public bool Active { get { lock (sync) { return active; } } }
        public long Gesture { get { lock (sync) { return gesture; } } }
        public static bool IsFresh(int timestamp, int now) { return unchecked((uint)(now - timestamp)) <= 150; }
        public void Select(long device) { lock (sync) { selected = device; Reset(); } }
        public void Reset()
        {
            lock (sync) { x = y = 0; active = false; requireRelease = true; gesture++; }
        }
        // A slow successful COM call is not a stop request. Clear accumulated and
        // still-queued input through this timestamp without changing activation/gesture.
        // Keeping the gesture also avoids repeating the costly CAD context setup.
        public void DiscardPendingThrough(int now)
        {
            lock (sync) { x = y = 0; began = unchecked(now + 1); }
        }
        public bool UpdateGate(bool allowed, bool keyDown, int now)
        {
            lock (sync)
            {
                if (!allowed || !keyDown)
                {
                    x = y = 0;
                    if (active) gesture++;
                    active = false;
                    if (!allowed) requireRelease = true;
                    else if (!keyDown) requireRelease = false;
                    return false;
                }
                if (requireRelease || selected == 0) return false;
                if (!active) { active = true; began = now; gesture++; }
                return true;
            }
        }
        public void Add(RawMousePacket packet, int now)
        {
            lock (sync)
            {
                if (!active || packet.Device != selected || packet.Absolute || !IsFresh(packet.MessageTick, now)) return;
                // Drop packets queued before this key-hold gesture, even if processed after key-down.
                if (unchecked((uint)(packet.MessageTick - began)) > int.MaxValue) return;
                if (packet.X == 0 && packet.Y == 0) return;
                if (x == 0 && y == 0) firstPending = packet.MessageTick;
                if (!IsFresh(firstPending, now)) { x = y = 0; firstPending = packet.MessageTick; }
                x = Math.Max(-1000000, Math.Min(1000000, x + packet.X));
                y = Math.Max(-1000000, Math.Min(1000000, y + packet.Y));
            }
        }
        public RotationCommand Consume(int now)
        {
            lock (sync)
            {
                RotationCommand command = new RotationCommand { Gesture = gesture, MessageTick = firstPending };
                if (active && IsFresh(firstPending, now))
                {
                    command.Horizontal = Clamp(x * settings.RadiansPerCount * (settings.InvertX ? -1 : 1));
                    command.Vertical = Clamp(y * settings.RadiansPerCount * (settings.InvertY ? -1 : 1));
                }
                x = y = 0; return command;
            }
        }
        private static double Clamp(double angle) { return Math.Max(-0.15, Math.Min(0.15, angle)); }
    }
}
