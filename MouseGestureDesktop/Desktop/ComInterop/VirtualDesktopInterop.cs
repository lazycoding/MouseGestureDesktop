using System.Runtime.InteropServices;

namespace MouseGestureDesktop.Desktop.ComInterop;

// COM definitions for the UNDOCUMENTED virtual-desktop switching API.
//
// These interface IIDs and vtable layouts are build-specific and change between Windows
// releases. The GUIDs below correspond to the Windows 11 22H2/23H2 generation (build
// 22621/22631) and are used best-effort. On any other build — or if binding/calling fails —
// the factory falls back to the keyboard backend. See ComVirtualDesktopSwitcher.
internal static class VirtualDesktopClsids
{
    // CLSID_ImmersiveShell — exposes IServiceProvider.
    public static readonly Guid ImmersiveShell = new("C2F03A33-21F5-47FA-B4BB-156362A2F239");

    // CLSID for the internal manager, resolved through IServiceProvider.QueryService.
    public static readonly Guid VirtualDesktopManagerInternal = new("C5E0CDCA-7B6E-41B2-9FC4-D93975CC467B");
}

/// <summary>Per-build interface IIDs for the undocumented API.</summary>
internal readonly struct BuildProfile
{
    public required Guid ManagerIid { get; init; }
    public required Guid DesktopIid { get; init; }
}

/// <summary>
/// Build → interface IID map. Only builds whose vtable layout matches the declarations below are
/// listed; <see cref="ManagerIid"/> must equal the attribute GUID on
/// <see cref="IVirtualDesktopManagerInternal"/> for the managed cast to bind, otherwise the
/// factory degrades to the keyboard backend (see ComVirtualDesktopSwitcher.TryCreate).
///
/// Values are the Windows 11 22H2/23H2 generation IIDs. Build 26100 (24H2) is listed per request
/// and attempts the same layout; it must be verified on-device — the GetCount probe plus Auto
/// fallback guard against a mismatch.
/// </summary>
internal static class BuildProfiles
{
    private static readonly Guid ManagerIid = new("B2F925B9-5A0F-4D2E-9F4D-2B1507593C10");
    private static readonly Guid DesktopIid = new("536D3495-B208-4CC9-AE26-DE8111275BF8");

    private static readonly Dictionary<int, BuildProfile> Map = new()
    {
        [22621] = new BuildProfile { ManagerIid = ManagerIid, DesktopIid = DesktopIid }, // Win11 22H2
        [22631] = new BuildProfile { ManagerIid = ManagerIid, DesktopIid = DesktopIid }, // Win11 23H2
        [26100] = new BuildProfile { ManagerIid = ManagerIid, DesktopIid = DesktopIid }, // Win11 24H2 (verify on-device)
    };

    public static bool TryGet(int build, out BuildProfile profile) => Map.TryGetValue(build, out profile);
}

[ComImport]
[Guid("6D5140C1-7436-11CE-8034-00AA006009FA")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IServiceProvider
{
    [return: MarshalAs(UnmanagedType.IUnknown)]
    object QueryService(ref Guid guidService, ref Guid riid);
}

[ComImport]
[Guid("536D3495-B208-4CC9-AE26-DE8111275BF8")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IVirtualDesktop
{
    bool IsViewVisible(object view);
    Guid GetId();
}

// IVirtualDesktopManagerInternal — Windows 11 22H2/23H2 generation layout.
// Only the members up to GetAdjacentDesktop/SwitchDesktop are exercised; earlier members must
// still be declared in vtable order. Reserved slots use IntPtr placeholders.
[ComImport]
[Guid("B2F925B9-5A0F-4D2E-9F4D-2B1507593C10")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IVirtualDesktopManagerInternal
{
    int GetCount(IntPtr hWndOrMon);
    void MoveViewToDesktop(IntPtr view, IVirtualDesktop desktop);
    bool CanViewMoveDesktops(IntPtr view);
    IVirtualDesktop GetCurrentDesktop(IntPtr hWndOrMon);
    void GetDesktops(IntPtr hWndOrMon, out IntPtr desktops);
    [PreserveSig]
    int GetAdjacentDesktop(IVirtualDesktop from, int direction, out IVirtualDesktop desktop);
    void SwitchDesktop(IntPtr hWndOrMon, IVirtualDesktop desktop);
}

internal static class AdjacentDirection
{
    public const int Left = 3;   // LeftDirection
    public const int Right = 4;  // RightDirection
}
