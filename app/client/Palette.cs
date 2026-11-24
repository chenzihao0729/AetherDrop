// SPDX-License-Identifier: GPL-2.0-only
using System.Windows;
using System.Windows.Media;
using System.ComponentModel;
using System.Windows.Data;
using System.Globalization;

namespace AetherDrop;

/// <summary>Shared light/dark semantic equivalents for every custom surface.</summary>
public static class Palette
{
    static readonly System.Collections.Concurrent.ConcurrentDictionary<Color,SolidColorBrush> Brushes = new();
    sealed class Signals : INotifyPropertyChanged
    {
        public int Revision { get; private set; }
        public event PropertyChangedEventHandler? PropertyChanged;
        public void Refresh() { Revision++; PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(nameof(Revision))); }
    }
    static readonly Signals Appearance = new();
    sealed class LiveColor(Func<Color> get) : IValueConverter
    {
        public object Convert(object value, Type type, object parameter, CultureInfo culture) => get();
        public object ConvertBack(object value, Type type, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }
    public static string Preference { get; private set; } = "auto";
    static bool _systemDark = Native.AppsUseDarkTheme();
    public static void RefreshSystemAppearance() => _systemDark = Native.AppsUseDarkTheme();
    public static void SetAppearance(string preference) { RefreshSystemAppearance(); Preference=preference; Appearance.Refresh(); }
    static SolidColorBrush LiveBrush(Func<Color> color)
    {
        var brush=new SolidColorBrush(color());
        BindingOperations.SetBinding(brush,SolidColorBrush.ColorProperty,new Binding(nameof(Signals.Revision))
            { Source=Appearance, Converter=new LiveColor(color), Mode=BindingMode.OneWay });
        return brush;
    }
    public static SolidColorBrush SemanticBrush(string role) => LiveBrush(()=>UiColors.Get(role,Dark));
    public static SolidColorBrush Brush(byte r,byte g,byte b) => Brushes.GetOrAdd(System.Windows.Media.Color.FromRgb(r,g,b),
        original=>LiveBrush(()=>Adapt(original)));
    public static bool Dark => Preference switch
    {
        "dark" => true, "light" => false, _ => Environment.GetEnvironmentVariable("AETHERDROP_APPEARANCE") switch
        { "dark"=>true, "light"=>false, _=>_systemDark },
    };
    public static Color Color(byte r, byte g, byte b) => Adapt(System.Windows.Media.Color.FromRgb(r,g,b));
    public static Color Adapt(Color light)
    {
        string key=$"{light.R:X2}{light.G:X2}{light.B:X2}";
        string? role = key switch
        {
            "007AFF" => "Accent",
            "1C2024" => "Text", "6C737A" or "5C656E" => "Secondary",
            "87929D" or "9EAFBE" or "AAAFB4" => "Muted",
            "FDFEFF" or "FAFDFF" or "FFFFFF" => "Raised",
            "F5FAFC" or "F6F9FC" => "Background",
            "F8FBFE" or "F5F9FD" => "Hover",
            "EDF5FF" or "D6E8FF" or "E3F0FF" or "E6F1FF" or "B8D9FF" or "B9D7FF" or "D3E5FC" => "Selection",
            "C5D1DC" or "C7D2DD" or "BCCBD8" or "C2C9D0" => "StrongBorder",
            "D2E0ED" or "DDE6EE" or "DFE7EE" or "E2EAF2" or "C4CFD8" => "Border",
            "E8EFF6" or "DEE4E9" => "Track",
            "BFC5C9" => "SwitchOff",
            _ => null,
        };
        return role is null ? light : UiColors.Get(role, Dark);
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
