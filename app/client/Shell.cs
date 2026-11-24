// SPDX-License-Identifier: GPL-2.0-only
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using Microsoft.Win32;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace AetherDrop;

/// <summary>Where AetherDrop meets Windows: notifications, Explorer, startup.</summary>
public static class Shell
{
    /// <summary>The app's identity for notifications; the Start menu shortcut carries the same one.</summary>
    public const string Aumid = "AetherDrop.app";

    static string Exe => Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "AetherDrop.exe");

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern int SetCurrentProcessExplicitAppUserModelID(string id);

    public static void SetIdentity() => SetCurrentProcessExplicitAppUserModelID(Aumid);

    /// <summary>
    /// Per-user registrations, safe to repeat at every start:
    /// "用 AetherDrop 发送" on files and folders (classic menu; Windows 11 shows it
    /// under "Show more options"), the AetherDrop: protocol that notification
    /// buttons use, Send To, and starting with Windows.
    /// </summary>
    public static void Register(bool startWithWindows)
    {
        Try(LegacyMigration.RemoveShellRegistrations);
        var exe = Exe;
        // Each step on its own: one refused write (security software guarding
        // the context menu) must not leave notifications without their
        // protocol (#17). The protocol goes first.
        Try(RegisterProtocol);
        Try(() => UpdateClassicMenu(!HasModernMenu()));
        Try(() =>
        {
            using var app = Registry.CurrentUser.CreateSubKey(@"Software\AetherDrop");
            app.SetValue("Language", L.Zh ? "zh" : "en");
        });
        Try(() =>
        {
            using var run = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
            run.DeleteValue("nekodrop", false);
            if (startWithWindows) run.SetValue("AetherDrop", $"\"{exe}\" --background");
            else run.DeleteValue("AetherDrop", false);
        });
        Try(() =>
        {
            var folder = Environment.GetFolderPath(Environment.SpecialFolder.SendTo);
            var legacy = Path.Combine(folder, "nekodrop.lnk");
            if (File.Exists(legacy)) File.Delete(legacy);
            CreateShortcut(Path.Combine(folder, "AetherDrop.lnk"), exe, "--send");
        });
        _ = RegisterMenuPackage();
    }

    /// <summary>Change only the startup preference; leave Explorer integration intact.</summary>
    public static void SetStartup(bool enabled)
    {
        using var run = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        run.DeleteValue("nekodrop", false);
        if (enabled) run.SetValue("AetherDrop", $"\"{Exe}\" --background");
        else run.DeleteValue("AetherDrop", false);
    }

    static void Try(Action step)
    {
        try { step(); } catch (Exception) { }
    }

    /// <summary>The AetherDrop: protocol that notification clicks and buttons open; checked again before each notification.</summary>
    static void RegisterProtocol()
    {
        var command = $"\"{Exe}\" \"%1\"";
        using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\AetherDrop"))
        using (var existing = key?.OpenSubKey(@"shell\open\command"))
            if (key?.GetValue("URL Protocol") is not null && existing?.GetValue("") as string == command) return;
        using var proto = Registry.CurrentUser.CreateSubKey(@"Software\Classes\AetherDrop");
        proto.SetValue("", "URL:AetherDrop");
        proto.SetValue("URL Protocol", "");
        using var cmd = proto.CreateSubKey(@"shell\open\command");
        cmd.SetValue("", command);
    }

    const string MenuPackage = "AetherDrop.shell";
    const string LegacyMenuPackage = "nekodrop.shell";

    static IEnumerable<Windows.ApplicationModel.Package> MenuPackages() =>
        new Windows.Management.Deployment.PackageManager().FindPackagesForUser(string.Empty)
            .Where(p => p.Id.Name is MenuPackage or LegacyMenuPackage);

    static bool HasModernMenu() => OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000) &&
        MenuPackages().Any(p => p.Id.Name == MenuPackage);

    static void UpdateClassicMenu(bool visible)
    {
        using var classes = Registry.CurrentUser.CreateSubKey(@"Software\Classes");
        foreach (var target in new[] { @"*\shell\AetherDrop", @"Directory\shell\AetherDrop" })
        {
            if (!visible) { classes.DeleteSubKeyTree(target, false); continue; }
            using var key = classes.CreateSubKey(target);
            key.SetValue("", L.T("verb"));
            key.SetValue("Icon", $"\"{Exe}\",0");
            key.SetValue("MultiSelectModel", "Player");
            using var cmd = key.CreateSubKey("command");
            cmd.SetValue("", $"\"{Exe}\" --send \"%1\"");
        }
    }

    /// <summary>
    /// Windows 11 puts "用 AetherDrop 发送" on the first level of the context menu
    /// only for a packaged app: registers, for this user, the sparse package
    /// that install.ps1 signed (external location = the install folder). Without
    /// it the classic verb above still works under "Show more options".
    /// </summary>
    static async Task RegisterMenuPackage()
    {
        try
        {
            var manager = new Windows.Management.Deployment.PackageManager();
            foreach (var old in MenuPackages().Where(p => p.Id.Name == LegacyMenuPackage).ToList())
                await manager.RemovePackageAsync(old.Id.FullName);
            var dir = Path.GetDirectoryName(Exe)!;
            var msix = Path.Combine(dir, "shell", "AetherDrop-shell.msix");
            if (!File.Exists(msix) || !File.Exists(Path.Combine(dir, "AetherDrop-shell.dll"))) return;
            // Registered already, unless with an older manifest (0.2: the Share sheet).
            if (MenuPackages().Any(p => p.Id.Version.Major > 0 || p.Id.Version.Minor >= 2)) return;
            var options = new Windows.Management.Deployment.AddPackageOptions
            {
                ExternalLocationUri = new Uri(dir + Path.DirectorySeparatorChar),
                ForceAppShutdown = true,
            };
            await new Windows.Management.Deployment.PackageManager().AddPackageByUriAsync(new Uri(msix), options);
        }
        catch (Exception) { /* unsigned (a development run) or sideloading turned off */ }
        finally
        {
            // The sparse Windows 11 verb and the registry verb are alternatives.
            // Keeping both registered displays two identical "send" commands.
            Try(() => UpdateClassicMenu(!HasModernMenu()));
        }
    }

    public static void Unregister()
    {
        Try(LegacyMigration.RemoveShellRegistrations);
        var classes = Registry.CurrentUser.OpenSubKey(@"Software\Classes", true);
        classes?.DeleteSubKeyTree(@"*\shell\AetherDrop", false);
        classes?.DeleteSubKeyTree(@"Directory\shell\AetherDrop", false);
        classes?.DeleteSubKeyTree("AetherDrop", false);
        Registry.CurrentUser.DeleteSubKeyTree(@"Software\AetherDrop", false);
        using var run = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
        run?.DeleteValue("nekodrop", false);
        run?.DeleteValue("AetherDrop", false);
        var sendTo = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.SendTo), "AetherDrop.lnk");
        if (File.Exists(sendTo)) File.Delete(sendTo);
        try
        {
            var pm = new Windows.Management.Deployment.PackageManager();
            foreach (var p in MenuPackages().ToList())
                pm.RemovePackageAsync(p.Id.FullName).AsTask().Wait(TimeSpan.FromSeconds(30));
        }
        catch (Exception) { }
    }

    static void CreateShortcut(string path, string target, string args)
    {
        var type = Type.GetTypeFromProgID("WScript.Shell");
        if (type is null) return;
        dynamic shell = Activator.CreateInstance(type)!;
        dynamic link = shell.CreateShortcut(path);
        link.TargetPath = target;
        link.Arguments = args;
        link.IconLocation = target + ",0";
        link.Save();
    }

    static string X(string s) => SecurityElement.Escape(s) ?? "";

    static void Show(string xml, string? tag = null)
    {
        Try(RegisterProtocol);
        try
        {
            var doc = new XmlDocument();
            doc.LoadXml(xml);
            var toast = new ToastNotification(doc);
            if (tag is not null) { toast.Tag = tag; toast.Group = "AetherDrop"; }
            ToastNotificationManager.CreateToastNotifier(Aumid).Show(toast);
        }
        catch (Exception) { /* no Start menu identity yet (a development run) */ }
    }

    /// <summary>"iPhone 想发送 2 张照片" with 接受 / 拒绝, answered through AetherDrop:answer.</summary>
    public static void AskToast(string id, string sender, string what)
    {
        var accept = X($"AetherDrop:answer?id={Uri.EscapeDataString(id)}&accept=1");
        var decline = X($"AetherDrop:answer?id={Uri.EscapeDataString(id)}&accept=0");
        Show($"""
            <toast scenario="reminder" launch="{X("AetherDrop:open")}" activationType="protocol">
              <visual><binding template="ToastGeneric">
                <text>{X(L.F("ask.title", sender))}</text>
                <text>{X(what)}</text>
              </binding></visual>
              <actions>
                <action content="{X(L.T("accept"))}" arguments="{accept}" activationType="protocol"/>
                <action content="{X(L.T("decline"))}" arguments="{decline}" activationType="protocol"/>
              </actions>
            </toast>
            """, "ask-" + id);
    }

    /// <summary>"AetherDrop 0.1.17 is available" with Update / Later.</summary>
    public static void UpdateToast(string version, bool preview)
    {
        Show($"""
            <toast launch="{X("AetherDrop:update")}" activationType="protocol">
              <visual><binding template="ToastGeneric">
                <text>{X(L.F("update.title", version))}</text>
                <text>{X(L.T(preview ? "update.bodyPreview" : "update.body"))}</text>
              </binding></visual>
              <actions>
                <action content="{X(L.T("update.now"))}" arguments="{X("AetherDrop:update")}" activationType="protocol"/>
                <action content="{X(L.T("update.later"))}" arguments="dismiss" activationType="system"/>
              </actions>
            </toast>
            """, "update");
    }

    public static void DoneToast(string id, string title, string body, string? folder)
    {
        var launch = folder is null ? "AetherDrop:open" : $"AetherDrop:folder?path={Uri.EscapeDataString(folder)}";
        Show($"""
            <toast launch="{X(launch)}" activationType="protocol">
              <visual><binding template="ToastGeneric">
                <text>{X(title)}</text>
                <text>{X(body)}</text>
              </binding></visual>
            </toast>
            """);
        try { ToastNotificationManager.History.Remove("ask-" + id, "AetherDrop", Aumid); }
        catch (Exception) { }
    }

    [DllImport("user32.dll")]
    static extern IntPtr GetForegroundWindow();

    /// <summary>
    /// The files selected in the Explorer window in front, for the hotkey;
    /// none when Explorer is not in front or nothing is selected.
    /// </summary>
    public static string[] ExplorerSelection()
    {
        try
        {
            var front = GetForegroundWindow();
            if (Type.GetTypeFromProgID("Shell.Application") is not { } type || Activator.CreateInstance(type) is not { } shellApp) return [];
            dynamic shell = shellApp;
            foreach (dynamic window in shell.Windows())
            {
                try
                {
                    if (new IntPtr(Convert.ToInt64(window.HWND)) != front) continue;
                    var picked = new List<string>();
                    foreach (dynamic item in window.Document.SelectedItems()) picked.Add((string)item.Path);
                    return [.. picked];
                }
                catch (Exception) { /* not a folder window */ }
            }
        }
        catch (Exception) { }
        return [];
    }

    /// <summary>
    /// When Windows started AetherDrop from the Share sheet (the sparse package's
    /// share target): the shared files' paths, and the sheet is told it is done.
    /// None for an ordinary start.
    /// </summary>
    public static string[] SharedFiles()
    {
        try
        {
            if (Windows.ApplicationModel.AppInstance.GetActivatedEventArgs() is not
                Windows.ApplicationModel.Activation.ShareTargetActivatedEventArgs share) return [];
            var op = share.ShareOperation;
            var items = op.Data.GetStorageItemsAsync().AsTask().GetAwaiter().GetResult();
            var paths = items.Select(i => i.Path).Where(p => !string.IsNullOrEmpty(p)).ToArray();
            op.ReportCompleted();
            return paths;
        }
        catch (Exception) { return []; /* not packaged, or not a share */ }
    }
}
