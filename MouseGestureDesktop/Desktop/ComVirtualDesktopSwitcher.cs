using System.Runtime.InteropServices;
using MouseGestureDesktop.Desktop.ComInterop;
using MouseGestureDesktop.Gesture;
using IServiceProvider = MouseGestureDesktop.Desktop.ComInterop.IServiceProvider;

namespace MouseGestureDesktop.Desktop;

/// <summary>
/// Switches desktops via the undocumented IVirtualDesktopManagerInternal COM API.
/// Because the interface layout is build-specific, construction is gated to the per-build GUID
/// map in <see cref="ComInterop.BuildProfiles"/> and every call is guarded — any failure is
/// surfaced so the factory can fall back.
/// </summary>
public sealed class ComVirtualDesktopSwitcher : IVirtualDesktopSwitcher, IDisposable
{
    private readonly IVirtualDesktopManagerInternal _manager;

    private ComVirtualDesktopSwitcher(IVirtualDesktopManagerInternal manager) => _manager = manager;

    public string BackendName => "COM (IVirtualDesktopManagerInternal)";

    /// <summary>Attempts to construct a COM switcher; returns null when unavailable on this build.</summary>
    public static ComVirtualDesktopSwitcher? TryCreate()
    {
        int build = Environment.OSVersion.Version.Build;
        if (!BuildProfiles.TryGet(build, out BuildProfile profile))
            return null;

        // Only bind when the build's manager IID matches the layout we actually implement;
        // a mismatch would risk a bad cast / vtable call, so degrade to keyboard instead.
        if (profile.ManagerIid != typeof(IVirtualDesktopManagerInternal).GUID)
            return null;

        try
        {
            Type? shellType = Type.GetTypeFromCLSID(VirtualDesktopClsids.ImmersiveShell);
            if (shellType is null)
                return null;

            object shell = Activator.CreateInstance(shellType)!;
            var provider = (IServiceProvider)shell;

            Guid service = VirtualDesktopClsids.VirtualDesktopManagerInternal;
            Guid iid = profile.ManagerIid;
            object raw = provider.QueryService(ref service, ref iid);
            var manager = (IVirtualDesktopManagerInternal)raw;

            // Probe: a successful GetCount validates the binding before we trust it.
            _ = manager.GetCount(IntPtr.Zero);

            return new ComVirtualDesktopSwitcher(manager);
        }
        catch
        {
            return null;
        }
    }

    public bool TrySwitch(SwitchDirection direction)
    {
        try
        {
            IVirtualDesktop current = _manager.GetCurrentDesktop(IntPtr.Zero);
            int dir = direction == SwitchDirection.Right ? AdjacentDirection.Right : AdjacentDirection.Left;

            int hr = _manager.GetAdjacentDesktop(current, dir, out IVirtualDesktop? target);
            if (hr != 0 || target is null)
                return false; // no adjacent desktop (first/last) — do not wrap

            _manager.SwitchDesktop(IntPtr.Zero, target);
            return true;
        }
        catch (COMException)
        {
            return false;
        }
        catch (InvalidCastException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (Marshal.IsComObject(_manager))
            Marshal.FinalReleaseComObject(_manager);
    }
}
