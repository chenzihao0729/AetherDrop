// SPDX-License-Identifier: GPL-2.0-only
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;

namespace NekoDrop;

/// <summary>Windows 11 window chrome: Mica backdrop, dark title bar, rounded corners.</summary>
public static class Native
{
    [StructLayout(LayoutKind.Sequential)]
    struct NotifyIconIdentifier { public uint Size; public IntPtr Window; public uint Id; public Guid Guid; }
    [StructLayout(LayoutKind.Sequential)] struct IconRect { public int Left, Top, Right, Bottom; }
    [DllImport("shell32.dll")] static extern int Shell_NotifyIconGetRect(ref NotifyIconIdentifier icon, out IconRect rect);

    public static System.Drawing.Point TrayIconCenter(System.Windows.Forms.NotifyIcon? icon)
    {
        var bounds = TrayIconBounds(icon);
        return bounds is { } rect ? new System.Drawing.Point((rect.Left+rect.Right)/2,(rect.Top+rect.Bottom)/2) : System.Windows.Forms.Cursor.Position;
    }
    public static System.Drawing.Rectangle? TrayIconBounds(System.Windows.Forms.NotifyIcon? icon)
    {
        if (icon is not null)
        {
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var fields = typeof(System.Windows.Forms.NotifyIcon).GetFields(flags);
            var id = fields.FirstOrDefault(f => f.Name is "_id" or "id")?.GetValue(icon);
            var window = fields.Select(f => f.GetValue(icon)).OfType<System.Windows.Forms.NativeWindow>().FirstOrDefault();
            uint? iconId = id switch { int value => (uint)value, uint value => value, _ => null };
            if (iconId is { } identifierValue && window is not null)
            {
                var identifier = new NotifyIconIdentifier { Size = (uint)Marshal.SizeOf<NotifyIconIdentifier>(), Window = window.Handle, Id = identifierValue };
                if (Shell_NotifyIconGetRect(ref identifier, out var rect) == 0 && rect.Right > rect.Left && rect.Bottom > rect.Top)
                    return System.Drawing.Rectangle.FromLTRB(rect.Left,rect.Top,rect.Right,rect.Bottom);
            }
        }
        return null;
    }
    const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
    [DllImport("user32.dll")]
    static extern int SetWindowRgn(IntPtr hwnd, IntPtr region, bool redraw);

    /// <summary>Let DWM antialias the outer edge instead of WindowChrome's pixel region.</summary>
    public static void UseSmoothCorners(Window window)
    {
        void Apply()
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero || Environment.OSVersion.Version.Build < 22000) return;
            SetWindowRgn(hwnd, IntPtr.Zero, true);
            int round = 2;
            DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref round, sizeof(int));
        }
        window.SourceInitialized += (_, _) => Apply();
        window.Loaded += (_, _) => Apply();
        window.SizeChanged += (_, _) => window.Dispatcher.BeginInvoke(Apply, System.Windows.Threading.DispatcherPriority.Loaded);
        window.StateChanged += (_, _) => window.Dispatcher.BeginInvoke(Apply, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    public static bool AppsUseDarkTheme()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("AppsUseLightTheme") is int v && v == 0;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct Margins { public int Left, Right, Top, Bottom; }

    [DllImport("dwmapi.dll")]
    static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref Margins margins);

    static bool TransparencyEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("EnableTransparency") is not int v || v != 0;
    }

    /// <summary>
    /// Dark title bar, rounded corners, and Mica on Windows 11 22H2+ with
    /// transparency effects on. Mica needs the DWM frame over the whole client
    /// area and a transparent WPF surface; without both the window turns black,
    /// so either all of it applies (true: make the background transparent) or
    /// the plain theme background stays (false).
    /// </summary>
    public static bool ApplyMica(Window w)
    {
        var hwnd = new WindowInteropHelper(w).EnsureHandle();
        int dark = AppsUseDarkTheme() ? 1 : 0;
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
        int round = 2;
        DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref round, sizeof(int));
        if (Environment.OSVersion.Version.Build < 22621 || !TransparencyEnabled()) return false;
        if (HwndSource.FromHwnd(hwnd) is not { CompositionTarget: { } target }) return false;
        var margins = new Margins { Left = -1, Right = -1, Top = -1, Bottom = -1 };
        if (DwmExtendFrameIntoClientArea(hwnd, ref margins) != 0) return false;
        int mica = 2;
        if (DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref mica, sizeof(int)) != 0)
        {
            var none = new Margins();
            DwmExtendFrameIntoClientArea(hwnd, ref none);
            return false;
        }
        target.BackgroundColor = System.Windows.Media.Colors.Transparent;
        return true;
    }

    /// <summary>The user's accent color (DWM colorization), opaque.</summary>
    public static System.Windows.Media.Color Accent()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM");
        if (key?.GetValue("AccentColor") is int abgr)
            return System.Windows.Media.Color.FromRgb((byte)abgr, (byte)(abgr >> 8), (byte)(abgr >> 16));
        return System.Windows.Media.Color.FromRgb(0x00, 0x67, 0xC0);
    }
}
