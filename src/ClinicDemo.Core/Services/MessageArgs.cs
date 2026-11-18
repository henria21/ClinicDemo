using ClinicDemo.Core.Models;

namespace ClinicDemo.Core.Services;

/// <summary>
/// Typed values for <see cref="MessageCatalog.Render"/>. Only validated model values go in;
/// the catalog formats them with <see cref="HebrewFormatter"/>. Each template reads only the
/// fields it needs, and throws if one of those is missing.
/// </summary>
public sealed record MessageArgs
{
    public static MessageArgs None { get; } = new();

    /// <summary>{categories} in ClarifyCategory.</summary>
    public IReadOnlyList<Category>? Categories { get; init; }

    /// <summary>{days} in ClarifyDay.</summary>
    public IReadOnlyList<DayReference>? Days { get; init; }

    /// <summary>{windows} in ClarifyWindow.</summary>
    public IReadOnlyList<TimeWindow>? Windows { get; init; }

    /// <summary>{lastDate} in DayOutOfRange: the last selectable day.</summary>
    public DateOnly? LastDate { get; init; }

    /// <summary>{category} in Summary.</summary>
    public Category? Category { get; init; }

    /// <summary>{time} in Summary.</summary>
    public TimePreference? Time { get; init; }

    /// <summary>{contact} in Summary.</summary>
    public DemoContact? Contact { get; init; }
}
