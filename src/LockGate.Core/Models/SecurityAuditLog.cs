namespace LockGate.Core.Models;

public sealed record SecurityAuditLog
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public string EventType { get; init; } = ""; // e.g. "FailedPin", "FailedHello", "SuccessUnlock", "AppLocked"
    public string AppId { get; init; } = "";
    public string? ProcessName { get; init; }
    public string? Details { get; init; }
    public string? SnapshotPath { get; init; }
}
