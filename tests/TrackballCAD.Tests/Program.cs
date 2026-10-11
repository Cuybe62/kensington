using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using TrackballCAD.Input;
using TrackballCAD.Navigation;
namespace TrackballCAD.Tests
{
    internal static class Program
    {
        private static int passed;
        private static void Check(bool value, string name)
        { if (!value) throw new Exception(name); passed++; Console.WriteLine("PASS " + name); }
        private static void Near(double value, double expected, string name) => Check(Math.Abs(value - expected) < 0.0000001, name);
        private static MotionFrame Frame(double x = 0, double y = 0, bool active = true) => new MotionFrame { X = x, Y = y, Active = active, UtcTicks = DateTime.UtcNow.Ticks };
        private sealed class FakeCad : ICadAdapter
        {
            public bool IsAvailable { get; set; } = true;
            public int Calls; public double X, Y;
            public void Rotate(double x, double y) { Calls++; X = x; Y = y; }
        }
        private static int Main()
        {
            try
            {
                var id = new DeviceIdentifier(@"\\?\HID#VID_047d&PID_8018#instanceA");
                Check(id.Vid == "047D" && id.Pid == "8018", "VID/PID parsing");
                Check(!id.MatchesExact(new DeviceIdentifier(@"\\?\HID#VID_047d&PID_8018#instanceB")), "Identical models remain distinct");
                Check(id.MatchesExact(new DeviceIdentifier(id.DevicePath.ToLowerInvariant())), "Path matching ignores case");
                Check(!new DeviceIdentifier("").MatchesExact(new DeviceIdentifier("")), "Missing identities do not match");
                var gate = new GestureGate();
                Check(!gate.Update(0, 4, true), "No activation from motion alone");
                Check(gate.Update(0x40, 4, true), "Button 4 activates");
                Check(!gate.Update(0, 4, false), "Focus loss cancels");
                Check(!gate.Update(0x40, 4, true), "Repeated down cannot rearm held button");
                gate.Update(0x80, 4, true);
                Check(gate.Update(0x40, 4, true), "Release and fresh press rearm");
                Check(!gate.Update(0x80, 4, true), "Release ends gesture");
                for (int b = 1; b <= 5; b++)
                { gate.Reset(); Check(gate.Update((ushort)(1 << ((b - 1) * 2)), b, true), "Activation button " + b); }
                var accumulator = new MotionAccumulator();
                accumulator.Add(Frame(2, -3)); accumulator.Add(Frame(4, 5));
                var frame = accumulator.Consume(); Near(frame.X, 6, "Accumulation X"); Near(frame.Y, 2, "Accumulation Y");
                Near(accumulator.Consume().X, 0, "Consume does not replay movement");
                accumulator.Add(Frame(10)); accumulator.Add(Frame(active: false));
                Check(!accumulator.Consume().Active, "Inactive clears pending gesture");
                accumulator.Add(Frame(10)); accumulator.Reset(); Near(accumulator.Consume().X, 0, "Reset drops pending motion");
                var old = Frame(8); old.UtcTicks -= TimeSpan.TicksPerSecond; accumulator.Add(old); accumulator.Add(Frame(1));
                Near(accumulator.Consume().X, 1, "Old displacement cannot survive a long input gap");
                accumulator.Reset();
                long timestamp = DateTime.UtcNow.Ticks;
                double total = 0;
                var producer = Task.Run(() => { for (int i = 0; i < 10000; i++) accumulator.Add(new MotionFrame { X = 0.0001, Active = true, UtcTicks = timestamp }); });
                while (!producer.IsCompleted) total += accumulator.Consume().X;
                producer.GetAwaiter().GetResult(); total += accumulator.Consume().X;
                Near(total, 1, "Concurrent producer and consumer conserve deltas");
                frame = Frame(0.1, -0.2);
                using (var stream = new MemoryStream())
                {
                    frame.Write(new BinaryWriter(stream)); stream.Position = 0;
                    var restored = MotionFrame.Read(new BinaryReader(stream));
                    Check(restored.Active && restored.UtcTicks == frame.UtcTicks && restored.X == frame.X && restored.Y == frame.Y, "Protocol round trip");
                }
                Check(frame.IsFresh(frame.UtcTicks + 100000) && !frame.IsFresh(frame.UtcTicks + TimeSpan.TicksPerSecond) && !frame.IsFresh(frame.UtcTicks - 1), "Expiry and future timestamp rejected");
                Check(!new MotionFrame { UtcTicks = long.MinValue }.IsFresh(DateTime.UtcNow.Ticks), "Invalid timestamp cannot overflow freshness check");
                using (var stream = new MemoryStream())
                {
                    var invalid = Frame(double.NaN); invalid.Write(new BinaryWriter(stream)); stream.Position = 0;
                    bool rejected = false; try { MotionFrame.Read(new BinaryReader(stream)); } catch (InvalidDataException) { rejected = true; }
                    Check(rejected, "Protocol rejects NaN");
                }
                using (var stream = new MemoryStream(new byte[] { 1, 2 }))
                {
                    bool rejected = false; try { MotionFrame.Read(new BinaryReader(stream)); } catch (EndOfStreamException) { rejected = true; }
                    Check(rejected, "Protocol rejects truncated packet");
                }
                var cad = new FakeCad(); var engine = new NavigationEngine(cad);
                Check(!engine.Update(Frame(active: false)) && cad.Calls == 0, "No COM on inactive frame");
                Check(!engine.Update(Frame()) && cad.Calls == 0, "No COM on stationary frame");
                cad.IsAvailable = false; Check(!engine.Update(Frame(0.1)), "No document means no rotation");
                cad.IsAvailable = true; Check(engine.Update(Frame(4, -4)), "Rotation dispatched");
                Near(cad.X, 0.25, "Positive angular cap"); Near(cad.Y, -0.25, "Negative angular cap");
                var assembly = typeof(RawInputManager).Assembly;
                var header = assembly.GetType("TrackballCAD.Input.NativeMethods+Header");
                var mouse = assembly.GetType("TrackballCAD.Input.NativeMethods+Mouse");
                Check(IntPtr.Size == 8 && Marshal.SizeOf(header) == 24 && Marshal.SizeOf(mouse) == 24, "Win64 RAWINPUT layout");
                Check(Marshal.OffsetOf(mouse, "ButtonFlags").ToInt32() == 4 && Marshal.OffsetOf(mouse, "X").ToInt32() == 12, "RAWMOUSE union alignment");
                Console.WriteLine(passed + " checks passed."); return 0;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
    }
}
