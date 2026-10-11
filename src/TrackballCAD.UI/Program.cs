using System;
using System.Windows.Forms;
namespace TrackballCAD.UI
{
    internal static class Program
    {
        [STAThread] private static void Main()
        {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            try { Application.Run(new DiagnosticsWindow()); }
            catch (Exception ex) { MessageBox.Show(ex.ToString(), "TrackballCAD startup failed"); }
        }
    }
}
