// SPDX-License-Identifier: GPL-2.0-only
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace AetherDrop;

public sealed class TrayWindow : FloatingCard
{
    readonly App _app;
    readonly TextBlock _status;
    readonly Ellipse _dot;
    readonly CheckBox _receive;
    DesktopClickMonitor? _desktop;
    readonly System.Windows.Shapes.Path _outline, _outlineShadow;
    ScaleTransform? _openingScale;
    System.Windows.Threading.DispatcherTimer? _overflowMonitor;
    public bool IsClosing => Dismissing;
    public TrayWindow(App app) : base(244, 208)
    {
        _app = app; Title = Branding.Name;
        // A single filled/stroked silhouette includes the pointer: no horizontal
        // card edge crosses its base, and the shadow follows the same contour.
        _outline = new System.Windows.Shapes.Path
        {
            Fill = Card.Background, Stroke = Card.BorderBrush, StrokeThickness = 1,
            StrokeLineJoin = PenLineJoin.Round, SnapsToDevicePixels = false,
            IsHitTestVisible = false,
        };
        _outlineShadow = new System.Windows.Shapes.Path
        {
            Fill = Card.Background, SnapsToDevicePixels = false, IsHitTestVisible = false,
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            { BlurRadius = 12, ShadowDepth = 3, Opacity = .16, Color = Colors.Black },
        };
        Surface.Children.Remove(Shadow);
        Surface.Children.Insert(0, _outlineShadow);
        Surface.Children.Insert(1, _outline);
        Card.Background = Brushes.Transparent; Card.BorderBrush = Brushes.Transparent;
        SetPointer(Width / 2);
        Body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) });
        Body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) });
        Body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) });
        Body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        // Include the top padding in the header band: center all three items
        // between the card's inner top edge and the first separator.
        var heading = new Grid { VerticalAlignment = VerticalAlignment.Stretch, Margin = new Thickness(0, -15, 0, 0) };
        heading.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
        heading.ColumnDefinitions.Add(new ColumnDefinition());
        var logo = new Image { Source = Branding.TrayPanelLogo, Width = 33, Height = 33, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left };
        RenderOptions.SetBitmapScalingMode(logo, BitmapScalingMode.HighQuality);
        heading.Children.Add(logo);
        var title = TrayLabel(Branding.Name, 17, true);
        Grid.SetColumn(title, 1); heading.Children.Add(title); Body.Children.Add(heading);
        var row = new Grid(); row.Children.Add(TrayLabel(L.T("tray.receive"), 13, true));
        _receive = new ReceiveToggle { IsChecked = app.Settings.Discoverable, Width = 39, Height = 20,
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Cursor = System.Windows.Input.Cursors.Hand };
        var template = new ControlTemplate(typeof(CheckBox));
        var track = new FrameworkElementFactory(typeof(Border)); track.Name = "Track";
        track.SetValue(Border.CornerRadiusProperty, new CornerRadius(10)); track.SetValue(Border.BackgroundProperty, Palette.Brush(191, 197, 201));
        var knob = new FrameworkElementFactory(typeof(Border)); knob.Name = "Knob";
        knob.SetValue(Border.CornerRadiusProperty, new CornerRadius(9)); knob.SetValue(FrameworkElement.WidthProperty, 16.0);
        knob.SetValue(FrameworkElement.HeightProperty, 16.0); knob.SetValue(Border.BackgroundProperty, Brushes.White);
        knob.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left); knob.SetValue(FrameworkElement.MarginProperty, new Thickness(2));
        track.AppendChild(knob); template.VisualTree = track;
        _receive.Template = template;
        System.Windows.Automation.AutomationProperties.SetName(_receive, L.T("tray.receive"));
        _receive.Click += (_, _) => { app.SetDiscoverable(_receive.IsChecked == true); Refresh(); };
        row.Children.Add(_receive); AddRow(row, 1);
        var name = new Grid(); name.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(64) }); name.ColumnDefinitions.Add(new ColumnDefinition());
        var nameCaption = TrayLabel(Text("本机名称", "This device"), 12, false, Palette.Brush(92, 101, 110));
        nameCaption.FontWeight = Typography.RegularWeight; name.Children.Add(nameCaption);
        var device = TrayLabel(Environment.MachineName, 14); device.FontWeight = Typography.EmphasisWeight; Grid.SetColumn(device, 1); name.Children.Add(device); AddRow(name, 2);
        var statusRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        _dot = new Ellipse { Width = 8, Height = 8, Margin = new Thickness(0, 0, 8, 0) }; statusRow.Children.Add(_dot);
        _status = TrayLabel("", 13, true, Palette.Brush(92, 101, 110)); statusRow.Children.Add(_status);
        var footer = new Grid { VerticalAlignment = VerticalAlignment.Center };
        footer.Children.Add(statusRow);
        var settings = Button("", () =>
        {
            var window = app.Windows.OfType<SettingsWindow>().FirstOrDefault();
            if (window is null) { window = new SettingsWindow(); window.Show(); }
            window.CenterOnDesktop(); window.Activate();
        });
        settings.Width = settings.Height = settings.MinWidth = 24;
        settings.BorderThickness = new Thickness(0); settings.Foreground = Muted;
        settings.HorizontalAlignment = HorizontalAlignment.Right; settings.VerticalAlignment = VerticalAlignment.Bottom;
        var gearText = new FormattedText("\uE713", System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), 14, Muted, 1);
        var gear = new System.Windows.Shapes.Path { Data = gearText.BuildGeometry(new Point()), Fill = Muted, Width = 14, Height = 14, Stretch = Stretch.Fill };
        settings.Content = gear;
        System.Windows.Automation.AutomationProperties.SetName(settings, Text("设置", "Settings"));
        _status.SizeChanged += (_, _) =>
        {
            var ink = VisualTreeHelper.GetDescendantBounds(_status);
            if (!ink.IsEmpty) settings.Margin = new Thickness(0, 0, 0, _status.ActualHeight - ink.Bottom - 5);
        };
        footer.Children.Add(settings); AddRow(footer, 3);
        app.Model.PropertyChanged += Changed;
        app.ReceivingChanged += Refresh;
        Closed += (_, _) =>
        {
            app.Model.PropertyChanged -= Changed; app.ReceivingChanged -= Refresh; _desktop?.Dispose();
            _overflowMonitor?.Stop();
            Surface.BeginAnimation(OpacityProperty, null);
            if (Surface.RenderTransform is ScaleTransform scale)
            {
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            }
            Surface.RenderTransform = Transform.Identity; _openingScale = null;
        };
        Refresh();
        PreviewMouseDown += (_, e) =>
        {
            if (e.OriginalSource is not (Grid or Border)) return;
            for (DependencyObject? node = e.OriginalSource as DependencyObject; node is not null && node != this; node = VisualTreeHelper.GetParent(node))
                if (node is System.Windows.Controls.Primitives.ButtonBase) return;
            Dismiss();
        };
    }
    void SetPointer(double center)
    {
        const double inset = 10.5, radius = 13, half = 10;
        double right = Width - inset, bottom = Height - inset;
        center = Math.Clamp(center, inset + radius + half, right - radius - half);
        var shape = new StreamGeometry();
        using (var c = shape.Open())
        {
            c.BeginFigure(new Point(inset + radius, inset), true, true);
            c.LineTo(new Point(right - radius, inset), true, false);
            c.ArcTo(new Point(right, inset + radius), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, false);
            c.LineTo(new Point(right, bottom - radius), true, false);
            c.ArcTo(new Point(right - radius, bottom), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, false);
            c.LineTo(new Point(center + half, bottom), true, false);
            c.LineTo(new Point(center, Height - 1), true, false);
            c.LineTo(new Point(center - half, bottom), true, false);
            c.LineTo(new Point(inset + radius, bottom), true, false);
            c.ArcTo(new Point(inset, bottom - radius), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, false);
            c.LineTo(new Point(inset, inset + radius), true, false);
            c.ArcTo(new Point(inset + radius, inset), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, false);
        }
        shape.Freeze();
        _outline.Data = _outlineShadow.Data = shape;
    }
    static TextBlock TrayLabel(string text, double size, bool bold = false, Brush? color = null)
    {
        var label = Label(text, size, bold, color);
        // Derive the line box from the selected font. Extra leading and inset protect
        // ascenders/descenders from rounding at small sizes and mixed DPI.
        label.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
        label.LineHeight = Math.Ceiling(size * label.FontFamily.LineSpacing) + 2;
        label.Padding = new Thickness(0, 2, 0, 2);
        label.ClipToBounds = false;
        return label;
    }
    void AddRow(UIElement child, int row)
    {
        var border = new Border { BorderBrush = Palette.Brush(196, 207, 216), BorderThickness = new Thickness(0, 1, 0, 0), Margin = row == 3 ? new Thickness(0, 0, 0, -15) : new Thickness(0), Child = child };
        Grid.SetRow(border, row); Body.Children.Add(border);
    }
    void Changed(object? sender, PropertyChangedEventArgs e) => Refresh();
    void Refresh()
    {
        _receive.IsChecked = _app.Settings.Discoverable;
        _status.Text = _app.Settings.Discoverable ? Text("接收已开启", "Receiving is on") : Text("接收已关闭", "Receiving is off");
        _dot.Fill = _app.Settings.Discoverable ? Blue : Palette.Brush(170, 175, 180);
    }
    public void OpenAt(System.Drawing.Point anchor)
    {
        if (IsVisible) return; // Repeated tray clicks never toggle the card closed.
        var work = WorkArea(this, anchor);
        var source = System.Windows.Interop.HwndSource.FromHwnd(new System.Windows.Interop.WindowInteropHelper(this).Handle);
        var p = source?.CompositionTarget?.TransformFromDevice.Transform(new Point(anchor.X, anchor.Y)) ?? new Point(anchor.X, anchor.Y);
        Left = Math.Clamp(p.X - Width / 2, work.Left, Math.Max(work.Left, work.Right - Width));
        var overflow = OverflowBounds(anchor, out var overflowWindow);
        var overflowTop = overflow is { } bounds
            ? source?.CompositionTarget?.TransformFromDevice.Transform(new Point(bounds.Left, bounds.Top)).Y ?? bounds.Top
            : (double?)null;
        Top = PopupTop(work, Height, overflowTop);
        var dpi = VisualTreeHelper.GetDpi(this);
        Left = Math.Round(Left * dpi.DpiScaleX) / dpi.DpiScaleX;
        Top = Math.Round(Top * dpi.DpiScaleY) / dpi.DpiScaleY;
        SetPointer(p.X - Left);
        Surface.RenderTransformOrigin = new Point(Math.Clamp((p.X - Left) / Width, 0, 1), 1);
        if (Motion)
        {
            var scale = _openingScale = new ScaleTransform(.78, .78); Surface.RenderTransform = scale;
            var grow = Animate(.78, 1, .32);
            grow.EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseOut };
            var growY = grow.Clone();
            growY.Completed += (_, _) =>
            {
                if (!Dismissing && ReferenceEquals(_openingScale, scale))
                {
                    Surface.RenderTransform = Transform.Identity; _openingScale = null;
                }
            };
            // Prepare before Show, then start clocks after native layout returns.
            Surface.Opacity = .45;
            Show();
            scale.ScaleX = scale.ScaleY = 1; Surface.Opacity = 1;
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, growY);
            Surface.BeginAnimation(OpacityProperty, Animate(.45, 1, .14));
        }
        else Show();
        _desktop = new DesktopClickMonitor(this, Dismiss, _app.IsTrayIconPoint);
        if (overflowWindow != IntPtr.Zero)
        {
            _overflowMonitor = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(60) };
            _overflowMonitor.Tick += (_, _) =>
            {
                if (!IsWindowVisible(overflowWindow)) { _overflowMonitor.Stop(); Dismiss(); }
            };
            _overflowMonitor.Start();
        }
    }
    internal static double PopupTop(Rect work, double height, double? overflowTop) =>
        Math.Clamp((overflowTop ?? work.Bottom) - height - 4, work.Top, Math.Max(work.Top, work.Bottom - height));

    static System.Drawing.Rectangle? OverflowBounds(System.Drawing.Point anchor, out IntPtr host)
    {
        host = IntPtr.Zero;
        var hit = WindowFromPoint(anchor);
        // Classic and Windows 11 tray overflow hosts. Anchor above the whole
        // host so the open overflow panel cannot cover the popup's lower half.
        for (var window = hit; window != IntPtr.Zero; window = GetParent(window))
        {
            var name = new StringBuilder(256); GetClassName(window, name, name.Capacity);
            if (name.ToString() is "NotifyIconOverflowWindow" or "TopLevelWindowForOverflowXamlIsland")
                if (GetWindowRect(window, out var rect)) { host = window; return System.Drawing.Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom); }
        }
        return null;
    }
    [StructLayout(LayoutKind.Sequential)] struct WindowRect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(System.Drawing.Point point);
    [DllImport("user32.dll")] static extern IntPtr GetParent(IntPtr window);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr window, StringBuilder name, int length);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window, out WindowRect rect);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
    public override void Dismiss()
    {
        if (Dismissing) return; Dismissing = true; _desktop?.Dispose(); _overflowMonitor?.Stop();
        if (!Motion) { Close(); return; }
        // Reverse from the current visual state even when dismissed mid-entry.
        var current = Surface.RenderTransform as ScaleTransform;
        var scale = new ScaleTransform(current?.ScaleX ?? 1, current?.ScaleY ?? 1);
        Surface.RenderTransform = scale; _openingScale = null;
        var shrink = Animate(scale.ScaleX, .78, .18);
        shrink.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn };
        var shrinkY = shrink.Clone(); shrinkY.From = scale.ScaleY;
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, shrink);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, shrinkY);
        var fade = Animate(Surface.Opacity, 0, .18);
        fade.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn };
        fade.Completed += (_, _) => Close(); Surface.BeginAnimation(OpacityProperty, fade);
    }
}
