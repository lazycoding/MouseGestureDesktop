using System.Runtime.InteropServices;
using MouseGestureDesktop.Config;

namespace MouseGestureDesktop.Input;

/// <summary>Synthesizes input via SendInput: replaying a side-button click and sending key chords.</summary>
public sealed class InputSynthesizer
{
    private const int INPUT_MOUSE = 0;
    private const int INPUT_KEYBOARD = 1;

    private const uint MOUSEEVENTF_XDOWN = 0x0080;
    private const uint MOUSEEVENTF_XUP = 0x0100;
    private const uint XBUTTON1 = 0x0001;
    private const uint XBUTTON2 = 0x0002;

    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;

    private const ushort VK_CONTROL = 0x11;
    private const ushort VK_LWIN = 0x5B;
    private const ushort VK_LEFT = 0x25;
    private const ushort VK_RIGHT = 0x27;

    /// <summary>Replays a real side-button click (down+up) so apps receive native single/double clicks.</summary>
    public void ClickSideButton(TriggerButton button)
    {
        uint data = button == TriggerButton.XButton1 ? XBUTTON1 : XBUTTON2;
        var inputs = new[]
        {
            MouseXButton(data, MOUSEEVENTF_XDOWN),
            MouseXButton(data, MOUSEEVENTF_XUP),
        };
        Send(inputs);
    }

    /// <summary>Sends Ctrl+Win+Left (previous desktop).</summary>
    public void SwitchDesktopLeft() => SendDesktopChord(VK_LEFT);

    /// <summary>Sends Ctrl+Win+Right (next desktop).</summary>
    public void SwitchDesktopRight() => SendDesktopChord(VK_RIGHT);

    private void SendDesktopChord(ushort arrowKey)
    {
        var inputs = new[]
        {
            Key(VK_CONTROL, false, false),
            Key(VK_LWIN, false, false),
            Key(arrowKey, false, true),
            Key(arrowKey, true, true),
            Key(VK_LWIN, true, false),
            Key(VK_CONTROL, true, false),
        };
        Send(inputs);
    }

    private static void Send(INPUT[] inputs)
    {
        uint sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        if (sent != inputs.Length)
            throw new InvalidOperationException($"SendInput sent {sent}/{inputs.Length} (error {Marshal.GetLastWin32Error()}).");
    }

    private static INPUT MouseXButton(uint xbutton, uint flags) => new()
    {
        type = INPUT_MOUSE,
        u = new InputUnion
        {
            mi = new MOUSEINPUT { dwFlags = flags, mouseData = xbutton },
        },
    };

    private static INPUT Key(ushort vk, bool keyUp, bool extended) => new()
    {
        type = INPUT_KEYBOARD,
        u = new InputUnion
        {
            ki = new KEYBDINPUT
            {
                wVk = vk,
                dwFlags = (keyUp ? KEYEVENTF_KEYUP : 0) | (extended ? KEYEVENTF_EXTENDEDKEY : 0),
            },
        },
    };

    // --- Win32 interop ---

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public int type;
        public InputUnion u;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
        [FieldOffset(0)] public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
}
