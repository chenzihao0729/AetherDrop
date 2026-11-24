// SPDX-License-Identifier: GPL-2.0-only
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace AetherDrop;

/// <summary>
/// The tray icon's menu as the design draws it: open, allow receiving,
/// network mode (a panel beside the menu), Wi-Fi, settings, diagnostic
/// report, quit. It opens at the pointer and closes when it loses focus.
/// </summary>
public partial class TrayMenu : Window
{
    static App A => App.Instance;
    bool _closing;

    public TrayMenu()
    {
        InitializeComponent();
        Typography.Apply(this);
        AllowItem.Tag = A.Model.Allow ? "" : "";
        Deactivated += (_, _) => Shut();
    }

    /// <summary>Shows the menu with its lower right corner at the pointer, inside the work area.</summary>
    public void ShowAt(Point pointer)
    {
        Show();
        UpdateLayout();
        var area = SystemParameters.WorkArea;
        Left = Math.Max(area.Left, Math.Min(pointer.X - ActualWidth + 10, area.Right - ActualWidth));
        Top = Math.Max(area.Top, Math.Min(pointer.Y - ActualHeight + 10, area.Bottom - ActualHeight));
        Activate();
    }

    void Shut()
    {
        if (_closing) return;
        _closing = true;
        Close();
    }

    /// <summary>Opens the network mode panel (also for --snapshot).</summary>
    internal void OpenModes() { if (ModePanel.Visibility != Visibility.Visible) Mode_Click(this, new RoutedEventArgs()); }

    void Mode_Click(object sender, RoutedEventArgs e)
    {
        var open = ModePanel.Visibility != Visibility.Visible;
        var right = Left + ActualWidth;
        ModePanel.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        ModeItem.SetResourceReference(BackgroundProperty, open ? "AccentSubtle" : "Transparent");
        if (open) BuildModes();
        // The panel opens to the left: keep the menu where it was.
        UpdateLayout();
        Left = right - ActualWidth;
    }

    void BuildModes()
    {
        ModeList.Children.Clear();
        foreach (var (key, text) in new[] { ("concurrent", L.T("mode.concurrent.short")), ("ondemand", L.T("mode.ondemand.short")), ("off", L.T("mode.off.short")) })
        {
            var chosen = A.Model.Mode == key;
            var b = new Button { Style = (Style)FindResource("ChoiceRow"), Content = text, Margin = new Thickness(0, 1, 0, 1) };
            UiMotion.SetSelected(b, chosen);
            b.Click += (_, _) => { A.Service.SetMode(key); Shut(); };
            ModeList.Children.Add(b);
        }
    }

    void Open_Click(object sender, RoutedEventArgs e) { Shut(); A.ShowWindow(); }
    void Allow_Click(object sender, RoutedEventArgs e) { Shut(); A.SetDiscoverable(!A.Model.Allow); }
    void Wifi_Click(object sender, RoutedEventArgs e) { Shut(); A.ShowWifi(); }
    void Settings_Click(object sender, RoutedEventArgs e) { Shut(); A.ShowSettings("general"); }
    void Report_Click(object sender, RoutedEventArgs e) { Shut(); App.RunReport(); }
    void Quit_Click(object sender, RoutedEventArgs e) { Shut(); A.QuitApp(); }
}
