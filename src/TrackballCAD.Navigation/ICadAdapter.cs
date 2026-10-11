namespace TrackballCAD.Navigation
{
    // The MVP exposes only implemented capabilities. Extend for pan/zoom after validation.
    public interface ICadAdapter
    {
        bool IsAvailable { get; }
        void Rotate(double horizontalRadians, double verticalRadians);
    }
    public enum NavigationMode { None, Rotate, Pan, Zoom, Roll }
}
