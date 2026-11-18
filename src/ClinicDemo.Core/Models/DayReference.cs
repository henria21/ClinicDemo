namespace ClinicDemo.Core.Models;

/// <summary>What the visitor said about the day. Exactly one of Weekday / Relative is set.</summary>
public sealed record DayReference(DayOfWeek? Weekday, RelativeDay? Relative)
{
    public static DayReference Of(DayOfWeek weekday) => new(weekday, null);

    public static DayReference Of(RelativeDay relative) => new(null, relative);
}
