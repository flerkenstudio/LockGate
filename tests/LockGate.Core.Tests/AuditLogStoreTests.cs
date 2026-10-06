using LockGate.Core.Models;
using LockGate.Infrastructure.Storage;

namespace LockGate.Core.Tests;

public sealed class AuditLogStoreTests
{
    [Test]
    public void MissingFileReturnsEmptyList()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"audit_test_{Guid.NewGuid():N}.json");
        try
        {
            var store = new AuditLogStore(tempFile);
            var logs = store.LoadRecent(50);
            Check.Eq(0, logs.Count);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Test]
    public void AppendAndLoadRecentReturnsInDescendingOrder()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"audit_test_{Guid.NewGuid():N}.json");
        try
        {
            var store = new AuditLogStore(tempFile);

            store.Append(new SecurityAuditLog
            {
                EventType = "FailedPin",
                AppId = "notepad",
                Timestamp = DateTime.UtcNow.AddMinutes(-5),
                Details = "First event"
            });

            store.Append(new SecurityAuditLog
            {
                EventType = "SuccessUnlock",
                AppId = "notepad",
                Timestamp = DateTime.UtcNow,
                Details = "Second event"
            });

            var logs = store.LoadRecent(10);
            Check.Eq(2, logs.Count);
            Check.Eq("SuccessUnlock", logs[0].EventType);
            Check.Eq("FailedPin", logs[1].EventType);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Test]
    public void CapsLogsAt500Entries()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"audit_test_{Guid.NewGuid():N}.json");
        try
        {
            var store = new AuditLogStore(tempFile);

            for (int i = 0; i < 520; i++)
            {
                store.Append(new SecurityAuditLog
                {
                    EventType = "AppLocked",
                    AppId = $"app_{i}",
                    Timestamp = DateTime.UtcNow.AddSeconds(i)
                });
            }

            var logs = store.LoadRecent(600);
            Check.True(logs.Count <= 500, "Store should cap total records at 500");
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}
