// SPDX-License-Identifier: GPL-2.0-only
using System.Windows.Media;

namespace AetherDrop;

/// <summary>One accent family and neutral surface steps shared by WPF and native menus.</summary>
public static class UiColors
{
    public static Color Get(string role, bool dark)
    {
        var pair = role switch
        {
            "Accent" => ("315F8C", "8FB4D8"),
            "OnAccent" => ("FFFFFF", "17212B"),
            "Background" => ("F5F5F4", "202122"),
            "Surface" => ("FFFFFF", "2A2C2E"),
            "Raised" => ("FFFFFF", "303234"),
            "Hover" => ("ECEEED", "36393B"),
            "Selection" => ("E6EDF3", "303D49"),
            "Text" => ("242729", "EFF0EF"),
            "Secondary" => ("565C60", "BCC0C2"),
            "Muted" => ("646B70", "A4ABAF"),
            "Border" => ("D9DCDA", "45494B"),
            "StrongBorder" => ("B7BEBC", "646C70"),
            "Track" => ("DFE3E1", "42484B"),
            "SwitchOff" => ("8B9499", "687176"),
            "Success" => ("397052", "91B89C"),
            "SuccessBg" => ("E9F0EB", "2D3B32"),
            "Warning" => ("85602B", "CEAF7C"),
            "WarningBg" => ("F3EDE3", "40382C"),
            "Error" => ("A34A45", "D99A94"),
            "ErrorBg" => ("F5EAE8", "432F2E"),
            "StoppedMark" => ("984C46", "BD7972"),
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown colour role"),
        };
        var hex = dark ? pair.Item2 : pair.Item1;
        return Color.FromRgb(Convert.ToByte(hex[..2],16), Convert.ToByte(hex[2..4],16), Convert.ToByte(hex[4..],16));
    }
}
