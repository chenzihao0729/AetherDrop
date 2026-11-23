// SPDX-License-Identifier: GPL-2.0-only
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using Microsoft.Win32;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace NekoDrop;

/// <summary>Where nekodrop meets Windows: notifications, Explorer, startup.</summary>
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
    /// "用 nekodrop 发送" on files and folders (classic menu; Windows 11 shows it
    /// under "Show more options"), the nekodrop: protocol that notification
    /// buttons use, Send To, and starting with Windows.
    /// </summary>
    public static void Register(bool startWithWindows)
    {
        var exe = Exe;
        // Each step on its own: one refused write (security software guarding
        // the context menu) must not leave notifications without their
        // protocol (#17). The protocol goes first.
        Try(RegisterProtocol);
        Try(() =>
        {
            using var classes = Registry.CurrentUser.CreateSubKey(@"Software\Classes");
            foreach (var target in new[] { @"*\shell\nekodrop", @"Directory\shell\nekodrop" })
            {
                using var key = classes.CreateSubKey(target);
                key.SetValue("", L.T("verb"));
                key.SetValue("Icon", $"\"{exe}\",0");
                key.SetValue("MultiSelectModel", "Player");
                using var cmd = key.CreateSubKey("command");
                cmd.SetValue("", $"\"{exe}\" --send \"%1\"");
            }
        });
        Try(() =>
        {
            using var app = Registry.CurrentUser.CreateSubKey(@"Software\nekodrop");
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

    static void Try(Action step)
    {
        try { step(); } catch (Exception) { }
    }

    /// <summary>The nekodrop: protocol that notification clicks and buttons open; checked again before each notification.</summary>
    static void RegisterProtocol()
    {
        var command = $"\"{Exe}\" \"%1\"";
        using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\nekodrop"))
        using (var existing = key?.OpenSubKey(@"shell\open\command"))
            if (key?.GetValue("URL Protocol") is not null && existing?.GetValue("") as string == command) return;
        using var proto = Registry.CurrentUser.CreateSubKey(@"Software\Classes\nekodrop");
        proto.SetValue("", "URL:nekodrop");
        proto.SetValue("URL Protocol", "");
        using var cmd = proto.CreateSubKey(@"shell\open\command");
        cmd.SetValue("", command);
    }

    const string MenuPackage = "nekodrop.shell";

    static IEnumerable<Windows.ApplicationModel.Package> MenuPackages() =>
        new Windows.Management.Deployment.PackageManager().FindPackagesForUser(string.Empty)
            .Where(p => p.Id.Name == MenuPackage);

    /// <summary>
    /// Windows 11 puts "用 nekodrop 发送" on the first level of the context menu
    /// only for a packaged app: registers, for this user, the sparse package
    /// that install.ps1 signed (external location = the install folder). Without
    /// it the classic verb above still works under "Show more options".
    /// </summary>
    static async Task RegisterMenuPackage()
    {
        try
        {
            var dir = Path.GetDirectoryName(Exe)!;
            var msix = Path.Combine(dir, "shell", "nekodrop-shell.msix");
            if (!File.Exists(msix) || !File.Exists(Path.Combine(dir, "nekodrop-shell.dll"))) return;
            if (MenuPackages().Any()) return;
            var options = new Windows.Management.Deployment.AddPackageOptions
            {
                ExternalLocationUri = new Uri(dir + Path.DirectorySeparatorChar),
                ForceAppShutdown = true,
            };
            await new Windows.Management.Deployment.PackageManager().AddPackageByUriAsync(new Uri(msix), options);
        }
        catch (Exception) { /* unsigned (a development run) or sideloading turned off */ }
    }

    public static void Unregister()
    {
        var classes = Registry.CurrentUser.OpenSubKey(@"Software\Classes", true);
        classes?.DeleteSubKeyTree(@"*\shell\nekodrop", false);
        classes?.DeleteSubKeyTree(@"Directory\shell\nekodrop", false);
        classes?.DeleteSubKeyTree("nekodrop", false);
        Registry.CurrentUser.DeleteSubKeyTree(@"Software\nekodrop", false);
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
            if (tag is not null) { toast.Tag = tag; toast.Group = "nekodrop"; }
            ToastNotificationManager.CreateToastNotifier(Aumid).Show(toast);
        }
        catch (Exception) { /* no Start menu identity yet (a development run) */ }
    }

    /// <summary>"iPhone 想发送 2 张照片" with 接受 / 拒绝, answered through nekodrop:answer.</summary>
    public static void AskToast(string id, string sender, string what)
    {
        var accept = X($"nekodrop:answer?id={Uri.EscapeDataString(id)}&accept=1");
        var decline = X($"nekodrop:answer?id={Uri.EscapeDataString(id)}&accept=0");
        Show($"""
            <toast scenario="reminder" launch="{X("nekodrop:open")}" activationType="protocol">
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

    /// <summary>"nekodrop 0.1.17 is available" with Update / Later.</summary>
    public static void UpdateToast(string version, bool preview)
    {
        Show($"""
            <toast launch="{X("nekodrop:update")}" activationType="protocol">
              <visual><binding template="ToastGeneric">
                <text>{X(L.F("update.title", version))}</text>
                <text>{X(L.T(preview ? "update.bodyPreview" : "update.body"))}</text>
              </binding></visual>
              <actions>
                <action content="{X(L.T("update.now"))}" arguments="{X("nekodrop:update")}" activationType="protocol"/>
                <action content="{X(L.T("update.later"))}" arguments="dismiss" activationType="system"/>
              </actions>
            </toast>
            """, "update");
    }

    public static void DoneToast(string id, string title, string body, string? folder)
    {
        var launch = folder is null ? "nekodrop:open" : $"nekodrop:folder?path={Uri.EscapeDataString(folder)}";
        Show($"""
            <toast launch="{X(launch)}" activationType="protocol">
              <visual><binding template="ToastGeneric">
                <text>{X(title)}</text>
                <text>{X(body)}</text>
              </binding></visual>
            </toast>
            """);
        try { ToastNotificationManager.History.Remove("ask-" + id, "nekodrop", Aumid); }
        catch (Exception) { }
    }
}
