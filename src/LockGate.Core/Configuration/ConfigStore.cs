using System.Text.Json;

namespace LockGate.Core.Configuration;

/// <summary>
/// JSON config on disk with atomic writes. Holds NO secrets (no PIN, keys or embeddings).
/// A corrupt file throws instead of silently falling back to "nothing protected".
/// Tamper-resistance (ACLs, authenticated edits) is a later hardening phase.
/// </summary>
public sealed class ConfigStore
{
    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    readonly string _path;

    public ConfigStore(string path) => _path = path;

    public AppConfig Load()
    {
        if (!File.Exists(_path)) return new AppConfig();
        AppConfig? cfg;
        try { cfg = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(_path), Json); }
        catch (JsonException ex) { throw new InvalidDataException("Config file is corrupt.", ex); }
        if (cfg is null) throw new InvalidDataException("Config file is empty.");
        cfg.Validate();
        return cfg;
    }

    public void Save(AppConfig config)
    {
        config.Validate();
        var dir = Path.GetDirectoryName(Path.GetFullPath(_path))!;
        Directory.CreateDirectory(dir);
        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(config, Json));
        File.Move(tmp, _path, overwrite: true);
    }
}
