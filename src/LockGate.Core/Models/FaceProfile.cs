namespace LockGate.Core.Models;

public sealed record FaceProfile
{
    public int Slot { get; init; } = 1; // Slot 1, 2, or 3
    public string Name { get; init; } = "Primary Face";
    public bool Enrolled { get; init; } = true;
    public DateTime EnrolledAt { get; init; } = DateTime.UtcNow;
    public string? Description { get; init; }
}
