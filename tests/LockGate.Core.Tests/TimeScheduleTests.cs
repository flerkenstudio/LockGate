using LockGate.Core.Models;

namespace LockGate.Core.Tests;

public sealed class TimeScheduleTests
{
    [Test]
    public void DisabledScheduleNeverMatches()
    {
        var schedule = new TimeSchedule
        {
            Enabled = false,
            StartTime = "09:00",
            EndTime = "17:00"
        };

        var time = new DateTime(2026, 10, 6, 12, 0, 0);
        Check.False(schedule.IsActiveAt(time));
    }

    [Test]
    public void NormalSpanMatchesWithinWindow()
    {
        var schedule = new TimeSchedule
        {
            Enabled = true,
            StartTime = "09:00",
            EndTime = "17:00"
        };

        var inside = new DateTime(2026, 10, 6, 12, 30, 0);
        var outsideBefore = new DateTime(2026, 10, 6, 8, 59, 0);
        var outsideAfter = new DateTime(2026, 10, 6, 17, 01, 0);

        Check.True(schedule.IsActiveAt(inside));
        Check.False(schedule.IsActiveAt(outsideBefore));
        Check.False(schedule.IsActiveAt(outsideAfter));
    }

    [Test]
    public void OvernightSpanMatchesPastMidnight()
    {
        var schedule = new TimeSchedule
        {
            Enabled = true,
            StartTime = "22:00",
            EndTime = "07:00"
        };

        var lateNight = new DateTime(2026, 10, 6, 23, 15, 0);
        var earlyMorning = new DateTime(2026, 10, 7, 3, 30, 0);
        var daytime = new DateTime(2026, 10, 7, 14, 0, 0);

        Check.True(schedule.IsActiveAt(lateNight));
        Check.True(schedule.IsActiveAt(earlyMorning));
        Check.False(schedule.IsActiveAt(daytime));
    }

    [Test]
    public void DaysOfWeekFilterRespected()
    {
        var schedule = new TimeSchedule
        {
            Enabled = true,
            StartTime = "09:00",
            EndTime = "17:00",
            Days = new List<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday }
        };

        // 2026-10-05 is Monday
        var mondayNoon = new DateTime(2026, 10, 5, 12, 0, 0);
        // 2026-10-06 is Tuesday
        var tuesdayNoon = new DateTime(2026, 10, 6, 12, 0, 0);

        Check.True(schedule.IsActiveAt(mondayNoon));
        Check.False(schedule.IsActiveAt(tuesdayNoon));
    }
}
