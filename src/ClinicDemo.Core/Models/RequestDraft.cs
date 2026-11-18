namespace ClinicDemo.Core.Models;

/// <summary>
/// Mutable during a conversation. The resolved date is never stored: it is recomputed from
/// DayReference + WeekHint + the simulation date every time the draft changes.
/// </summary>
public sealed class RequestDraft
{
    public Category? Category { get; set; }

    public DayReference? DayReference { get; set; }

    public WeekHint WeekHint { get; set; } = WeekHint.None;

    public TimeWindow? Window { get; set; }

    public List<string> OriginalWording { get; } = new();
}
