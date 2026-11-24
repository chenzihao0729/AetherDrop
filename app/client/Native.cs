// SPDX-License-Identifier: GPL-2.0-only
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;

namespace AetherDrop;

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
        int dark = Palette.Dark ? 1 : 0;
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

    /// <summary>
    /// The design's window: no system title bar (each window draws its own,
    /// 48 px, with its own buttons), Mica where Windows has it, the theme's
    /// solid background elsewhere.
    /// </summary>
    public static void Chrome(Window w, double caption = UiLayout.TitleBarHeight)
    {
        w.Icon = Branding.WindowIcon;
        if (w is MainWindow or SettingsWindow)
        {
            RoundedChrome(w, caption);
            return;
        }
        Typography.Apply(w);
        if (w is not MainWindow) w.Loaded += (_, _) => { if (w.Content is UIElement body) UiMotion.Reveal(body, 5); };
        UseSmoothCorners(w);
        System.Windows.Shell.WindowChrome.SetWindowChrome(w, new System.Windows.Shell.WindowChrome
        {
            CaptionHeight = caption,
            GlassFrameThickness = new Thickness(-1),
            ResizeBorderThickness = new Thickness(6),
            UseAeroCaptionButtons = false,
        });
        w.SetResourceReference(System.Windows.Controls.Control.BackgroundProperty, "MicaSolid");
        w.SourceInitialized += (_, _) =>
        {
            NoSystemButtons(w);
            FitWorkArea(w);
            if (ApplyMica(w)) w.Background = System.Windows.Media.Brushes.Transparent;
        };
    }

    static void RoundedChrome(Window window, double caption)
    {
        window.WindowStyle = WindowStyle.None;
        window.AllowsTransparency = true;
        window.Background = System.Windows.Media.Brushes.Transparent;
        Typography.Apply(window);
        if (window.Content is UIElement content)
        {
            window.Content = null;
            window.Content = new WindowSurface(content);
            if (window is not MainWindow) window.Loaded += (_, _) => UiMotion.Reveal(content, 5);
        }
        window.SourceInitialized += (_, _) =>
        {
            NoSystemButtons(window);
            FitWorkArea(window);
            if (HwndSource.FromVisual(window) is not HwndSource source) return;
            source.AddHook((IntPtr hwnd, int message, IntPtr wp, IntPtr lp, ref bool handled) =>
            {
                if (message != 0x84) return IntPtr.Zero; // WM_NCHITTEST
                long packed = lp.ToInt64();
                var point = window.PointFromScreen(new Point((short)(packed & 0xffff), (short)((packed >> 16) & 0xffff)));
                bool interactive = false;
                for (DependencyObject? node = window.InputHitTest(point) as DependencyObject; node is not null && node != window;)
                {
                    if (node is System.Windows.Controls.Primitives.ButtonBase or System.Windows.Controls.Primitives.TextBoxBase or System.Windows.Controls.PasswordBox or System.Windows.Controls.ComboBox)
                    { interactive = true; break; }
                    node = node is System.Windows.Media.Visual ? System.Windows.Media.VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node);
                }
                handled = true;
                if (interactive) return new IntPtr(1); // HTCLIENT, including transparent button padding
                if (window.WindowState == WindowState.Normal && window.ResizeMode is ResizeMode.CanResize or ResizeMode.CanResizeWithGrip)
                {
                    // The diagonal resize zone follows the larger visible corner arc.
                    const double corner = WindowSurface.Radius;
                    bool nearLeft = point.X < corner, nearRight = point.X >= window.ActualWidth - corner;
                    bool nearTop = point.Y < corner, nearBottom = point.Y >= window.ActualHeight - corner;
                    if (nearTop && (nearLeft || nearRight)) return new IntPtr(nearLeft ? 13 : 14);
                    if (nearBottom && (nearLeft || nearRight)) return new IntPtr(nearLeft ? 16 : 17);
                    const double edge = 6;
                    bool left = point.X < edge, right = point.X >= window.ActualWidth - edge;
                    bool top = point.Y < edge, bottom = point.Y >= window.ActualHeight - edge;
                    int hit = top ? (left ? 13 : right ? 14 : 12) : bottom ? (left ? 16 : right ? 17 : 15) : left ? 10 : right ? 11 : 0;
                    if (hit != 0) return new IntPtr(hit);
                }
                return new IntPtr(point.Y >= 0 && point.Y < caption ? 2 : 1); // HTCAPTION / HTCLIENT
            });
        };
    }

    const int GWL_STYLE = -16;
    const int WS_SYSMENU = 0x00080000;

    [DllImport("user32.dll")]
    static extern short GetAsyncKeyState(int key);

    /// <summary>The left mouse button is held now, whichever program has the focus (#45).</summary>
    public static bool LeftButtonDown()
    {
        // VK_LBUTTON is the physical left button; with the buttons swapped it is VK_RBUTTON.
        var key = System.Windows.Forms.SystemInformation.MouseButtonsSwapped ? 0x02 : 0x01;
        return (GetAsyncKeyState(key) & 0x8000) != 0;
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    static extern int SetWindowLong(IntPtr hwnd, int index, int value);

    /// <summary>
    /// With the DWM frame over the whole window (Mica), Windows draws its own
    /// minimize / maximize / close buttons over the design's, a second set in
    /// the corner. Without the system menu style it draws none; the window
    /// still minimizes from the taskbar and closes with Alt+F4.
    /// </summary>
    static void NoSystemButtons(Window w)
    {
        var hwnd = new WindowInteropHelper(w).EnsureHandle();
        SetWindowLong(hwnd, GWL_STYLE, GetWindowLong(hwnd, GWL_STYLE) & ~WS_SYSMENU);
    }

    /// <summary>The work area (no taskbar) of the screen under the mouse, in WPF units.</summary>
    public static Rect WorkArea(Window w)
    {
        var area = System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Cursor.Position).WorkingArea;
        var m = PresentationSource.FromVisual(w)?.CompositionTarget?.TransformFromDevice ?? System.Windows.Media.Matrix.Identity;
        var tl = m.Transform(new Point(area.Left, area.Top));
        var br = m.Transform(new Point(area.Right, area.Bottom));
        return new Rect(tl, br);
    }

    /// <summary>
    /// Puts a fixed-size panel in the bottom-right corner of the work area of
    /// the screen under the mouse. Positions in physical pixels so this also
    /// works before the window has a PresentationSource and when the target
    /// screen has a different DPI from the window's current screen.
    /// </summary>
    public static void DockBottomRight(Window w, double width, double tall, double gap)
    {
        var cursor = System.Windows.Forms.Cursor.Position;
        var screen = System.Windows.Forms.Screen.FromPoint(cursor);
        var scale = DpiScaleAt(cursor, w);
        var widthPx = (int)Math.Round(width * scale);
        var gapPx = (int)Math.Round(gap * scale);
        var maxHeight = Math.Max(1, (screen.WorkingArea.Height - 2 * gapPx) / scale);
        var height = Math.Min(tall, maxHeight);
        var heightPx = (int)Math.Round(height * scale);
        var left = screen.WorkingArea.Right - widthPx - gapPx;
        var top = screen.WorkingArea.Bottom - heightPx - gapPx;
        w.Width = width;
        w.Height = height;
        SetWindowPos(new WindowInteropHelper(w).Handle, IntPtr.Zero, left, top, widthPx, heightPx,
            SWP_NOZORDER | SWP_NOACTIVATE);
    }

    [StructLayout(LayoutKind.Sequential)]
    struct POINT { public int X, Y; }

    [DllImport("user32.dll")]
    static extern IntPtr MonitorFromPoint(POINT point, uint flags);

    [DllImport("shcore.dll")]
    static extern int GetDpiForMonitor(IntPtr monitor, int dpiType, out uint dpiX, out uint dpiY);

    [DllImport("user32.dll")]
    static extern uint GetDpiForWindow(IntPtr hwnd);

    static double DpiScaleAt(System.Drawing.Point point, Window w)
    {
        var monitor = MonitorFromPoint(new POINT { X = point.X, Y = point.Y }, 2 /* nearest */);
        if (monitor != IntPtr.Zero && GetDpiForMonitor(monitor, 0 /* effective */, out var dpiX, out _) == 0 && dpiX > 0)
            return dpiX / 96.0;
        var dpi = GetDpiForWindow(new WindowInteropHelper(w).Handle);
        return dpi > 0 ? dpi / 96.0 : 1.0;
    }

    /// <summary>
    /// Shows a native file/folder dialog, then keeps it on the owner's screen.
    /// Windows centres these dialogs on the owner window, but the panel is a
    /// narrow window glued to the right edge; at 125 % the dialog is wider than
    /// the remaining space and is pushed across the monitor seam. Move it to
    /// the centre of the owner's work area after it becomes visible.
    /// </summary>
    public static bool ShowDialog(Window owner, CommonDialog dialog, string title)
    {
        var ownerHwnd = new WindowInteropHelper(owner).Handle;
        var work = System.Windows.Forms.Screen.FromHandle(ownerHwnd).WorkingArea;
        using var stop = new System.Threading.CancellationTokenSource();
        var thread = new System.Threading.Thread(() =>
        {
            for (var attempts = 0; attempts < 100 && !stop.IsCancellationRequested; attempts++)
            {
                System.Threading.Thread.Sleep(40);
                var hwnd = FindDialog((uint)Environment.ProcessId, title);
                if (hwnd == IntPtr.Zero) continue;
                GetWindowRect(hwnd, out var r);
                var width = r.Right - r.Left;
                var height = r.Bottom - r.Top;
                if (width <= 200 || height <= 200) continue;
                var fitWidth = Math.Min(width, work.Width - 16);
                var fitHeight = Math.Min(height, work.Height - 16);
                var x = work.Left + (work.Width - fitWidth) / 2;
                var y = work.Top + (work.Height - fitHeight) / 2;
                SetWindowPos(hwnd, IntPtr.Zero, x, y, fitWidth, fitHeight,
                    SWP_NOZORDER | SWP_NOACTIVATE | SWP_NOOWNERZORDER);
                return;
            }
        })
        {
            IsBackground = true,
        };
        thread.Start();
        try { return dialog.ShowDialog(owner) == true; }
        finally
        {
            stop.Cancel();
            thread.Join(200);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct RECT { public int Left, Top, Right, Bottom; }

    delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll")]
    static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll")]
    static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern int GetWindowText(IntPtr hwnd, System.Text.StringBuilder text, int max);

    [DllImport("user32.dll")]
    static extern bool IsWindowVisible(IntPtr hwnd);

    [DllImport("user32.dll")]
    static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);

    [DllImport("user32.dll")]
    static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

    const uint SWP_NOZORDER = 0x0004;
    const uint SWP_NOACTIVATE = 0x0010;
    const uint SWP_NOOWNERZORDER = 0x0200;

    static IntPtr FindDialog(uint processId, string title)
    {
        var found = IntPtr.Zero;
        EnumWindows((hwnd, _) =>
        {
            GetWindowThreadProcessId(hwnd, out var pid);
            if (pid != processId || !IsWindowVisible(hwnd)) return true;
            var text = new System.Text.StringBuilder(256);
            if (GetWindowText(hwnd, text, text.Capacity) <= 0) return true;
            if (!string.Equals(text.ToString(), title, StringComparison.Ordinal)) return true;
            found = hwnd;
            return false;
        }, IntPtr.Zero);
        return found;
    }

    /// <summary>
    /// A window taller or wider than the screen (a laptop at 150 % has 720
    /// units) is cut off at the bottom: shrink it to the work area, centred.
    /// </summary>
    static void FitWorkArea(Window w)
    {
        if (w.Left < -10000) return; // drawn off screen for a snapshot
        var a = WorkArea(w);
        var fit = false;
        if (w.Height > a.Height - 16) { w.Height = a.Height - 16; fit = true; }
        if (w.Width > a.Width - 16) { w.Width = a.Width - 16; fit = true; }
        if (w.MinHeight > w.Height) w.MinHeight = w.Height;
        if (w.MinWidth > w.Width) w.MinWidth = w.Width;
        if (fit && w.WindowStartupLocation == WindowStartupLocation.CenterScreen)
        {
            w.Left = a.Left + (a.Width - w.Width) / 2;
            w.Top = a.Top + (a.Height - w.Height) / 2;
        }
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
