// SPDX-License-Identifier: GPL-2.0-only
using System.IO;
using System.Text.Json;

namespace AetherDrop;

/// <summary>Per-user settings, in %LocalAppData%\AetherDrop\settings.json.</summary>
public sealed class Settings
{
    public string Appearance { get; set; } = "auto";
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsTransient { get; init; }
    public string DeviceName { get; set; } = Environment.MachineName;
    public string SaveFolder { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "AetherDrop");
    /// <summary>Ask before storing what someone sends (an AirDrop receiver's default).</summary>
    public bool Ask { get; set; } = true;
    /// <summary>Senders accepted without asking, by the name and model they give.</summary>
    public List<string> AlwaysAccept { get; set; } = [];
    /// <summary>The last senders that asked ("name|model", newest first), offered by Settings' Add (#45).</summary>
    public List<string> RecentSenders { get; set; } = [];
    public bool StartWithWindows { get; set; } = true;
    /// <summary>Visible to everyone for 10 minutes at a time, as on an iPhone.</summary>
    public bool Discoverable { get; set; } = true;
    /// <summary>"auto" (follow Windows), "zh" or "en".</summary>
    public string Language { get; set; } = "auto";
    /// <summary>The AirDrop receiver's mDNS instance name, kept so engine restarts stay one device.</summary>
    public string ServiceId { get; set; } = "";
    /// <summary>Look for a newer release now and then (never installs on its own).</summary>
    public bool CheckUpdates { get; set; } = false;
    /// <summary>Count pre-releases as updates (every AetherDrop release is one for now).</summary>
    public bool PreviewUpdates { get; set; } = true;
    /// <summary>Delete the downloaded installer after a successful update, so %TEMP% does not keep every release.</summary>
    public bool DeleteInstallerAfterInstall { get; set; } = true;
    /// <summary>This PC's icon on the radar: "auto" (a laptop when there is a battery), "desktop" or "laptop".</summary>
    public string HubIcon { get; set; } = "auto";
    /// <summary>The global shortcut (Win+Shift+D unless recorded otherwise) opens the send window (with Explorer's selection) or the main window.</summary>
    public bool Hotkey { get; set; } = true;
    /// <summary>The global shortcut's keys, as the user recorded them ("Ctrl+Alt+K").</summary>
    public string HotkeyKeys { get; set; } = AetherDrop.Hotkey.Default;
    /// <summary>Experimental: use the Apple ID identity (AppleId) at all. Off, the engine stays anonymous (Everyone).</summary>
    public bool AppleIdOn { get; set; }
    /// <summary>Who may send to this PC: "everyone", or "contacts" (needs AppleIdOn and an identity, see AppleId).</summary>
    public string Receive { get; set; } = "everyone";
    /// <summary>Nearby devices as a "radar" or a "list".</summary>
    public string DeviceView { get; set; } = "radar";
    /// <summary>
    /// The accent colour: "" follows Windows, blue, indigo, teal or rose; or
    /// "colourful", Windows' accent with each state in its own colour.
    /// </summary>
    public string Accent { get; set; } = "";
    /// <summary>A version the user chose to skip.</summary>
    public string SkipVersion { get; set; } = "";

    static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AetherDrop");
    static string FilePath => Path.Combine(Folder, "settings.json");
    static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static Settings Load()
    {
        LegacyMigration.CopyUserSettings(Folder);
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
