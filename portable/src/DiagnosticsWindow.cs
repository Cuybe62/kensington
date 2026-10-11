using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace TrackballCAD.Portable
{
    // All handlers are managed C#. No PowerShell script blocks invoked from WndProc.
    public sealed class DiagnosticsWindow : Form
    {
        private RawInputBridge bridge;
        private readonly Timer timer;
        private readonly ListView list;
        private readonly TextBox identity;
        private readonly Label status;
        private readonly Label selected;
        private readonly Stopwatch elapsed = Stopwatch.StartNew();
        private double previousSeconds;
        private bool resourcesDisposed;
        public DeviceInfo SelectedDevice { get; private set; }
        public string CaptureError { get; private set; }

        public DiagnosticsWindow(int refreshMs, string savedPath)
        {
            if (refreshMs < 50 || refreshMs > 1000) throw new ArgumentOutOfRangeException("refreshMs");
            Text = "TrackballCAD Portable - Raw Input diagnostic ONLY";
            ClientSize = new Size(1160, 550); MinimumSize = new Size(800, 400);
            CaptureError = "";
            FlowLayoutPanel header = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 90, FlowDirection = FlowDirection.TopDown, WrapContents = false };
            header.Controls.Add(new Label { AutoSize = true, Text = "Move ONE device at a time. X/Y are raw counts; no CAD navigation or cursor suppression." });
            header.Controls.Add(new Label { AutoSize = true, Text = "Move the ring/wheel to see whether Windows exposes wheel events. No third rotation axis is assumed." });
            selected = new Label { AutoSize = true, Text = "No device selected. Select a row, then confirm below." };
            header.Controls.Add(selected);
            status = new Label { AutoSize = true, Text = "Starting capture..." }; header.Controls.Add(status);
            list = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = false, HideSelection = false };
            string[] columns = { "Handle", "Events", "Events/s", "sum dx", "sum dy", "last X", "last Y", "Absolute", "Wheel", "HWheel", "Held", "Flags / interval", "Wheel seen", "VID:PID" };
            foreach (string column in columns) list.Columns.Add(column, 78);
            list.Columns[11].Width = 120;
            identity = new TextBox { Dock = DockStyle.Bottom, Height = 65, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };
            identity.Text = string.IsNullOrEmpty(savedPath) ? "No saved device. Device paths appear here when a row is selected." : "Saved path (manual confirmation required): " + savedPath;
            FlowLayoutPanel buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 38 };
            Button choose = new Button { Text = "Use selected device (save on close)", AutoSize = true };
            Button refresh = new Button { Text = "Refresh / reset counters", AutoSize = true };
            Button stop = new Button { Text = "Stop / close (Esc)", AutoSize = true, DialogResult = DialogResult.Cancel };
            buttons.Controls.Add(choose); buttons.Controls.Add(refresh); buttons.Controls.Add(stop);
            Controls.Add(list); Controls.Add(identity); Controls.Add(buttons); Controls.Add(header);
            CancelButton = stop;
            choose.Click += delegate {
                if (list.SelectedItems.Count == 0) return;
                SelectedDevice = (DeviceInfo)list.SelectedItems[0].Tag;
                selected.Text = "Chosen: " + SelectedDevice.Label + " (configuration saved when this window closes).";
            };
            refresh.Click += delegate { RefreshDevices(); };
            stop.Click += delegate { Close(); };
            list.SelectedIndexChanged += delegate {
                if (list.SelectedItems.Count != 0)
                    identity.Text = ((DeviceInfo)list.SelectedItems[0].Tag).Path;
            };
            timer = new Timer { Interval = refreshMs };
            timer.Tick += delegate { UpdateSamples(); };
            Shown += delegate {
                try { bridge = new RawInputBridge(); RefreshDevices(); timer.Start(); }
                catch (Exception ex) { Fail(ex.Message); }
            };
        }
        private void RefreshDevices()
        {
            if (bridge == null) return;
            SelectedDevice = null;
            selected.Text = "Selection cleared after refresh / hotplug. Confirm the device again.";
            try
            {
                DeviceInfo[] devices = bridge.RefreshDevices();
                list.Items.Clear();
                foreach (DeviceInfo device in devices)
                {
                    string[] values = new string[14];
                    for (int i = 0; i < values.Length; i++) values[i] = "0";
                    values[0] = device.Handle.ToString("X"); values[13] = device.Vid + ":" + device.Pid;
                    ListViewItem row = new ListViewItem(values) { Tag = device };
                    list.Items.Add(row);
                }
                previousSeconds = elapsed.Elapsed.TotalSeconds;
                status.Text = devices.Length + " mouse collections found. Waiting for movement.";
            }
            catch (Exception ex) { Fail(ex.Message); }
        }
        private void UpdateSamples()
        {
            if (bridge == null) return;
            try
            {
                if (!string.IsNullOrEmpty(bridge.LastError)) { Fail(bridge.LastError); return; }
                if (bridge.DevicesChanged) { RefreshDevices(); return; }
                double now = elapsed.Elapsed.TotalSeconds, seconds = Math.Max(0.001, now - previousSeconds);
                previousSeconds = now;
                InputSnapshot[] snapshots = bridge.ConsumeSnapshots();
                list.BeginUpdate();
                try
                {
                    for (int i = 0; i < snapshots.Length; i++)
                    {
                        InputSnapshot s = snapshots[i]; ListViewItem row = list.Items[i];
                        string[] values = { s.Events.ToString(), (s.IntervalEvents / seconds).ToString("F0"), s.DeltaX.ToString(), s.DeltaY.ToString(),
                            s.LastX.ToString(), s.LastY.ToString(), s.Absolute.ToString(), s.Wheel.ToString(), s.HorizontalWheel.ToString(),
                            "0x" + s.HeldButtons.ToString("X2"), "0x" + s.IntervalButtonFlags.ToString("X4"),
                            (s.WheelObserved ? "V " : "") + (s.HorizontalWheelObserved ? "H" : ""), s.Device.Vid + ":" + s.Device.Pid };
                        for (int j = 0; j < values.Length; j++) row.SubItems[j + 1].Text = values[j];
                        row.BackColor = s.IntervalEvents > 0 ? Color.LightGoldenrodYellow : SystemColors.Window;
                    }
                }
                finally { list.EndUpdate(); }
                status.Text = "Capture only - no SolidWorks calls. Unidentified packets: " + bridge.UnidentifiedEvents + ". Native cursor behavior unchanged.";
            }
            catch (Exception ex) { Fail(ex.Message); }
        }
        private void Fail(string message)
        {
            CaptureError = message; timer.Stop(); SelectedDevice = null;
            status.Text = "Capture stopped: " + message;
            identity.Text = message + Environment.NewLine + "Close, run Check-Environment.ps1, then retry only if permitted.";
            if (bridge != null) { bridge.Dispose(); bridge = null; }
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing && !resourcesDisposed)
            {
                resourcesDisposed = true;
                timer.Stop(); timer.Dispose();
                if (bridge != null)
                {
                    bridge.Dispose();
                    if (!string.IsNullOrEmpty(bridge.LastError)) CaptureError = bridge.LastError;
                    bridge = null;
                }
            }
            base.Dispose(disposing);
        }
    }
}
