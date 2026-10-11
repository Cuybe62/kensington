namespace TrackballCAD.Settings
{
    // Session settings only. Persistence and advanced mappings belong to the next milestone.
    public sealed class NavigationSettings
    {
        public int ActivationButton { get; set; } = 4;
        public double RadiansPerCount { get; set; } = 0.002;
        public bool ExperimentalCursorConstraint { get; set; }
    }
    public sealed class DeviceSettings
    {
        public string DevicePath { get; set; }
        public string Vid { get; set; }
        public string Pid { get; set; }
    }
}
