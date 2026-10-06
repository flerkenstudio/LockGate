using System.Text.Json;
using LockGate.Core.Models;

namespace LockGate.Infrastructure.Storage;

public sealed class AuditLogStore
{
    readonly string _filePath;
    readonly object _gate = new();

    public AuditLogStore(string filePath)
    {
        _filePath = filePath;
    }

    public List<SecurityAuditLog> LoadRecent(int limit = 50)
    {
        lock (_gate)
        {
            if (!File.Exists(_filePath)) return new();
            try
            {
                var json = File.ReadAllText(_filePath);
                var logs = JsonSerializer.Deserialize<List<SecurityAuditLog>>(json);
                return logs?.OrderByDescending(l => l.Timestamp).Take(limit).ToList() ?? new();
            }
            catch
            {
                return new();
            }
        }
    }

    public void Append(SecurityAuditLog log)
    {
        lock (_gate)
        {
            var logs = new List<SecurityAuditLog>();
            if (File.Exists(_filePath))
            {
                try
                {
                    var existing = JsonSerializer.Deserialize<List<SecurityAuditLog>>(File.ReadAllText(_filePath));
                    if (existing != null) logs = existing;
                }
                catch { }
            }

            logs.Add(log);

            // Cap logs at 500 entries to prevent unbounded growth
            if (logs.Count > 500)
            {
                logs = logs.OrderByDescending(l => l.Timestamp).Take(500).ToList();
            }

            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var tmp = _filePath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(logs, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(tmp, _filePath, overwrite: true);
        }
    }
}
