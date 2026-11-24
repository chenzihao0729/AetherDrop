// SPDX-License-Identifier: GPL-2.0-only
using System.IO;
using Microsoft.Win32;

namespace AetherDrop;

/// <summary>Old names identify settings to import and registrations to remove.</summary>
internal static class LegacyMigration
{
    internal const string Name = "nekodrop";

    internal static void CopyUserSettings(string destination, string? source = null)
    {
        source ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Name);
        if (!Directory.Exists(source) || File.Exists(Path.Combine(destination, ".migration-complete"))) return;
        try
        {
            CopyMissing(source, destination);
            File.WriteAllText(Path.Combine(destination, ".migration-complete"), "AetherDrop");
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    static void CopyMissing(string source, string destination)
    {
        if ((File.GetAttributes(source) & FileAttributes.ReparsePoint) != 0) return;
        Directory.CreateDirectory(destination);
        if ((File.GetAttributes(destination) & FileAttributes.ReparsePoint) != 0) throw new IOException("Settings directory is a link");
        foreach (var file in Directory.EnumerateFiles(source))
        {
            var target = Path.Combine(destination, Path.GetFileName(file));
            if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) == 0 && !File.Exists(target))
                File.Copy(file, target);
        }
        foreach (var directory in Directory.EnumerateDirectories(source))
            CopyMissing(directory, Path.Combine(destination, Path.GetFileName(directory)));
    }

    internal static void RemoveShellRegistrations()
    {
        using var classes = Registry.CurrentUser.OpenSubKey(@"Software\Classes", true);
        foreach (var key in new[] { @"*\shell\" + Name, @"Directory\shell\" + Name, Name })
            classes?.DeleteSubKeyTree(key, false);
        Registry.CurrentUser.DeleteSubKeyTree(@"Software\" + Name, false);
        using var run = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
        run?.DeleteValue(Name, false);
        var shortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.SendTo), Name + ".lnk");
        if (File.Exists(shortcut)) File.Delete(shortcut);
    }
}
