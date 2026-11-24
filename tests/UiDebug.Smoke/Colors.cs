// SPDX-License-Identifier: GPL-2.0-only
using System;
using System.Linq;
using System.Windows.Media;
using AetherDrop;

static partial class Check
{
    static double Luminance(Color color)
    {
        static double Linear(byte channel) { var value=channel/255.0; return value<=.04045 ? value/12.92 : Math.Pow((value+.055)/1.055,2.4); }
        return .2126*Linear(color.R)+.7152*Linear(color.G)+.0722*Linear(color.B);
    }
    static double Contrast(Color foreground, Color background)
    {
        var a=Luminance(foreground);var b=Luminance(background);
        return (Math.Max(a,b)+.05)/(Math.Min(a,b)+.05);
    }
    static void VerifyPalette()
    {
        foreach(var dark in new[]{false,true})
        {
            foreach(var ink in new[]{"Text","Secondary","Muted","Accent"})
                foreach(var surface in new[]{"Background","Surface","Raised","Hover","Selection"})
                {
                    var ratio=Contrast(UiColors.Get(ink,dark),UiColors.Get(surface,dark));
                    Assert(ratio>=4.5,$"{(dark?"Dark":"Light")} {ink} on {surface} has readable contrast ({ratio:F2}:1)");
                }
            foreach(var state in new[]{"Success","Warning","Error"})
            {
                Assert(Contrast(UiColors.Get(state,dark),UiColors.Get(state+"Bg",dark))>=4.5,"Semantic state text passes contrast on its matching background");
                Assert(Contrast(UiColors.Get(state,dark),UiColors.Get("Surface",dark))>=4.5,"Semantic state text passes contrast on the plain surface");
            }
            Assert(Contrast(UiColors.Get("OnAccent",dark),UiColors.Get("Accent",dark))>=4.5,"Primary-button foreground passes contrast");
            foreach(var saved in new[]{"","blue","indigo","teal","rose",Theme.Colourful})
            {
                var colors=Theme.Build(saved,dark);
                Assert(((SolidColorBrush)colors["Accent"]).Color==UiColors.Get("Accent",dark),"Legacy theme choices resolve to the single accent family");
            }
        }
        Theme.Build(forceDark:Palette.Dark);
    }
}
