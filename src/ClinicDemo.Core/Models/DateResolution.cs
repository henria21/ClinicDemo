namespace ClinicDemo.Core.Models;

/// <summary>Result of resolving a DayReference + WeekHint against the simulation date.</summary>
public abstract record DateResolution
{
    private DateResolution()
    {
    }

    public sealed record Resolved(DateOnly Date) : DateResolution;

    public sealed record OutOfRange : DateResolution;

    public sealed record Conflict : DateResolution;
}
