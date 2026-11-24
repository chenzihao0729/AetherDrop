// SPDX-License-Identifier: GPL-2.0-only
using System;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AetherDrop;

static partial class Check
{
    static void LayoutPass(App app, MainWindow main)
    {
        var settings = new SettingsWindow(); settings.Show(); Pump(220);
        var accent = (Border)System.Windows.Media.VisualTreeHelper.GetParent((StackPanel)((RadioButton)settings.FindName("AppearanceAuto")).Parent);
        var accentRow = (DockPanel)accent.Parent;
        var chosen = (RadioButton)settings.FindName("AppearanceAuto");
        Assert(DockPanel.GetDock(accent) == Dock.Right, "Regular settings keep labels and controls in two columns");
        foreach (var width in new[] { 460.0, 520.0 })
        {
            settings.Width = width; settings.Height = width < 500 ? 520 : 620; Pump(100);
            Assert(ReferenceEquals(accent.Parent, accentRow) && chosen.IsChecked == true,
                "Settings resize preserves the existing components and selections");
            Assert(DockPanel.GetDock(accent) == (width < 500 ? Dock.Top : Dock.Right),
                "Long settings rows adapt at the 500-DIP breakpoint");
            Assert(!((Button)settings.FindName("SaveButton")).IsEnabled, "Resizing settings does not create an unsaved edit");
            HeaderButtons(settings, UiLayout.TitleBarHeight);
            foreach (var name in new[] { "TabGeneral", "TabNetwork", "TabAppleId", "TabHardware", "TabUpdates" })
            {
                ((RadioButton)settings.FindName(name)).IsChecked = true; Pump(180);
                Capture(settings, $"layout-settings-{width}-{name}");
            }
            ((RadioButton)settings.FindName("TabGeneral")).IsChecked = true; Pump(100);
        }
        settings.Close();
        var languageWasChinese = L.Zh;
        L.Use("en");
        var english = new SettingsWindow(); english.Width = 460; english.Height = 620; english.Show(); Pump(220);
        foreach (var name in new[] { "TabGeneral", "TabNetwork", "TabAppleId", "TabHardware", "TabUpdates" })
        {
            ((RadioButton)english.FindName(name)).IsChecked = true; Pump(180);
            Capture(english, "layout-settings-en-460-" + name);
        }
        Assert(!((Button)english.FindName("SaveButton")).IsEnabled, "Wrapped English navigation and compact rows retain a clean draft");
        english.Close(); L.Use(languageWasChinese ? "zh" : "en");
        var tall = main.Height;
        main.Height = 560; Pump(100);
        Assert(((RadarView)main.FindName("MainRadar")).ActualHeight == 344,
            "Short main windows reduce radar height without scaling devices or labels");
        Capture(main, "layout-main-compact");
        main.Height = tall; Pump(100);
        Assert(((RadarView)main.FindName("MainRadar")).ActualHeight == 408,
            "Regular main windows restore the full radar layout");
        Capture(main, "layout-main-regular");

        var send = new SendWindow(); send.Show(); Pump(220);
        HeaderButtons(send, UiLayout.TitleBarHeight); Capture(send, "layout-send-regular");
        send.Width = 400; send.Height = 560; Pump(100); Capture(send, "layout-send-compact"); send.Close();

        var wifi = new WifiWindow(); wifi.Show();
        typeof(WifiWindow).GetMethod("Render", BindingFlags.NonPublic | BindingFlags.Instance, null,
            new[] { typeof(System.Text.Json.Nodes.JsonObject) }, null)!.Invoke(wifi,
                new object[] { System.Text.Json.Nodes.JsonNode.Parse("""{"mode":"concurrent","radio":"on","wifi":"connected","ssid":"测试网络","networks":[{"ssid":"测试网络","dbm":-48,"security":"wpa2-psk","saved":true,"connected":true},{"ssid":"访客网络","dbm":-65,"security":"open"}],"known":[{"ssid":"测试网络","auto":true}]}""")! });
        Pump(220); HeaderButtons(wifi, UiLayout.TitleBarHeight); Capture(wifi, "layout-wifi-regular");
        wifi.Width = 380; wifi.Height = 460; Pump(100); Capture(wifi, "layout-wifi-compact"); wifi.Close();

        var identity = new AppleIdWindow(); identity.Show(); Pump(220);
        HeaderButtons(identity, UiLayout.TitleBarHeight); Capture(identity, "layout-apple-login");
        ((StackPanel)identity.FindName("Credentials")).Visibility = Visibility.Collapsed;
        ((StackPanel)identity.FindName("CodePanel")).Visibility = Visibility.Visible;
        Pump(100); Capture(identity, "layout-apple-verification"); identity.Close();

        var tray = new TrayWindow(app); tray.Show(); Pump(220); Capture(tray, "layout-tray");
        Assert(tray.Width == 244 && tray.Height == 208, "Tray layout retains its established compact dimensions"); tray.Close();
        var invitation=new ReceiveWindow(new Request{Id="layout-invitation",Sender="iPhone",Model="iPhone15,2",What=""},(_,_)=>{});
        invitation.Show();Pump(120);Capture(invitation,"layout-card-invitation");invitation.Close();
        foreach (var state in new[] { "CreatePreview", "CreateCompletedPreview", "CreateFailedPreview" })
        {
            var card = (ReceiveWindow)typeof(ReceiveWindow).GetMethod(state, BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, null)!;
            card.Show(); Pump(240); Capture(card, "layout-card-" + state);
            Assert(card.Width == 302 && card.Height == 142, "Receive states retain their shared three-slot card dimensions");
            Assert(!Nodes<Button>(card).Any(b => b.Content?.ToString() == "×"), "Receive cards expose no top-right close button");
            if (state == "CreateFailedPreview")
            {
                Assert(Nodes<TextBlock>(card).Any(t => t.Text == "接收已停止"), "Interrupted receive uses the stopped title");
                Assert(Nodes<TextBlock>(card).Single(t => t.Text == "连接中断").FontSize == 16, "Interruption message uses the enlarged 16-DIP type");
                Assert(Nodes<System.Windows.Shapes.Path>(card).Count(p=>p.IsVisible && ((SolidColorBrush)p.Stroke).Color==UiColors.Get("StoppedMark",Palette.Dark))==2,"Stopped card shows a subdued red two-stroke X");
            }
            card.Close();
        }
        Assert(((SolidColorBrush)main.FindResource("Card")).Color.A == 255,
            "Shared page surfaces remain opaque in both appearances");
    }
}
