// SPDX-License-Identifier: GPL-2.0-only
// Offline STA smoke checks: temporary windows and synthetic engine events, no radio or settings writes.
using System;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using AetherDrop;
static class Check {
    static App app = null!;
    static double? deviceTemplateY;
    static MethodInfo engine = typeof(App).GetMethod("OnEngine", BindingFlags.NonPublic|BindingFlags.Instance)!;
    static void Event(string json) => engine.Invoke(app, new object[]{JsonNode.Parse(json)!});
    static void Pump(int ms) { var frame=new DispatcherFrame(); var timer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(ms)}; timer.Tick+=(_,_)=>{timer.Stop();frame.Continue=false;}; timer.Start(); Dispatcher.PushFrame(frame); }
    static void Assert(bool ok,string message) { if(!ok)throw new Exception(message); Console.WriteLine("PASS "+message); }
    static T[] Nodes<T>(DependencyObject parent) where T:DependencyObject { var list=new System.Collections.Generic.List<T>(); for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++){var c=VisualTreeHelper.GetChild(parent,i);if(c is T t)list.Add(t);list.AddRange(Nodes<T>(c));} return list.ToArray(); }
    static ReceiveWindow Active()=>app.Windows.OfType<ReceiveWindow>().First(x=>x.IsVisible);
    static void MotionFrame(Window window, string name, double destination) {
        var dir=Environment.GetEnvironmentVariable("AETHERDROP_MOTION_CAPTURE_DIR");
        if(string.IsNullOrEmpty(dir))return;
        System.IO.Directory.CreateDirectory(dir);
        var card=new System.Windows.Media.Imaging.RenderTargetBitmap((int)window.Width,(int)window.Height,96,96,PixelFormats.Pbgra32);
        card.Render(window);
        var visual=new DrawingVisual();
        using(var dc=visual.RenderOpen()) {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(233,237,242)),null,new System.Windows.Rect(0,0,660,260));
            dc.DrawImage(card,new System.Windows.Rect(window.Left-destination+12,22,window.Width,window.Height));
            dc.DrawText(new FormattedText(name,System.Globalization.CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Microsoft YaHei"),11,Brushes.Black,1),new Point(12,3));
        }
        var frame=new System.Windows.Media.Imaging.RenderTargetBitmap(660,260,96,96,PixelFormats.Pbgra32); frame.Render(visual);
        var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(frame));
        using(var file=System.IO.File.Create(System.IO.Path.Combine(dir,name+".png")))encoder.Save(file);
    }
    static double FooterBottom(FrameworkElement footer, Window card) {
        // Check the fixed template slot rather than font-specific ink bounds.
        var parent=(UIElement)VisualTreeHelper.GetParent(footer);
        var slot=System.Windows.Controls.Primitives.LayoutInformation.GetLayoutSlot(footer);
        return parent.TranslatePoint(new Point(0,slot.Bottom),card).Y;
    }
    static void Template(ReceiveWindow card, FrameworkElement footer) {
        var heading=Nodes<TextBlock>(card).Single(t=>t.IsVisible && (t.Text.StartsWith("正在接收") || t.Text is "收到隔空投送邀请" or "接收已停止" or "接收完成"));
        double top=heading.TranslatePoint(new Point(),card).Y;
        double bottom=FooterBottom(footer,card);
        Assert(Math.Abs(top-(card.Height-bottom))<=1,"Title and footer have equal distances from the symmetric card borders");
        var sender=Nodes<TextBlock>(card).SingleOrDefault(t=>t.IsVisible && t.Text is "iPhone" or "已保存到本地");
        if(sender!=null) {
            double center=sender.TranslatePoint(new Point(0,sender.ActualHeight/2),card).Y;
            deviceTemplateY ??= center;
            Assert(Math.Abs(center-deviceTemplateY.Value)<=0.5,"Device name keeps the identical fixed position in every card state");
        }
    }
    static void Ask(string id)=>Event("{\"event\":\"asked\",\"id\":\""+id+"\",\"sender\":\"iPhone\",\"model\":\"iPhone15,2\",\"waits\":true,\"files\":[{\"name\":\"IMG_0074.JPG\",\"type\":\"image/jpeg\"}]}");
    [STAThread] static void Main(){
        app=new App {ShutdownMode=ShutdownMode.OnExplicitShutdown}; L.Use("zh"); app.Resources.MergedDictionaries.Add(Theme.Build()); app.Settings.AlwaysAccept.Clear();
        var popupTop=typeof(TrayWindow).GetMethod("PopupTop",BindingFlags.NonPublic|BindingFlags.Static)!;
        double Place(System.Windows.Rect area,double? overflow)=>(double)popupTop.Invoke(null,new object?[]{area,208.0,overflow})!;
        Assert(Place(new System.Windows.Rect(0,0,1920,1040),null)==828,"Regular tray popup stays above the taskbar");
        Assert(Place(new System.Windows.Rect(0,0,1920,1040),880)==668,"Overflow tray popup clears the whole expanded tray panel");
        Assert(Place(new System.Windows.Rect(-1920,-400,1920,1040),-300)==-400,"Overflow placement respects monitor work-area top on offset displays");
        var tray=new TrayWindow(app); tray.OpenAt(new System.Drawing.Point(100, 100)); Pump(25);
        var traySurface=(Grid)tray.Content;
        double trayLeft=tray.Left, trayTop=tray.Top;
        MotionFrame(tray,"tray-open-025",tray.Left);
        if(FloatingCard.Motion) Assert(traySurface.RenderTransform is ScaleTransform entry && entry.ScaleX>=.78 && entry.ScaleX<1,"Tray enters from its anchored smaller state");
        Pump(100); MotionFrame(tray,"tray-open-125",tray.Left);
        Pump(120); MotionFrame(tray,"tray-open-245",tray.Left);
        Pump(170); MotionFrame(tray,"tray-open-settled",tray.Left);
        Assert(tray.Left==trayLeft && tray.Top==trayTop && traySurface.RenderTransform.Value.IsIdentity && traySurface.Opacity==1,"Tray holds its screen anchor and restores crisp native-size content after opening");
        var monitor=typeof(TrayWindow).GetField("_desktop",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(tray)!;
        var hook=(IntPtr)monitor.GetType().GetField("_hook",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(monitor)!;
        Assert(hook!=IntPtr.Zero,"Desktop-only click listener installs successfully");
        Assert(tray.Width==244 && tray.Height==208,"Tray uses explicit final dimensions (+15% from compact size)");
        Assert(Nodes<System.Windows.Controls.Viewbox>(tray).Length==0,"Tray has no whole-card scaling container");
        Pump(300);
        var receiveSwitch=Nodes<CheckBox>(tray).Single();
        var receiveKnob=(Border)receiveSwitch.Template.FindName("Knob",receiveSwitch);
        Assert(Nodes<System.Windows.FrameworkElement>(tray).All(e=>e.LayoutTransform.Value.IsIdentity && (e.RenderTransform.Value.IsIdentity || ReferenceEquals(e,receiveKnob) && e.RenderTransform is TranslateTransform { X: 19, Y: 0 })),"Tray resting transforms are identity after entry except the receive switch's intentional on-position");
        foreach(var text in Nodes<TextBlock>(tray)) {
            for(DependencyObject? node=text;node!=null;node=VisualTreeHelper.GetParent(node))
                if(node is UIElement element) Assert(element.Effect is null,"Tray text has no rasterizing ancestor effect");
        }
        var machine=Nodes<TextBlock>(tray).Single(t=>t.Text==Environment.MachineName);
        var caption=Nodes<TextBlock>(tray).Single(t=>t.Text=="本机名称");
        foreach(var text in new[]{machine,caption}) {
            var ink=VisualTreeHelper.GetDescendantBounds(text);
            Assert(!ink.IsEmpty && ink.Top>=-0.5 && ink.Bottom<=text.ActualHeight+0.5,"Machine-name glyph bounds fit inside their text layout without vertical clipping");
            Assert(text.Padding.Top>=2 && text.Padding.Bottom>=2,"Machine-name text has explicit ascender/descender inset");
        }
        var status=Nodes<TextBlock>(tray).Single(t=>t.Text is "接收已开启" or "接收已关闭");
        DependencyObject footerParent=status;
        while(footerParent is not Border) footerParent=VisualTreeHelper.GetParent(footerParent);
        var footer=(Border)footerParent;
        var inkBounds=VisualTreeHelper.GetDescendantBounds(status);
        double inkCenter=status.TranslatePoint(new Point(0,inkBounds.Top+inkBounds.Height/2),tray).Y;
        double footerCenter=footer.TranslatePoint(new Point(0,footer.ActualHeight/2),tray).Y;
        Assert(Math.Abs(inkCenter-footerCenter)<=2,"Footer status glyphs are vertically centered in the full bottom band");
        Assert(!Nodes<Button>(tray).Any(b=>System.Windows.Automation.AutomationProperties.GetName(b)=="关闭"),"Tray popup has no close X");
        var settingsButton=Nodes<Button>(tray).Single(b=>System.Windows.Automation.AutomationProperties.GetName(b)=="设置");
        var gear=(System.Windows.Shapes.Path)settingsButton.Content;
        Assert(settingsButton.Width==24 && settingsButton.Height==24 && gear.Width==14 && gear.Height==14,"Settings gear retains its icon and hit-target dimensions");
        Assert(Math.Abs(gear.TranslatePoint(new Point(0,gear.ActualHeight),tray).Y-status.TranslatePoint(new Point(0,inkBounds.Bottom),tray).Y)<=1,"Settings gear bottom aligns with status text ink");
        settingsButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump(80);
        Assert(app.Windows.OfType<SettingsWindow>().Count(w=>w.IsVisible)==1 && tray.IsVisible,"Settings gear opens actual settings without dismissing tray");
        settingsButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump(30);
        Assert(app.Windows.OfType<SettingsWindow>().Count(w=>w.IsVisible)==1,"Repeated gear click reuses the settings window");
        app.Windows.OfType<SettingsWindow>().Single().Close();
        var left=tray.Left; var top=tray.Top; tray.OpenAt(new System.Drawing.Point(100, 100)); Pump(30);
        Assert(tray.IsVisible && tray.Left==left && tray.Top==top,"Repeated tray click leaves card open and stationary");
        // Raise deactivation directly to verify it is deliberately not a dismissal condition.
        typeof(Window).GetMethod("OnDeactivated", BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(tray, new object[]{EventArgs.Empty});
        Assert(tray.IsVisible,"Deactivation does not dismiss tray");
        Ask("r1"); Pump(20); var request=Active();
        double destination=(double)typeof(ReceiveWindow).GetField("_destination",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(request)!;
        double initialLeft=request.Left, previousLeft=initialLeft;
        MotionFrame(request,"fly-020",destination);
        for(int i=1;i<=7;i++) {
            Pump(70);
            Assert(request.Left<=previousLeft+.5 && request.Left>=destination-.5,"Fly-in decelerates toward its final anchor without reversing or overshooting");
            previousLeft=request.Left; MotionFrame(request,$"fly-{20+70*i:000}",destination);
        }
        Assert((!FloatingCard.Motion || initialLeft>destination+1) && Math.Abs(request.Left-destination)<.5,"Fly-in starts outside its resting position and settles exactly at the final anchor");
        var handle=new System.Windows.Interop.WindowInteropHelper(request).Handle; GetWindowRect(handle,out var rect); var work=System.Windows.Forms.Screen.FromHandle(handle).WorkingArea;
        Assert(rect.Bottom<=work.Bottom && rect.Right<=work.Right && rect.Top>=work.Top,"Receive card remains entirely inside monitor work area above taskbar");
        Assert(request.Width==302 && request.Height==142,"Receive/progress uses explicit final dimensions (+15%)");
        Assert(Nodes<Viewbox>(request).Length==0,"Receive/progress has no scaling container");
        Assert(Nodes<TextBlock>(request).All(t=>t.FontSize==Math.Round(t.FontSize)),"Text renders at individually assigned integer font sizes");
        var body=(Grid)typeof(FloatingCard).GetField("Body",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(request)!;
        foreach(var button in Nodes<Button>(request).Where(b=>b.Content?.ToString() is "接受" or "拒绝")) {
            var bounds=button.TransformToAncestor(body).TransformBounds(new System.Windows.Rect(0,0,button.ActualWidth,button.ActualHeight));
            Assert(bounds.Top>=0 && bounds.Bottom<=body.ActualHeight-0.5,"Invitation button fits entirely inside its content area without bottom clipping");
        }
        var x=request.Left; var y=request.Top;
        Assert(!Nodes<TextBlock>(request).Any(t=>t.Text.Contains("IMG_0074")),"Invitation has no filename text or hidden filename label");
        var senderLabel=Nodes<TextBlock>(request).Single(t=>t.Text=="iPhone");
        var titleLabel=Nodes<TextBlock>(request).Single(t=>t.Text=="收到隔空投送邀请");
        var acceptButton=Nodes<Button>(request).Single(b=>b.Content?.ToString()=="接受");
        var buttonSurface=(Border)acceptButton.Template.FindName("ButtonSurface",acceptButton);
        var interactionTint=(Border)acceptButton.Template.FindName("InteractionTint",acceptButton);
        MotionFrame(request,"button-rest",destination);
        var pressedProperty=typeof(System.Windows.Controls.Primitives.ButtonBase).GetProperty("IsPressed")!;
        pressedProperty.SetValue(acceptButton,true); Pump(100);
        MotionFrame(request,"button-pressed",destination);
        Assert(interactionTint.Opacity>.15 && ((ScaleTransform)buttonSurface.RenderTransform).ScaleX<.98,"Press animates the button surface and tint");
        Assert(acceptButton.RenderTransform.Value.IsIdentity && Nodes<TextBlock>(acceptButton).All(t=>t.RenderTransform.Value.IsIdentity),"Press never scales or moves the button text");
        pressedProperty.SetValue(acceptButton,false); Pump(220);
        MotionFrame(request,"button-released",destination);
        Assert(buttonSurface.RenderTransform.Value.IsIdentity && Math.Abs(interactionTint.Opacity-(acceptButton.IsMouseOver?.08:0))<.01,"Release settles at the original geometry with no lingering animation offset");
        double footerBottom=acceptButton.TranslatePoint(new Point(0,acceptButton.ActualHeight),request).Y;
        Template(request,acceptButton);
        double titleBottom=titleLabel.TranslatePoint(new Point(0,titleLabel.ActualHeight),request).Y;
        double actionsTop=acceptButton.TranslatePoint(new Point(),request).Y;
        double senderY=senderLabel.TranslatePoint(new Point(0,senderLabel.ActualHeight/2),request).Y;
        Assert(senderLabel.FontSize==13 && Math.Abs(senderY-(titleBottom+actionsTop)/2)<=2,"Enlarged device name centers between invitation title and buttons");
        var failed=new ReceiveWindow(new Request {Id="failed-layout",Sender="iPhone",Model="iPhone15,2",What="secret-file.jpg"},(_,_)=>{});
        failed.Present(); failed.Accept(); Pump(520);
        var failureHandle=new System.Windows.Interop.WindowInteropHelper(failed).Handle;
        double failureLeft=failed.Left, failureTop=failed.Top;
        var failureTitle=Nodes<TextBlock>(failed).Single(t=>t.Text.StartsWith("正在接收"));
        var retainedFailureSender=Nodes<TextBlock>(failed).Single(t=>t.Text=="iPhone");
        MotionFrame(failed,"failure-000-progress",failureLeft);
        failed.Finish(false,"测试中断"); Pump(40);
        if(FloatingCard.Motion) Assert(failureTitle.Text.StartsWith("正在接收") && failureTitle.Opacity>0 && failureTitle.Opacity<1,"Interruption uses the same outgoing title animation as completion");
        MotionFrame(failed,"failure-040",failureLeft);
        for(int i=1;i<=7;i++) {
            Pump(60);
            Assert(failed.IsVisible && new System.Windows.Interop.WindowInteropHelper(failed).Handle==failureHandle && failed.Left==failureLeft && failed.Top==failureTop,"Interruption retains the original card and screen anchor throughout replacement");
            MotionFrame(failed,$"failure-{40+60*i:000}",failureLeft);
        }
        var failureText=Nodes<TextBlock>(failed).Single(t=>t.Text=="连接中断");
        var failureSender=Nodes<TextBlock>(failed).Single(t=>t.Text=="iPhone");
        Template(failed,failureText);
        Assert(Math.Abs(failureText.TranslatePoint(new Point(0,failureText.ActualHeight),failed).Y-footerBottom)<=1,"Interruption message uses the shared footer bottom anchor");
        Assert(failureText.FontSize==16 && failureSender.FontSize==senderLabel.FontSize,"Failure uses enlarged message and matching device-name size");
        Assert(!Nodes<TextBlock>(failed).Any(t=>t.Text.Contains("secret-file")),"Failure contains no filename");
        Assert(ReferenceEquals(retainedFailureSender,failureSender) && failureSender.Opacity==1 && failureTitle.Text=="接收已停止" && Nodes<FrameworkElement>(failed).All(e=>e.RenderTransform.Value.IsIdentity),"Interruption preserves unchanged device text and settles shared slots without transform residue");
        Assert(!Nodes<Button>(failed).Any(b=>b.IsVisible && b.Content?.ToString()=="打开所在位置") && Nodes<System.Windows.Shapes.Path>(failed).Count(p=>p.IsVisible)==2,"Interruption shows two stopped-X strokes and no completion-only controls");
        failed.Close();
        Nodes<Button>(request).Single(b=>b.Content?.ToString()=="接受").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump(550);
        Assert(request.IsVisible && request.Left==x && request.Top==y,"Accept changes to progress at the identical position");
        var rateLabel=Nodes<TextBlock>(request).Single(t=>t.IsVisible && t.Text=="等待传输…");
        Template(request,rateLabel);
        Assert(Math.Abs(FooterBottom(rateLabel,request)-footerBottom)<=1,"Live speed uses the shared footer bottom anchor");
        Event("{\"event\":\"incoming\",\"id\":\"r1\",\"done\":0,\"total\":100000000}"); Pump(300);
        Event("{\"event\":\"incoming\",\"id\":\"r1\",\"done\":68000000,\"total\":100000000}"); Pump(650);
        var speedTimer=(DispatcherTimer)typeof(ReceiveWindow).GetField("_speedTimer",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(request)!;
        Assert(speedTimer.Interval==TimeSpan.FromMilliseconds(800),"Live rate refreshes every 0.8 seconds");
        Assert(Nodes<TextBlock>(request).Any(t=>t.IsVisible && t.Text=="正在接收 68%"),"Incoming percentage updates dynamically after the receiving title");
        Assert(!Nodes<TextBlock>(request).Any(t=>t.IsVisible && t.Text=="0%"),"No separate percentage line remains below the speed");
        Assert(Nodes<TextBlock>(request).Any(t=>t.Text.Contains("MB/s")),"Incoming byte deltas produce live transfer speed");
        string firstRate=Nodes<TextBlock>(request).Single(t=>t.Text.Contains("MB/s")).Text;
        Event("{\"event\":\"incoming\",\"id\":\"r1\",\"done\":69000000,\"total\":100000000}"); Pump(800);
        Assert(Nodes<TextBlock>(request).Any(t=>t.Text.Contains("MB/s") && t.Text!=firstRate),"Different byte deltas change the measured rate instead of displaying a constant");
        Pump(850);
        Assert(Nodes<TextBlock>(request).Any(t=>t.IsVisible && t.Text=="0 KB/s"),"No incoming bytes reduces the live rate to zero");
        var rateInk=VisualTreeHelper.GetDescendantBounds(rateLabel);
        Assert(rateLabel.Height==acceptButton.Height && rateLabel.FontSize==22,"Moderate speed text keeps the shared button-height footer slot");
        var rateParent=(Visual)VisualTreeHelper.GetParent(rateLabel);
        var painted=rateLabel.TransformToAncestor(rateParent).TransformBounds(rateInk);
        var layout=System.Windows.Controls.Primitives.LayoutInformation.GetLayoutSlot(rateLabel);
        Assert(painted.Top>=layout.Top-1 && painted.Bottom<=layout.Bottom+1,"Rate glyphs fit within the footer without clipping or extending above it");
        MotionFrame(request,"complete-000-progress",destination);
        var transferHandle=new System.Windows.Interop.WindowInteropHelper(request).Handle;
        Event("{\"event\":\"stored\",\"id\":\"r1\",\"folder\":\"C:\\\\Test\",\"files\":[\"IMG_0074.JPG\"]}"); Pump(40);
        if(FloatingCard.Motion) Assert(titleLabel.Text.StartsWith("正在接收") && titleLabel.Opacity<1 && titleLabel.Opacity>0,"Completion first animates the retained title out instead of replacing it in one frame");
        MotionFrame(request,"complete-040",destination);
        for(int i=1;i<=7;i++) {
            Pump(60);
            Assert(ReferenceEquals(Active(),request) && new System.Windows.Interop.WindowInteropHelper(request).Handle==transferHandle && request.Left==x && request.Top==y,"Completion retains the transfer card, native window and screen position throughout replacement");
            MotionFrame(request,$"complete-{40+60*i:000}",destination);
        }
        Assert(Nodes<TextBlock>(request).Any(t=>t.Text=="接收完成"),"Stored event shows completion");
        var completion=Nodes<TextBlock>(request).Where(t=>t.IsVisible && !string.IsNullOrWhiteSpace(t.Text)).ToArray();
        Assert(completion.Any(t=>t.Text=="接收完成") && completion.Any(t=>t.Text=="已保存到本地") && !completion.Any(t=>t.Text.Contains("Test")),"Completion has generic saved-local text and exposes no file address");
        var openButton=Nodes<Button>(request).Single(b=>b.IsVisible);
        Assert(openButton.Content?.ToString()=="打开所在位置" && openButton.Height==acceptButton.Height && openButton.FontSize==acceptButton.FontSize && openButton.Background==acceptButton.Background,"Completion has one open-folder button using the accept-button style");
        Template(request,openButton);
        Assert(acceptButton.ActualWidth==72 && Nodes<Button>(request).Single(b=>b.Content?.ToString()=="拒绝").Width==acceptButton.Width && openButton.ActualWidth==acceptButton.ActualWidth*2,"Footer buttons use a shared 72-DIP width unit: Accept/Decline 1x, Open folder 2x");
        var savedText=completion.Single(t=>t.Text=="已保存到本地");
        Assert(ReferenceEquals(savedText,senderLabel) && ReferenceEquals(titleLabel,completion.Single(t=>t.Text=="接收完成")),"Completion replaces text on the original title and device-name elements");
        Assert(Nodes<FrameworkElement>(request).All(e=>e.RenderTransform.Value.IsIdentity) && titleLabel.Opacity==1 && savedText.Opacity==1,"Replacement finishes with native-size text and no remaining translation or opacity offset");
        request.Finish(true,"",@"C:\Ignored duplicate");
        Assert(Nodes<System.Windows.Shapes.Path>(request).Count(p=>p.IsVisible)==1,"Repeated completion cannot add a second checkmark or restart the transition");
        Assert(savedText.FontSize==13 && savedText.ToolTip is null && Math.Abs(savedText.TranslatePoint(new Point(0,savedText.ActualHeight/2),request).Y-senderY)<=0.5,"Saved-local text reuses the fixed device-name style and position without a path tooltip");
        string? openedFolder=null;
        var openCheck=new ReceiveWindow(new Request {Id="open-folder",Sender="iPhone",Model="iPhone15,2",What=""},(_,_)=>{},folder=>openedFolder=folder);
        openCheck.Present(); openCheck.Finish(true,"",@"C:\Received Files\测试"); Pump(50);
        Nodes<Button>(openCheck).Single(b=>b.IsVisible).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert(openedFolder==@"C:\Received Files\测试","Open-location button receives the actual saved directory including spaces and Unicode");
        Assert(!openCheck.IsVisible,"Open-location click closes the completed card immediately");
        var finishTimer=(DispatcherTimer)typeof(ReceiveWindow).GetField("_finishTimer",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(request)!;
        Assert(finishTimer.Interval==TimeSpan.FromSeconds(FloatingCard.Motion?4.4:4),"Completion timer reserves four seconds plus the replacement-animation allowance");
        Pump(500); Assert(request.IsVisible,"Completed card remains visible before its four-second hold expires");
        Pump(4000); Assert(!request.IsVisible,"Completed card exits automatically after the four-second hold");
        Ask("r2"); Ask("r3"); Pump(550);
        Assert(app.Windows.OfType<ReceiveWindow>().Count(w=>w.IsVisible)==1,"Simultaneous invitations queue without overlap");
        var declined=Active(); var declineClock=System.Diagnostics.Stopwatch.StartNew(); TimeSpan? declinedAt=null;
        declined.Closed+=(_,_)=>declinedAt=declineClock.Elapsed;
        Nodes<Button>(declined).Single(b=>b.Content?.ToString()=="拒绝").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        // A nominal 100ms dispatcher sample can run after the 280ms exit under
        // rendering load. Check visibility at the action, then time the real close.
        Assert(!FloatingCard.Motion || declined.IsVisible,"Decline retains window during exit animation");
        Pump(600); Assert(!declined.IsVisible && Active().RequestId=="r3","Decline flies out then presents next invitation");
        if(FloatingCard.Motion) Assert(declinedAt>=TimeSpan.FromMilliseconds(240),"Decline waits for its exit animation before closing the native window");
        Event("{\"event\":\"answered\",\"id\":\"r3\",\"how\":\"expired\"}"); Pump(400);
        Assert(!app.Windows.OfType<ReceiveWindow>().Any(w=>w.IsVisible),"Expired invitation clears the card");
        Ask("r4"); Pump(500); app.Answer("r4",true); Pump(50); var ongoing=Active();
        Assert(!Nodes<Button>(ongoing).Any(b=>b.Content?.ToString()=="×"),"Receive cards have no close button");
        ongoing.Dismiss(); Pump(400);
        Assert(!ongoing.IsVisible && app.Model.Requests.Any(r=>r.Id=="r4"),"Dismissing progress leaves the transfer intact");
        Event("{\"event\":\"ready\"}"); Pump(50); Assert(app.Model.Requests.Count==0,"Engine restart clears stale request identities");
        // Other windows can dismiss the tray during the receive-card checks.
        // Reopen it so this assertion measures exit animation from an open popup.
        tray = new TrayWindow(app); tray.OpenAt(new System.Drawing.Point(100, 100)); Pump(360);
        traySurface = (Grid)tray.Content;
        tray.Dismiss(); Pump(55);
        if(FloatingCard.Motion) {
            Assert(tray.IsVisible && traySurface.RenderTransform is ScaleTransform exit && exit.ScaleX<1 && traySurface.Opacity<1,"Tray stays mounted while shrinking and fading toward its original anchor");
            MotionFrame(tray,"tray-close-055",tray.Left);
        }
        Pump(200); Assert(!tray.IsVisible,"Tray dismisses with exit animation");
        var interruptedTray=new TrayWindow(app); interruptedTray.OpenAt(new System.Drawing.Point(100, 100)); Pump(30);
        var interruptedSurface=(Grid)interruptedTray.Content;
        double beforeDismiss=interruptedSurface.RenderTransform.Value.M11;
        interruptedTray.Dismiss();
        if(FloatingCard.Motion) Assert(Math.Abs(interruptedSurface.RenderTransform.Value.M11-beforeDismiss)<.01,"Closing during entry continues from its current size without a jump");
        Pump(240); Assert(!interruptedTray.IsVisible,"Tray can close safely during its entry animation");
        var outsideCard=new TrayWindow(app);outsideCard.OpenAt(new System.Drawing.Point(100, 100));Pump(360);
        var other=new Window {Width=180,Height=100,Left=100,Top=100,WindowStartupLocation=WindowStartupLocation.Manual};other.Show();Pump(50);
        var otherSource=System.Windows.Interop.HwndSource.FromHwnd(new System.Windows.Interop.WindowInteropHelper(other).Handle)!;
        var position=otherSource.CompositionTarget!.TransformToDevice.Transform(new Point(other.Left+50,other.Top+50));
        var outsideMonitor=typeof(TrayWindow).GetField("_desktop",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(outsideCard)!;
        IntPtr mouseData=Marshal.AllocHGlobal(40);
        try {
            for(int n=0;n<40;n++)Marshal.WriteByte(mouseData,n,0);
            Marshal.WriteInt32(mouseData,0,(int)position.X);Marshal.WriteInt32(mouseData,4,(int)position.Y);
            outsideMonitor.GetType().GetMethod("Hook",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(outsideMonitor,new object[]{0,(IntPtr)0x201,mouseData});
            Pump(280);Assert(!outsideCard.IsVisible,"Outside click on another window closes the tray popup");
        }finally{Marshal.FreeHGlobal(mouseData);other.Close();outsideCard.Close();}
        app.Shutdown();
    }
    [StructLayout(LayoutKind.Sequential)] struct Rect {public int Left,Top,Right,Bottom;}
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h,out Rect rect);
}



