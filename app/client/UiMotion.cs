// SPDX-License-Identifier: GPL-2.0-only
using System.Runtime.CompilerServices;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace AetherDrop;

/// <summary>Shared surface feedback. Text never scales or becomes a cached bitmap.</summary>
public static class UiMotion
{
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
        "Enabled", typeof(bool), typeof(UiMotion), new PropertyMetadata(false, Enable));
    public static void SetEnabled(DependencyObject o, bool value) => o.SetValue(EnabledProperty, value);
    public static bool GetEnabled(DependencyObject o) => (bool)o.GetValue(EnabledProperty);
    public static readonly DependencyProperty SelectedProperty = DependencyProperty.RegisterAttached(
        "Selected", typeof(bool), typeof(UiMotion), new PropertyMetadata(false, SelectionChanged));
    public static void SetSelected(DependencyObject o, bool value) => o.SetValue(SelectedProperty, value);
    public static bool GetSelected(DependencyObject o) => (bool)o.GetValue(SelectedProperty);
    public static readonly DependencyProperty PopoverProperty = DependencyProperty.RegisterAttached(
        "Popover", typeof(bool), typeof(UiMotion), new PropertyMetadata(false, EnablePopover));
    public static void SetPopover(DependencyObject o, bool value) => o.SetValue(PopoverProperty, value);
    public static bool GetPopover(DependencyObject o) => (bool)o.GetValue(PopoverProperty);
    public static readonly DependencyProperty EntranceProperty = DependencyProperty.RegisterAttached(
        "Entrance", typeof(bool), typeof(UiMotion), new PropertyMetadata(false, (o, e) =>
        { if (o is FrameworkElement f && (bool)e.NewValue) f.Loaded += (_, _) => Reveal(f); }));
    public static void SetEntrance(DependencyObject o, bool value) => o.SetValue(EntranceProperty, value);
    public static bool GetEntrance(DependencyObject o) => (bool)o.GetValue(EntranceProperty);
    sealed class State { public bool Wired, Hover, Press; public DependencyPropertyDescriptor? Highlight; public EventHandler? Changed; }
    static readonly ConditionalWeakTable<Control, State> States = new();

    static void Enable(DependencyObject o, DependencyPropertyChangedEventArgs e)
    {
        if (o is not Control c || !(bool)e.NewValue) return;
        var state = States.GetOrCreateValue(c);
        if (state.Wired) return;
        state.Wired = true;
        c.IsVisibleChanged += (_, _) => { if (c.IsVisible) { c.ApplyTemplate(); Update(c, false); } };
        c.Loaded += (_, _) =>
        {
            c.ApplyTemplate();
            Update(c, false);
            var property = c is ComboBoxItem ? ComboBoxItem.IsHighlightedProperty : c is MenuItem ? MenuItem.IsHighlightedProperty : null;
            if (property is not null && state.Highlight is null)
            {
                state.Highlight = DependencyPropertyDescriptor.FromProperty(property, c.GetType());
                state.Changed = (_, _) => Update(c, true);
                state.Highlight?.AddValueChanged(c, state.Changed);
            }
        };
        c.Unloaded += (_, _) =>
        {
            if (state.Highlight is not null && state.Changed is not null) state.Highlight.RemoveValueChanged(c, state.Changed);
            state.Highlight = null; state.Changed = null; state.Hover = state.Press = false;
        };
        c.MouseEnter += (_, _) => { state.Hover = true; Update(c, true); };
        c.MouseLeave += (_, _) => { state.Hover = state.Press = false; Update(c, true); };
        c.PreviewMouseLeftButtonDown += (_, _) => { state.Press = true; Update(c, true); };
        c.PreviewMouseLeftButtonUp += (_, _) => { state.Press = false; Update(c, true); };
        c.LostMouseCapture += (_, _) => { state.Press = false; Update(c, true); };
        c.GotKeyboardFocus += (_, _) => Update(c, true);
        c.LostKeyboardFocus += (_, _) => Update(c, true);
        c.PreviewKeyDown += (_, e) => { if (e.Key is Key.Space or Key.Enter) state.Press = true; Update(c, true); };
        c.PreviewKeyUp += (_, _) => { state.Press = false; Update(c, true); };
        c.IsEnabledChanged += (_, _) => Update(c, true);
        c.AddHandler(ToggleButton.CheckedEvent, new RoutedEventHandler((_, _) => Update(c, c.IsLoaded)));
        c.AddHandler(ToggleButton.UncheckedEvent, new RoutedEventHandler((_, _) => Update(c, c.IsLoaded)));
        c.AddHandler(MenuItem.CheckedEvent, new RoutedEventHandler((_, _) => Update(c, c.IsLoaded)));
        c.AddHandler(MenuItem.UncheckedEvent, new RoutedEventHandler((_, _) => Update(c, c.IsLoaded)));
        c.AddHandler(Selector.SelectedEvent, new RoutedEventHandler((_, _) => Update(c, c.IsLoaded)));
        c.AddHandler(Selector.UnselectedEvent, new RoutedEventHandler((_, _) => Update(c, c.IsLoaded)));
    }

    static void SelectionChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
    {
        if (o is Control c && GetEnabled(c))
        {
            if (c is Button) System.Windows.Automation.AutomationProperties.SetItemStatus(c, (bool)e.NewValue ? (L.Zh ? "已选中" : "Selected") : "");
            Update(c, c.IsLoaded);
        }
    }
    static T? Part<T>(Control c, string name) where T : DependencyObject => c.Template?.FindName(name, c) as T;
    static void Update(Control c, bool animate)
    {
        if (!GetEnabled(c)) return;
        var state = States.GetOrCreateValue(c);
        bool selected = GetSelected(c) || c is ToggleButton { IsChecked: true } || c is ComboBoxItem { IsSelected: true } || c is MenuItem { IsChecked: true };
        bool enabled = c.IsEnabled;
        Fade(Part<UIElement>(c, "HoverWash"), enabled && (state.Hover || c is ComboBoxItem { IsHighlighted: true } || c is MenuItem { IsHighlighted: true }) ? 1 : 0, 120, animate);
        Fade(Part<UIElement>(c, "PressWash"), enabled && state.Press ? 1 : 0, state.Press ? 70 : 160, animate);
        Fade(Part<UIElement>(c, "SelectionWash"), selected ? 1 : 0, 160, animate);
        Fade(Part<UIElement>(c, "SelectionMark"), selected ? 1 : 0, 130, animate);
        Fade(Part<UIElement>(c, "FocusRing"), c.IsKeyboardFocused && (c is TextBoxBase or PasswordBox || InputManager.Current.MostRecentInputDevice is KeyboardDevice) ? 1 : 0, 120, animate);
        // Only the marker moves. Its reserved column and the label never change size.
        if (Part<FrameworkElement>(c, "SelectionMark") is { } mark)
        {
            if (mark.RenderTransform is not TranslateTransform) mark.RenderTransform = new TranslateTransform();
            var move = (TranslateTransform)mark.RenderTransform;
            Ease(move, TranslateTransform.XProperty, selected ? 0 : 4, 150, animate);
        }
        if (c is CheckBox toggle && Part<TranslateTransform>(c, "move") is { } thumb)
            Ease(thumb, TranslateTransform.XProperty, toggle.IsChecked == true ? 18 : 0, 180, animate);
    }
    static void Fade(UIElement? target, double value, int ms, bool animate)
    {
        if (target is null) return;
        var current = target.Opacity;
        target.BeginAnimation(UIElement.OpacityProperty, null);
        target.Opacity = value;
        if (animate && !Motion.Reduced && Math.Abs(current - value) > .001)
            target.BeginAnimation(UIElement.OpacityProperty, Animation(current, value, ms));
    }
    static void Ease(Animatable target, DependencyProperty property, double value, int ms, bool animate)
    {
        var current = (double)target.GetValue(property);
        target.BeginAnimation(property, null); target.SetValue(property, value);
        if (animate && !Motion.Reduced && Math.Abs(current - value) > .001)
            target.BeginAnimation(property, Animation(current, value, ms));
    }
    static DoubleAnimation Animation(double from, double to, int ms) => new(from, to, TimeSpan.FromMilliseconds(ms))
    { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop };

    public static void Reveal(UIElement element, double rise = 6)
    {
        element.BeginAnimation(UIElement.OpacityProperty, null);
        element.Opacity = 1; element.RenderTransform = Transform.Identity;
        if (Motion.Reduced) return;
        var move = new TranslateTransform(); element.RenderTransform = move;
        var slide = Animation(rise, 0, 180);
        slide.Completed += (_, _) => { if (ReferenceEquals(element.RenderTransform, move)) element.RenderTransform = Transform.Identity; };
        move.BeginAnimation(TranslateTransform.YProperty, slide);
        element.BeginAnimation(UIElement.OpacityProperty, Animation(0, 1, 140));
    }
    static void EnablePopover(DependencyObject o, DependencyPropertyChangedEventArgs e)
    {
        if (o is not Popup popup || !(bool)e.NewValue) return;
        popup.Opened += (_, _) =>
        {
            popup.PopupAnimation = Motion.Reduced ? PopupAnimation.None : PopupAnimation.Fade;
            if (popup.Child is UIElement child) Reveal(child, 4);
        };
    }
}
