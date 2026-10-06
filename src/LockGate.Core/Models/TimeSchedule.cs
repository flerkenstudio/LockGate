namespace LockGate.Core.Models;

public sealed record TimeSchedule
{
    public bool Enabled { get; init; } = false;

    /// <summary>Format "HH:mm" (24-hour).</summary>
    public string StartTime { get; init; } = "22:00";

    /// <summary>Format "HH:mm" (24-hour).</summary>
    public string EndTime { get; init; } = "07:00";

    /// <summary>Days of week where rule applies. If empty or null, applies all days.</summary>
    public List<DayOfWeek> Days { get; init; } = new();

    public bool IsActiveAt(DateTime dateTime)
    {
        if (!Enabled) return false;

        if (Days != null && Days.Count > 0 && !Days.Contains(dateTime.DayOfWeek))
            return false;

        if (!TimeOnly.TryParse(StartTime, out var start) || !TimeOnly.TryParse(EndTime, out var end))
            return false;

        var current = TimeOnly.FromDateTime(dateTime);

        if (start < end)
        {
            // E.g. 09:00 to 17:00
            return current >= start && current < end;
        }
        else if (start > end)
        {
            // Overnight schedule, e.g. 22:00 to 07:00
            return current >= start || current < end;
        }
        else
        {
            // start == end
            return false;
        }
    }
}
