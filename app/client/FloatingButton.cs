// SPDX-License-Identifier: GPL-2.0-only
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace NekoDrop;

/// <summary>Animate only the button surface; its text stays at native size.</summary>
internal sealed class FloatingButton : Button
{
    Border? _surface, _feedback;

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _surface = GetTemplateChild("ButtonSurface") as Border;
        _feedback = GetTemplateChild("InteractionTint") as Border;
        // Template Freezables can be frozen/shared. Each instance owns its motion.
        if (_surface is not null) _surface.RenderTransform = new ScaleTransform(1, 1);
        if (_feedback is not null) _feedback.RenderTransform = new ScaleTransform(1, 1);
        UpdateFeedback(false);
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == IsPressedProperty || e.Property == IsMouseOverProperty || e.Property == IsEnabledProperty)
            UpdateFeedback(IsLoaded);
    }

    void UpdateFeedback(bool animate)
    {
        if (_surface is null || _feedback is null) return;
        bool pressed = IsEnabled && IsPressed;
        double opacity = !IsEnabled ? 0 : pressed ? .18 : IsMouseOver ? .08 : 0;
        double scale = pressed ? .97 : 1;
        double seconds = pressed ? .07 : .16;
        Ease(_feedback, OpacityProperty, opacity, seconds, animate);
        foreach (var border in new[] { _surface, _feedback })
        {
            if (border.RenderTransform is not ScaleTransform transform) continue;
            Ease(transform, ScaleTransform.ScaleXProperty, scale, seconds, animate);
            Ease(transform, ScaleTransform.ScaleYProperty, scale, seconds, animate);
        }
        Opacity = IsEnabled ? 1 : .45;
    }

    static void Ease(Animatable target, DependencyProperty property, double value, double seconds, bool animate)
    {
        double from = (double)target.GetValue(property);
        target.BeginAnimation(property, null);
        target.SetValue(property, value);
        if (animate && FloatingCard.Motion)
            target.BeginAnimation(property, new DoubleAnimation(from, value, TimeSpan.FromSeconds(seconds))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop,
            });
    }

    static void Ease(UIElement target, DependencyProperty property, double value, double seconds, bool animate)
    {
        double from = (double)target.GetValue(property);
        target.BeginAnimation(property, null);
        target.SetValue(property, value);
        if (animate && FloatingCard.Motion)
            target.BeginAnimation(property, new DoubleAnimation(from, value, TimeSpan.FromSeconds(seconds))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop,
            });
    }
}
