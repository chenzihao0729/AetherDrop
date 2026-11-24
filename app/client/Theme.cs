// SPDX-License-Identifier: GPL-2.0-only
using System.Windows;
using System.Windows.Media;

namespace AetherDrop;

/// <summary>
/// The colours of the design (docs: the nekodrop UI prototype): a light and a
/// dark set after Windows 11 Mica, and one accent at a time, either Windows'
/// own or one of four the user can switch to in the settings.
/// </summary>
public static class Theme
{
    /// <summary>An accent: its colour on light and on dark, and the text drawn on it in dark mode.</summary>
    public sealed record AccentSet(string Key, Color Light, Color Dark, Color DarkOn);

    static Color C(uint argb) => Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);

    /// <summary>The accents offered besides Windows' own ("").</summary>
    public static readonly AccentSet[] Accents =
    [
        new("blue", UiColors.Get("Accent", false), UiColors.Get("Accent", true), UiColors.Get("OnAccent", true)),
        new("indigo", UiColors.Get("Accent", false), UiColors.Get("Accent", true), UiColors.Get("OnAccent", true)),
        new("teal", UiColors.Get("Accent", false), UiColors.Get("Accent", true), UiColors.Get("OnAccent", true)),
        new("rose", UiColors.Get("Accent", false), UiColors.Get("Accent", true), UiColors.Get("OnAccent", true)),
    ];

    public static bool Dark { get; private set; }

    /// <summary>The accent setting for colourful: Windows' accent, each state in its own colour.</summary>
    public const string Colourful = "colourful";

    /// <summary>
    /// The colours. In the default one-colour look, only the accent (and green
    /// for success, red for real errors) is used; colourful shows a declined
    /// send in amber and a failed one in red, as the design drew them.
    /// </summary>
    public static ResourceDictionary Build(string chosen = "", bool? forceDark = null, bool colourful = false)
    {
        var dark = Dark = forceDark ?? Palette.Dark;
        colourful |= chosen == Colourful;
        Color accent, onAccent;
        if (Accents.FirstOrDefault(a => a.Key == chosen) is { } set)
        {
            accent = dark ? set.Dark : set.Light;
            onAccent = dark ? set.DarkOn : Colors.White;
        }
        else
        {
            accent = UiColors.Get("Accent", dark);
            onAccent = UiColors.Get("OnAccent", dark);
        }
        SolidColorBrush B(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }

        var text = UiColors.Get("Text", dark);
        var text2 = UiColors.Get("Secondary", dark);
        var text3 = UiColors.Get("Muted", dark);
        var border = UiColors.Get("Border", dark);
        var borderStrong = UiColors.Get("StrongBorder", dark);
        var card = UiColors.Get("Surface", dark);
        var cardHover = UiColors.Get("Hover", dark);
        var success = UiColors.Get("Success", dark);
        var amber = UiColors.Get("Warning", dark);
        var error = UiColors.Get("Error", dark);

        var resources = new ResourceDictionary
        {
            // The design's names
            ["Accent"] = B(accent),
            ["AccentSubtle"] = B(UiColors.Get("Selection", dark)),
            ["OnAccent"] = B(onAccent),
            ["Mica"] = B(UiColors.Get("Background", dark)),
            ["MicaSolid"] = B(UiColors.Get("Background", dark)),
            ["MicaAlt"] = B(UiColors.Get("Surface", dark)),
            ["Card"] = B(card),
            ["NoticeSurface"] = B(UiColors.Get("Raised", dark)),
            ["CardHover"] = B(cardHover),
            ["PressTint"] = B(dark ? C(0x1AFFFFFF) : C(0x12000000)),
            ["RadarBg"] = B(UiColors.Get("Surface", dark)),
            ["Border"] = B(border),
            ["BorderStrong"] = B(borderStrong),
            ["Text"] = B(text),
            ["Text2"] = B(text2),
            ["Text3"] = B(text3),
            // Semantic colour is reserved for actual success, warning and error states.
            ["Success"] = B(success),
            ["SuccessBg"] = B(UiColors.Get("SuccessBg", dark)),
            ["Warn"] = B(amber),
            ["WarnBg"] = B(UiColors.Get("WarningBg", dark)),
            // A device's send that was declined or failed (the radar and the list).
            ["Declined"] = B(text3),
            ["Failed"] = B(error),
            ["FailedText"] = B(error),
            ["Error"] = B(error),
            ["ErrorBg"] = B(UiColors.Get("ErrorBg", dark)),
            // Older names still used by some windows
            ["AccentText"] = B(accent),
            ["TextSecondary"] = B(text2),
            ["Window"] = B(UiColors.Get("Background", dark)),
            ["CardStroke"] = B(border),
            ["Divider"] = B(border),
            ["DropWell"] = B(card),
            ["Caution"] = B(amber),
            ["Critical"] = B(error),
            ["UiFont"] = Typography.Regular,
            ["IconFont"] = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
        };
        Palette.AddResources(resources);
        return resources;
    }

    static Color Lighten(Color c, double t) =>
        Color.FromRgb((byte)(c.R + (255 - c.R) * t), (byte)(c.G + (255 - c.G) * t), (byte)(c.B + (255 - c.B) * t));
    static Color Darken(Color c, double t) =>
        Color.FromRgb((byte)(c.R * (1 - t)), (byte)(c.G * (1 - t)), (byte)(c.B * (1 - t)));
}
