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
    static void VerifyAppearance(App app)
    {
        var settings=new SettingsWindow();settings.Show();Pump(180);
        var card=(ReceiveWindow)typeof(ReceiveWindow).GetMethod("CreateFailedPreview",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,null)!;
        card.Show();Pump(220);
        var cardHandle=new System.Windows.Interop.WindowInteropHelper(card).Handle;
        var save=(Button)settings.FindName("SaveButton");
        var stroke=Nodes<System.Windows.Shapes.Path>(card).First(p=>p.IsVisible);
        foreach(var mode in new[]{"light","dark","auto"})
        {
            var button=(RadioButton)settings.FindName(mode=="light"?"AppearanceLight":mode=="dark"?"AppearanceDark":"AppearanceAuto");
            button.IsChecked=true;Pump(30);
            Assert(save.IsEnabled,"Changing appearance is a pending setting: "+mode);
            save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump(300);
            Assert(app.Settings.Appearance==mode && Palette.Preference==mode,"Saving persists and applies appearance: "+mode);
            Assert(mode=="auto" || Palette.Dark==(mode=="dark"),"Explicit appearance overrides system preference");
            Assert(!save.IsEnabled,"Appearance save returns settings to a clean baseline");
            Assert(((SolidColorBrush)app.FindResource("MicaSolid")).Color==UiColors.Get("Background",Palette.Dark),"Window tokens follow appearance");
            Assert(((SolidColorBrush)stroke.Stroke).Color==UiColors.Get("StoppedMark",Palette.Dark),"Already open receive-card brush follows appearance");
            Assert(card.IsVisible && new System.Windows.Interop.WindowInteropHelper(card).Handle==cardHandle,"Switching appearance retains the live receive-card window");
            Nodes<ScrollViewer>(settings).First().ScrollToEnd();Pump(80);
            Capture(settings,"appearance-settings-"+mode);Capture(card,"appearance-card-"+mode);
        }
        card.Close();settings.Close();
        Assert(!app.Engine.Running && app.Service.Last is null,"Appearance changes require no engine or service restart");
    }
}
