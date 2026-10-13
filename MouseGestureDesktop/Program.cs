using System.Threading;
using System.Windows.Forms;
using MouseGestureDesktop.App;

namespace MouseGestureDesktop;

internal static class Program
{
    private static Mutex? _singleInstanceMutex;

    [STAThread]
    private static void Main()
    {
        // Single-instance guard: a global mutex keeps only one tray app alive.
        _singleInstanceMutex = new Mutex(initiallyOwned: true, "MouseGestureDesktop.SingleInstance", out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show("鼠标手势切换桌面已在运行（见系统托盘）。", "MouseGestureDesktop",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApplicationContext());

        GC.KeepAlive(_singleInstanceMutex);
    }
}
