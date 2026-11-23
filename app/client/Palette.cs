// SPDX-License-Identifier: GPL-2.0-only
using System.Windows;
using System.Windows.Media;

namespace NekoDrop;

/// <summary>Shared light/dark semantic equivalents for every custom surface.</summary>
public static class Palette
{
    static readonly System.Collections.Concurrent.ConcurrentDictionary<Color,SolidColorBrush> Brushes = new();
    static readonly Lazy<bool> SystemDark = new(Native.AppsUseDarkTheme);
    public static SolidColorBrush Brush(byte r,byte g,byte b) => Brushes.GetOrAdd(Color(r,g,b),color =>
    {
        var brush=new SolidColorBrush(color);brush.Freeze();return brush;
    });
    public static bool Dark => Environment.GetEnvironmentVariable("AETHERDROP_APPEARANCE") switch
    {
        "dark" => true, "light" => false, _ => SystemDark.Value,
    };
    public static Color Color(byte r, byte g, byte b) => Adapt(System.Windows.Media.Color.FromRgb(r,g,b));
    public static Color Adapt(Color light)
    {
        if (!Dark) return light;
        string key=$"{light.R:X2}{light.G:X2}{light.B:X2}";
        string hex=key switch
        {
            "1C2024" => "EEF3F8", "6C737A" or "5C656E" => "AAB7C5", "87929D" => "91A1B2",
            "007AFF" => "409CFF", "FDFEFF" => "202731", "F5FAFC" or "F6F9FC" => "192029",
            "F8FBFE" or "F5F9FD" => "252F3B", "FFFFFF" => "202A35",
            "EDF5FF" => "20354E", "D6E8FF" => "243F60", "E3F0FF" or "E6F1FF" => "2B4562",
            "B8D9FF" => "345C85", "B9D7FF" or "D3E5FC" => "365777",
            "C5D1DC" or "C7D2DD" or "BCCBD8" or "C2C9D0" => "455363",
            "D2E0ED" or "DDE6EE" or "DFE7EE" or "E2EAF2" or "C4CFD8" => "354252",
            "E8EFF6" or "DEE4E9" => "3B4857", "9EAFBE" => "6B7F94",
            "BFC5C9" => "526172", "AAAFB4" => "8291A1", "FAFDFF" => "202A35",
            _ => key,
        };
        return System.Windows.Media.Color.FromRgb(Convert.ToByte(hex[..2],16),Convert.ToByte(hex[2..4],16),Convert.ToByte(hex[4..],16));
    }
    public static System.Drawing.Color Drawing(byte r,byte g,byte b)
    {
        var color=Color(r,g,b);return System.Drawing.Color.FromArgb(color.R,color.G,color.B);
    }
    public static void AddResources(ResourceDictionary resources)
    {
        const string colors="007AFF 1C2024 6C737A 87929D 9EAFBE B8D9FF B9D7FF BCCBD8 C5D1DC C7D2DD D2E0ED D3E5FC D6E8FF DDE6EE DFE7EE E2EAF2 E6F1FF E8EFF6 EDF5FF F5F9FD F5FAFC F8FBFE FDFEFF FFFFFF";
        foreach(var hex in colors.Split(' '))
        {
            var color=Color(Convert.ToByte(hex[..2],16),Convert.ToByte(hex[2..4],16),Convert.ToByte(hex[4..],16));
            resources["ToneColor"+hex]=color;
            var brush=new SolidColorBrush(color);brush.Freeze();resources["Tone"+hex]=brush;
        }
    }
}
