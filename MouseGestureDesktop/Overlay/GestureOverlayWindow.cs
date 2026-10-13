using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using MouseGestureDesktop.Config;

namespace MouseGestureDesktop.Overlay;

/// <summary>
/// Transparent, click-through, top-most layered window that renders the gesture trajectory.
/// All methods must be called on the UI thread. A horizontal band around the gesture origin is
/// used (rather than the full virtual screen) to keep rendering cheap.
/// </summary>
public sealed class GestureOverlayWindow : Form
{
    private const int BandHeight = 160;

    private const int WS_EX_LAYERED = 0x00080000;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_TOPMOST = 0x00000008;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    private GestureConfig _config = new();
    private Point _origin;         // screen coords of gesture origin
    private int _offset;
    private readonly System.Windows.Forms.Timer _fadeTimer;
    private float _alpha;

    public GestureOverlayWindow()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        Visible = false;

        _fadeTimer = new System.Windows.Forms.Timer { Interval = 16 };
        _fadeTimer.Tick += OnFadeTick;
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
            return cp;
        }
    }

    public void Begin(Point originScreen, GestureConfig config)
    {
        if (!config.ShowTrajectory)
            return;

        _config = config;
        _origin = originScreen;
        _offset = 0;
        _alpha = 1f;
        _fadeTimer.Stop();

        PositionBand();
        if (!Visible)
            Show();
        Render();
    }

    public void Update(int offset)
    {
        if (!_config.ShowTrajectory || !Visible)
            return;
        _offset = offset;
        Render();
    }

    public void End()
    {
        if (!Visible)
            return;
        _fadeTimer.Start();
    }

    private void OnFadeTick(object? sender, EventArgs e)
    {
        int fadeMs = Math.Max(1, _config.TrajectoryFadeMs);
        _alpha -= _fadeTimer.Interval / (float)fadeMs;
        if (_alpha <= 0f)
        {
            _alpha = 0f;
            _fadeTimer.Stop();
            Hide();
            return;
        }
        Render();
    }

    private void PositionBand()
    {
        Rectangle vs = SystemInformation.VirtualScreen;
        int top = Math.Clamp(_origin.Y - BandHeight / 2, vs.Top, vs.Bottom - BandHeight);
        Bounds = new Rectangle(vs.Left, top, vs.Width, BandHeight);
    }

    private void Render()
    {
        if (Width <= 0 || Height <= 0)
            return;

        using var bitmap = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bitmap))
        {
            int originX = _origin.X - Left;
            int centerY = _origin.Y - Top;
            TrajectoryRenderer.Render(g, originX, centerY, _offset, _config, _alpha);
        }

        UpdateLayeredFromBitmap(bitmap);
    }

    private void UpdateLayeredFromBitmap(Bitmap bitmap)
    {
        IntPtr screenDc = GetDC(IntPtr.Zero);
        IntPtr memDc = CreateCompatibleDC(screenDc);
        IntPtr hBitmap = IntPtr.Zero;
        IntPtr oldBitmap = IntPtr.Zero;

        try
        {
            hBitmap = bitmap.GetHbitmap(Color.FromArgb(0));
            oldBitmap = SelectObject(memDc, hBitmap);

            var size = new SIZE { cx = bitmap.Width, cy = bitmap.Height };
            var pointSource = new POINT { x = 0, y = 0 };
            var topPos = new POINT { x = Left, y = Top };
            var blend = new BLENDFUNCTION
            {
                BlendOp = 0,          // AC_SRC_OVER
                BlendFlags = 0,
                SourceConstantAlpha = 255,
                AlphaFormat = 1,      // AC_SRC_ALPHA
            };

            UpdateLayeredWindow(Handle, screenDc, ref topPos, ref size, memDc, ref pointSource, 0, ref blend, 2 /*ULW_ALPHA*/);
        }
        finally
        {
            ReleaseDC(IntPtr.Zero, screenDc);
            if (hBitmap != IntPtr.Zero)
            {
                SelectObject(memDc, oldBitmap);
                DeleteObject(hBitmap);
            }
            DeleteDC(memDc);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _fadeTimer.Dispose();
        base.Dispose(disposing);
    }

    // --- Win32 interop ---

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int x; public int y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct SIZE { public int cx; public int cy; }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct BLENDFUNCTION
    {
        public byte BlendOp;
        public byte BlendFlags;
        public byte SourceConstantAlpha;
        public byte AlphaFormat;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize,
        IntPtr hdcSrc, ref POINT pptSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteDC(IntPtr hDC);
}
