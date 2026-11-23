// SPDX-License-Identifier: GPL-2.0-only
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace NekoDrop;

public static class Branding
{
    public const string Name = "AetherDrop";
    public static ImageSource WindowIcon { get; } = LoadImage("AetherDrop.ico");
    public static ImageSource Logo { get; } = LoadImage("AetherDrop-logo.png");
    public static ImageSource TrayPanelLogo { get; } = LoadImage("AirDrop_logo_none.png");

    static Uri Asset(string name) => new($"pack://application:,,,/AetherDrop;component/Assets/{name}");

    static ImageSource LoadImage(string name)
    {
        var image = new BitmapImage(Asset(name));
        image.Freeze();
        return image;
    }

    public static System.Drawing.Icon TrayIcon()
    {
        var resource = Application.GetResourceStream(Asset("AetherDrop-tray.ico"))
            ?? throw new InvalidOperationException("AetherDrop tray icon is missing.");
        using var stream = resource.Stream;
        using var icon = new System.Drawing.Icon(stream, System.Windows.Forms.SystemInformation.SmallIconSize);
        return (System.Drawing.Icon)icon.Clone();
    }
}
