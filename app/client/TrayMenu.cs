// SPDX-License-Identifier: GPL-2.0-only
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using Forms = System.Windows.Forms;

namespace AetherDrop;

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
    internal bool IsHighlightedItem(Forms.ToolStrip menu, Forms.ToolStripItem item) =>
        ReferenceEquals(_selections.GetOrCreateValue(menu).Selected, item);
    bool _nativeCorners;
    public TrayMenuRenderer() => RoundedEdges = false;
    static float Scale(Forms.ToolStrip? strip) => (strip?.DeviceDpi ?? 96) / 96f;
    // ToolStrip can retain a pre-margin item width after its popup shrinks.
    static int RowWidth(Forms.ToolStrip? menu, Forms.ToolStripItem item) => menu is null ? item.Width : Math.Min(item.Width, menu.ClientSize.Width-menu.Padding.Horizontal);
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
        // DWM owns both the antialiased edge and its outline on Windows 11.
        // A second GDI curve has a different radius and leaves a visible inner rim.
        if (_nativeCorners) return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = Rounded(new RectangleF(.5f,.5f,e.ToolStrip.Width-1,e.ToolStrip.Height-1),10*Scale(e.ToolStrip));
        using var pen = new Pen(Palette.Drawing(197,209,220)); e.Graphics.DrawPath(pen,path);
    }
    protected override void OnRenderImageMargin(Forms.ToolStripRenderEventArgs e) { }
    protected override void OnRenderMenuItemBackground(Forms.ToolStripItemRenderEventArgs e)
    {
        if(e.ToolStrip is null)return;
        if(e.Item is Forms.ToolStripMenuItem {Checked:true})
        {
            float scale=Scale(e.ToolStrip);
            var box=new RectangleF(4*scale,scale,e.ToolStrip.ClientSize.Width-8*scale,e.Item.Height-2*scale);
            e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            using var path=Rounded(box,6*scale);
            using var fill=new SolidBrush(Palette.Drawing(237,245,255));e.Graphics.FillPath(fill,path);
        }
        DrawHighlight(e.Graphics,e.ToolStrip,e.Item.Bounds.Location);

    }
    void DrawHighlight(Graphics graphics, Forms.ToolStrip menu, Point origin)
    {
        var highlight=_selections.GetOrCreateValue(menu).Highlight;
        if(highlight.IsEmpty || highlight.Width<=0 || highlight.Height<=0)return;
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
            // Crossing a separator or the top/bottom padding briefly clears
            // native selection. Keep the last geometry so the next action can
            // animate from it instead of appearing instantly at the endpoint.
            return;
        }
        float s=Scale(menu);
        var target=new RectangleF(4*s,selected.Bounds.Top+1*s,menu.ClientSize.Width-8*s,selected.Height-2*s);
        if(!ReferenceEquals(selected,state.Selected) || target!=state.Target)
        {
            state.Selected=selected;
            state.From=state.Highlight.IsEmpty
                ? new RectangleF(target.X,target.Top+target.Height/2,target.Width,0)
                : state.Highlight;
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
        // Text and the moving background share the same selected row. Native
        // Selected briefly clears in the gutter even though our highlight remains.
        bool highlighted = e.ToolStrip is not null && IsHighlightedItem(e.ToolStrip, e.Item);
        e.TextColor = !e.Item.Enabled ? Muted : highlighted || e.Item.Selected || e.Item is Forms.ToolStripMenuItem {Checked:true} ? Blue : Ink;
        // Native padding can shift TextRectangle away from the row center.
        // Keep its horizontal text column but center in the full row height.
        float scale=Scale(e.ToolStrip);
        bool markers = e.ToolStrip?.Items.OfType<Forms.ToolStripMenuItem>().Any(i => i.CheckOnClick || i.Checked || i.HasDropDownItems) == true;
        e.TextRectangle=new Rectangle((int)(12*scale),0,Math.Max(1,RowWidth(e.ToolStrip,e.Item)-(int)((markers ? 40 : 24)*scale)),e.Item.Height);
        e.TextFormat=(e.TextFormat & ~Forms.TextFormatFlags.Bottom) | Forms.TextFormatFlags.VerticalCenter | Forms.TextFormatFlags.SingleLine;
        base.OnRenderItemText(e);
        // Paint last: native text rendering may clear its complete label lane.
        if(e.Item is Forms.ToolStripMenuItem {Checked:true})
        {
            var saved=e.Graphics.Save();
            try
            {
                e.Graphics.ResetClip();e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
                float cx=RowWidth(e.ToolStrip,e.Item)-14*scale,cy=e.Item.Height/2f;
                using var pen=new Pen(Blue,1.7f*scale){StartCap=LineCap.Round,EndCap=LineCap.Round,LineJoin=LineJoin.Round};
                e.Graphics.DrawLines(pen,[new PointF(cx-4*scale,cy),new PointF(cx-scale,cy+3*scale),new PointF(cx+5*scale,cy-4*scale)]);
            }
            finally { e.Graphics.Restore(saved); }
        }
    }
    protected override void OnRenderSeparator(Forms.ToolStripSeparatorRenderEventArgs e)
    {
        float s = Scale(e.ToolStrip);
        using var pen = new Pen(Palette.Drawing(221,230,238));
        e.Graphics.DrawLine(pen,12*s,e.Item.Height/2f,RowWidth(e.ToolStrip,e.Item)-12*s,e.Item.Height/2f);
    }
    // The selected state is drawn in the shared trailing column above.
    protected override void OnRenderItemCheck(Forms.ToolStripItemImageRenderEventArgs e) { }
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
        menu.Padding=new Forms.Padding(4,8,4,8);
        int menuScale = menu.DeviceDpi;
        menu.MinimumSize = new Size((int)Math.Round(176 * menuScale / 96.0), 0);
        if(menu is Forms.ToolStripDropDownMenu childMenu)
        {
            childMenu.ShowImageMargin=false;
            childMenu.ShowCheckMargin=false;
        }
        menu.ShowItemToolTips=false;
        menu.MouseMove += (_,e) => { if(menu.Visible) CloseOtherSubmenus(menu,e.Location); };
        foreach(Forms.ToolStripItem item in menu.Items)
        {
            item.Padding = item is Forms.ToolStripSeparator ? new Forms.Padding(0,3,0,3)
                : new Forms.Padding(8,8,12,8);
            if(item is Forms.ToolStripMenuItem entry)
            {
                entry.MouseEnter += (_,_) => { if(menu.Visible) UpdateHoveredItem(menu,entry); };
                if(entry.HasDropDownItems) Apply(entry.DropDown,true);
            }
        }
        void ClipWindow()
        {
            if(!menu.IsHandleCreated || menu.Width<24 || menu.Height<24)return;
            // DWM rounds the actual popup with antialiasing. An HRGN clips to
            // integer pixels, producing a staircase on curved edges.
            SetWindowRgn(menu.Handle,IntPtr.Zero,true);
            int preference=2; // DWMWCP_ROUND
            renderer._nativeCorners = DwmSetWindowAttribute(menu.Handle,33,ref preference,sizeof(int))==0;
            if(renderer._nativeCorners)
            {
                var ink=Palette.Drawing(197,209,220);
                int border=ink.R | ink.G<<8 | ink.B<<16;
                DwmSetWindowAttribute(menu.Handle,34,ref border,sizeof(int));
                int dark=Palette.Dark ? 1 : 0;
                DwmSetWindowAttribute(menu.Handle,20,ref dark,sizeof(int));
                menu.Invalidate();
                return;
            }
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
        var hovered = menu.GetItemAt(point);
        float scale = Scale(menu);
        if (hovered is null && point.X >= 4*scale && point.X < menu.ClientSize.Width-4*scale)
            hovered = menu.Items.OfType<Forms.ToolStripMenuItem>().FirstOrDefault(item =>
                point.Y >= item.Bounds.Top+scale && point.Y < item.Bounds.Bottom-scale);
        UpdateHoveredItem(menu,hovered);
    }
    static void UpdateHoveredItem(Forms.ToolStripDropDown menu, Forms.ToolStripItem? hovered)
    {
        // Item.MouseEnter can precede ToolStrip's own selection update, notably
        // at the first/last row. Synchronize it for the shared animation track.
        if(hovered is Forms.ToolStripMenuItem {Enabled:true}) hovered.Select();
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
