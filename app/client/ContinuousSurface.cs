// SPDX-License-Identifier: GPL-2.0-only
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AetherDrop;

/// <summary>Vector corners with continuous tangent and curvature, including the straight joins.</summary>
public sealed class ContinuousSurface : Decorator
{
    public static readonly DependencyProperty BackgroundProperty = Border.BackgroundProperty.AddOwner(typeof(ContinuousSurface),
        new FrameworkPropertyMetadata(Brushes.Transparent, FrameworkPropertyMetadataOptions.AffectsRender));
    public Brush Background { get => (Brush)GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }
    public static readonly DependencyProperty BorderBrushProperty = Border.BorderBrushProperty.AddOwner(typeof(ContinuousSurface),
        new FrameworkPropertyMetadata(Brushes.Transparent, FrameworkPropertyMetadataOptions.AffectsRender));
    public Brush BorderBrush { get => (Brush)GetValue(BorderBrushProperty); set => SetValue(BorderBrushProperty, value); }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var w = Math.Max(0, ActualWidth - 1);
        var h = Math.Max(0, ActualHeight - 1);
        var r = Math.Min(26, Math.Min(w, h) / 2);
        if (r <= 0) return;
        var outline = new StreamGeometry();
        using (var g = outline.Open())
        {
            Point P(double x, double y) => new(x + .5, y + .5);
            g.BeginFigure(P(r, 0), true, true);
            g.LineTo(P(w-r, 0), true, false);
            g.BezierTo(P(w-.55*r, 0), P(w-.4*r, 0), P(w-.2*r, .2*r), true, true);
            g.BezierTo(P(w, .4*r), P(w, .55*r), P(w, r), true, true);
            g.LineTo(P(w, h-r), true, false);
            g.BezierTo(P(w, h-.55*r), P(w, h-.4*r), P(w-.2*r, h-.2*r), true, true);
            g.BezierTo(P(w-.4*r, h), P(w-.55*r, h), P(w-r, h), true, true);
            g.LineTo(P(r, h), true, false);
            g.BezierTo(P(.55*r, h), P(.4*r, h), P(.2*r, h-.2*r), true, true);
            g.BezierTo(P(0, h-.4*r), P(0, h-.55*r), P(0, h-r), true, true);
            g.LineTo(P(0, r), true, false);
            g.BezierTo(P(0, .55*r), P(0, .4*r), P(.2*r, .2*r), true, true);
            g.BezierTo(P(.4*r, 0), P(.55*r, 0), P(r, 0), true, true);
        }
        outline.Freeze();
        dc.DrawGeometry(Background, new Pen(BorderBrush, 1), outline);
    }
}
