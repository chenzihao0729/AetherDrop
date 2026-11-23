// SPDX-License-Identifier: GPL-2.0-only
/*
 * nekodrop's Wi-Fi settings, for while nekodrop owns the card and Windows'
 * own Wi-Fi list is gone: nearby networks (the radio's firmware scan, via the
 * service), connect with a saved or new password, hidden networks, the saved
 * networks with forget and auto-connect, and the current connection.
 */
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NekoDrop;

public partial class WifiWindow : Window
{
    static App A => App.Instance;
    string? _expanded; /* the network whose password box is open */

    public WifiWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => { if (Native.ApplyMica(this)) Background = Brushes.Transparent; };
        A.Service.State += OnState;
        A.Service.Reply += OnReply;
        Closed += (_, _) =>
        {
            A.Service.State -= OnState;
            A.Service.Reply -= OnReply;
        };
        if (A.Service.Last is { } last) Render(last);
        Scan();
    }

    void OnState(JsonObject o) => Dispatcher.BeginInvoke(() => Render(o));

    void OnReply(JsonObject o) => Dispatcher.BeginInvoke(() =>
    {
        if ((string?)o["event"] != "wifi-connect") return;
        var error = (string?)o["error"] ?? "";
        if (error == "password needed")
        {
            _expanded = (string?)o["ssid"];
            if (A.Service.Last is { } last) Render(last);
            return;
        }
        ShowError(error == "needs concurrent mode" ? L.T("wifi.needConcurrent") : error);
    });

    void ShowError(string text)
    {
        ErrorText.Text = text;
        ErrorText.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
    }

    void Scan() => A.Service.Send(new JsonObject { ["do"] = "wifi-scan" });

    void Connect(string ssid, string? password, bool? auto, bool hidden = false)
    {
        ShowError("");
        var cmd = new JsonObject { ["do"] = "wifi-connect", ["ssid"] = ssid, ["hidden"] = hidden };
        if (!string.IsNullOrEmpty(password)) cmd["password"] = password;
        if (auto is bool a) cmd["auto"] = a;
        A.Service.Send(cmd);
    }

    static string Bars(int dbm) => dbm >= -55 ? "" : dbm >= -67 ? "" : dbm >= -78 ? "" : "";

    static string SecurityText(string s) => s switch
    {
        "open" => L.T("wifi.sec.open"),
        "wpa2-psk" => "WPA2",
        "wpa2/3-psk" => "WPA2/WPA3",
        "wpa3-sae" => "WPA3",
        "owe" => L.T("wifi.sec.owe"),
        "802.1x" => L.T("wifi.sec.enterprise"),
        "wep" => "WEP",
        _ => s,
    };

    static (string Ip, string Gateway) Address()
    {
        foreach (var n in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (n.Name != "nekodrop") continue;
            var p = n.GetIPProperties();
            var ip = p.UnicastAddresses.FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork)?.Address.ToString() ?? "";
            var gw = p.GatewayAddresses.FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork)?.Address.ToString() ?? "";
            return (ip, gw);
        }
        return ("", "");
    }

    void Render(JsonObject o)
    {
        var wifi = (string?)o["wifi"] ?? "";
        var ssid = (string?)o["ssid"] ?? "";
        var radio = (string?)o["radio"] ?? "";
        var mode = (string?)o["mode"] ?? "";
        var scanning = (bool?)o["scanning"] ?? false;
        var connected = wifi == "connected";

        if (mode != "concurrent")
        {
            StateTitle.Text = L.T("wifi.notConcurrent");
            StateDetail.Text = L.T("wifi.notConcurrentHint");
        }
        else if (connected)
        {
            var (ip, gw) = Address();
            StateTitle.Text = L.F("wifi.connectedTo", ssid);
            StateDetail.Text = ip == "" ? L.T("wifi.gettingAddress") : L.F("wifi.address", ip, gw);
        }
        else if (ssid != "" && wifi != "")
        {
            StateTitle.Text = L.F("wifi.connectingTo", ssid);
            StateDetail.Text = wifi;
        }
        else
        {
            StateTitle.Text = L.T("wifi.notConnected");
            StateDetail.Text = radio == "on" ? L.T("wifi.pick") : L.T("wifi.radioOff");
        }
        StateGlyph.Text = connected ? "" : "";
        DisconnectButton.IsEnabled = ssid != "";
        RefreshButton.IsEnabled = radio == "on" && !scanning;
        ScanText.Text = scanning ? L.T("wifi.scanning") : "";
        var serviceError = (string?)o["wifiError"] ?? "";
        if (serviceError != "") ShowError(serviceError);

        NetworkList.Children.Clear();
        var networks = (o["networks"] as JsonArray)?.OfType<JsonObject>().ToList() ?? [];
        EmptyText.Visibility = networks.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        foreach (var n in networks) NetworkList.Children.Add(Row(n));

        SavedList.Children.Clear();
        foreach (var k in (o["known"] as JsonArray)?.OfType<JsonObject>() ?? [])
            SavedList.Children.Add(SavedRow((string?)k["ssid"] ?? "", (bool?)k["auto"] ?? true));
    }

    UIElement Row(JsonObject n)
    {
        var ssid = (string?)n["ssid"] ?? "";
        var dbm = (int?)n["dbm"] ?? -90;
        var sec = (string?)n["security"] ?? "";
        var saved = (bool?)n["saved"] ?? false;
        var isConnected = (bool?)n["connected"] ?? false;
        var card = new Border
        {
            Background = (Brush)FindResource("Card"),
            BorderBrush = (Brush)FindResource("CardStroke"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 0, 0, 6),
        };
        var stack = new StackPanel();
        var line = new DockPanel();
        var action = new Button
        {
            Content = isConnected ? L.T("wifi.connected") : L.T("wifi.connect"),
            Style = (Style)FindResource(isConnected ? "Flat" : "Primary"),
            IsEnabled = !isConnected,
            VerticalAlignment = VerticalAlignment.Center,
        };
        DockPanel.SetDock(action, Dock.Right);
        line.Children.Add(action);
        var glyph = new TextBlock
        {
            FontFamily = (FontFamily)FindResource("IconFont"),
            Text = Bars(dbm),
            FontSize = 18,
            Margin = new Thickness(0, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        DockPanel.SetDock(glyph, Dock.Left);
        line.Children.Add(glyph);
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock { Text = ssid, FontWeight = Typography.EmphasisWeight, TextTrimming = TextTrimming.CharacterEllipsis });
        var sub = SecurityText(sec) + (saved ? " · " + L.T("wifi.savedTag") : "") + $" · {dbm} dBm";
        text.Children.Add(new TextBlock { Text = sub, Style = (Style)FindResource("Caption") });
        line.Children.Add(text);
        stack.Children.Add(line);

        if (_expanded == ssid && !isConnected)
        {
            var box = new PasswordBox { Padding = new Thickness(6, 4, 6, 4), Margin = new Thickness(0, 8, 0, 6) };
            var auto = new CheckBox { Content = L.T("wifi.autoConnect"), IsChecked = true, Foreground = (Brush)FindResource("Text") };
            var go = new Button { Content = L.T("wifi.join"), Style = (Style)FindResource("Primary"), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 6, 0, 0) };
            go.Click += (_, _) => { _expanded = null; Connect(ssid, box.Password, auto.IsChecked == true); };
            box.KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Enter) { _expanded = null; Connect(ssid, box.Password, auto.IsChecked == true); } };
            stack.Children.Add(new TextBlock { Text = L.T("wifi.password"), Style = (Style)FindResource("Caption"), Margin = new Thickness(0, 8, 0, 0) });
            stack.Children.Add(box);
            stack.Children.Add(auto);
            stack.Children.Add(go);
            Dispatcher.BeginInvoke(() => box.Focus());
        }
        action.Click += (_, _) =>
        {
            if (saved || sec is "open" or "owe") Connect(ssid, null, null);
            else
            {
                _expanded = ssid;
                if (A.Service.Last is { } last) Render(last);
            }
        };
        card.Child = stack;
        return card;
    }

    UIElement SavedRow(string ssid, bool auto)
    {
        var row = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
        var forget = new Button { Content = L.T("wifi.forget"), Style = (Style)FindResource("Flat"), Margin = new Thickness(8, 0, 0, 0) };
        forget.Click += (_, _) => A.Service.Send(new JsonObject { ["do"] = "wifi-forget", ["ssid"] = ssid });
        DockPanel.SetDock(forget, Dock.Right);
        row.Children.Add(forget);
        var check = new CheckBox { Content = L.T("wifi.auto"), IsChecked = auto, VerticalAlignment = VerticalAlignment.Center, Foreground = (Brush)FindResource("Text") };
        check.Click += (_, _) => A.Service.Send(new JsonObject { ["do"] = "wifi-auto", ["ssid"] = ssid, ["on"] = check.IsChecked == true });
        DockPanel.SetDock(check, Dock.Right);
        row.Children.Add(check);
        row.Children.Add(new TextBlock { Text = ssid, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
        return row;
    }

    void Refresh_Click(object sender, RoutedEventArgs e) => Scan();

    void Disconnect_Click(object sender, RoutedEventArgs e) => A.Service.Send(new JsonObject { ["do"] = "wifi-disconnect" });

    void Hidden_Click(object sender, RoutedEventArgs e)
    {
        var ssid = HiddenSsid.Text.Trim();
        if (ssid.Length is 0 or > 32) return;
        Connect(ssid, HiddenPassword.Password, true, hidden: true);
    }
}
