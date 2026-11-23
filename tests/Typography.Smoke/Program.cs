// SPDX-License-Identifier: GPL-2.0-only
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using NekoDrop;
using Typography = NekoDrop.Typography;
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
        var app=new App(uiDebugMode:true){ShutdownMode=ShutdownMode.OnExplicitShutdown};L.Use("zh");app.Resources.MergedDictionaries.Add(Theme.Build());
        foreach(var weight in new[]{FontWeights.Normal,FontWeights.Bold}) {
            var glyph=Verify(Typography.Regular,FontStyles.Normal,weight);
            Assert("AetherDrop 接受 38.6 MB/s".All(c=>glyph.CharacterToGlyphMap.ContainsKey(c)),"Microsoft YaHei "+weight+" covers UI Chinese, Latin and numbers without synthetic weight");
        }
        Assert(Typography.MenuFont.FontFamily.Name.Contains("微软雅黑")||Typography.MenuFont.FontFamily.Name.Contains("Microsoft YaHei",StringComparison.OrdinalIgnoreCase),"Native menu family is Microsoft YaHei");
        using(var menu=(System.Windows.Forms.ContextMenuStrip)typeof(App).GetMethod("BuildMenu",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.Invoke(app,null)!) {
            Assert(menu.Font.Name==Typography.MenuFont.Name,"Application tray menu actually uses the Microsoft YaHei font");
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
                var sub=menu.Items.OfType<System.Windows.Forms.ToolStripMenuItem>().Single(i=>i.HasDropDownItems);
                sub.ShowDropDown();System.Windows.Forms.Application.DoEvents();
                CheckCorners(sub.DropDown);
                var child=sub.DropDownItems.OfType<System.Windows.Forms.ToolStripMenuItem>().Last();child.Select();
                MenuCapture(menu,System.IO.Path.Combine(capture,"parent-submenu-open.png"));
                MenuCapture(sub.DropDown,System.IO.Path.Combine(capture,"submenu-selected-start.png"));
                MenuPump(180);
                MenuCapture(menu,System.IO.Path.Combine(capture,"parent-submenu-settled.png"));
                MenuCapture(sub.DropDown,System.IO.Path.Combine(capture,"submenu-selected.png"));
                var parentBounds=(System.Drawing.RectangleF)highlight.Invoke(menu.Renderer,new object[]{menu})!;
                var childBounds=(System.Drawing.RectangleF)sub.DropDown.Renderer.GetType().GetMethod("HighlightBounds",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.Invoke(sub.DropDown.Renderer,new object[]{sub.DropDown})!;
                Assert(Math.Abs(parentBounds.Top+parentBounds.Height/2-sub.Bounds.Top-sub.Height/2f)<1,"Parent highlight stays on the open submenu launcher while child rows are selected");
                Assert(Math.Abs(childBounds.Right-(sub.DropDown.ClientSize.Width-childBounds.Left))<1,"Submenu highlight has equal left and right insets across its full current width");
                var move=menu.Renderer.GetType().GetMethod("CloseOtherSubmenus",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static)!;
                move.Invoke(null,new object[]{menu,new System.Drawing.Point(sub.Bounds.Left+20,sub.Bounds.Top+sub.Height/2)});
                Assert(sub.DropDown.Visible,"Returning to the submenu launcher keeps its submenu open");
                move.Invoke(null,new object[]{menu,new System.Drawing.Point(first.Bounds.Left+20,first.Bounds.Top+first.Height/2)});
                Assert(!sub.DropDown.Visible,"Returning to another main-menu row automatically closes the submenu");
                move.Invoke(null,new object[]{menu,new System.Drawing.Point(sub.Bounds.Left+40,sub.Bounds.Top+sub.Height/2)});
                Assert(sub.DropDown.Visible,"Entering submenu launcher opens child synchronously without the native hover delay");
                move.Invoke(null,new object[]{menu,new System.Drawing.Point(first.Bounds.Left+40,first.Bounds.Top+first.Height/2)});
                Assert(!sub.DropDown.Visible,"Leaving launcher closes child synchronously without waiting for an animation or timer");
                sub.HideDropDown();menu.Close();
            }
        }
        using var session=new UiDebugSession(app);
        var windows=new Window[]{new MainWindow(),new SendWindow(),new SettingsWindow(),new WifiWindow(),new TrayWindow(app),new ReceiveWindow(new Request{Id="fonts",Sender="iPhone",Model="iPhone15,2",What="IMG_0074.JPG"},(_,_)=>{}),new UiDebugWindow(session)};
        foreach(var window in windows){
            window.WindowStartupLocation=WindowStartupLocation.Manual;window.Left=-20000;window.Top=-20000;window.ShowActivated=false;window.ShowInTaskbar=false;window.Show();window.UpdateLayout();
            int count=0;
            foreach(var node in Nodes(window)){
                if(node is TextBlock text){
                    if(Symbol(text.Text))continue;
                    Verify(text.FontFamily,text.FontStyle,text.FontWeight);count++;
                    foreach(var run in text.Inlines.OfType<Run>())if(!Symbol(run.Text))Verify(run.FontFamily,run.FontStyle,run.FontWeight);
                }else if(node is Control control)Verify(control.FontFamily,control.FontStyle,control.FontWeight);
            }
            Assert(count>0,window.GetType().Name+" uses Microsoft YaHei for its text and controls");window.Close();
        }
        app.Shutdown();
    }
}


