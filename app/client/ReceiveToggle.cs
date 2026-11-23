// SPDX-License-Identifier: GPL-2.0-only
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace NekoDrop;

/// <summary>Retains the native CheckBox input/accessibility behavior with a sliding thumb.</summary>
internal sealed class ReceiveToggle : CheckBox
{
    static Color Off => Palette.Color(191, 197, 201);
    static Color On => Palette.Color(0, 122, 255);
    Border? _track, _knob;
    TranslateTransform? _offset;
    SolidColorBrush? _fill;

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _track = GetTemplateChild("Track") as Border;
        _knob = GetTemplateChild("Knob") as Border;
        if (_track is null || _knob is null) return;
        _knob.RenderTransform = _offset = new TranslateTransform();
        _track.Background = _fill = new SolidColorBrush(Off);
        UpdateState(false);
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == IsCheckedProperty) UpdateState(IsLoaded);
    }

    void UpdateState(bool animate)
    {
        if (_offset is null || _fill is null) return;
        // 39-wide track minus the 16-wide thumb and two 2-DIP edge insets.
        double destination = IsChecked == true ? 19 : 0;
        var color = IsChecked == true ? On : Off;
        double from = _offset.X; var fromColor = _fill.Color;
        _offset.BeginAnimation(TranslateTransform.XProperty, null);
        _fill.BeginAnimation(SolidColorBrush.ColorProperty, null);
        _offset.X = destination; _fill.Color = color;
        if (!animate || !FloatingCard.Motion) return;
        _offset.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(from, destination, TimeSpan.FromMilliseconds(200))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop,
        });
        _fill.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(fromColor, color, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop,
        });
    }
}
