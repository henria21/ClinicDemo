namespace ClinicDemo.Core.Models;

/// <summary>
/// Candidate sets from one visitor message. More than one candidate in a set is a conflict.
/// <see cref="WeekHintConflict"/> is true when one message names both this week and next week
/// ("השבוע או בשבוע הבא"); <see cref="WeekHint"/> is then <see cref="WeekHint.None"/> and the engine
/// treats it as a day conflict (ClarifyDay).
/// <see cref="ExactTime"/> is the first 24-hour HH:MM in the message that is not after "אחרי" / "לאחר"
/// ("ב-20:00", "בשעה 20:00", "16:00"). The demo can't request an exact time, so it yields no window.
/// </summary>
public sealed record ExtractionResult(
    IReadOnlyList<Category> Categories,
    IReadOnlyList<DayReference> Days,
    IReadOnlyList<TimeWindow> Windows,
    WeekHint WeekHint,
    bool AmbiguousTime,
    string? UnknownDayName,
    bool WeekHintConflict = false,
    TimeOnly? ExactTime = null)
{
    public static ExtractionResult Empty { get; } =
        new(Array.Empty<Category>(), Array.Empty<DayReference>(), Array.Empty<TimeWindow>(), WeekHint.None, false, null);
}
