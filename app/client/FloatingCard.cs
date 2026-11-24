// SPDX-License-Identifier: GPL-2.0-only
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Interop;
using System.Text;

namespace AetherDrop;

/// <summary>Shared floating card chrome and motion. Coordinates use the target monitor's work area.</summary>
public abstract class FloatingCard : Window
{
    protected readonly Grid Surface;
    protected readonly Border Card;
    protected readonly Border Shadow;
    protected readonly Grid Body = new();
    protected static Brush Ink => Palette.Brush(28, 32, 36);
    protected static Brush Muted => Palette.Brush(108, 115, 122);
    protected static Brush Blue => Palette.Brush(0, 122, 255);
    protected bool Dismissing;
    public static string Text(string zh, string en) => L.Zh ? zh : en;
    public static bool Motion => SystemParameters.ClientAreaAnimation;

    protected FloatingCard(double width, double height)
    {
        Width = width; Height = height;
        UseLayoutRounding = true; SnapsToDevicePixels = true;
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
        // Grayscale avoids colored subpixel fringes on per-pixel-alpha windows.
        TextOptions.SetTextRenderingMode(this, TextRenderingMode.Grayscale);
        TextOptions.SetTextHintingMode(this, TextHintingMode.Fixed);
        Surface = new Grid { Width = width, Height = height };
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true; Background = Brushes.Transparent;
        Typography.Apply(this);
        ShowInTaskbar = false; Topmost = true; ShowActivated = false;
        WindowStartupLocation = WindowStartupLocation.Manual;
        FontFamily = Typography.Regular;
        Foreground = Ink; FontSize = 10; Icon = Branding.WindowIcon;
        Card = new Border
        {
            // Keep layout/text pixel-aligned, but let curved chrome retain subpixel coverage.
            SnapsToDevicePixels = false,
            Margin = new Thickness(10), Padding = new Thickness(15), CornerRadius = new CornerRadius(13),
            BorderBrush = Palette.SemanticBrush("Border"), BorderThickness = new Thickness(1),
            Background = Palette.Brush(253, 254, 255),
            Child = Body,
        };
        // Shadow is a separate sibling. Putting an Effect above the text would
        // rasterize the whole card and soften glyphs, even without a Viewbox.
        Shadow = new Border
        {
            SnapsToDevicePixels = false,
            Margin = Card.Margin, CornerRadius = Card.CornerRadius, Background = Card.Background,
            IsHitTestVisible = false,
            Effect = new DropShadowEffect { BlurRadius = 12, ShadowDepth = 3, Opacity = .16, Color = Colors.Black },
        };
        Surface.Children.Add(Shadow);
        Surface.Children.Add(Card);
        Content = Surface;
    }
    protected static TextBlock Label(string text, double size = 10, bool bold = false, Brush? color = null) => new()
    {
        Text = text, FontSize = size, FontFamily = Typography.Regular, FontWeight = bold ? Typography.EmphasisWeight : Typography.RegularWeight,
        Foreground = color ?? Ink, VerticalAlignment = VerticalAlignment.Center,
        TextTrimming = TextTrimming.CharacterEllipsis,
    };
    protected static Button Button(string text, Action action, bool primary = false)
    {
        var b = new FloatingButton { Content = text, Height = 26, MinWidth = 64, Padding = new Thickness(10, 0, 10, 0),
            Cursor = System.Windows.Input.Cursors.Hand, FontSize = 11, FontFamily = Typography.Regular, FontWeight = Typography.EmphasisWeight,
            Foreground = primary ? Palette.SemanticBrush("OnAccent") : Ink, Background = primary ? Blue : Brushes.Transparent,
            BorderBrush = primary ? Blue : Palette.Brush(194, 201, 208), BorderThickness = new Thickness(1) };
        var template = new ControlTemplate(typeof(Button));
        var root = new FrameworkElementFactory(typeof(Grid));
        var border = new FrameworkElementFactory(typeof(Border));
        border.Name = "ButtonSurface";
        border.SetValue(FrameworkElement.RenderTransformOriginProperty, new Point(.5, .5));
        border.SetValue(UIElement.RenderTransformProperty, new ScaleTransform(1, 1));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
        border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
        root.AppendChild(border);
        var feedback = new FrameworkElementFactory(typeof(Border));
        feedback.Name = "InteractionTint";
        feedback.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
        feedback.SetValue(Border.BackgroundProperty, primary ? Brushes.Black : Blue);
        feedback.SetValue(UIElement.OpacityProperty, 0.0);
        feedback.SetValue(UIElement.IsHitTestVisibleProperty, false);
        feedback.SetValue(FrameworkElement.RenderTransformOriginProperty, new Point(.5, .5));
        feedback.SetValue(UIElement.RenderTransformProperty, new ScaleTransform(1, 1));
        root.AppendChild(feedback);
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        presenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        root.AppendChild(presenter); template.VisualTree = root; b.Template = template;
        b.Click += (_, _) => action();
        return b;
    }
    protected Button CloseButton(Action close)
    {
        var b = Button("×", close); b.MinWidth = 20; b.Width = 20; b.Height = 20; b.FontSize = 17;
        b.BorderThickness = new Thickness(0); b.Foreground = Muted;
        b.HorizontalAlignment = HorizontalAlignment.Right; b.VerticalAlignment = VerticalAlignment.Top;
        b.Margin = new Thickness(0, -8, -8, 0);
        System.Windows.Automation.AutomationProperties.SetName(b, Text("关闭", "Close"));
        return b;
    }
    public static Rect WorkArea(Window window, System.Drawing.Point point)
    {
        var screen = System.Windows.Forms.Screen.FromPoint(point);
        var hwnd = new WindowInteropHelper(window).EnsureHandle();
        // Establish monitor DPI before converting physical screen coordinates to WPF units.
        SetWindowPos(hwnd, IntPtr.Zero, screen.WorkingArea.Left + 20, screen.WorkingArea.Top + 20, 0, 0, 0x15);
        var source = HwndSource.FromHwnd(hwnd);
        var transform = source?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
        var r = screen.WorkingArea;
        return new Rect(transform.Transform(new Point(r.Left, r.Top)), transform.Transform(new Point(r.Right, r.Bottom)));
    }
    protected static DoubleAnimation Animate(double from, double to, double seconds, bool spring = false) => new(from, to, TimeSpan.FromSeconds(Motion ? seconds : 0))
    {
        EasingFunction = spring ? new BackEase { Amplitude = .25, EasingMode = EasingMode.EaseOut } : new CubicEase { EasingMode = EasingMode.EaseOut },
        FillBehavior = FillBehavior.Stop,
    };
    public virtual void Dismiss()
    {
        if (Dismissing) return;
        Dismissing = true;
        Close();
    }
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int w, int h, uint flags);
}

/// <summary>Dismisses on outside clicks, excluding the popup and its tray icon.</summary>
internal sealed class DesktopClickMonitor : IDisposable
{
    readonly HookProc _callback;
    readonly Action _clicked;
    readonly Window _owner;
    readonly Func<System.Drawing.Point, bool> _ignore;
    IntPtr _hook;
    public DesktopClickMonitor(Window owner, Action clicked, Func<System.Drawing.Point, bool> ignore)
    {
        _owner = owner; _clicked = clicked; _ignore = ignore;
        _callback = Hook;
        _hook = SetWindowsHookEx(14, _callback, GetModuleHandle(null), 0);
    }
    IntPtr Hook(int code, IntPtr message, IntPtr data)
    {
        if (code >= 0 && message.ToInt64() is 0x201 or 0x204 or 0x207)
        {
            var p = Marshal.PtrToStructure<MouseData>(data).Point;
            var root = GetAncestor(WindowFromPoint(p), 2);
            var ownerHandle = new WindowInteropHelper(_owner).Handle;
            if (root != ownerHandle && !_ignore(new System.Drawing.Point(p.X, p.Y)))
                _owner.Dispatcher.BeginInvoke(() =>
                {
                    if (_hook != IntPtr.Zero && _owner.IsVisible) _clicked();
                }, System.Windows.Threading.DispatcherPriority.Input);
        }
        return CallNextHookEx(_hook, code, message, data);
    }
    public void Dispose() { if (_hook != IntPtr.Zero) { UnhookWindowsHookEx(_hook); _hook = IntPtr.Zero; } }
    [StructLayout(LayoutKind.Sequential)] struct XY { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] struct MouseData { public XY Point; public uint Mouse, Flags, Time; public UIntPtr Extra; }
    delegate IntPtr HookProc(int code, IntPtr message, IntPtr data);
    [DllImport("user32.dll")] static extern IntPtr SetWindowsHookEx(int id, HookProc callback, IntPtr module, uint thread);
    [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern IntPtr GetModuleHandle(string? name);
    [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(XY point);
    [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr hwnd, StringBuilder name, int length);
}
