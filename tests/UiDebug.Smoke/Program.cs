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
using AetherDrop;
static partial class Check
{
    [System.Runtime.InteropServices.DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr hwnd,int attribute,out int value,int size);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern int GetWindowRgn(IntPtr hwnd,IntPtr region);
    [System.Runtime.InteropServices.DllImport("gdi32.dll")] static extern IntPtr CreateRectRgn(int l,int t,int r,int b);
    [System.Runtime.InteropServices.DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr region);
    static void SmoothCorners(Window window) {
        if(window.AllowsTransparency && window.Content?.GetType().Name=="WindowSurface") {
            var shape=Nodes<Border>(window).Single(b=>b.Name=="WindowShape");
            var clip=(RectangleGeometry)Nodes<Decorator>(window).Single(b=>b.Name=="WindowClip").Clip;
            Assert(shape.CornerRadius.TopLeft==12 && clip.RadiusX==11,"Main/settings use a 12-DIP antialiased silhouette with matching content clipping");
            Assert(System.Windows.Shell.WindowChrome.GetWindowChrome(window) is null,"Large rounded windows do not use a jagged integer window region");
            return;
        }
        if(Environment.OSVersion.Version.Build<22000)return;
        var hwnd=new System.Windows.Interop.WindowInteropHelper(window).Handle;
        var region=CreateRectRgn(0,0,0,0);
        try {Assert(DwmGetWindowAttribute(hwnd,33,out int preference,sizeof(int))==0 && preference==2 && GetWindowRgn(hwnd,region)<=0,window.GetType().Name+" uses native smooth corners without a pixel clipping region");}
        finally{DeleteObject(region);}
    }
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hwnd,int message,IntPtr wp,IntPtr lp);
    static void HeaderButtons(Window window,double caption) {
        foreach(var button in Nodes<Button>(window).Where(b=>b.IsVisible && b.TranslatePoint(new Point(),window).Y<caption)) {
            foreach(var point in new[]{new Point(1,1),new Point(button.ActualWidth-1,1),new Point(1,button.ActualHeight-1),new Point(button.ActualWidth-1,button.ActualHeight-1),new Point(button.ActualWidth/2,button.ActualHeight/2)}) {
                var screen=button.PointToScreen(point);var local=window.PointFromScreen(screen);
                var hit=window.InputHitTest(local) as DependencyObject;
                while(hit is not null && hit is not Button) hit=hit is Visual?VisualTreeHelper.GetParent(hit):LogicalTreeHelper.GetParent(hit);
                Assert(ReferenceEquals(hit,button),"Every corner and center of a title button receives its click");
                int packed=((int)screen.X & 0xffff)|(((int)screen.Y & 0xffff)<<16);
                Assert(SendMessage(new System.Windows.Interop.WindowInteropHelper(window).Handle,0x84,IntPtr.Zero,new IntPtr(packed)).ToInt32()==1,"Title-button padding is not stolen by native dragging or resizing");
            }
            Assert(Nodes<Border>(button).Length==0,"Title-button hover has no square background or outline");
        }
    }
    static void Centered(Window window) {
        var source=(System.Windows.Interop.HwndSource)PresentationSource.FromVisual(window);
        var work=System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Cursor.Position).WorkingArea;
        var matrix=source.CompositionTarget.TransformFromDevice;
        var area=new Rect(matrix.Transform(new Point(work.Left,work.Top)),matrix.Transform(new Point(work.Right,work.Bottom)));
        Assert(Math.Abs(window.Left+window.Width/2-area.Left-area.Width/2)<1 && Math.Abs(window.Top+window.Height/2-area.Top-area.Height/2)<1,"Main window opens centered in the monitor work area");
    }
    static void Assert(bool value,string message) { if(!value)throw new Exception(message); Console.WriteLine("PASS "+message); }
    static void Pump(int ms) { var f=new DispatcherFrame();var t=new DispatcherTimer(DispatcherPriority.Send){Interval=TimeSpan.FromMilliseconds(ms)};t.Tick+=(_,_)=>{t.Stop();f.Continue=false;};t.Start();Dispatcher.PushFrame(f); }
    static string Hash(string path)=>File.Exists(path)?Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))):"missing";
    static string Registration(){using var protocol=Registry.CurrentUser.OpenSubKey(@"Software\Classes\AetherDrop\shell\open\command");using var run=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");return $"{protocol?.GetValue("")}|{run?.GetValue("AetherDrop")}|{run?.GetValue("AetherDrop")}";}
    static T[] Nodes<T>(DependencyObject p)where T:DependencyObject {var l=new System.Collections.Generic.List<T>();for(int i=0;i<VisualTreeHelper.GetChildrenCount(p);i++){var c=VisualTreeHelper.GetChild(p,i);if(c is T x)l.Add(x);l.AddRange(Nodes<T>(c));}return l.ToArray();}
    static void Capture(Window window,string name) {
        string? dir=Environment.GetEnvironmentVariable("AETHERDROP_SETTINGS_CAPTURE_DIR");if(string.IsNullOrEmpty(dir))return;
        Directory.CreateDirectory(dir);window.UpdateLayout();
        var image=new System.Windows.Media.Imaging.RenderTargetBitmap((int)window.Width,(int)window.Height,96,96,PixelFormats.Pbgra32);var backing=new DrawingVisual();using(var dc=backing.RenderOpen())dc.DrawRectangle(window.AllowsTransparency?Brushes.Transparent:(Brush)window.FindResource("MicaSolid"),null,new Rect(0,0,window.Width,window.Height));image.Render(backing);image.Render((Visual)window.Content);
        var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image));
        using var stream=File.Create(Path.Combine(dir,name+".png"));encoder.Save(stream);
    }
    static void CaptureElement(FrameworkElement element,string name) {
        string? dir=Environment.GetEnvironmentVariable("AETHERDROP_SETTINGS_CAPTURE_DIR");if(string.IsNullOrEmpty(dir))return;
        Directory.CreateDirectory(dir);element.UpdateLayout();
        var captureSize=element.DesiredSize;var bmp=new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(captureSize.Width),(int)Math.Ceiling(captureSize.Height),96,96,PixelFormats.Pbgra32);var visual=new DrawingVisual();using(var dc=visual.RenderOpen()) {var brush=new VisualBrush(element){ViewboxUnits=BrushMappingMode.Absolute,Viewbox=new Rect(0,0,captureSize.Width,captureSize.Height)};dc.DrawRectangle(brush,null,new Rect(0,0,captureSize.Width,captureSize.Height));}bmp.Render(visual);
        var png=new System.Windows.Media.Imaging.PngBitmapEncoder();png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bmp));using var stream=File.Create(Path.Combine(dir,name+".png"));png.Save(stream);
    }
    [STAThread] static int Main(string[] args)
    {
        Console.SetOut(new StreamWriter(Console.OpenStandardOutput()){AutoFlush=true});
        string path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AetherDrop","settings.json");
        string before=Hash(path), registry=Registration();
        var app=new App(uiDebugMode:true){ShutdownMode=ShutdownMode.OnMainWindowClose};
        Exception? failure=null;
        app.Startup+=(_,_)=>app.Dispatcher.BeginInvoke(new Action(()=>{
            try{
                Assert(app.MainWindow is UiDebugWindow && app.MainWindow.IsVisible,"UI debug control panel starts through actual startup path");
                if(args.Contains("--colors-only")) { VerifyPalette(); return; }
                if(args.Contains("--appearance-only")) { VerifyAppearance(app); Assert(Registration()==registry && Hash(path)==before,"Appearance checks leave production preferences unchanged"); return; }
                foreach(var key in new[]{"pill.ready","view.radar","view.list","radar.thisPc","main.dropHint","aid.signInTitle","set.tab.appleid"}) Assert(L.T(key)!=key,"Upstream translation resolves: "+key);
                Assert(app.UiDebugMode && app.Settings.IsTransient,"Debug settings are memory-only");
                app.SetDiscoverable(false);app.SetDiscoverable(true);app.Settings.Save();
                Assert(Hash(path)==before,"Receive switch and Save do not modify production settings");
                var settings=new SettingsWindow();settings.Show();Pump(100);
                var saveButton=(Button)settings.FindName("SaveButton");
                var nameBox=(TextBox)settings.FindName("NameBox");
                string originalName=nameBox.Text;
                Assert(!saveButton.IsEnabled,"Save starts disabled when settings have no edits");
                nameBox.Text=originalName+" test";Pump(30);
                Assert(saveButton.IsEnabled,"Editing a setting enables Save");
                nameBox.Text=originalName;Pump(30);
                Assert(!saveButton.IsEnabled,"Reverting an edit disables Save again");
                foreach(var key in new[]{"AskBox","StartupBox","HotkeyBox","AppleIdOnBox","FastUnlockBox","CoexPlanBox","CheckUpdatesBox","PreviewUpdatesBox","DeleteInstallerBox"}) {
                    var settingToggle=(CheckBox)settings.FindName(key);var original=settingToggle.IsChecked;
                    settingToggle.IsChecked=original!=true;Pump(20);Assert(saveButton.IsEnabled,"Changing "+key+" enables Save");
                    settingToggle.IsChecked=original;Pump(20);Assert(!saveButton.IsEnabled,"Reverting "+key+" clears the pending edit");
                }
                foreach(var key in new[]{"HubAuto","HubDesktop","HubLaptop"}) {
                    var segment=(RadioButton)settings.FindName(key);segment.ApplyTemplate();
                    Assert(segment.Template.FindName("SelectionMark",segment) is null,"Compact icon choices use highlight without a check");
                }
                Assert(settings.FindName("AccentPanel") is null,"Theme-colour picker has been removed");
                var appearanceAuto=(RadioButton)settings.FindName("AppearanceAuto");
                var appearanceDark=(RadioButton)settings.FindName("AppearanceDark");
                Assert(appearanceAuto.IsChecked==true,"Appearance defaults to following Windows");
                appearanceDark.IsChecked=true;Pump(30);Assert(saveButton.IsEnabled,"Changing appearance enables Save");
                appearanceAuto.IsChecked=true;Pump(30);Assert(!saveButton.IsEnabled,"Reverting appearance clears the pending edit");
                Assert(((Border)settings.FindName("TitleBar")).BorderThickness==new Thickness(0),"Settings title bar has no dividing line");
                nameBox.Text=originalName+" test";Pump(30);saveButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump(40);
                var notice=(Grid)settings.FindName("SaveNotice");
                var noticeSurface=(ContinuousSurface)settings.FindName("SaveNoticeSurface");
                var surfaceScale=(ScaleTransform)settings.FindName("SaveSurfaceScale");
                var noticeLabel=(TextBlock)settings.FindName("SaveNoticeLabel");
                var noticeCheck=(System.Windows.Shapes.Path)settings.FindName("SaveCheck");
                Capture(settings,"save-motion-0040");
                Assert(noticeSurface.Opacity==1 && notice.Opacity==1 && noticeLabel.RenderTransform is TranslateTransform,"Only the opaque vector background scales; text has a separate translation track");
                if(!Motion.Reduced) Assert(surfaceScale.ScaleX>0 && surfaceScale.ScaleX<1,"Save surface unfolds through intermediate vector geometry");
                Pump(120);Capture(settings,"save-motion-0160");
                Pump(260);Capture(settings,"save-motion-0420");
                Assert(surfaceScale.ScaleX==1 && surfaceScale.ScaleY==1 && noticeCheck.StrokeDashOffset==0 && noticeLabel.Opacity==1,"Save animation lands exactly on a crisp complete result");
                Assert(notice.IsVisible && notice.ActualWidth==notice.ActualHeight && notice.Opacity==1 && ((SolidColorBrush)noticeSurface.Background).Color.A==255,"Save feedback is a fully opaque square with continuous vector corners");
                Assert(!saveButton.IsEnabled,"Successful save establishes a clean baseline");Capture(settings,"settings-save-square");
                Pump(900);Capture(settings,"save-motion-1320-hold");
                Assert(notice.IsVisible,"Completed success result remains readable before leaving");
                Pump(170);Capture(settings,"save-motion-1490-exit");
                if(!Motion.Reduced) Assert(surfaceScale.ScaleX>0 && surfaceScale.ScaleX<1 && noticeSurface.Opacity==1,"Exit contracts the opaque surface instead of cutting it off");
                nameBox.Text=originalName;Pump(30);saveButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump(400);
                Assert(notice.IsVisible && surfaceScale.ScaleX==1,"A save during exit reuses and reopens the current surface");
                Capture(settings,"save-motion-restarted");Pump(1350);
                Assert(!notice.IsVisible && !saveButton.IsEnabled,"Repeated save restarts the notice timer and returns to a clean state");
                SmoothCorners(settings);
                HeaderButtons(settings,UiLayout.TitleBarHeight);
                var resizePoint=settings.PointToScreen(new Point(settings.ActualWidth-10,settings.ActualHeight-10));
                int resizePacked=((int)resizePoint.X & 0xffff)|(((int)resizePoint.Y & 0xffff)<<16);
                Assert(SendMessage(new System.Windows.Interop.WindowInteropHelper(settings).Handle,0x84,IntPtr.Zero,new IntPtr(resizePacked)).ToInt32()==17,"Settings retains a usable diagonal resize zone along its larger corner");
                settings.Width+=20;settings.Height+=20;Pump(50);SmoothCorners(settings);
                settings.Width-=20;settings.Height-=20;Pump(50);SmoothCorners(settings);
                var settingsWork=FloatingCard.WorkArea(settings,System.Windows.Forms.Cursor.Position);
                settings.CenterOnDesktop();Pump(30);
                Assert(Math.Abs(settings.Left+(settings.Width/2)-(settingsWork.Left+settingsWork.Width/2))<=1 && Math.Abs(settings.Top+(settings.Height/2)-(settingsWork.Top+settingsWork.Height/2))<=1,"Settings opens centered within the current desktop work area");
                Assert(settings.FindName("TabAppleId") is RadioButton && settings.FindName("AppleIdOnBox") is CheckBox,"Upstream Apple ID settings retained");
                foreach (var tab in new[]{"TabGeneral","TabNetwork","TabAppleId","TabHardware","TabUpdates"}) {
                    ((RadioButton)settings.FindName(tab)).IsChecked=true;Pump(220);Capture(settings,tab);
                    if(tab=="TabNetwork") {
                        var concurrent=(RadioButton)settings.FindName("ModeConcurrent");var demand=(RadioButton)settings.FindName("ModeOnDemand");
                        Assert(Nodes<System.Windows.Shapes.Ellipse>(concurrent).Length==0,"Settings mode cards have no leading radio circles");
                        Assert(((FrameworkElement)concurrent.Template.FindName("SelectionMark",concurrent)).Opacity==1,"Existing selected mode is highlighted the first time a tab opens");
                        var modeCheck=(FrameworkElement)demand.Template.FindName("SelectionMark",demand);
                        demand.IsChecked=true;Pump(45);Capture(settings,"mode-changing-045");
                        if(!Motion.Reduced) Assert(modeCheck.Opacity>0 && modeCheck.Opacity<1,"Mode selection marker transitions through intermediate opacity");
                        concurrent.IsChecked=true;Pump(200);Capture(settings,"mode-settled");
                        Assert(modeCheck.Opacity==0 && ((FrameworkElement)concurrent.Template.FindName("SelectionMark",concurrent)).Opacity==1,"Rapid selection reversal settles on the latest mode");
                    }
                }
                Capture(settings,"settings-2.1.3");
                ((RadioButton)settings.FindName("TabGeneral")).IsChecked=true;Pump(220);
                var language=(ComboBox)settings.FindName("LanguageBox");language.IsDropDownOpen=true;Pump(180);
                var selectedLanguage=(ComboBoxItem)language.ItemContainerGenerator.ContainerFromIndex(language.SelectedIndex);
                Assert(((FrameworkElement)selectedLanguage.Template.FindName("SelectionMark",selectedLanguage)).Opacity==1,"Dropdown selection uses the same trailing check");
                language.IsDropDownOpen=false;
                var identity=new AppleIdWindow {Owner=settings};identity.Show();Pump(220);Capture(identity,"apple-id");
                Assert(identity.FindName("EmailBox") is TextBox && identity.FindName("PasswordBox") is PasswordBox,"Apple ID sign-in and verification UI loads without signing in");identity.Close();settings.Close();
                app.Model.Air="ready";app.Model.Mode="concurrent";
                app.Settings.DeviceView="list";
                var main=new MainWindow();main.Show();Pump(90);Capture(main,"main-empty");
                Assert(main.ShowInTaskbar && main.Icon==Branding.WindowIcon && main.Owner is null,"Main window has a taskbar entry with the AetherDrop icon");
                Centered(main);
                SmoothCorners(main);HeaderButtons(main,52);
                var headerPoint=main.PointToScreen(new Point(220,26));
                var packed=((int)headerPoint.X & 0xffff) | (((int)headerPoint.Y & 0xffff)<<16);
                Assert(SendMessage(new System.Windows.Interop.WindowInteropHelper(main).Handle,0x84,IntPtr.Zero,new IntPtr(packed)).ToInt32()==2,"Empty title-bar space is a native drag area");
                Assert(((Grid)main.FindName("Root")).RowDefinitions[0].Height.Value==52,"Title bar adds vertical breathing room without scaling text");
                app.Model.Air="service_stopped";Pump(60);Capture(main,"main-header-stopped");
                var headerStatus=(TextBlock)main.FindName("StatusText");
                var statusInk=new FormattedText(headerStatus.Text,System.Globalization.CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface(headerStatus.FontFamily,headerStatus.FontStyle,headerStatus.FontWeight,headerStatus.FontStretch),headerStatus.FontSize,Brushes.Black,null,TextFormattingMode.Display,VisualTreeHelper.GetDpi(headerStatus).PixelsPerDip);
                Assert(headerStatus.Text==L.T("pill.noService") && statusInk.Width<=headerStatus.ActualWidth,"Stopped-service status fits fully in its title-bar column");
                var mark=(Image)main.FindName("Mark");
                Assert(RenderOptions.GetBitmapScalingMode(mark)==BitmapScalingMode.HighQuality && !mark.SnapsToDevicePixels,"Brand logo uses high-quality downsampling without pixel-snapped bitmap edges");
                var statusPill=(System.Windows.Controls.Primitives.ToggleButton)main.FindName("StatusPill");
                Assert(mark.Source==Branding.TrayPanelLogo && mark.ActualWidth==24 && main.FindName("ReceivePopup") is null,"Brand icon stays intact and the separate receiving popup is removed");
                statusPill.IsChecked=true;Pump(220);
                var receiveList=(StackPanel)main.FindName("ReceiveList");
                var choices=receiveList.Children.Cast<Button>().ToArray();
                var choiceLeft=choices.Select(b=>Nodes<ContentPresenter>(b).First().TranslatePoint(new Point(),receiveList).X).ToArray();
                Assert(choiceLeft.Length>=1 && choiceLeft.Max()-choiceLeft.Min()<.5,"All receive choices share the same left edge regardless of text length");
                Assert(choices.All(b=>Nodes<System.Windows.Shapes.Ellipse>(b).Length==0),"Receive choices contain no leading radio circles");
                foreach(var choice in choices) {
                    var check=(FrameworkElement)choice.Template.FindName("SelectionMark",choice);
                    var selected=(Border)choice.Template.FindName("SelectionWash",choice);
                    Assert(check.Opacity==(UiMotion.GetSelected(choice)?1:0) && selected.Opacity==check.Opacity,"Selection uses a highlighted row and matching trailing check");
                    Assert(check.TranslatePoint(new Point(),choice).X>choice.ActualWidth/2,"Selection check occupies the right-hand column");
                }
                var receiveSurface=(FrameworkElement)((System.Windows.Controls.Primitives.Popup)main.FindName("StatusPopup")).Child;
                CaptureElement(receiveSurface,"receive-options");
                var hoveredChoice=choices[0];var hover=(Border)hoveredChoice.Template.FindName("HoverWash",hoveredChoice);
                hoveredChoice.RaiseEvent(new System.Windows.Input.MouseEventArgs(System.Windows.Input.Mouse.PrimaryDevice,0){RoutedEvent=UIElement.MouseEnterEvent});Pump(45);CaptureElement(receiveSurface,"receive-hover-045");
                if(!Motion.Reduced) Assert(hover.Opacity>0 && hover.Opacity<1,"Choice hover feedback eases through an intermediate state");
                hoveredChoice.RaiseEvent(new System.Windows.Input.MouseEventArgs(System.Windows.Input.Mouse.PrimaryDevice,0){RoutedEvent=UIElement.MouseLeaveEvent});Pump(170);
                Assert(hover.Opacity==0,"Choice hover feedback clears without leaving a stuck highlight");
                var allow=(CheckBox)main.FindName("AllowToggle");
                Assert(receiveList.TranslatePoint(new Point(),receiveSurface).Y>allow.TranslatePoint(new Point(0,allow.ActualHeight),receiveSurface).Y,"Receiving choices are directly below Allow receiving in the unified popup");
                Assert(receiveList.Children.Cast<Button>().Select(b=>(string?)b.Tag).SequenceEqual(new[]{"everyone","contacts"}) && !receiveList.Children.Cast<Button>().Last().IsEnabled,"Everyone is first; Contacts Only stays visible and disabled without Apple ID");
                Assert(main.FindName("ReceiveSettingsLink") is null,"Quick controls have no Apple ID settings link");
                foreach(var policy in new[]{(false,"contacts",true,true,"off"),(true,"contacts",false,true,"everyone"),(true,"contacts",true,false,"everyone"),(true,"contacts",true,true,"contacts"),(true,"everyone",true,true,"everyone")})
                    Assert(AppleId.ResolveReceiving(policy.Item1,policy.Item2,policy.Item3,policy.Item4)==policy.Item5,"Effective receiving scope requires both the Apple ID feature and identity");
                string savedScope=app.Settings.Receive;
                allow.IsChecked=false;allow.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump(200);
                Assert(!app.Model.Allow && app.Settings.Receive==savedScope && receiveList.Children.Cast<Button>().All(b=>(string?)b.Tag!="off"),"Only the top switch turns receiving off; the selected range is remembered");
                foreach(var healthyState in new[]{"ready","starting","standby"}) {
                    app.Model.Air=healthyState;Pump(30);
                    Assert(((TextBlock)main.FindName("StatusText")).Text==L.T("recv.off") && MainWindow.Status(healthyState,false).Dot=="Text3","Receiving off overrides a healthy radio status: "+healthyState);
                }
                foreach(var faultState in new[]{"error","no_adapter","service_stopped","off"})
                    Assert(MainWindow.Status(faultState,false)==MainWindow.Status(faultState,true),"Radio faults and explicit pause remain visible with receiving off: "+faultState);
                app.Model.Air="ready";

                app.SetReceive("everyone");Pump(200);
                Assert(!app.Model.Allow && allow.IsChecked==false && UiMotion.GetSelected(receiveList.Children.Cast<Button>().Single(b=>(string?)b.Tag=="everyone")),"Editing the range while receiving is off does not turn it on");
                allow.IsChecked=true;allow.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump(200);
                Assert(app.Model.Allow && app.Settings.Receive=="everyone","Turning receiving back on restores the saved range");
                Assert(((TextBlock)main.FindName("StatusText")).Text==L.T("pill.ready"),"Re-enabling receiving immediately restores the ready label");
                app.Settings.AppleIdOn=true;app.NotifyReceivingChanged();Pump(220);
                Assert(receiveList.Children.Cast<Button>().Any(b=>(string?)b.Tag=="contacts"),"Saved Apple ID feature changes refresh the unified popup");
                CaptureElement(receiveSurface,"quick-controls-apple-enabled");
                double fullHeight=main.Height;main.Height=450;main.UpdateLayout();
                statusPill.IsChecked=false;statusPill.IsChecked=true;Pump(220);
                var compactSurface=(FrameworkElement)main.FindName("StatusPopupSurface");
                Assert(compactSurface.PointToScreen(new Point(0,compactSurface.ActualHeight+compactSurface.Margin.Bottom)).Y<=main.PointToScreen(new Point(0,main.ActualHeight)).Y+1.5,$"Expanded receiving controls stay vertically inside a compact main window (popupBottom={compactSurface.PointToScreen(new Point(0,compactSurface.ActualHeight+compactSurface.Margin.Bottom)).Y}, windowBottom={main.PointToScreen(new Point(0,main.ActualHeight)).Y}, height={compactSurface.ActualHeight}, max={compactSurface.MaxHeight})");
                Assert(((ScrollViewer)main.FindName("QuickControlsScroll")).ScrollableHeight>0,"Long quick controls scroll instead of overflowing the main window");
                main.Height=fullHeight;main.UpdateLayout();statusPill.IsChecked=false;statusPill.IsChecked=true;Pump(220);
                if(!AppleId.Present) {
                    string previous=app.Settings.Receive;
                    Assert(!receiveList.Children.Cast<Button>().Single(b=>(string?)b.Tag=="contacts").IsEnabled,"Contacts is disabled without a signed-in identity");
                    Assert(app.Settings.Receive==previous,"Unavailable Contacts leaves the saved scope unchanged");
                }
                else { app.SetReceive("contacts");Assert(AppleId.Receiving=="contacts","Ready identity enables Contacts Only");app.SetReceive("everyone"); }
                app.ShowSettings("appleid");Pump(220);
                var linkedSettings=app.Windows.OfType<SettingsWindow>().Single(w=>w.IsVisible);
                var mirrorSettings=new SettingsWindow("appleid");mirrorSettings.Show();Pump(220);
                typeof(Application).GetMethod("OnDeactivated",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(app,new object[]{EventArgs.Empty});Pump(700);
                Assert(main.IsVisible,"Main window stays open when another window or desktop takes focus");
                ((CheckBox)linkedSettings.FindName("AppleIdOnBox")).IsChecked=false;
                Nodes<Button>(linkedSettings).Single(b=>b.IsDefault).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump(220);
                Assert(!app.Settings.AppleIdOn && app.Settings.Receive=="everyone","Saving Apple ID off resets the effective scope to Everyone");
                Assert(((FrameworkElement)linkedSettings.FindName("SaveNotice")).Visibility==Visibility.Visible,"Saving immediately shows the success rectangle");
                Capture(linkedSettings,"settings-save-success");
                Assert(((CheckBox)mirrorSettings.FindName("AppleIdOnBox")).IsChecked==false && ((RadioButton)mirrorSettings.FindName("ReceiveEveryone")).IsChecked==true,"Other open settings windows follow the saved Apple ID and receiving state");
                mirrorSettings.Close();linkedSettings.Close();
                statusPill.IsChecked=true;Pump(220);
                Assert(!receiveList.Children.Cast<Button>().Single(b=>(string?)b.Tag=="contacts").IsEnabled,"Contacts remains visible and disabled after Apple ID is turned off");
                CaptureElement(receiveSurface,"quick-controls-apple-disabled");
                ((System.Windows.Controls.Primitives.Popup)main.FindName("StatusPopup")).IsOpen=false;
                app.Model.Air="ready";main.Show();main.Activate();main.CenterOnDesktop();Pump(30);
                var radar=Nodes<RadarView>(main).Single();
                Assert(radar.FindName("RadarChoice") is null && radar.FindName("ListChoice") is null && main.FindName("WifiButton") is null,"View switches and title-bar Wi-Fi entry are removed");
                Assert(statusPill.Parent==main.FindName("NearbyPanel") && statusPill.TranslatePoint(new Point(),main).Y>mark.TranslatePoint(new Point(0,mark.ActualHeight),main).Y,"Receiving status is beside the nearby-devices heading below the title bar");
                statusPill.IsChecked=false;statusPill.Focus();statusPill.IsChecked=true;Pump(200);
                Assert(((Border)statusPill.Template.FindName("b",statusPill)).BorderThickness==new Thickness(0),"Ready-status control has no border after clicking/focusing");
                Assert(((System.Windows.Controls.Primitives.Popup)main.FindName("StatusPopup")).IsOpen,"Relocated receiving status retains its quick-controls popup");
                var statusCard=(FrameworkElement)main.FindName("StatusPopupCard");
                var cardRight=statusCard.PointToScreen(new Point(statusCard.ActualWidth,0)).X;
                var buttonRight=statusPill.PointToScreen(new Point(statusPill.ActualWidth,0)).X;
                Assert(Math.Abs(cardRight-buttonRight)<=1.5,$"Quick-controls card right edge aligns with its trigger button (card={cardRight}, button={buttonRight})");
                Assert(Math.Abs(statusCard.PointToScreen(new Point()).Y-statusPill.PointToScreen(new Point(0,statusPill.ActualHeight)).Y-6)<=1.5,"Unified quick controls retain a six-pixel gap below their trigger");
                var statusSurface=(FrameworkElement)main.FindName("StatusPopupSurface");
                var surfaceLeft=statusSurface.PointToScreen(new Point()).X-statusSurface.Margin.Left;
                var surfaceRight=statusSurface.PointToScreen(new Point(statusSurface.ActualWidth,0)).X+statusSurface.Margin.Right;
                Assert(surfaceLeft>=main.PointToScreen(new Point()).X-1.5 && surfaceRight<=main.PointToScreen(new Point(main.ActualWidth,0)).X+1.5,"Quick-controls popup and shadow gutter remain inside the main window");
                CaptureElement((FrameworkElement)((System.Windows.Controls.Primitives.Popup)main.FindName("StatusPopup")).Child,"quick-controls");
                ((System.Windows.Controls.Primitives.Popup)main.FindName("StatusPopup")).IsOpen=false;
                app.Model.Devices.Add(new Device {Id="ui-main-device",Name="iPhone",Model="iPhone15,2"});
                app.Model.Files.Add(@"C:\private-filename.jpg");app.Model.ReceivedFiles.Add(new Received {Name="private-received.jpg",Path="",From="iPhone"});Pump(70);
                Pump(50);
                Assert(!((FrameworkElement)radar.FindName("ListLayer")).IsVisible,"Previous list preference does not override fixed radar view");
                Assert(!Nodes<TextBlock>(main).Any(t=>t.Text.Contains("private-")),"Main window preserves hidden file names");
                Pump(50);Capture(main,"main-populated");
                Assert(((FrameworkElement)radar.FindName("RadarLayer")).IsVisible,"Radar device view works");
                Assert(main.FontFamily.Source.Contains("YaHei"),"New UI uses Microsoft YaHei");
                var gear=(Button)main.FindName("SettingsButton");
                Assert(!gear.Template.Triggers.OfType<Trigger>().Any(t=>t.Property==UIElement.IsKeyboardFocusedProperty),"Title buttons do not retain accent color from keyboard focus");
                gear.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump(200);
                var gearSettings=app.Windows.OfType<SettingsWindow>().Single(w=>w.IsVisible);gearSettings.Close();main.Activate();gear.Focus();Pump(100);
                Assert(gear.IsMouseOver || ((SolidColorBrush)gear.Foreground).Color==((SolidColorBrush)main.FindResource("Text2")).Color,"Settings gear returns to its neutral color after settings closes");
                if(args.Contains("--layout-only")) {
                    LayoutPass(app,main);
                    Assert(!app.Engine.Running && app.Service.Last is null,"Layout checks start no real engine or service connection");
                    Assert(Registration()==registry && Hash(path)==before,"Layout checks preserve production settings and shell entries");
                    return;
                }
                if(args.Contains("--settings-only")) {
                    ReceiveToggleResponsiveness(app);
                    Assert(!app.Engine.Running && app.Service.Last is null,"Focused settings checks start no real engine or service connection");
                    Assert(Registration()==registry && Hash(path)==before,"Focused settings checks leave production settings and shell entries unchanged");
                    return;
                }
                var send=new SendWindow();send.Show();Pump(220);Capture(send,"send-window");send.Close();
                var wifi=new WifiWindow();wifi.Show();
                typeof(WifiWindow).GetMethod("Render",BindingFlags.NonPublic|BindingFlags.Instance,null,new[]{typeof(System.Text.Json.Nodes.JsonObject)},null)!.Invoke(wifi,new object[]{System.Text.Json.Nodes.JsonNode.Parse("""{"mode":"concurrent","radio":"on","wifi":"connected","ssid":"测试网络","networks":[{"ssid":"测试网络","dbm":-48,"security":"wpa2-psk","saved":true,"connected":true},{"ssid":"访客网络","dbm":-65,"security":"open"}],"known":[{"ssid":"测试网络","auto":true}]}""")!});
                Pump(220);Capture(wifi,"wifi-window");
                Assert(Nodes<TextBlock>(wifi).Any(t=>t.Text=="\uE73E"),"Connected Wi-Fi row has a trailing check");wifi.Close();
                main.Close();app.Model.Files.Clear();app.Model.Devices.Clear();app.Model.ReceivedFiles.Clear();
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
                var reopenedMain=app.Windows.OfType<MainWindow>().Single();Centered(reopenedMain);
                reopenedMain.Left+=70;reopenedMain.Top+=35;
                reopenedMain.Shut();Pump(20);app.ShowWindow();Pump(300);
                Assert(reopenedMain.IsVisible,"Reopening during the close animation does not hide the main window again");Centered(reopenedMain);
                reopenedMain.Left+=70;reopenedMain.Top+=35;
                reopenedMain.Shut();Pump(180);app.ShowWindow();Pump(300);Centered(reopenedMain);
                click.Invoke(actualIcon,new object[]{clickArgs});
                Assert(app.Windows.OfType<TrayWindow>().Any(w=>w.IsVisible),"New single press immediately after double-click opens the popup without a cooldown");
                app.Windows.OfType<TrayWindow>().Single().Close();
                app.Windows.OfType<MainWindow>().Single().Close();actualIcon.Visible=false;actualIcon.Dispose();trayField.SetValue(app,null);
                var session=(UiDebugSession)typeof(UiDebugWindow).GetField("_session",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(app.MainWindow)!;
                session.ShowTray();
                foreach(var popup in app.Windows.OfType<TrayWindow>()) (typeof(TrayWindow).GetField("_overflowMonitor",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(popup) as DispatcherTimer)?.Stop();
                Pump(450);Assert(app.Windows.OfType<TrayWindow>().Any(w=>w.IsVisible),"Real tray card opens in debug mode");
                var tray=app.Windows.OfType<TrayWindow>().Single(w=>w.IsVisible);
                var setPointer=typeof(TrayWindow).GetMethod("SetPointer",BindingFlags.NonPublic|BindingFlags.Instance)!;
                foreach(double anchor in new[]{34.0,tray.Width/2,tray.Width-34}) {
                    setPointer.Invoke(tray,new object[]{anchor});
                    var outline=(System.Windows.Shapes.Path)typeof(TrayWindow).GetField("_outline",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(tray)!;
                    var junction=new Point(anchor,tray.Height-10.5);
                    Assert(outline.Data.FillContains(junction) && !outline.Data.StrokeContains(new Pen(Brushes.Black,1),junction),"Pointer joins the card fill without a border crossing its base");
                }
                setPointer.Invoke(tray,new object[]{tray.Width/2});
                // These checks model a normal desktop tray popup. A real tray
                // overflow panel can close independently while this test runs;
                // its lifetime is covered separately by the placement checks.
                (typeof(TrayWindow).GetField("_overflowMonitor",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(tray) as DispatcherTimer)?.Stop();
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
                app.SetDiscoverable(false);Pump(400);Capture(tray,"toggle-settled-off");
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
                app.InjectUiDebugEvent(new System.Text.Json.Nodes.JsonObject { ["event"]="negotiating", ["peer"]="::1" });
                var negotiationDeadline=(DateTime)typeof(App).GetField("_negotiationUntil",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(app)!;
                Assert(negotiationDeadline>DateTime.UtcNow && negotiationDeadline<=DateTime.UtcNow.AddSeconds(46),"Pre-invitation negotiation receives a bounded protection window");
                Assert(!app.Windows.OfType<ReceiveWindow>().Any(w=>w.IsVisible),"TLS negotiation does not create a fake receive invitation");
                app.InjectUiDebugEvent(new System.Text.Json.Nodes.JsonObject { ["event"]="ready" });
                Assert((DateTime)typeof(App).GetField("_negotiationUntil",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(app)! == DateTime.MinValue,"Engine restart clears stale negotiation protection");
                session.Invite();Pump(500);var invitation=app.Windows.OfType<ReceiveWindow>().Single(w=>w.IsVisible);
                Nodes<Button>(invitation).Single(b=>b.Content?.ToString()=="接受").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump(800);
                Assert(app.Model.Requests.Single().Progress>0,"Accepting mock invitation drives actual progress view");
                // Allow two 0.8-second samples: the first can include bytes
                // that arrived immediately before Pause.
                session.TogglePause();double percent=app.Model.Requests.Single().Progress;Pump(1700);
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
            finally{app.Shutdown();}
        }),DispatcherPriority.ApplicationIdle);
        app.Run();
        return failure is null?0:1;
    }
}
