using AetherDrop;

var root = Path.Combine(Path.GetTempPath(), "AetherDrop-migration-test-" + Guid.NewGuid().ToString("N"));
var source = Path.Combine(root, "legacy");
var destination = Path.Combine(root, "current");
try
{
    Directory.CreateDirectory(Path.Combine(source, "identity"));
    Directory.CreateDirectory(destination);
    File.WriteAllText(Path.Combine(source, "settings.json"), "old settings");
    File.WriteAllText(Path.Combine(source, "identity", "certificate.pem"), "saved identity");
    File.WriteAllText(Path.Combine(source, "keep.txt"), "old");
    File.WriteAllText(Path.Combine(destination, "keep.txt"), "current");
    LegacyMigration.CopyUserSettings(destination, source);
    Assert(File.ReadAllText(Path.Combine(destination, "settings.json")) == "old settings", "imports settings");
    Assert(File.ReadAllText(Path.Combine(destination, "identity", "certificate.pem")) == "saved identity", "imports nested identity");
    Assert(File.ReadAllText(Path.Combine(destination, "keep.txt")) == "current", "preserves current files");
    File.Delete(Path.Combine(destination, "settings.json"));
    LegacyMigration.CopyUserSettings(destination, source);
    Assert(!File.Exists(Path.Combine(destination, "settings.json")), "does not resurrect deliberately removed files");
    Assert(File.Exists(Path.Combine(source, "settings.json")), "preserves original backup");
}
finally { if (Directory.Exists(root)) Directory.Delete(root, true); }

static void Assert(bool condition, string name)
{
    if (!condition) throw new Exception(name);
    Console.WriteLine("PASS " + name);
}
