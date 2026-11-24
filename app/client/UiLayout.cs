// SPDX-License-Identifier: GPL-2.0-only
using System.Windows;
using System.Windows.Controls;

namespace AetherDrop;

/// <summary>Desktop layout metrics; adapts existing controls without scaling their content.</summary>
public static class UiLayout
{
    public const double TitleBarHeight = 48;
    public const double MainTitleBarHeight = 52;
    public const double FooterHeight = 56;
    public const double ControlHeight = 32;
    public const double WindowRadius = 12;
    public const double CompactWidth = 500;
    public const double CompactHeight = 640;
    public static GridLength TitleBarRow => new(TitleBarHeight);
    public static GridLength MainTitleBarRow => new(MainTitleBarHeight);
    public static GridLength FooterRow => new(FooterHeight);
    public static Thickness PageMargin => new(16);
    public static Thickness HeaderPadding => new(16, 0, 8, 0);
    public static Thickness FooterPadding => new(16, 0, 16, 0);
    public static Thickness GroupMargin => new(0, 0, 0, 12);

    public static readonly DependencyProperty AdaptiveRowProperty = DependencyProperty.RegisterAttached(
        "AdaptiveRow", typeof(bool), typeof(UiLayout), new PropertyMetadata(false, OnAdaptiveRow));
    public static void SetAdaptiveRow(DependencyObject o, bool value) => o.SetValue(AdaptiveRowProperty, value);
    public static bool GetAdaptiveRow(DependencyObject o) => (bool)o.GetValue(AdaptiveRowProperty);

    sealed record Placement(UIElement Element, Dock Dock, Thickness Margin, HorizontalAlignment Alignment);
    static void OnAdaptiveRow(DependencyObject o, DependencyPropertyChangedEventArgs e)
    {
        if (o is not DockPanel row || !(bool)e.NewValue) return;
        Window? owner = null;
        Placement[]? original = null;
        bool? compact = null;
        void Arrange()
        {
            if (owner is null || original is null) return;
            bool next = owner.ActualWidth < CompactWidth;
            if (compact == next) return;
            compact = next;
            row.Children.Clear();
            // Keep label before its existing control in the compact layout.
            foreach (var item in next ? original.OrderBy(p => p.Dock == Dock.Right ? 1 : 0) : original.AsEnumerable())
            {
                DockPanel.SetDock(item.Element, next ? Dock.Top : item.Dock);
                if (item.Element is FrameworkElement control)
                {
                    control.Margin = next && item.Dock == Dock.Right ? new Thickness(0, 12, 0, 0) : item.Margin;
                    control.HorizontalAlignment = next && item.Dock == Dock.Right ? HorizontalAlignment.Left : item.Alignment;
                }
                row.Children.Add(item.Element);
            }
        }
        void Resize(object? s, SizeChangedEventArgs args) => Arrange();
        row.Loaded += (_, _) =>
        {
            owner = Window.GetWindow(row);
            original ??= row.Children.Cast<UIElement>().Select(child => new Placement(child, DockPanel.GetDock(child),
                child is FrameworkElement f ? f.Margin : default,
                child is FrameworkElement element ? element.HorizontalAlignment : HorizontalAlignment.Stretch)).ToArray();
            if (owner is not null) { owner.SizeChanged -= Resize; owner.SizeChanged += Resize; }
            Arrange();
        };
        row.Unloaded += (_, _) => { if (owner is not null) owner.SizeChanged -= Resize; };
    }

    public static readonly DependencyProperty AdaptiveRadarProperty = DependencyProperty.RegisterAttached(
        "AdaptiveRadar", typeof(bool), typeof(UiLayout), new PropertyMetadata(false, OnAdaptiveRadar));
    public static void SetAdaptiveRadar(DependencyObject o, bool value) => o.SetValue(AdaptiveRadarProperty, value);
    public static bool GetAdaptiveRadar(DependencyObject o) => (bool)o.GetValue(AdaptiveRadarProperty);
    static void OnAdaptiveRadar(DependencyObject o, DependencyPropertyChangedEventArgs e)
    {
        if (o is not FrameworkElement radar || !(bool)e.NewValue) return;
        Window? owner = null;
        void Arrange() { if (owner is not null) radar.Height = owner.ActualHeight < CompactHeight ? 344 : 408; }
        void Resize(object? s, SizeChangedEventArgs args) => Arrange();
        radar.Loaded += (_, _) => { owner = Window.GetWindow(radar); if (owner is not null) { owner.SizeChanged -= Resize; owner.SizeChanged += Resize; } Arrange(); };
        radar.Unloaded += (_, _) => { if (owner is not null) owner.SizeChanged -= Resize; };
    }
}
