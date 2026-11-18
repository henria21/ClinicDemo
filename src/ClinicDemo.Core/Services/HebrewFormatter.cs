using System.Globalization;
using ClinicDemo.Core.Models;

namespace ClinicDemo.Core.Services;

/// <summary>
/// Formats values shown to the visitor or on cards: category labels, day and window phrases,
/// time preferences relative to the simulation date, and card dates. Stateless.
/// Weeks run Sunday–Saturday. Times are always 24-hour (HH:mm).
/// </summary>
public sealed class HebrewFormatter
{
    private const string Geresh = "׳";

    /// <summary>"בדיקה" / "ביקור מעקב" / "ייעוץ".</summary>
    public string CategoryLabel(Category category) => category switch
    {
        Category.Checkup => "בדיקה",
        Category.FollowUp => "ביקור מעקב",
        Category.Consultation => "ייעוץ",
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, "Unknown category."),
    };

    /// <summary>Full weekday name: "שלישי".</summary>
    public string WeekdayName(DayOfWeek day) => day switch
    {
        DayOfWeek.Sunday => "ראשון",
        DayOfWeek.Monday => "שני",
        DayOfWeek.Tuesday => "שלישי",
        DayOfWeek.Wednesday => "רביעי",
        DayOfWeek.Thursday => "חמישי",
        DayOfWeek.Friday => "שישי",
        DayOfWeek.Saturday => "שבת",
        _ => throw new ArgumentOutOfRangeException(nameof(day), day, "Unknown weekday."),
    };

    /// <summary>Short weekday letter with geresh: "ג׳".</summary>
    public string WeekdayLetter(DayOfWeek day) => day switch
    {
        DayOfWeek.Sunday => "א" + Geresh,
        DayOfWeek.Monday => "ב" + Geresh,
        DayOfWeek.Tuesday => "ג" + Geresh,
        DayOfWeek.Wednesday => "ד" + Geresh,
        DayOfWeek.Thursday => "ה" + Geresh,
        DayOfWeek.Friday => "ו" + Geresh,
        DayOfWeek.Saturday => "ש" + Geresh,
        _ => throw new ArgumentOutOfRangeException(nameof(day), day, "Unknown weekday."),
    };

    /// <summary>A day as the visitor said it: "יום שלישי", "היום", "מחר", "מחרתיים".</summary>
    public string DayReferenceLabel(DayReference day)
    {
        ArgumentNullException.ThrowIfNull(day);
        if (day.Weekday is { } weekday)
        {
            return "יום " + WeekdayName(weekday);
        }

        return day.Relative switch
        {
            RelativeDay.Today => "היום",
            RelativeDay.Tomorrow => "מחר",
            RelativeDay.DayAfterTomorrow => "מחרתיים",
            _ => throw new ArgumentException("A DayReference must have a weekday or a relative day.", nameof(day)),
        };
    }

    /// <summary>Window as a stand-alone label: "בוקר", "צהריים", "אחר הצהריים", "אחרי 16:00".</summary>
    public string WindowLabel(TimeWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);
        return window.Kind switch
        {
            WindowKind.Morning => "בוקר",
            WindowKind.Midday => "צהריים",
            WindowKind.Afternoon => "אחר הצהריים",
            WindowKind.After => "אחרי " + Time24(window),
            _ => throw new ArgumentOutOfRangeException(nameof(window), window.Kind, "Unknown window kind."),
        };
    }

    /// <summary>Window inside a sentence: "בבוקר", "בצהריים", "אחר הצהריים", "אחרי 16:00".</summary>
    public string WindowPhrase(TimeWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);
        return window.Kind switch
        {
            WindowKind.Morning => "בבוקר",
            WindowKind.Midday => "בצהריים",
            WindowKind.Afternoon => "אחר הצהריים",
            WindowKind.After => "אחרי " + Time24(window),
            _ => throw new ArgumentOutOfRangeException(nameof(window), window.Kind, "Unknown window kind."),
        };
    }

    /// <summary>
    /// "ביום שלישי בשבוע הבא אחרי 16:00". The week part is "השבוע" for the simulation date's week,
    /// "בשבוע הבא" for the following week, and the date ("1.12") for any other week.
    /// </summary>
    public string TimePreferencePhrase(TimePreference preference, DateOnly simulationDate)
    {
        ArgumentNullException.ThrowIfNull(preference);
        return $"ביום {WeekdayName(preference.Day.DayOfWeek)} {WeekPart(preference.Day, simulationDate)} {WindowPhrase(preference.Window)}";
    }

    /// <summary>Card date: "יום ג׳ 24.11".</summary>
    public string CardDate(DateOnly date) => $"יום {WeekdayLetter(date.DayOfWeek)} {ShortDate(date)}";

    /// <summary>Card time preference: "יום ג׳ 24.11, אחרי 16:00".</summary>
    public string CardTimePreference(TimePreference preference)
    {
        ArgumentNullException.ThrowIfNull(preference);
        return $"{CardDate(preference.Day)}, {WindowLabel(preference.Window)}";
    }

    /// <summary>Day.month without leading zeros: "24.11", "1.12".</summary>
    public string ShortDate(DateOnly date) =>
        date.Day.ToString(CultureInfo.InvariantCulture) + "." + date.Month.ToString(CultureInfo.InvariantCulture);

    /// <summary>Hebrew list: "א", "א או ב", "א, ב או ג".</summary>
    public string OrList(IEnumerable<string> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var list = items.ToList();
        if (list.Count == 0)
        {
            throw new ArgumentException("At least one item is required.", nameof(items));
        }

        return list.Count == 1
            ? list[0]
            : string.Join(", ", list.Take(list.Count - 1)) + " או " + list[^1];
    }

    private static string Time24(TimeWindow window)
    {
        if (window.After is not { } after)
        {
            throw new ArgumentException("An After window needs a time.", nameof(window));
        }

        return after.ToString("HH:mm", CultureInfo.InvariantCulture);
    }

    private string WeekPart(DateOnly day, DateOnly simulationDate)
    {
        var weekStart = StartOfWeek(simulationDate);
        var dayWeekStart = StartOfWeek(day);
        if (dayWeekStart == weekStart)
        {
            return "השבוע";
        }

        if (dayWeekStart == weekStart.AddDays(7))
        {
            return "בשבוע הבא";
        }

        return ShortDate(day);
    }

    private static DateOnly StartOfWeek(DateOnly date) => date.AddDays(-(int)date.DayOfWeek);
}
