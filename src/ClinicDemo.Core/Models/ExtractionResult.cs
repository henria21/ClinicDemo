namespace ClinicDemo.Core.Models;

/// <summary>Candidate sets from one visitor message. More than one candidate in a set is a conflict.</summary>
public sealed record ExtractionResult(
    IReadOnlyList<Category> Categories,
    IReadOnlyList<DayReference> Days,
    IReadOnlyList<TimeWindow> Windows,
    WeekHint WeekHint,
    bool AmbiguousTime,
    string? UnknownDayName)
{
    public static ExtractionResult Empty { get; } =
        new(Array.Empty<Category>(), Array.Empty<DayReference>(), Array.Empty<TimeWindow>(), WeekHint.None, false, null);
}
