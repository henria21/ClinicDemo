using ClinicDemo.Core.Models;

namespace ClinicDemo.Core.Services;

/// <summary>
/// Resolves what the visitor said about the day (<see cref="DayReference"/> + <see cref="WeekHint"/>)
/// to a calendar date relative to the simulation date. Pure and stateless: the date is always passed in.
/// </summary>
/// <remarks>
/// Weeks run Sunday to Saturday. The allowed range is the simulation date through the Saturday of the
/// following week (18.11 to 28.11 for the default simulation date, Wednesday 18.11.2026).
/// <list type="bullet">
/// <item>Weekday + <see cref="WeekHint.None"/>: nearest occurrence on or after the simulation date (today counts).</item>
/// <item>Weekday + <see cref="WeekHint.ThisWeek"/>: that day in the current week; a day before the simulation date is OutOfRange.</item>
/// <item>Weekday + <see cref="WeekHint.NextWeek"/>: that day in the next week.</item>
/// <item>Relative + <see cref="WeekHint.None"/>: simulation date + n.</item>
/// <item>Relative + a hint naming the week the resulting date falls in: Resolved (the hint agrees).
/// Relative + a hint naming any other week: Conflict. Conflict is checked before the range.</item>
/// <item>Anything that resolves outside the range: OutOfRange.</item>
/// </list>
/// </remarks>
public sealed class HebrewDateResolver
{
    public DateResolution Resolve(DayReference day, WeekHint hint, DateOnly simDate)
    {
        ArgumentNullException.ThrowIfNull(day);
        if (!Enum.IsDefined(hint))
        {
            throw new ArgumentOutOfRangeException(nameof(hint), hint, "Undefined week hint.");
        }

        if (day.Weekday.HasValue == day.Relative.HasValue)
        {
            throw new ArgumentException("Exactly one of Weekday / Relative must be set.", nameof(day));
        }

        return day.Weekday is { } weekday
            ? ResolveWeekday(weekday, hint, simDate)
            : ResolveRelative(day.Relative!.Value, hint, simDate);
    }

    /// <summary>First date of the allowed range: the simulation date itself.</summary>
    public DateOnly FirstDate(DateOnly simDate) => simDate;

    /// <summary>Last date of the allowed range: the Saturday of the week after the simulation date's week.</summary>
    public DateOnly LastDate(DateOnly simDate) => WeekStart(simDate).AddDays(13);

    /// <summary>True when <paramref name="date"/> is within [FirstDate(simDate), LastDate(simDate)].</summary>
    public bool IsInRange(DateOnly date, DateOnly simDate) =>
        date >= FirstDate(simDate) && date <= LastDate(simDate);

    private DateResolution ResolveWeekday(DayOfWeek weekday, WeekHint hint, DateOnly simDate)
    {
        if (!Enum.IsDefined(weekday))
        {
            throw new ArgumentOutOfRangeException(nameof(weekday), weekday, "Undefined weekday.");
        }

        var date = hint switch
        {
            WeekHint.None => simDate.AddDays(((int)weekday - (int)simDate.DayOfWeek + 7) % 7),
            WeekHint.ThisWeek => WeekStart(simDate).AddDays((int)weekday),
            _ => WeekStart(simDate).AddDays(7 + (int)weekday),
        };

        return InRangeOrOut(date, simDate);
    }

    private DateResolution ResolveRelative(RelativeDay relative, WeekHint hint, DateOnly simDate)
    {
        var offset = relative switch
        {
            RelativeDay.Today => 0,
            RelativeDay.Tomorrow => 1,
            RelativeDay.DayAfterTomorrow => 2,
            _ => throw new ArgumentOutOfRangeException(nameof(relative), relative, "Undefined relative day."),
        };

        var date = simDate.AddDays(offset);
        if (hint != WeekHint.None && hint != WeekOf(date, simDate))
        {
            return new DateResolution.Conflict();
        }

        return InRangeOrOut(date, simDate);
    }

    private DateResolution InRangeOrOut(DateOnly date, DateOnly simDate) =>
        IsInRange(date, simDate) ? new DateResolution.Resolved(date) : new DateResolution.OutOfRange();

    /// <summary>The hint that describes the week <paramref name="date"/> falls in, or None if neither.</summary>
    private static WeekHint WeekOf(DateOnly date, DateOnly simDate)
    {
        var weeks = (WeekStart(date).DayNumber - WeekStart(simDate).DayNumber) / 7;
        return weeks switch
        {
            0 => WeekHint.ThisWeek,
            1 => WeekHint.NextWeek,
            _ => WeekHint.None,
        };
    }

    private static DateOnly WeekStart(DateOnly date) => date.AddDays(-(int)date.DayOfWeek);
}
