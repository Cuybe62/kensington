using System;
using System.Text.RegularExpressions;

namespace TrackballCAD.Input
{
    public sealed class DeviceIdentifier
    {
        public string DevicePath { get; }
        public string Vid { get; }
        public string Pid { get; }
        public DeviceIdentifier(string path)
        {
            DevicePath = path ?? "";
            Vid = Extract("VID"); Pid = Extract("PID");
        }
        private string Extract(string key)
        {
            var match = Regex.Match(DevicePath, key + "_([0-9A-F]{4})", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value.ToUpperInvariant() : "";
        }
        public bool MatchesExact(DeviceIdentifier other) => other != null && DevicePath.Length != 0 &&
            string.Equals(DevicePath, other.DevicePath, StringComparison.OrdinalIgnoreCase);
        // VID/PID identifies a model, not an individual device. Never auto-select on this alone.
        public override string ToString() => "VID=" + Vid + " PID=" + Pid + " " + DevicePath;
    }
}
