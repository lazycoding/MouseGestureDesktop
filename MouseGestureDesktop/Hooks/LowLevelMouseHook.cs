using System.Drawing;
using System.Runtime.InteropServices;

namespace MouseGestureDesktop.Hooks;

/// <summary>
/// Global low-level mouse hook (WH_MOUSE_LL). Raises <see cref="MouseEvent"/> for move / X-button
/// messages; subscribers set <see cref="MouseHookEventArgs.Handled"/> to swallow an event.
/// Must be created on a thread with a running message loop (the UI thread).
/// </summary>
public sealed class LowLevelMouseHook : IDisposable
{
    private const int WH_MOUSE_LL = 14;
    private const int WM_MOUSEMOVE = 0x0200;
    private const int WM_XBUTTONDOWN = 0x020B;
    private const int WM_XBUTTONUP = 0x020C;
    private const uint LLMHF_INJECTED = 0x00000001;
    private const int XBUTTON1 = 0x0001;
    private const int XBUTTON2 = 0x0002;

    private readonly LowLevelMouseProc _proc;   // kept alive to prevent GC of the delegate
    private IntPtr _hookHandle = IntPtr.Zero;

    public event EventHandler<MouseHookEventArgs>? MouseEvent;

    public LowLevelMouseHook() => _proc = HookCallback;

    public bool IsInstalled => _hookHandle != IntPtr.Zero;

    public void Install()
    {
        if (_hookHandle != IntPtr.Zero)
            return;

        IntPtr hModule = GetModuleHandle(null);
        _hookHandle = SetWindowsHookEx(WH_MOUSE_LL, _proc, hModule, 0);
        if (_hookHandle == IntPtr.Zero)
            throw new InvalidOperationException($"SetWindowsHookEx failed (error {Marshal.GetLastWin32Error()}).");
    }

    public void Uninstall()
    {
        if (_hookHandle == IntPtr.Zero)
            return;
        UnhookWindowsHookEx(_hookHandle);
        _hookHandle = IntPtr.Zero;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0)
            return CallNextHookEx(_hookHandle, nCode, wParam, lParam);

        var data = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
        int msg = (int)wParam;

        MouseMessageType type = msg switch
        {
            WM_MOUSEMOVE => MouseMessageType.Move,
            WM_XBUTTONDOWN => MouseMessageType.XButtonDown,
            WM_XBUTTONUP => MouseMessageType.XButtonUp,
            _ => MouseMessageType.Other,
        };

        if (type == MouseMessageType.Other)
            return CallNextHookEx(_hookHandle, nCode, wParam, lParam);

        XButton button = XButton.None;
        if (type is MouseMessageType.XButtonDown or MouseMessageType.XButtonUp)
        {
            int whichButton = (int)((data.mouseData >> 16) & 0xFFFF);
            button = whichButton switch
            {
                XBUTTON1 => XButton.XButton1,
                XBUTTON2 => XButton.XButton2,
                _ => XButton.None,
            };
        }

        var args = new MouseHookEventArgs
        {
            Type = type,
            Button = button,
            Point = new Point(data.pt.x, data.pt.y),
            Timestamp = data.time,
            Injected = (data.flags & LLMHF_INJECTED) != 0,
        };

        try
        {
            MouseEvent?.Invoke(this, args);
        }
        catch
        {
            // Never let a subscriber exception break the global hook chain.
        }

        if (args.Handled)
            return 1;

        return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    public void Dispose() => Uninstall();

    // --- Win32 interop ---

    private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);
}
