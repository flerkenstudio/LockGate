using LockGate.Core.Configuration;
using LockGate.Core.Models;

namespace LockGate.Core.Tests;

public sealed class ConfigStoreTests
{
    static string TempPath() => Path.Combine(Path.GetTempPath(), "fg-" + Guid.NewGuid().ToString("N"), "config.json");

    [Test] public void MissingFileGivesDefaults()
    {
        var cfg = new ConfigStore(TempPath()).Load();
        Check.Eq(5, cfg.DefaultSessionMinutes); Check.Eq(0, cfg.ProtectedApps.Count);
    }

    [Test] public void RoundTripAndNoTempFileLeft()
    {
        var path = TempPath(); var store = new ConfigStore(path);
        store.Save(new AppConfig { ProtectedApps = { new ProtectedApp { AppId = "chrome", Executable = "chrome.exe", SessionMinutes = 10 } } });
        var back = store.Load();
        Check.Eq("chrome.exe", back.ProtectedApps[0].Executable); Check.Eq(10, back.ProtectedApps[0].SessionMinutes);
        Check.False(File.Exists(path + ".tmp"));
    }

    [Test] public void CorruptFileThrowsInsteadOfDisablingProtection()
    {
        var path = TempPath(); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{ not json");
        Check.Throws<InvalidDataException>(() => new ConfigStore(path).Load());
    }

    [Test] public void InvalidValuesRejected()
    {
        var store = new ConfigStore(TempPath());
        Check.Throws<InvalidDataException>(() => store.Save(new AppConfig { DefaultSessionMinutes = -5 }));
        Check.Throws<InvalidDataException>(() => store.Save(new AppConfig { Version = 99 }));
        var dup = new ProtectedApp { AppId = "a", Executable = "a.exe" };
        Check.Throws<InvalidDataException>(() => store.Save(new AppConfig { ProtectedApps = { dup, dup } }));
    }

    [Test] public void ConfigContainsNoSecretFields()
    {
        var path = TempPath(); new ConfigStore(path).Save(new AppConfig());
        var text = File.ReadAllText(path).ToLowerInvariant();
        foreach (var word in new[] { "pin", "password", "key", "embedding" }) Check.False(text.Contains(word), word);
    }
}
