namespace TrackballCAD.Navigation
{
    // Must see a fresh button-down in the target application; focus regain never re-arms.
    public sealed class GestureGate
    {
        public bool Active { get; private set; }
        private bool held;
        public bool Update(ushort flags, int button, bool allowed)
        {
            int down = 1 << ((button - 1) * 2), up = down << 1;
            if (!allowed) Active = false;
            if ((flags & down) != 0 && !held) { held = true; Active = allowed; }
            if ((flags & up) != 0) { held = false; Active = false; }
            return Active;
        }
        public void Cancel() { Active = false; }
        public void Reset() { held = false; Active = false; }
    }
}
