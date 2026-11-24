// SPDX-License-Identifier: GPL-2.0-only
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using AetherDrop;
using Typography = AetherDrop.Typography;
static class Check
{
    [System.Runtime.InteropServices.DllImport("gdi32.dll")] static extern IntPtr CreateRectRgn(int l,int t,int r,int b);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern int GetWindowRgn(IntPtr window,IntPtr region);
    [System.Runtime.InteropServices.DllImport("gdi32.dll")] static extern bool PtInRegion(IntPtr region,int x,int y);
    [System.Runtime.InteropServices.DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr obj);
    [System.Runtime.InteropServices.DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr window,int attribute,out int value,int size);
    static void CheckCorners(System.Windows.Forms.ToolStripDropDown menu) {
        var region=CreateRectRgn(0,0,0,0);
        try {
            bool smooth=DwmGetWindowAttribute(menu.Handle,33,out int preference,sizeof(int))==0 && preference==2;
            Assert(smooth || GetWindowRgn(menu.Handle,region)>0 && !PtInRegion(region,0,0) && PtInRegion(region,menu.Width/2,menu.Height/2),"Menu uses compositor-rounded corners, with clipped-region fallback on older Windows");
            if(smooth)Assert(GetWindowRgn(menu.Handle,region)<=0,"Compositor-rounded menu has no jagged pixel-region clipping");
        }
        finally {DeleteObject(region);}
    }
    static void MenuPump(int ms){var clock=System.Diagnostics.Stopwatch.StartNew();while(clock.ElapsedMilliseconds<ms){System.Windows.Forms.Application.DoEvents();System.Threading.Thread.Sleep(5);}}
    static void MenuCapture(System.Windows.Forms.ToolStrip menu,string file){using var bmp=new System.Drawing.Bitmap(menu.Width,menu.Height);menu.DrawToBitmap(bmp,new System.Drawing.Rectangle(0,0,bmp.Width,bmp.Height));bmp.Save(file);}
    static void TrailingCheck(System.Windows.Forms.ToolStripDropDown menu) {
        var row=menu.Items.OfType<System.Windows.Forms.ToolStripMenuItem>().First(i=>i.Checked);
        using var image=new System.Drawing.Bitmap(menu.Width,menu.Height);menu.DrawToBitmap(image,new System.Drawing.Rectangle(0,0,menu.Width,menu.Height));
        double scale=menu.DeviceDpi/96.0;var ink=Palette.Drawing(0,122,255);int count=0;
        for(int y=Math.Max(0,row.Bounds.Top+row.Height/2-(int)(7*scale));y<Math.Min(image.Height,row.Bounds.Top+row.Height/2+(int)(7*scale));y++)
            for(int x=Math.Max(0,menu.ClientSize.Width-(int)(24*scale));x<Math.Min(image.Width,menu.ClientSize.Width-(int)(6*scale));x++) {
                var pixel=image.GetPixel(x,y);if(Math.Abs(pixel.R-ink.R)+Math.Abs(pixel.G-ink.G)+Math.Abs(pixel.B-ink.B)<25)count++;
            }
        Assert(count>=3,"Native menu check is visibly painted inside the trailing column");
    }
    static void Assert(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS "+message);}
    static System.Collections.Generic.IEnumerable<DependencyObject> Nodes(DependencyObject p){yield return p;for(int i=0;i<VisualTreeHelper.GetChildrenCount(p);i++)foreach(var c in Nodes(VisualTreeHelper.GetChild(p,i)))yield return c;}
    static bool Symbol(string text)=>text.Length>0&&text.All(c=>char.IsWhiteSpace(c)||c is >= '\uE000' and <= '\uF8FF');
    static GlyphTypeface Verify(FontFamily family,FontStyle style,FontWeight weight)
    {
        if(!new Typeface(family,style,weight,FontStretches.Normal).TryGetGlyphTypeface(out var glyph))throw new Exception("Font did not resolve: "+family.Source);
        if(!glyph.FamilyNames.Values.Any(n=>n.Contains("Microsoft YaHei",StringComparison.OrdinalIgnoreCase)||n.Contains("微软雅黑")))throw new Exception("Unexpected font fallback: "+glyph.FontUri);
        if(glyph.StyleSimulations!=StyleSimulations.None)throw new Exception("Synthetic face: "+glyph.FontUri+" "+glyph.StyleSimulations);
        return glyph;
    }
    [STAThread] static void Main()
    {
        var app=new App(uiDebugMode:true){ShutdownMode=ShutdownMode.OnExplicitShutdown};L.Use("zh");app.ApplyTheme();
        foreach(var weight in new[]{FontWeights.Normal,FontWeights.Bold}) {
            var glyph=Verify(Typography.Regular,FontStyles.Normal,weight);
            Assert("AetherDrop 接受 38.6 MB/s".All(c=>glyph.CharacterToGlyphMap.ContainsKey(c)),"Microsoft YaHei "+weight+" covers UI Chinese, Latin and numbers without synthetic weight");
        }
        Assert(Typography.MenuFont.FontFamily.Name.Contains("微软雅黑")||Typography.MenuFont.FontFamily.Name.Contains("Microsoft YaHei",StringComparison.OrdinalIgnoreCase),"Native menu family is Microsoft YaHei");
        using(var menu=(System.Windows.Forms.ContextMenuStrip)typeof(App).GetMethod("BuildMenu",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.Invoke(app,null)!) {
            Assert(menu.Font.Name==Typography.MenuFont.Name,"Application tray menu actually uses the Microsoft YaHei font");
            var menuRows=menu.Items.OfType<System.Windows.Forms.ToolStripMenuItem>().ToArray();
            Assert(menuRows.Select(i=>i.Text).SequenceEqual(new[]{"打开 AetherDrop","Wi-Fi","设置","生成诊断报告","退出"}),"Tray menu contains exactly the five intended actions in order");
            Assert(menuRows.All(i=>!i.HasDropDownItems && !i.Checked && !(i.Text ?? "").Contains("...") && !(i.Text ?? "").Contains("…")),"Tray menu has no receive toggle, mode submenu, checkmark or ellipsis");
            foreach(var item in menu.Items.OfType<System.Windows.Forms.ToolStripMenuItem>())
                if(item.HasDropDownItems)Assert(item.DropDown.Font.Name==Typography.MenuFont.Name,"Tray submenu inherits Microsoft YaHei");
            var capture=Environment.GetEnvironmentVariable("AETHERDROP_MENU_CAPTURE_DIR");
            if(!string.IsNullOrEmpty(capture)) {
                System.IO.Directory.CreateDirectory(capture);
                menu.Show(30,30);System.Windows.Forms.Application.DoEvents();
                CheckCorners(menu);
                var last=menu.Items.OfType<System.Windows.Forms.ToolStripMenuItem>().Last();last.Select();MenuPump(190);
                MenuCapture(menu,System.IO.Path.Combine(capture,"highlight-last.png"));
                var highlight=menu.Renderer.GetType().GetMethod("HighlightBounds",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!;
                var start=(System.Drawing.RectangleF)highlight.Invoke(menu.Renderer,new object[]{menu})!;
                Assert(Math.Abs(start.Top+start.Height/2-last.Bounds.Top-last.Height/2f)<1,"Menu highlight centers on the full selected row");
                var first=menu.Items.OfType<System.Windows.Forms.ToolStripMenuItem>().First();first.Select();MenuCapture(menu,System.IO.Path.Combine(capture,"highlight-departure.png"));MenuPump(45);
                MenuCapture(menu,System.IO.Path.Combine(capture,"highlight-moving.png"));
                var mid=(System.Drawing.RectangleF)highlight.Invoke(menu.Renderer,new object[]{menu})!;
                MenuCapture(menu,System.IO.Path.Combine(capture,"highlight-moving.png"));
                if(FloatingCard.Motion)Assert(mid.Top<start.Top && mid.Top>first.Bounds.Top+1,"Menu highlight travels through intermediate vertical positions");
                MenuPump(160);MenuCapture(menu,System.IO.Path.Combine(capture,"highlight-first.png"));
                var end=(System.Drawing.RectangleF)highlight.Invoke(menu.Renderer,new object[]{menu})!;
                Assert(Math.Abs(end.Top+end.Height/2-first.Bounds.Top-first.Height/2f)<1,"Moving highlight settles centered on its new row");
                using(var bmp=new System.Drawing.Bitmap(menu.Width,menu.Height)) {menu.DrawToBitmap(bmp,new System.Drawing.Rectangle(0,0,menu.Width,menu.Height));bmp.Save(System.IO.Path.Combine(capture,"menu.png"));}
                Assert(menuRows.Select(i=>i.Height).Distinct().Count()==1,"All menu actions use the same row height");
                Assert(menu.Items.OfType<System.Windows.Forms.ToolStripSeparator>().Count()==2,"Two separators distinguish launch, tools and exit");
                var hoverMethod=menu.Renderer.GetType().GetMethod("CloseOtherSubmenus",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!;
                var followMethod=menu.Renderer.GetType().GetMethod("Follow",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!;
                foreach(var endpoint in new[]{first,last}) {
                    var neighbor=endpoint==first?menuRows[1]:menuRows[^2];neighbor.Select();followMethod.Invoke(menu.Renderer,new object[]{menu,false});MenuCapture(menu,System.IO.Path.Combine(capture,"neighbor-start.png"));MenuPump(180);followMethod.Invoke(menu.Renderer,new object[]{menu,false});MenuCapture(menu,System.IO.Path.Combine(capture,"neighbor-settled.png"));
                    var previous=(System.Drawing.RectangleF)highlight.Invoke(menu.Renderer,new object[]{menu})!;
                    // Exercise the real hover route through each adjacent separator.
                    var separator=menu.Items.OfType<System.Windows.Forms.ToolStripSeparator>().ElementAt(endpoint==first?0:1);
                    hoverMethod.Invoke(null,new object[]{menu,new System.Drawing.Point(separator.Bounds.Left+12,separator.Bounds.Top+separator.Height/2)});
                    MenuPump(30);
                    hoverMethod.Invoke(null,new object[]{menu,new System.Drawing.Point(endpoint.Bounds.Left+20,endpoint.Bounds.Top+endpoint.Height/2)});
                    followMethod.Invoke(menu.Renderer,new object[]{menu,false});
                    MenuCapture(menu,System.IO.Path.Combine(capture,endpoint==first?"first-enter-start.png":"last-enter-start.png"));MenuPump(40);
                    followMethod.Invoke(menu.Renderer,new object[]{menu,false});
                    var traveling=(System.Drawing.RectangleF)highlight.Invoke(menu.Renderer,new object[]{menu})!;
                    if(FloatingCard.Motion) Assert(Math.Abs(traveling.Top-previous.Top)>0.1 && Math.Abs(traveling.Top-(endpoint.Bounds.Top+1))>0.1,"First and last row hover moves through intermediate geometry");
                    MenuCapture(menu,System.IO.Path.Combine(capture,endpoint==first?"first-enter-moving.png":"last-enter-moving.png"));MenuPump(170);
                    followMethod.Invoke(menu.Renderer,new object[]{menu,false});
                    var settled=(System.Drawing.RectangleF)highlight.Invoke(menu.Renderer,new object[]{menu})!;
                    Assert(Math.Abs(settled.Top+settled.Height/2-endpoint.Bounds.Top-endpoint.Height/2f)<1,"Endpoint hover settles at its own row center");
                }
                var highlightedMethod=menu.Renderer.GetType().GetMethod("IsHighlightedItem",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!;
                foreach(var row in menuRows) {
                    foreach(var edge in new[]{4,menu.ClientSize.Width-5}) {
                        hoverMethod.Invoke(null,new object[]{menu,new System.Drawing.Point(edge,row.Bounds.Top+row.Height/2)});
                        followMethod.Invoke(menu.Renderer,new object[]{menu,false});
                        Assert((bool)highlightedMethod.Invoke(menu.Renderer,new object[]{menu,row})!,"Text and background retain the same highlighted item at both horizontal edges");
                    }
                }
                menu.Close();
            }
        }
        using var session=new UiDebugSession(app);
        var windows=new Window[]{new MainWindow(),new SendWindow(),new SettingsWindow(),new AppleIdWindow(),new WifiWindow(),new TrayMenu(),new TrayWindow(app),new ReceiveWindow(new Request{Id="fonts",Sender="iPhone",Model="iPhone15,2",What="IMG_0074.JPG"},(_,_)=>{}),new UiDebugWindow(session)};
        foreach(var window in windows){
            window.WindowStartupLocation=WindowStartupLocation.Manual;window.Left=-20000;window.Top=-20000;window.ShowActivated=false;window.ShowInTaskbar=false;window.Show();window.UpdateLayout();
            int count=0;
            foreach(var node in Nodes(window)){
                if(node is TextBlock text){
                    if(string.IsNullOrWhiteSpace(text.Text) || Symbol(text.Text))continue;
                    Verify(text.FontFamily,text.FontStyle,text.FontWeight);count++;
                    foreach(var run in text.Inlines.OfType<Run>())if(!Symbol(run.Text))Verify(run.FontFamily,run.FontStyle,run.FontWeight);
                }else if(node is Control control)Verify(control.FontFamily,control.FontStyle,control.FontWeight);
            }
            Assert(count>0,window.GetType().Name+" uses Microsoft YaHei for its text and controls");window.Close();
        }
        app.Shutdown();
    }
}


