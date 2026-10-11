using System;
namespace TrackballCAD.Navigation
{
    public sealed class NavigationEngine
    {
        private readonly ICadAdapter adapter;
        public NavigationEngine(ICadAdapter adapter) { this.adapter = adapter ?? throw new ArgumentNullException(nameof(adapter)); }
        // Only the SolidWorks STA calls this method. Angles are already calibrated by capture.
        public bool Update(MotionFrame frame)
        {
            if (!frame.Active || (frame.X == 0 && frame.Y == 0) || !adapter.IsAvailable) return false;
            adapter.Rotate(Math.Max(-0.25, Math.Min(0.25, frame.X)), Math.Max(-0.25, Math.Min(0.25, frame.Y)));
            return true;
        }
    }
}
