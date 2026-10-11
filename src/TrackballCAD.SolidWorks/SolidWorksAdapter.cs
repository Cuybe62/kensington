using System;
using System.Threading;
using SolidWorks.Interop.sldworks;
using TrackballCAD.Navigation;
namespace TrackballCAD.SolidWorks
{
    public sealed class SolidWorksAdapter : ICadAdapter
    {
        private readonly ISldWorks application;
        private readonly int ownerThread = Thread.CurrentThread.ManagedThreadId;
        private IModelDoc2 document;
        private IModelView view;
        public SolidWorksAdapter(ISldWorks application) { this.application = application; }
        private void CheckThread()
        { if (Thread.CurrentThread.ManagedThreadId != ownerThread) throw new InvalidOperationException("SolidWorks COM must stay on the add-in STA thread."); }
        public bool Refresh()
        {
            CheckThread();
            IModelDoc2 next = application.IActiveDoc2;
            // Drawings and view-only documents are deliberately excluded from this rotation MVP.
            IModelView nextView = next != null && (next.GetType() == 1 || next.GetType() == 2) && !next.IsOpenedViewOnly() ? next.IActiveView : null;
            bool changed = !ReferenceEquals(document, next) || !ReferenceEquals(view, nextView);
            document = next; view = nextView; return changed;
        }
        public bool IsAvailable { get { CheckThread(); return view != null; } }
        public void Rotate(double horizontalRadians, double verticalRadians)
        {
            CheckThread(); if (view == null) return;
            view.StartDynamics();
            try { view.RotateAboutCenter(verticalRadians, horizontalRadians); }
            finally { view.StopDynamics(); }
        }
        public void Clear() { CheckThread(); view = null; document = null; }
    }
}
