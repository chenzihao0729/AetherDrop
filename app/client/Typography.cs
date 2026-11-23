// SPDX-License-Identifier: GPL-2.0-only
using System.Windows;
using System.Windows.Media;

namespace NekoDrop;

/// <summary>Use Microsoft's installed YaHei faces, including its real Bold weight.</summary>
public static class Typography
{
    public static FontFamily Regular { get; } = new("Microsoft YaHei");
    public static FontWeight RegularWeight => FontWeights.Normal;
    public static FontWeight EmphasisWeight => FontWeights.Bold;
    static readonly Lazy<System.Drawing.Font> Native = new(() =>
    {
        var font = new System.Drawing.Font("Microsoft YaHei", 10, System.Drawing.FontStyle.Regular);
        AppDomain.CurrentDomain.ProcessExit += (_, _) => font.Dispose();
        return font;
    });
    public static System.Drawing.Font MenuFont => Native.Value;
}
