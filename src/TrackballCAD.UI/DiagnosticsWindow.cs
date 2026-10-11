using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using TrackballCAD.Input;
using TrackballCAD.Navigation;
using TrackballCAD.Settings;
namespace TrackballCAD.UI
{
    public sealed class DiagnosticsWindow : Form
    {
        private readonly ComboBox devices = new ComboBox { Width = 870, DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly NumericUpDown pid = new NumericUpDown { Minimum = 0, Maximum = int.MaxValue, Width = 100 };
        private readonly NumericUpDown button = new NumericUpDown { Minimum = 1, Maximum = 5, Value = 4, Width = 60 };
        private readonly NumericUpDown sensitivity = new NumericUpDown { Minimum = 0.0001M, Maximum = 0.02M, DecimalPlaces = 4, Increment = 0.0005M, Value = 0.002M };
        private readonly CheckBox enabled = new CheckBox { Text = "Enable held-button rotation", AutoSize = true };
        private readonly CheckBox clip = new CheckBox { Text = "Experimental cursor lock (blocks BOTH mice during gesture)", AutoSize = true };
        private readonly Label status = new Label { AutoSize = true };
        private readonly ListView events = new ListView { View = View.Details, Width = 880, Height = 260, FullRowSelect = true };
        private readonly Label error = new Label { AutoSize = true, ForeColor = Color.DarkRed, MaximumSize = new Size(860, 0) };
        private readonly Timer timer = new Timer { Interval = 100 };
        private readonly NavigationSettings settings = new NavigationSettings();
        private readonly GestureGate gate = new GestureGate();
        private readonly Dictionary<IntPtr, Sample> samples = new Dictionary<IntPtr, Sample>();
        private readonly RawInputManager input;
        private NavigationClient client;
        private IntPtr selected;
        private int targetPid;
        private DateTime started, rateStart = DateTime.UtcNow;
        private int lastFrames;
        private double rate;
        private sealed class Sample { public TrackballState Last; public long Count; }
        public DiagnosticsWindow()
        {
            Text = "TrackballCAD — MVP diagnostics"; Width = 930; Height = 640;
            var layout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
            Controls.Add(layout);
            layout.Controls.Add(new Label { Text = "Select by moving each device and comparing its handle/path below. Names are generic HID labels.", AutoSize = true });
            layout.Controls.Add(devices);
            var refresh = new Button { Text = "Refresh devices", AutoSize = true }; layout.Controls.Add(refresh);
            var row = new FlowLayoutPanel { Width = 880, Height = 36 };
            row.Controls.Add(new Label { Text = "SolidWorks PID", AutoSize = true }); row.Controls.Add(pid);
            var connect = new Button { Text = "Connect", AutoSize = true }; row.Controls.Add(connect);
            row.Controls.Add(new Label { Text = "Hold button", AutoSize = true }); row.Controls.Add(button);
            row.Controls.Add(new Label { Text = "rad / count", AutoSize = true }); row.Controls.Add(sensitivity);
            layout.Controls.Add(row); layout.Controls.Add(enabled); layout.Controls.Add(clip);
            layout.Controls.Add(new Label { Text = "Escape cancels. Release/repress after focus loss. Lock: 5 s maximum; other mouse input cancels.", AutoSize = true });
            layout.Controls.Add(status); layout.Controls.Add(events); layout.Controls.Add(error);
            foreach (var column in new[] { "Handle", "dx", "dy", "Wheel", "HWheel", "Button flags", "Absolute", "Events", "Device path" }) events.Columns.Add(column, column == "Device path" ? 350 : 75);
            input = new RawInputManager();
            input.Input += OnInput;
            input.DevicesChanged += (s, e) => RefreshDevices();
            input.Error += ex => { Cancel(); error.Text = ex.Message; };
            refresh.Click += (s, e) => RefreshDevices();
            devices.SelectedIndexChanged += (s, e) => { Cancel(); gate.Reset(); selected = (devices.SelectedItem as RawInputDevice)?.Handle ?? IntPtr.Zero; };
            enabled.CheckedChanged += (s, e) => Cancel();
            clip.CheckedChanged += (s, e) => { Cancel(); settings.ExperimentalCursorConstraint = clip.Checked; };
            button.ValueChanged += (s, e) => { Cancel(); gate.Reset(); settings.ActivationButton = (int)button.Value; };
            sensitivity.ValueChanged += (s, e) => { Cancel(); settings.RadiansPerCount = (double)sensitivity.Value; };
            connect.Click += (s, e) => { Cancel(); client?.Dispose(); targetPid = (int)pid.Value; client = targetPid > 0 ? new NavigationClient(targetPid) : null; lastFrames = 0; rateStart = DateTime.UtcNow; };
            timer.Tick += (s, e) => RefreshDiagnostics();
            FormClosed += (s, e) => { timer.Stop(); timer.Dispose(); Cancel(); input.Dispose(); client?.Dispose(); };
            RefreshDevices();
            var processes = Process.GetProcessesByName("SLDWORKS");
            if (processes.Length == 1) pid.Value = processes[0].Id;
            foreach (var process in processes) process.Dispose();
            timer.Start();
        }
        private bool Allowed => enabled.Checked && selected != IntPtr.Zero && client != null && client.Connected && WindowsContext.IsForeground(targetPid) && !WindowsContext.EscapePressed;
        private void Cancel()
        {
            gate.Cancel(); client?.Pending.Reset();
            try { WindowsContext.Release(); } catch (Exception ex) { error.Text = ex.Message; }
        }
        private void RefreshDevices()
        {
            Cancel(); gate.Reset(); selected = IntPtr.Zero;
            try
            {
                devices.Items.Clear(); samples.Clear();
                foreach (var device in DeviceManager.Enumerate()) devices.Items.Add(device);
                devices.SelectedIndex = -1; // Hotplug requires explicit selection; handles can be recycled.
                error.Text = "Select a device; no automatic VID/PID-only matching.";
            }
            catch (Exception ex) { error.Text = ex.Message; }
        }
        private void OnInput(object sender, TrackballState state)
        {
            Sample sample;
            if (!samples.TryGetValue(state.Device, out sample)) samples[state.Device] = sample = new Sample();
            sample.Last = state; sample.Count++;
            if (state.Device != selected)
            {
                if (settings.ExperimentalCursorConstraint && gate.Active) Cancel();
                return;
            }
            if (settings.ExperimentalCursorConstraint && gate.Active && DateTime.UtcNow - started > TimeSpan.FromSeconds(5)) { Cancel(); return; }
            bool wasActive = gate.Active;
            if (!gate.Update(state.ButtonFlags, settings.ActivationButton, Allowed) || state.IsAbsolute) { Cancel(); return; }
            if (!wasActive) started = DateTime.UtcNow;
            try { if (settings.ExperimentalCursorConstraint) WindowsContext.Constrain(); }
            catch (Exception ex) { Cancel(); error.Text = ex.Message; return; }
            client.Pending.Add(new MotionFrame { X = state.Dx * settings.RadiansPerCount, Y = state.Dy * settings.RadiansPerCount, Active = true, UtcTicks = DateTime.UtcNow.Ticks });
        }
        private void RefreshDiagnostics()
        {
            if (!Allowed || (settings.ExperimentalCursorConstraint && DateTime.UtcNow - started > TimeSpan.FromSeconds(5))) Cancel();
            // Keep the gesture alive without retaining any old displacement.
            if (gate.Active) client.Pending.Add(new MotionFrame { Active = true, UtcTicks = DateTime.UtcNow.Ticks });
            double elapsed = (DateTime.UtcNow - rateStart).TotalSeconds;
            if (elapsed >= 1)
            {
                int frames = client?.AppliedFrames ?? 0; rate = Math.Max(0, frames - lastFrames) / elapsed;
                lastFrames = frames; rateStart = DateTime.UtcNow;
            }
            status.Text = $"SolidWorks: {client?.Status ?? "Disconnected"} | Mode: {(gate.Active ? "Rotate" : "None")} | Applied rotation rate: {rate:F1} Hz";
            events.BeginUpdate(); events.Items.Clear();
            foreach (var pair in samples)
            {
                var s = pair.Value.Last;
                var device = devices.Items.Cast<RawInputDevice>().FirstOrDefault(d => d.Handle == pair.Key);
                events.Items.Add(new ListViewItem(new[] { pair.Key.ToInt64().ToString("X"), s.Dx.ToString(), s.Dy.ToString(), s.Wheel.ToString(), s.HorizontalWheel.ToString(), s.ButtonFlags.ToString("X4"), s.IsAbsolute.ToString(), pair.Value.Count.ToString(), device?.Identifier.DevicePath ?? "Unknown / detached" }));
            }
            events.EndUpdate();
        }
    }
}
