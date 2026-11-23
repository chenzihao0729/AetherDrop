// SPDX-License-Identifier: GPL-2.0-only
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using NekoDrop;
static class Check
{
    [System.Runtime.InteropServices.DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr hwnd,int attribute,out int value,int size);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern int GetWindowRgn(IntPtr hwnd,IntPtr region);
    [System.Runtime.InteropServices.DllImport("gdi32.dll")] static extern IntPtr CreateRectRgn(int l,int t,int r,int b);
    [System.Runtime.InteropServices.DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr region);
    static void SmoothCorners(Window window) {
        if(Environment.OSVersion.Version.Build<22000)return;
        var hwnd=new System.Windows.Interop.WindowInteropHelper(window).Handle;
        var region=CreateRectRgn(0,0,0,0);
        try {Assert(DwmGetWindowAttribute(hwnd,33,out int preference,sizeof(int))==0 && preference==2 && GetWindowRgn(hwnd,region)<=0,window.GetType().Name+" uses native smooth corners without a pixel clipping region");}
        finally{DeleteObject(region);}
    }
    static void Assert(bool value,string message) { if(!value)throw new Exception(message); Console.WriteLine("PASS "+message); }
    static void Pump(int ms) { var f=new DispatcherFrame();var t=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(ms)};t.Tick+=(_,_)=>{t.Stop();f.Continue=false;};t.Start();Dispatcher.PushFrame(f); }
    static string Hash(string path)=>File.Exists(path)?Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))):"missing";
    static string Registration(){using var protocol=Registry.CurrentUser.OpenSubKey(@"Software\Classes\nekodrop\shell\open\command");using var run=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");return $"{protocol?.GetValue("")}|{run?.GetValue("nekodrop")}|{run?.GetValue("AetherDrop")}";}
    static T[] Nodes<T>(DependencyObject p)where T:DependencyObject {var l=new System.Collections.Generic.List<T>();for(int i=0;i<VisualTreeHelper.GetChildrenCount(p);i++){var c=VisualTreeHelper.GetChild(p,i);if(c is T x)l.Add(x);l.AddRange(Nodes<T>(c));}return l.ToArray();}
    static void Capture(Window window,string name) {
        string? dir=Environment.GetEnvironmentVariable("AETHERDROP_SETTINGS_CAPTURE_DIR");if(string.IsNullOrEmpty(dir))return;
        Directory.CreateDirectory(dir);window.UpdateLayout();
        var image=new System.Windows.Media.Imaging.RenderTargetBitmap((int)window.Width,(int)window.Height,96,96,PixelFormats.Pbgra32);image.Render((Visual)window.Content);
        var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image));
        using var stream=File.Create(Path.Combine(dir,name+".png"));encoder.Save(stream);
    }
    [STAThread] static int Main()
    {
        string path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"nekodrop","settings.json");
        string before=Hash(path), registry=Registration();
        var app=new App(uiDebugMode:true){ShutdownMode=ShutdownMode.OnMainWindowClose};
        Exception? failure=null;
        app.Startup+=(_,_)=>app.Dispatcher.BeginInvoke(new Action(()=>{
            try{
                Assert(app.MainWindow is UiDebugWindow && app.MainWindow.IsVisible,"UI debug control panel starts through actual startup path");
                Assert(app.UiDebugMode && app.Settings.IsTransient,"Debug settings are memory-only");
                app.SetDiscoverable(false);app.SetDiscoverable(true);app.Settings.Save();
                Assert(Hash(path)==before,"Receive switch and Save do not modify production settings");
                var settings=new SettingsWindow();settings.Show();Pump(100);
                SmoothCorners(settings);
                var settingsWork=FloatingCard.WorkArea(settings,System.Windows.Forms.Cursor.Position);
                settings.CenterOnDesktop();Pump(30);
                Assert(Math.Abs(settings.Left+(settings.Width/2)-(settingsWork.Left+settingsWork.Width/2))<=1 && Math.Abs(settings.Top+(settings.Height/2)-(settingsWork.Top+settingsWork.Height/2))<=1,"Settings opens centered within the current desktop work area");
                Assert(((SolidColorBrush)settings.Foreground).Color==Palette.Color(28,32,36) && ((SolidColorBrush)settings.FindResource("Text")).Color==Palette.Color(28,32,36),"Settings uses popup light palette independently of system theme");
                var scroll=(ScrollViewer)settings.FindName("SettingsScroll");
                var save=Nodes<Button>(settings).Single(b=>b.IsDefault);
                var cancel=Nodes<Button>(settings).Single(b=>b.IsCancel);
                double saveY=save.TranslatePoint(new Point(),settings).Y;
                Capture(settings,"settings-top");
                scroll.ScrollToBottom();Pump(80);Capture(settings,"settings-bottom");
                Assert(scroll.VerticalOffset>0 && save.TranslatePoint(new Point(),settings).Y==saveY && save.IsVisible && cancel.IsVisible,"Save and Cancel stay fixed while every settings section scrolls");
                var experimental=(CheckBox)settings.FindName("BtCoexBox");
                Assert(experimental.TranslatePoint(new Point(),scroll).Y>=0 && experimental.TranslatePoint(new Point(0,experimental.ActualHeight),scroll).Y<=scroll.ActualHeight,"Final experimental settings remain fully reachable");
                var language=(ComboBox)settings.FindName("LanguageBox");language.BringIntoView();Pump(60);language.IsDropDownOpen=true;Pump(80);
                Assert(language.IsDropDownOpen && language.Items.Count==3 && ((System.Windows.Controls.Primitives.Popup)language.Template.FindName("PART_Popup",language)).IsOpen,"Restyled language selector opens its real dropdown");
                language.SelectedIndex=2;language.IsDropDownOpen=false;
                var nameBox=(TextBox)settings.FindName("NameBox");nameBox.Text="UI preview";
                settings.Width=520;settings.Height=500;scroll.ScrollToBottom();Pump(80);Capture(settings,"settings-compact-bottom");
                SmoothCorners(settings);
                Assert(save.TranslatePoint(new Point(0,save.ActualHeight),settings).Y<=settings.Height && cancel.TranslatePoint(new Point(),settings).X>=0,"Footer controls remain visible at minimum window size");
                cancel.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump(30);
                Assert(!settings.IsVisible && Hash(path)==before && app.Settings.DeviceName!="UI preview","Cancel closes redesigned settings without saving edits");
                var main=new MainWindow();main.Show();Pump(90);Capture(main,"main-empty");
                SmoothCorners(main);
                Assert(((SolidColorBrush)main.Foreground).Color==Palette.Color(28,32,36) && ((TextBlock)main.FindName("MachineText")).Text==Environment.MachineName,"Main window uses popup light palette and real machine name");
                Assert(((FrameworkElement)main.FindName("NoDevices")).IsVisible && ((FrameworkElement)main.FindName("DropHint")).IsVisible && ((FrameworkElement)main.FindName("NoReceived")).IsVisible,"Main window provides visible empty states for devices, files and history");
                app.Model.Devices.Add(new Device {Id="ui-main-device",Name="iPhone",Model="iPhone15,2"});
                app.Model.Files.Add(@"C:\private-filename.jpg");app.Model.ReceivedFiles.Add(new Received {Name="private-received.jpg",Path="",From="iPhone"});Pump(70);
                Assert(!((FrameworkElement)main.FindName("NoDevices")).IsVisible && ((FrameworkElement)main.FindName("SelectedFiles")).IsVisible && !((FrameworkElement)main.FindName("NoReceived")).IsVisible,"Main sections update when devices, selected files and received items change");
                Assert(!Nodes<TextBlock>(main).Any(t=>t.Text.Contains("private-")),"Redesigned main window does not expose file names");
                Capture(main,"main-populated");
                var mainScroll=(ScrollViewer)main.FindName("MainScroll");mainScroll.ScrollToBottom();Pump(50);Capture(main,"main-bottom");
                var clear=Nodes<Button>(main).Single(b=>b.Content?.ToString()==L.T("main.clear"));clear.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump(30);
                Assert(app.Model.Files.Count==0 && ((FrameworkElement)main.FindName("DropHint")).IsVisible,"Main clear action restores the drag-and-drop empty state");
                main.Width=540;main.Height=520;mainScroll.ScrollToBottom();Pump(60);Capture(main,"main-compact");
                SmoothCorners(main);
                Assert(Nodes<Button>(main).Where(b=>b.Content?.ToString()==L.T("main.settings") || b.Content?.ToString()==L.T("main.report")).All(b=>b.TranslatePoint(new Point(0,b.ActualHeight),main).Y<=main.Height),"Main footer actions stay visible at minimum window size");
                main.Close();app.Model.Devices.Clear();app.Model.ReceivedFiles.Clear();
                typeof(App).GetMethod("CreateTray",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(app,null);
                var trayField=typeof(App).GetField("_tray",BindingFlags.Instance|BindingFlags.NonPublic)!;
                var actualIcon=(System.Windows.Forms.NotifyIcon)trayField.GetValue(app)!;
                var click=typeof(System.Windows.Forms.NotifyIcon).GetMethod("OnMouseDown",BindingFlags.Instance|BindingFlags.NonPublic)!;
                var doubleClick=typeof(System.Windows.Forms.NotifyIcon).GetMethod("OnMouseDoubleClick",BindingFlags.Instance|BindingFlags.NonPublic)!;
                var clickArgs=new System.Windows.Forms.MouseEventArgs(System.Windows.Forms.MouseButtons.Left,1,0,0,0);
                click.Invoke(actualIcon,new object[]{clickArgs});
                Assert(app.Windows.OfType<TrayWindow>().Any(w=>w.IsVisible),"Actual tray single-click opens popup synchronously without a double-click timer");
                doubleClick.Invoke(actualIcon,new object[]{new System.Windows.Forms.MouseEventArgs(System.Windows.Forms.MouseButtons.Left,2,0,0,0)});
                click.Invoke(actualIcon,new object[]{new System.Windows.Forms.MouseEventArgs(System.Windows.Forms.MouseButtons.Left,2,0,0,0)});Pump(40);
                Assert(app.Windows.OfType<MainWindow>().Any(w=>w.IsVisible) && !app.Windows.OfType<TrayWindow>().Any(w=>w.IsVisible),"Actual tray double-click opens main UI and suppresses trailing single-click popup");
                click.Invoke(actualIcon,new object[]{clickArgs});
                Assert(app.Windows.OfType<TrayWindow>().Any(w=>w.IsVisible),"New single press immediately after double-click opens the popup without a cooldown");
                app.Windows.OfType<TrayWindow>().Single().Close();
                app.Windows.OfType<MainWindow>().Single().Close();actualIcon.Visible=false;actualIcon.Dispose();trayField.SetValue(app,null);
                var session=(UiDebugSession)typeof(UiDebugWindow).GetField("_session",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(app.MainWindow)!;
                session.ShowTray();Pump(450);Assert(app.Windows.OfType<TrayWindow>().Any(w=>w.IsVisible),"Real tray card opens in debug mode");
                var tray=app.Windows.OfType<TrayWindow>().Single(w=>w.IsVisible);
                var toggle=Nodes<CheckBox>(tray).Single();
                var knob=(Border)toggle.Template.FindName("Knob",toggle);
                var track=(Border)toggle.Template.FindName("Track",toggle);
                var offset=(TranslateTransform)knob.RenderTransform;
                Assert(offset.X==19 && toggle.Width==39 && toggle.Height==20,"Receive switch opens in the correct state with unchanged dimensions");
                Capture(tray,"toggle-000-on");
                app.SetDiscoverable(false);Pump(55);Capture(tray,"toggle-055-to-off");
                if(FloatingCard.Motion)Assert(offset.X>0 && offset.X<19 && ((SolidColorBrush)track.Background).Color!=Palette.Color(191,197,201),"Switch thumb and track color animate together through intermediate states");
                double turning=offset.X;app.SetDiscoverable(true);
                Pump(20);
                if(FloatingCard.Motion)Assert(offset.X>=turning-.5 && offset.X<19,"Rapid switch reversal continues from its current position without jumping to the endpoint");
                Pump(240);Capture(tray,"toggle-295-on");
                Assert(offset.X==19 && ((SolidColorBrush)track.Background).Color==Palette.Color(0,122,255),"Switch settles exactly at its on position and blue color");
                app.SetDiscoverable(false);Pump(240);Capture(tray,"toggle-535-off");
                Assert(offset.X==0 && ((SolidColorBrush)track.Background).Color==Palette.Color(191,197,201) && Nodes<TextBlock>(tray).Any(t=>t.Text=="接收已关闭"),"Switch off state and receiving-status text agree after animation");
                app.SetDiscoverable(true);Pump(240);
                Assert(tray.IsVisible && Hash(path)==before,"Animated toggles keep tray open and debug settings off disk");
                session.ShowTray();Pump(240);
                Assert(!tray.IsVisible,"Second tray-icon action closes the open popup");
                session.ShowTray();Pump(370);
                var reopened=app.Windows.OfType<TrayWindow>().Single(w=>w.IsVisible);
                Assert(reopened!=tray,"Tray action opens a fresh popup after the previous one closes");
                session.ShowTray();session.ShowTray();Pump(650);
                Assert(app.Windows.OfType<TrayWindow>().Count(w=>w.IsVisible)==1,"Click during close queues a single reopen without overlapping popups");
                session.Invite();Pump(500);var invitation=app.Windows.OfType<ReceiveWindow>().Single(w=>w.IsVisible);
                Nodes<Button>(invitation).Single(b=>b.Content?.ToString()=="接受").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump(800);
                Assert(app.Model.Requests.Single().Progress>0,"Accepting mock invitation drives actual progress view");
                session.TogglePause();double percent=app.Model.Requests.Single().Progress;Pump(700);
                Assert(session.Paused && app.Model.Requests.Single().Progress==percent,"Pause stops simulated byte progress");
                Assert(Nodes<TextBlock>(invitation).Any(t=>t.IsVisible && t.Text=="0 KB/s"),"Paused simulation reports zero rate rather than a constant speed");
                session.TogglePause();Pump(400);Assert(app.Model.Requests.Single().Progress>percent,"Continue resumes mock progress");
                session.Complete();Pump(40);Assert(app.Model.Requests.Count==0 && app.Model.ReceivedFiles.Count==1,"Complete uses the real stored-event UI flow");
                Assert(!Directory.Exists("模拟保存位置（不会写入文件）"),"Mock completion creates no files");
                session.Invite(true);Pump(600);session.Fail();Pump(40);Assert(app.Model.Requests.Count==0,"Failure clears mock request");
                session.Invite();Pump(450);session.Expire();Pump(400);Assert(app.Model.Requests.Count==0,"Expiry clears mock invitation");
                session.Invite();Pump(450);session.Reset();Pump(40);Assert(app.Model.Requests.Count==0 && !app.Windows.OfType<ReceiveWindow>().Any(w=>w.IsVisible),"Reset clears mock floating cards");
                Assert(!app.Engine.Running && app.Service.Last is null,"No AirDrop engine or service connection was started");
                Assert(Registration()==registry && Hash(path)==before,"Production protocol, startup entries and settings remain unchanged");
            }catch(Exception e){failure=e;Console.WriteLine(e);}
            finally{app.MainWindow?.Close();}
        }),DispatcherPriority.ApplicationIdle);
        app.Run();
        return failure is null?0:1;
    }
}
