// SPDX-License-Identifier: GPL-2.0-only
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AetherDrop;

/// <summary>Per-pixel antialiased window silhouette, with a fixed clip outside animated content.</summary>
internal sealed class WindowSurface : Grid
{
    public const double Radius = UiLayout.WindowRadius;
    public WindowSurface(UIElement content)
    {
        var shape = new Border { Name = "WindowShape", CornerRadius = new CornerRadius(Radius), BorderThickness = new Thickness(1), SnapsToDevicePixels = false };
        shape.SetResourceReference(Border.BackgroundProperty, "MicaSolid");
        shape.SetResourceReference(Border.BorderBrushProperty, "BorderStrong");
        var clip = new Decorator { Name = "WindowClip", Margin = new Thickness(1), Child = content };
        clip.SizeChanged += (_, _) => clip.Clip = new RectangleGeometry(new Rect(0, 0, clip.ActualWidth, clip.ActualHeight), Radius - 1, Radius - 1);
        Children.Add(shape); Children.Add(clip);
    }
}
