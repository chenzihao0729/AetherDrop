// SPDX-License-Identifier: GPL-2.0-only
using System.IO;
using System.Text.Json;

namespace NekoDrop;

/// <summary>Per-user settings, in %LocalAppData%\nekodrop\settings.json.</summary>
public sealed class Settings
{
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsTransient { get; init; }
    public string DeviceName { get; set; } = Environment.MachineName;
    public string SaveFolder { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "AetherDrop");
    /// <summary>Ask before storing what someone sends (an AirDrop receiver's default).</summary>
    public bool Ask { get; set; } = true;
    /// <summary>Senders accepted without asking, by the name and model they give.</summary>
    public List<string> AlwaysAccept { get; set; } = [];
    public bool StartWithWindows { get; set; } = true;
    /// <summary>Visible to everyone for 10 minutes at a time, as on an iPhone.</summary>
    public bool Discoverable { get; set; } = true;
    /// <summary>"auto" (follow Windows), "zh" or "en".</summary>
    public string Language { get; set; } = "auto";
    /// <summary>The AirDrop receiver's mDNS instance name, kept so engine restarts stay one device.</summary>
    public string ServiceId { get; set; } = "";
    /// <summary>Look for a newer release now and then (never installs on its own).</summary>
    public bool CheckUpdates { get; set; } = false;
    /// <summary>Count pre-releases as updates (every nekodrop release is one for now).</summary>
    public bool PreviewUpdates { get; set; } = true;
    /// <summary>A version the user chose to skip.</summary>
    public string SkipVersion { get; set; } = "";

    static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "nekodrop");
    static string FilePath => Path.Combine(Folder, "settings.json");
    static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath), Options) ?? new();
                var oldDefault = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "nekodrop");
                if (string.Equals(settings.SaveFolder.TrimEnd('\\', '/'), oldDefault, StringComparison.OrdinalIgnoreCase))
                {
                    settings.SaveFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "AetherDrop");
                    settings.Save();
                }
                return settings;
            }
        }
        catch (Exception) { }
        return new();
    }

    public void Save()
    {
        if (IsTransient) return;
        Directory.CreateDirectory(Folder);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Options));
    }
}
