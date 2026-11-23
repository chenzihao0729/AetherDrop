// SPDX-License-Identifier: GPL-2.0-only
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using Forms = System.Windows.Forms;

namespace NekoDrop;

/// <summary>Native menu behavior with the floating-card palette and rounded surfaces.</summary>
internal sealed class TrayMenuRenderer : Forms.ToolStripProfessionalRenderer
{
    static Color Ink => Palette.Drawing(28, 32, 36);
    static Color Muted => Palette.Drawing(108, 115, 122);
    static Color Blue => Palette.Drawing(0, 122, 255);
    sealed class SelectionState
    {
        public RectangleF Highlight, From, Target;
        public readonly System.Diagnostics.Stopwatch Travel = new();
        public Forms.ToolStripItem? Selected;
    }
    readonly System.Runtime.CompilerServices.ConditionalWeakTable<Forms.ToolStrip, SelectionState> _selections = new();
    internal RectangleF HighlightBounds(Forms.ToolStrip menu) => _selections.GetOrCreateValue(menu).Highlight;
    public TrayMenuRenderer() => RoundedEdges = false;
    static float Scale(Forms.ToolStrip? strip) => (strip?.DeviceDpi ?? 96) / 96f;
    static GraphicsPath Rounded(RectangleF box, float radius)
    {
        var p = new GraphicsPath(); float d = radius * 2;
        p.AddArc(box.Left, box.Top, d, d, 180, 90);
        p.AddArc(box.Right-d, box.Top, d, d, 270, 90);
        p.AddArc(box.Right-d, box.Bottom-d, d, d, 0, 90);
        p.AddArc(box.Left, box.Bottom-d, d, d, 90, 90); p.CloseFigure(); return p;
    }
    protected override void OnRenderToolStripBackground(Forms.ToolStripRenderEventArgs e)
    {
        Follow(e.ToolStrip, false);
        e.Graphics.Clear(Palette.Drawing(250, 253, 255));
        DrawHighlight(e.Graphics, e.ToolStrip, Point.Empty);
    }
    protected override void OnRenderToolStripBorder(Forms.ToolStripRenderEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = Rounded(new RectangleF(.5f,.5f,e.ToolStrip.Width-1,e.ToolStrip.Height-1),10*Scale(e.ToolStrip));
        using var pen = new Pen(Palette.Drawing(197,209,220)); e.Graphics.DrawPath(pen,path);
    }
    protected override void OnRenderImageMargin(Forms.ToolStripRenderEventArgs e) { }
    protected override void OnRenderMenuItemBackground(Forms.ToolStripItemRenderEventArgs e)
    {
        if(e.ToolStrip is not null) DrawHighlight(e.Graphics,e.ToolStrip,e.Item.Bounds.Location);
    }
    void DrawHighlight(Graphics graphics, Forms.ToolStrip menu, Point origin)
    {
        var highlight=_selections.GetOrCreateValue(menu).Highlight;
        if(highlight.IsEmpty)return;
        var box=highlight;box.Offset(-origin.X,-origin.Y);
        graphics.SmoothingMode=SmoothingMode.AntiAlias;
        using var path=Rounded(box,Math.Min(6*Scale(menu),box.Height/2));
        using var brush=new SolidBrush(Palette.Drawing(227,240,255));graphics.FillPath(brush,path);
    }
    void Follow(Forms.ToolStrip menu, bool invalidate = true)
    {
        var state=_selections.GetOrCreateValue(menu);
        // An open submenu owns the pointer; retain the parent launcher highlight.
        var selected=menu.Items.OfType<Forms.ToolStripMenuItem>().FirstOrDefault(i=>i.Enabled && i.HasDropDownItems && i.DropDown.Visible)
            ?? menu.Items.Cast<Forms.ToolStripItem>().FirstOrDefault(i=>i.Selected && i.Enabled && i is not Forms.ToolStripSeparator);
        if(selected is null)
        {
            state.Selected=null;state.Highlight=RectangleF.Empty;state.Travel.Reset();
            if(invalidate)menu.Invalidate();return;
        }
        float s=Scale(menu);
        var target=new RectangleF(4*s,selected.Bounds.Top+1*s,menu.ClientSize.Width-8*s,selected.Height-2*s);
        if(!ReferenceEquals(selected,state.Selected) || target!=state.Target)
        {
            state.Selected=selected;
            state.From=state.Highlight.IsEmpty ? target : state.Highlight;
            state.Target=target;state.Travel.Restart();
        }
        double t=FloatingCard.Motion ? Math.Min(1,state.Travel.Elapsed.TotalMilliseconds/140) : 1;
        float ease=(float)(1-Math.Pow(1-t,3));
        state.Highlight=new RectangleF(state.From.X+(state.Target.X-state.From.X)*ease,state.From.Y+(state.Target.Y-state.From.Y)*ease,
            state.Target.Width,state.From.Height+(state.Target.Height-state.From.Height)*ease);
        if(invalidate)menu.Invalidate();
    }
    protected override void OnRenderItemText(Forms.ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = !e.Item.Enabled ? Muted : e.Item.Selected ? Blue : Ink;
        // Native padding can shift TextRectangle away from the row center.
        // Keep its horizontal text column but center in the full row height.
        e.TextRectangle=new Rectangle(e.TextRectangle.X,0,e.TextRectangle.Width,e.Item.Height);
        e.TextFormat=(e.TextFormat & ~Forms.TextFormatFlags.Bottom) | Forms.TextFormatFlags.VerticalCenter | Forms.TextFormatFlags.SingleLine;
        base.OnRenderItemText(e);
    }
    protected override void OnRenderSeparator(Forms.ToolStripSeparatorRenderEventArgs e)
    {
        float s = Scale(e.ToolStrip);
        using var pen = new Pen(Palette.Drawing(221,230,238));
        e.Graphics.DrawLine(pen,12*s,e.Item.Height/2f,e.Item.Width-12*s,e.Item.Height/2f);
    }
    protected override void OnRenderItemCheck(Forms.ToolStripItemImageRenderEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var b=e.ImageRectangle; float cx=b.Left+b.Width/2f, cy=b.Top+b.Height/2f, s=Scale(e.ToolStrip);
        using var pen=new Pen(Blue,1.8f*s){StartCap=LineCap.Round,EndCap=LineCap.Round,LineJoin=LineJoin.Round};
        e.Graphics.DrawLines(pen,[new PointF(cx-4*s,cy),new PointF(cx-1*s,cy+3*s),new PointF(cx+5*s,cy-4*s)]);
    }
    protected override void OnRenderArrow(Forms.ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor=Muted; base.OnRenderArrow(e);
    }
    internal static void Apply(Forms.ToolStripDropDown menu, bool submenu = false)
    {
        var renderer=new TrayMenuRenderer();menu.Renderer = renderer; menu.Font = Typography.MenuFont;
        var animation=new Forms.Timer {Interval=15};
        animation.Tick+=(_,_)=>renderer.Follow(menu);
        menu.Opened+=(_,_)=>{renderer._selections.Remove(menu);renderer.Follow(menu);animation.Start();};
        menu.Closed+=(_,_)=>{animation.Stop();renderer._selections.Remove(menu);};
        menu.Disposed+=(_,_)=>animation.Dispose();
        menu.BackColor=Palette.Drawing(250,253,255); menu.ForeColor=Ink;
        menu.Padding=submenu ? new Forms.Padding(3,6,3,6) : new Forms.Padding(4,12,4,12);
        if(submenu && menu is Forms.ToolStripDropDownMenu childMenu)
        {
            childMenu.ShowImageMargin=false;
            childMenu.ShowCheckMargin=true;
        }
        menu.ShowItemToolTips=false;
        menu.MouseMove += (_,e) => { if(menu.Visible) CloseOtherSubmenus(menu,e.Location); };
        foreach(Forms.ToolStripItem item in menu.Items)
        {
            item.Padding = item is Forms.ToolStripSeparator ? new Forms.Padding(0,3,0,3)
                : submenu ? new Forms.Padding(8,6,8,6) : new Forms.Padding(4,6,12,6);
            if(item is Forms.ToolStripMenuItem entry)
            {
                entry.MouseEnter += (_,_) => { if(menu.Visible) UpdateHoveredItem(menu,entry); };
                if(entry.HasDropDownItems) Apply(entry.DropDown,true);
            }
        }
        if(!submenu && menu.Items.Count>0)
        {
            menu.Items[0].Margin=new Forms.Padding(0,8,0,0);
            menu.Items[menu.Items.Count-1].Margin=new Forms.Padding(0,0,0,8);
        }
        void ClipWindow()
        {
            if(!menu.IsHandleCreated || menu.Width<24 || menu.Height<24)return;
            // DWM rounds the actual popup with antialiasing. An HRGN clips to
            // integer pixels, producing a staircase on curved edges.
            SetWindowRgn(menu.Handle,IntPtr.Zero,true);
            int preference=2; // DWMWCP_ROUND
            if(DwmSetWindowAttribute(menu.Handle,33,ref preference,sizeof(int))==0)return;
            // Older Windows versions do not support compositor-rounded popups.
            if(!GetWindowRect(menu.Handle,out var bounds))return;
            int diameter=(int)Math.Round(20*Scale(menu));
            var region=CreateRoundRectRgn(0,0,bounds.Right-bounds.Left+1,bounds.Bottom-bounds.Top+1,diameter,diameter);
            if(region!=IntPtr.Zero && SetWindowRgn(menu.Handle,region,true)==0)DeleteObject(region);
        }
        menu.SizeChanged += (_,_) => ClipWindow();
        menu.HandleCreated += (_,_) => ClipWindow();
        menu.Opened += (_,_) => ClipWindow();
    }
    internal static void CloseOtherSubmenus(Forms.ToolStripDropDown menu, Point point)
    {
        UpdateHoveredItem(menu,menu.GetItemAt(point));
    }
    static void UpdateHoveredItem(Forms.ToolStripDropDown menu, Forms.ToolStripItem? hovered)
    {
        foreach(var entry in menu.Items.OfType<Forms.ToolStripMenuItem>())
            if(entry.HasDropDownItems && entry.DropDown.Visible && !ReferenceEquals(entry,hovered))
            {
                if(hovered is { Enabled: true } && hovered is not Forms.ToolStripSeparator) hovered.Select();
                entry.HideDropDown();
            }
        if(hovered is Forms.ToolStripMenuItem {Enabled:true,HasDropDownItems:true} launcher && !launcher.DropDown.Visible)
        {
            launcher.Select();
            launcher.ShowDropDown();
        }
    }
    [StructLayout(LayoutKind.Sequential)] struct WindowRect { public int Left,Top,Right,Bottom; }
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd,out WindowRect rect);
    [DllImport("gdi32.dll")] static extern IntPtr CreateRoundRectRgn(int left,int top,int right,int bottom,int width,int height);
    [DllImport("user32.dll")] static extern int SetWindowRgn(IntPtr hwnd,IntPtr region,bool redraw);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr obj);
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int size);
}
