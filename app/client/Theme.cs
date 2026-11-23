// SPDX-License-Identifier: GPL-2.0-only
using System.Windows;
using System.Windows.Media;

namespace NekoDrop;

/// <summary>
/// Colors after the Windows 11 design (Fluent): follow the system light/dark
/// setting, with the shared blue accent; Microsoft YaHei
/// for text and Segoe Fluent Icons for symbolic device glyphs.
/// </summary>
public static class Theme
{
    public static ResourceDictionary Build()
    {
        var dark = Palette.Dark;
        var accent = Palette.Color(0,122,255);
        SolidColorBrush B(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }
        var d = new ResourceDictionary
        {
            ["Accent"] = B(accent),
            ["AccentText"] = B(dark ? Lighten(accent, 0.45) : Darken(accent, 0.15)),
            ["OnAccent"] = B(Colors.White),
            ["Text"] = B(Palette.Color(28,32,36)),
            ["TextSecondary"] = B(Palette.Color(108,115,122)),
            ["Window"] = B(Palette.Color(245,250,252)),
            ["Card"] = B(Palette.Color(255,255,255)),
            ["CardHover"] = B(Palette.Color(230,241,255)),
            ["CardStroke"] = B(Palette.Color(223,231,238)),
            ["Divider"] = B(Palette.Color(221,230,238)),
            ["DropWell"] = B(Palette.Color(248,251,254)),
            ["UiFont"] = Typography.Regular,
            ["IconFont"] = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
        };
        foreach (var type in new[] { typeof(System.Windows.Controls.ToolTip), typeof(System.Windows.Controls.ContextMenu) })
        {
            var style = new Style(type);
            style.Setters.Add(new Setter(System.Windows.Controls.Control.FontFamilyProperty, Typography.Regular));
            d.Add(type, style);
        }
        Palette.AddResources(d);
        return d;
    }

    static Color Lighten(Color c, double t) =>
        Color.FromRgb((byte)(c.R + (255 - c.R) * t), (byte)(c.G + (255 - c.G) * t), (byte)(c.B + (255 - c.B) * t));
    static Color Darken(Color c, double t) =>
        Color.FromRgb((byte)(c.R * (1 - t)), (byte)(c.G * (1 - t)), (byte)(c.B * (1 - t)));
}
