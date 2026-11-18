namespace ClinicDemo.Core.Models;

/// <summary>
/// Candidate sets from one visitor message. More than one candidate in a set is a conflict.
/// <see cref="WeekHintConflict"/> is true when one message names both this week and next week
/// ("השבוע או בשבוע הבא"); <see cref="WeekHint"/> is then <see cref="WeekHint.None"/> and the engine
/// treats it as a day conflict (ClarifyDay).
/// </summary>
public sealed record ExtractionResult(
    IReadOnlyList<Category> Categories,
    IReadOnlyList<DayReference> Days,
    IReadOnlyList<TimeWindow> Windows,
    WeekHint WeekHint,
    bool AmbiguousTime,
    string? UnknownDayName,
    bool WeekHintConflict = false)
{
    public static ExtractionResult Empty { get; } =
        new(Array.Empty<Category>(), Array.Empty<DayReference>(), Array.Empty<TimeWindow>(), WeekHint.None, false, null);
}
