// SPDX-License-Identifier: GPL-2.0-only
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace AetherDrop;

/// <summary>
/// The Apple ID AirDrop identity for Contacts Only, the way a Mac has one: an
/// Apple-signed certificate, its key, and the validation record that ties
/// them to the Apple ID, in %LocalAppData%\AetherDrop\appleid\identity. The
/// engine reads that folder (installer\AetherDrop_identity.py).
///
/// Two ways to get it:
/// - sign in to the Apple ID on this PC with WirDrop's appleid.exe (MIT), which
///   asks Apple for the certificate as a Mac's sharingd does, keeps it renewed
///   and reads the iCloud contacts;
/// - import what seemoo-lab's airdrop-keychain-extractor took from a Mac's
///   keychain (certificate.pem, key.pem, validation_record.cms), with the
///   contacts from a vCard file.
/// </summary>
public static partial class AppleId
{
    static string LocalAppData => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    public static string Out => Path.Combine(LocalAppData, "AetherDrop", "appleid");
    public static string Dir => Path.Combine(Out, "identity");
    static string WirDropDevice => Path.Combine(LocalAppData, "WirDrop", "device.txt");
    static string VirtualStoreAdi => Path.Combine(LocalAppData, "VirtualStore", "ProgramData", "Apple Computer", "iTunes", "adi");
    const string WirDropRegistryKey = @"Software\WirDrop";
    static string Exe => Path.Combine(AppContext.BaseDirectory, "appleid", "appleid.exe");

    /// <summary>appleid.exe is installed (it needs Rust at build time).</summary>
    public static bool CanSignIn => File.Exists(Exe);

    /// <summary>True when either AetherDrop's Apple ID folder or WirDrop's per-PC device file is on disk.</summary>
    public static bool HasLocalState => Directory.Exists(Out) || File.Exists(WirDropDevice);

    /// <summary>The experimental switch is on and there is an identity: the engine uses it.</summary>
    public static bool InUse => App.Instance.Settings.AppleIdOn && Present;

    /// <summary>Who can send to this PC right now: "off", "contacts" or "everyone".</summary>
    public static string Receiving => ResolveReceiving(App.Instance.Model.Allow, App.Instance.Settings.Receive, App.Instance.Settings.AppleIdOn, Present);

    /// <summary>Effective receiving scope: saved preference alone never enables Contacts Only.</summary>
    public static string ResolveReceiving(bool allow, string saved, bool appleIdOn, bool identityPresent) =>
        !allow ? "off" : saved == "contacts" && appleIdOn && identityPresent ? "contacts" : "everyone";

    public static bool Present =>
        File.Exists(Path.Combine(Dir, "certificate.der")) && File.Exists(Path.Combine(Dir, "key.pk8.der")) &&
        File.Exists(Path.Combine(Dir, "validation_record.cms"));

    /// <summary>"appleid" (signed in here) or "mac" (imported), "" without one.</summary>
    public static string Source => Read("source.txt").Trim();

    /// <summary>The Apple ID's e-mail as typed at sign-in, or "" (an import has none).</summary>
    public static string Account => Read("account.txt").Trim();

    public static int ContactCount =>
        Read("contact_hashes.txt").Split('\n').Count(l => l.Trim().Length == 64);

    /// <summary>When the certificate runs out, if it can be read.</summary>
    public static DateTime? CertificateUntil
    {
        get
        {
            try { using var c = new X509Certificate2(Path.Combine(Dir, "certificate.der")); return c.NotAfter; }
            catch (Exception) { return null; }
        }
    }

    static string Read(string name)
    {
        try { return File.ReadAllText(Path.Combine(Dir, name)); }
        catch (Exception) { return ""; }
    }

    static void Write(string name, string text) => File.WriteAllText(Path.Combine(Dir, name), text, new UTF8Encoding(false));

    /// <summary>
    /// Forgets the identity, the sign-in session, and the per-PC Anisette
    /// registration (device.txt, HKCU\Software\WirDrop, and CoreADI's
    /// VirtualStore adi folder) so the next sign-in provisions a fresh device
    /// identity. Keeps %LocalAppData%\WirDrop\icloud so it is not re-downloaded.
    /// </summary>
    public static void SignOut()
    {
        StopHelpers();
        if (Directory.Exists(Out)) Directory.Delete(Out, true);
        if (File.Exists(WirDropDevice)) File.Delete(WirDropDevice);
        if (Directory.Exists(VirtualStoreAdi)) Directory.Delete(VirtualStoreAdi, true);
        Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(WirDropRegistryKey, throwOnMissingSubKey: false);
    }

    static void StopHelpers()
    {
        var toolDir = Path.GetDirectoryName(Exe);
        if (string.IsNullOrEmpty(toolDir)) return;
        foreach (var name in new[] { "appleid", "appleid-anisette" })
        {
            foreach (var p in Process.GetProcessesByName(name))
            {
                try
                {
                    if (p.MainModule?.FileName is { } path && path.StartsWith(toolDir, StringComparison.OrdinalIgnoreCase))
                    {
                        p.Kill(entireProcessTree: true);
                        p.WaitForExit(3000);
                    }
                }
                catch (Exception) { }
                finally { p.Dispose(); }
            }
        }
    }

    /// <summary>The country of phone numbers written without one, for the contact hashes.</summary>
    static string CountryCode()
    {
        var region = RegionInfo.CurrentRegion.TwoLetterISORegionName;
        return region switch { "TW" => "886", "HK" => "852", "MO" => "853", "SG" => "65", "US" or "CA" => "1", "JP" => "81", "KR" => "82", _ => "86" };
    }

    static ProcessStartInfo Info(params string[] args)
    {
        Directory.CreateDirectory(Out);
        var psi = new ProcessStartInfo(Exe)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            WorkingDirectory = Path.GetDirectoryName(Exe)!,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        psi.Environment["APPLEID_OUT"] = Out;
        psi.Environment["APPLEID_LOG"] = Path.Combine(Out, "appleid.log");
        psi.Environment["APPLEID_CC"] = CountryCode();
        return psi;
    }

    /// <summary>
    /// Signs in (appleid.exe --json). Events go to <paramref name="say"/> as
    /// they come ("status", "need_code", "done", "later", "error");
    /// <paramref name="code"/> is asked for the verification code when Apple
    /// wants one. Returns null on success, else why not.
    /// </summary>
    public static async Task<string?> SignIn(string email, string password, Action<JsonObject> say, Func<bool, Task<string?>> code)
    {
        if (!CanSignIn) return L.T("aid.noTool");
        using var p = Process.Start(Info("--json"));
        if (p is null) return L.T("aid.noTool");
        await p.StandardInput.WriteLineAsync(new JsonObject { ["email"] = email, ["password"] = password, ["model"] = "MacBookPro18,3" }.ToJsonString());
        await p.StandardInput.FlushAsync();
        string? failure = L.T("aid.ended");
        while (await p.StandardOutput.ReadLineAsync() is { } line)
        {
            JsonObject? e;
            try { e = JsonNode.Parse(line) as JsonObject; } catch (System.Text.Json.JsonException) { continue; }
            if (e is null) continue;
            say(e);
            switch ((string?)e["ev"])
            {
                case "need_code":
                    var typed = await code((bool?)e["sent"] ?? false);
                    if (typed is null) { try { p.Kill(); } catch (Exception) { } return L.T("aid.cancelled"); }
                    await p.StandardInput.WriteLineAsync(new JsonObject { ["code"] = typed }.ToJsonString());
                    await p.StandardInput.FlushAsync();
                    break;
                case "done":
                case "later":
                    failure = null;
                    break;
                case "error":
                    failure = (string?)e["msg"] ?? L.T("aid.ended");
                    break;
            }
        }
        await p.WaitForExitAsync();
        if (failure is null)
        {
            Directory.CreateDirectory(Dir);
            Write("source.txt", "appleid");
            Write("account.txt", email);
        }
        return failure;
    }

    /// <summary>appleid.exe --refresh, then --contacts: 0 renewed or nothing to do, 2 signed out, 1 later.</summary>
    public static async Task<int> Refresh()
    {
        if (!CanSignIn || Source != "appleid") return 0;
        try
        {
            using var p = Process.Start(Info("--refresh"));
            if (p is null) return 1;
            p.StandardInput.Close();
            await p.WaitForExitAsync();
            if (p.ExitCode == 0)
            {
                using var c = Process.Start(Info("--contacts"));
                if (c is not null) { c.StandardInput.Close(); await c.WaitForExitAsync(); }
            }
            return p.ExitCode;
        }
        catch (Exception) { return 1; }
    }

    // ------------------------------------------------------- from a Mac

    /// <summary>
    /// Takes certificate.pem, key.pem and validation_record.cms from a folder
    /// (airdrop-keychain-extractor's output). Returns null when done, else why
    /// not; "need_passphrase" when the key is encrypted and none was given.
    /// </summary>
    public static string? ImportFromMac(string folder, string? passphrase)
    {
        string F(string n) => Path.Combine(folder, n);
        foreach (var n in new[] { "certificate.pem", "key.pem", "validation_record.cms" })
            if (!File.Exists(F(n))) return L.F("aid.missing", n);
        try
        {
            var certificate = X509Certificate2.CreateFromPem(File.ReadAllText(F("certificate.pem")));
            var keyText = File.ReadAllText(F("key.pem"));
            using var rsa = RSA.Create();
            if (keyText.Contains("ENCRYPTED"))
            {
                if (string.IsNullOrEmpty(passphrase)) return "need_passphrase";
                rsa.ImportFromEncryptedPem(keyText, passphrase);
            }
            else rsa.ImportFromPem(keyText);
            // The key must be the certificate's.
            using var withKey = certificate.CopyWithPrivateKey(rsa);
            var record = File.ReadAllBytes(F("validation_record.cms"));
            if (RecordHashes(record).Count == 0) return L.T("aid.badRecord");
            var contacts = Read("contact_hashes.txt");
            if (Directory.Exists(Out)) Directory.Delete(Out, true);
            Directory.CreateDirectory(Dir);
            File.WriteAllBytes(Path.Combine(Dir, "certificate.der"), certificate.RawData);
            File.WriteAllBytes(Path.Combine(Dir, "key.pk8.der"), rsa.ExportPkcs8PrivateKey());
            File.WriteAllBytes(Path.Combine(Dir, "validation_record.cms"), record);
            Write("hashes.txt", string.Join("\n", RecordHashes(record)));
            Write("source.txt", "mac");
            if (contacts.Length > 0) Write("contact_hashes.txt", contacts);
            return null;
        }
        catch (CryptographicException e) { return L.T("aid.badKey") + " " + e.Message; }
        catch (Exception e) { return e.Message; }
    }

    /// <summary>"email &lt;hex&gt;" and "phone &lt;hex&gt;" lines for the hashes a record vouches for.</summary>
    static List<string> RecordHashes(byte[] record)
    {
        var text = Encoding.Latin1.GetString(record);
        var lines = new List<string>();
        foreach (var (key, kind) in new[] { ("ValidatedEmailHashes", "email"), ("ValidatedPhoneHashes", "phone") })
        {
            var at = text.IndexOf("<key>" + key + "</key>", StringComparison.Ordinal);
            if (at < 0) continue;
            var start = text.IndexOf("<array>", at, StringComparison.Ordinal);
            var end = start < 0 ? -1 : text.IndexOf("</array>", start, StringComparison.Ordinal);
            if (end < 0) continue;
            var body = text[start..end];
            foreach (Match m in HexString().Matches(body)) lines.Add($"{kind} {m.Groups[1].Value.ToLowerInvariant()}");
            foreach (Match m in DataBlob().Matches(body))
            {
                try
                {
                    var raw = Convert.FromBase64String(Regex.Replace(m.Groups[1].Value, @"\s", ""));
                    if (raw.Length == 32) lines.Add($"{kind} {Convert.ToHexString(raw).ToLowerInvariant()}");
                }
                catch (FormatException) { }
            }
        }
        return lines;
    }

    [GeneratedRegex(@"<string>\s*([0-9A-Fa-f]{64})\s*</string>")]
    private static partial Regex HexString();
    [GeneratedRegex(@"<data>\s*([A-Za-z0-9+/=\s]+?)\s*</data>")]
    private static partial Regex DataBlob();

    // ------------------------------------------------------ the contacts

    /// <summary>
    /// The contacts from a vCard file (exported from Contacts on a Mac or an
    /// iPhone, or from iCloud.com): SHA-256 of each e-mail address and phone
    /// number in the spellings Apple may have hashed. Returns how many people.
    /// </summary>
    public static int ImportContacts(string vcf)
    {
        var text = File.ReadAllText(vcf);
        var hashes = new HashSet<string>();
        var people = 0;
        foreach (var raw in Regex.Split(text, "\r?\n(?![ \t])")) // unfolded lines
        {
            var line = raw.Replace("\r\n ", "").Replace("\n ", "");
            if (line.StartsWith("BEGIN:VCARD", StringComparison.OrdinalIgnoreCase)) { people++; continue; }
            var colon = line.IndexOf(':');
            if (colon < 0) continue;
            var name = line[..colon].Split(';')[0];
            var dot = name.LastIndexOf('.');
            if (dot >= 0) name = name[(dot + 1)..]; // item1.EMAIL
            var value = line[(colon + 1)..].Trim();
            if (name.Equals("EMAIL", StringComparison.OrdinalIgnoreCase))
                foreach (var s in new[] { value.ToLowerInvariant(), value }) hashes.Add(Sha(s));
            else if (name.Equals("TEL", StringComparison.OrdinalIgnoreCase))
                foreach (var s in PhoneSpellings(value.Replace("tel:", ""))) hashes.Add(Sha(s));
        }
        Directory.CreateDirectory(Dir);
        Write("contact_hashes.txt", string.Join("\n", hashes));
        return people;
    }

    static string Sha(string s) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s))).ToLowerInvariant();

    /// <summary>The same spellings as appleid.exe (patched by scripts\patch-appleid.py).</summary>
    static List<string> PhoneSpellings(string raw)
    {
        var digits = new string(raw.Where(char.IsAsciiDigit).ToArray());
        if (digits.Length < 5) return [];
        var cc = CountryCode();
        string e164 = raw.TrimStart().StartsWith('+') ? "+" + digits
            : digits.StartsWith("00") ? "+" + digits[2..]
            : digits.Length == 11 && digits[0] == '1' ? "+86" + digits
            : digits.StartsWith('0') ? "+" + cc + digits[1..]
            : "+" + digits;
        var out_ = new List<string> { e164, e164[1..], digits };
        if (e164.StartsWith("+" + cc)) out_.Add("0" + e164[(cc.Length + 1)..]);
        if (e164.StartsWith("+86")) out_.Add(e164[3..]);
        return out_;
    }
}
