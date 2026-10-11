using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace TrackballCAD.Portable
{
    public sealed class NavigationWindow : Form
    {
        private readonly RotationSettings settings;
        private readonly RotationEngine engine;
        private readonly SolidWorksRotationAdapter adapter = new SolidWorksRotationAdapter();
        private readonly Timer timer;
        private RawInputBridge bridge;
        private readonly ComboBox devices = new ComboBox { Width = 830, DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly ComboBox modifier = new ComboBox { Width = 125, DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly NumericUpDown sensitivity = new NumericUpDown { Minimum = 0.0001M, Maximum = 0.02M, DecimalPlaces = 4, Increment = 0.0005M, Width = 95 };
        private readonly NumericUpDown hz = new NumericUpDown { Minimum = 30, Maximum = 60, Width = 65 };
        private readonly CheckBox invertX = new CheckBox { Text = "Invert X", AutoSize = true };
        private readonly CheckBox invertY = new CheckBox { Text = "Invert Y", AutoSize = true };
        private readonly Label status = new Label { AutoSize = true, MaximumSize = new Size(830, 0) };
        private readonly Label metrics = new Label { AutoSize = true };
        private readonly Button enable = new Button { Text = "Enable rotation", AutoSize = true };
        private readonly Button connect = new Button { Text = "Connect SolidWorks", AutoSize = true };
        private readonly Button save = new Button { Text = "Save settings on close", AutoSize = true };
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private bool running, connected, updating, disposed, closePending;
        private string stopRequested;
        private int applied, previousApplied;
        private double rateStart, uiTime, measuredHz, lastComMs;
        private long lastRawX, lastRawY;
        private int keyCode;
        private long generation;
        private readonly string savedPath;
        public DeviceInfo SelectedDevice { get; private set; }
        public bool SaveRequested { get; private set; }
        public RotationSettings Settings { get { return settings; } }
        public string LastError { get; private set; }

        public NavigationWindow(RotationSettings settings, string savedPath)
        {
            settings.Validate(); this.settings = settings; this.savedPath = savedPath;
            keyCode = settings.VirtualKey; engine = new RotationEngine(settings);
            Text = "TrackballCAD Portable - continuous rotation prototype";
            ClientSize = new Size(870, 350); MinimumSize = new Size(890, 390);
            FlowLayoutPanel layout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
            Controls.Add(layout);
            layout.Controls.Add(new Label { Text = "Cursor remains shared. Rotation only; no pan/zoom. Hold the chosen keyboard modifier in SolidWorks.", AutoSize = true });
            layout.Controls.Add(devices);
            FlowLayoutPanel actions = new FlowLayoutPanel { Width = 840, Height = 38 };
            Button refresh = new Button { Text = "Refresh devices", AutoSize = true };
            Button stop = new Button { Text = "STOP (Escape)", AutoSize = true };
            actions.Controls.Add(refresh); actions.Controls.Add(connect); actions.Controls.Add(enable); actions.Controls.Add(stop);
            layout.Controls.Add(actions);
            FlowLayoutPanel options = new FlowLayoutPanel { Width = 840, Height = 38 };
            modifier.Items.AddRange(new object[] { "LeftShift", "RightShift", "LeftControl", "RightControl" });
            modifier.SelectedItem = settings.ActivationKey; sensitivity.Value = (decimal)settings.RadiansPerCount;
            hz.Value = settings.UpdateHz; invertX.Checked = settings.InvertX; invertY.Checked = settings.InvertY;
            options.Controls.Add(new Label { Text = "Hold key", AutoSize = true }); options.Controls.Add(modifier);
            options.Controls.Add(new Label { Text = "rad/count", AutoSize = true }); options.Controls.Add(sensitivity);
            options.Controls.Add(new Label { Text = "Target Hz", AutoSize = true }); options.Controls.Add(hz);
            options.Controls.Add(invertX); options.Controls.Add(invertY); layout.Controls.Add(options);
            layout.Controls.Add(save); layout.Controls.Add(status); layout.Controls.Add(metrics);
            layout.Controls.Add(new Label { Text = "Release/repress the modifier after focus loss. Escape stops navigation globally while this tool responds.\r\nClose modal dialogs and finish active CAD commands before rotating. Native clicks/keys are not suppressed.", AutoSize = true });
            timer = new Timer { Interval = (int)Math.Ceiling(1000.0 / settings.UpdateHz) };
            timer.Tick += delegate { TickNavigation(); };
            devices.SelectedIndexChanged += delegate {
                RequestStop("Device changed; enable rotation explicitly.");
                SelectedDevice = devices.SelectedItem as DeviceInfo;
                engine.Select(SelectedDevice == null ? 0 : SelectedDevice.Handle);
            };
            refresh.Click += delegate { if (!updating) RefreshDevices(false); };
            connect.Click += delegate {
                if (updating) return;
                RequestStop("Connecting..."); updating = true;
                try { adapter.Connect(); connected = true; status.Text = "Connected, revision " + adapter.Revision + ". Select the trackball, then Enable rotation."; stopRequested = null; }
                catch (Exception ex) { connected = false; RecordError(ex); }
                finally { updating = false; }
            };
            enable.Click += delegate {
                if (updating) return;
                if (!connected || SelectedDevice == null) { status.Text = "Select a device and connect to SolidWorks first."; return; }
                if (bridge == null || !string.IsNullOrEmpty(bridge.LastError)) { status.Text = "Capture unavailable. Close and restart the tool."; return; }
                engine.Reset(); generation++; running = true; stopRequested = null;
                status.Text = "Enabled. Return to SolidWorks with modifier released, then hold " + settings.ActivationKey + " and move the ball.";
            };
            stop.Click += delegate { RequestStop("Stopped by user."); };
            EventHandler optionsChanged = delegate {
                RequestStop("Settings changed; enable rotation again.");
                settings.ActivationKey = (string)modifier.SelectedItem; settings.RadiansPerCount = (double)sensitivity.Value;
                settings.UpdateHz = (int)hz.Value; settings.InvertX = invertX.Checked; settings.InvertY = invertY.Checked;
                keyCode = settings.VirtualKey; timer.Interval = (int)Math.Ceiling(1000.0 / settings.UpdateHz);
            };
            modifier.SelectedIndexChanged += optionsChanged; sensitivity.ValueChanged += optionsChanged;
            hz.ValueChanged += optionsChanged; invertX.CheckedChanged += optionsChanged; invertY.CheckedChanged += optionsChanged;
            save.Click += delegate { SaveRequested = true; status.Text = "Settings will be saved on close. Device selection must be present."; };
            Shown += delegate {
                try { bridge = new RawInputBridge(); bridge.MouseInput += OnRawInput; RefreshDevices(true); timer.Start(); }
                catch (Exception ex) { RecordError(ex); }
            };
            FormClosing += delegate(object sender, FormClosingEventArgs args) {
                RequestStop("Closing.");
                if (updating) { args.Cancel = true; closePending = true; }
            };
        }
        private void RequestStop(string reason)
        { running = false; generation++; engine.Reset(); stopRequested = reason; }
        private void RecordError(Exception ex)
        {
            while (ex.InnerException != null) ex = ex.InnerException;
            LastError = adapter.Stage + " / 0x" + ex.HResult.ToString("X8") + ": " + ex.Message;
            RequestStop(LastError);
            status.Text = LastError;
        }
        private void RefreshDevices(bool initial)
        {
            RequestStop("Devices refreshed; selection and navigation reset.");
            SelectedDevice = null; engine.Select(0); devices.Items.Clear();
            if (bridge == null) return;
            try
            {
                foreach (DeviceInfo device in bridge.RefreshDevices()) devices.Items.Add(device);
                if (initial && !string.IsNullOrEmpty(savedPath))
                {
                    int match = -1;
                    for (int i = 0; i < devices.Items.Count; i++)
                        if (string.Equals(((DeviceInfo)devices.Items[i]).Path, savedPath, StringComparison.OrdinalIgnoreCase))
                        { if (match != -1) { match = -1; break; } match = i; }
                    if (match >= 0) devices.SelectedIndex = match;
                }
            }
            catch (Exception ex) { RecordError(ex); }
        }
        private bool UpdateGate()
        {
            if (NavigationWindows.Down(0x1B)) RequestStop("Stopped by Escape. Click Enable rotation to resume.");
            bool allowed = running && connected && bridge != null && !bridge.DevicesChanged && string.IsNullOrEmpty(bridge.LastError) && adapter.IsForeground;
            return engine.UpdateGate(allowed, NavigationWindows.Down(keyCode), Environment.TickCount);
        }
        private void OnRawInput(RawMousePacket packet)
        {
            // Win32 checks and pure accumulation only. No COM, PowerShell or UI operations.
            UpdateGate();
            engine.Add(packet, Environment.TickCount);
        }
        private void TickNavigation()
        {
            if (updating || disposed) return;
            updating = true;
            try
            {
                if (bridge == null) return;
                if (!string.IsNullOrEmpty(bridge.LastError)) RequestStop(bridge.LastError);
                if (bridge.DevicesChanged) RefreshDevices(false);
                if (connected && !adapter.IsAlive) { connected = false; RequestStop("SolidWorks closed; reconnect explicitly."); }
                if (!UpdateGate()) adapter.EndGesture();
                RotationCommand command = engine.Consume(Environment.TickCount);
                if (command.HasMotion)
                {
                    long expectedGeneration = generation;
                    Stopwatch duration = Stopwatch.StartNew();
                    if (adapter.Rotate(command, delegate {
                        return expectedGeneration == generation && UpdateGate() && engine.Gesture == command.Gesture;
                    })) applied++;
                    lastComMs = duration.Elapsed.TotalMilliseconds;
                    if (lastComMs > 150) RequestStop("CAD update exceeded 150 ms; stopped to discard delayed movement. Re-enable when SolidWorks is ready.");
                }
                if (clock.Elapsed.TotalSeconds - uiTime >= 0.1)
                {
                    uiTime = clock.Elapsed.TotalSeconds;
                    foreach (InputSnapshot snapshot in bridge.ConsumeSnapshots())
                        if (SelectedDevice != null && snapshot.Device.Handle == SelectedDevice.Handle) { lastRawX = snapshot.DeltaX; lastRawY = snapshot.DeltaY; }
                    double elapsed = clock.Elapsed.TotalSeconds - rateStart;
                    if (elapsed >= 1) { measuredHz = (applied - previousApplied) / elapsed; previousApplied = applied; rateStart = clock.Elapsed.TotalSeconds; }
                    metrics.Text = "Mode: " + (engine.Active ? "Rotate" : "None") + " | raw sums: " + lastRawX + ", " + lastRawY +
                        " | API updates: " + measuredHz.ToString("F1") + " Hz | last COM: " + lastComMs.ToString("F1") + " ms | total: " + applied;
                }
                if (stopRequested != null) { status.Text = stopRequested; stopRequested = null; }
            }
            catch (Exception ex) { RecordError(ex); adapter.EndGesture(); }
            finally
            {
                updating = false;
                if (closePending) { closePending = false; BeginInvoke(new Action(Close)); }
            }
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing && !disposed)
            {
                disposed = true; timer.Stop(); timer.Dispose(); engine.Reset();
                if (bridge != null) { bridge.MouseInput -= OnRawInput; bridge.Dispose(); bridge = null; }
                adapter.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
