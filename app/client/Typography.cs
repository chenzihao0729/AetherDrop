// SPDX-License-Identifier: GPL-2.0-only
using System.Windows;
using System.Windows.Media;

namespace AetherDrop;

/// <summary>Use Microsoft's installed YaHei faces, including its real Bold weight.</summary>
public static class Typography
{
    public static FontFamily Regular { get; } = new("Microsoft YaHei");
    public static FontWeight RegularWeight => FontWeights.Normal;
    public static FontWeight EmphasisWeight => FontWeights.Bold;
    /// <summary>Shared metrics and text rendering for every owned WPF window.</summary>
    public static void Apply(Window window)
    {
        window.FontFamily = Regular;
        window.FontWeight = RegularWeight;
        window.UseLayoutRounding = true;
        window.SnapsToDevicePixels = true;
        TextOptions.SetTextFormattingMode(window, TextFormattingMode.Display);
        TextOptions.SetTextHintingMode(window, TextHintingMode.Fixed);
        TextOptions.SetTextRenderingMode(window, window.AllowsTransparency ? TextRenderingMode.Grayscale : TextRenderingMode.Auto);
    }

    static readonly Lazy<System.Drawing.Font> Native = new(() =>
    {
        var font = new System.Drawing.Font("Microsoft YaHei", 10, System.Drawing.FontStyle.Regular);
        AppDomain.CurrentDomain.ProcessExit += (_, _) => font.Dispose();
        return font;
    });
    public static System.Drawing.Font MenuFont => Native.Value;
}
