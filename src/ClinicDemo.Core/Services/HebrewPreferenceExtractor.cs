using ClinicDemo.Core.Models;

namespace ClinicDemo.Core.Services;

/// <summary>
/// Turns one visitor message into an <see cref="ExtractionResult"/>: candidate sets for category, day and time window,
/// plus a week hint and two flags. It does not resolve dates and does not decide whether candidates conflict;
/// that is the engine's job (a set with more than one entry, or window compatibility, design §5).
/// Deterministic and stateless: one normalisation, then one pass over the tokens with O(1) work per token.
/// </summary>
/// <remarks>
/// <para>Keywords (each may carry the one-letter prefixes handled by <see cref="HebrewPrefixMatcher"/>, e.g. "לבדיקה", "וביום"):</para>
/// <list type="bullet">
/// <item>Categories: בדיקה → Checkup; מעקב (so also "ביקור מעקב") → FollowUp; ייעוץ / יעוץ → Consultation.</item>
/// <item>Day names: ראשון, שני, שלישי, רביעי, חמישי, שישי, שבת. Letter forms only after "יום": יום א׳ .. יום ו׳, יום ש׳
/// (the geresh is required, so "יום ב-16:00" is not read as Monday).</item>
/// <item>Relative days: היום → Today, מחר → Tomorrow, מחרתיים → DayAfterTomorrow.</item>
/// <item>Week hints: "שבוע" followed by "הבא" → NextWeek ("שבוע הבא", "בשבוע הבא", "השבוע הבא");
/// "שבוע" followed by "הזה", or "השבוע" → ThisWeek. A bare "בשבוע" gives no hint. If one message names both weeks,
/// the hint is <see cref="WeekHint.None"/>: the extractor never picks one.</item>
/// <item>Day parts: בוקר → Morning; צהריים / צהרים → Midday; "אחר הצהריים" / "אחרי הצהריים" / אחה״צ → Afternoon.</item>
/// <item>Times, 24-hour only, after "אחרי" or "לאחר": HH:MM (00–23, 00–59) or a bare hour 13–23 → After(time).
/// A bare 0–12, a number word or anything else (including nothing) after the marker sets
/// <see cref="ExtractionResult.AmbiguousTime"/> and yields no window. Nothing is ever read as PM.</item>
/// </list>
/// <para><see cref="ExtractionResult.UnknownDayName"/> is set conservatively: only for "יום" followed by a single Hebrew
/// letter with a geresh that is not a day letter (e.g. "יום ז׳"). The value is the normalised token; the first one wins.</para>
/// <para>Candidates are distinct and in first-seen order, so repeating the same value is not a conflict.</para>
/// </remarks>
public sealed class HebrewPreferenceExtractor
{
    private const string MarkerAfter = "after";
    private const string MarkerOther = "other";
    private const string MarkerDay = "day";
    private const string MarkerWeek = "week";

    private readonly HebrewTextNormalizer _normalizer;
    private readonly HebrewPrefixMatcher _matcher;

    private readonly HebrewKeywordDictionary<Category> _categories;
    private readonly HebrewKeywordDictionary<DayOfWeek> _dayNames;
    private readonly HebrewKeywordDictionary<DayOfWeek> _dayLetters;
    private readonly HebrewKeywordDictionary<RelativeDay> _relativeDays;
    private readonly HebrewKeywordDictionary<WindowKind> _dayParts;
    private readonly HebrewKeywordDictionary<string> _markers;
    private readonly HebrewKeywordDictionary<WeekHint> _weekQualifiers;
    private readonly HebrewKeywordDictionary<bool> _afternoonTails;

    public HebrewPreferenceExtractor(HebrewTextNormalizer normalizer, HebrewPrefixMatcher matcher)
    {
        ArgumentNullException.ThrowIfNull(normalizer);
        ArgumentNullException.ThrowIfNull(matcher);
        _normalizer = normalizer;
        _matcher = matcher;

        _categories = new(normalizer, new[]
        {
            ("בדיקה", Category.Checkup),
            ("מעקב", Category.FollowUp),
            ("ייעוץ", Category.Consultation),
            ("יעוץ", Category.Consultation),
        });

        _dayNames = new(normalizer, new[]
        {
            ("ראשון", DayOfWeek.Sunday),
            ("שני", DayOfWeek.Monday),
            ("שלישי", DayOfWeek.Tuesday),
            ("רביעי", DayOfWeek.Wednesday),
            ("חמישי", DayOfWeek.Thursday),
            ("שישי", DayOfWeek.Friday),
            ("שבת", DayOfWeek.Saturday),
        });

        _dayLetters = new(normalizer, new[]
        {
            ("א׳", DayOfWeek.Sunday),
            ("ב׳", DayOfWeek.Monday),
            ("ג׳", DayOfWeek.Tuesday),
            ("ד׳", DayOfWeek.Wednesday),
            ("ה׳", DayOfWeek.Thursday),
            ("ו׳", DayOfWeek.Friday),
            ("ש׳", DayOfWeek.Saturday),
        });

        _relativeDays = new(normalizer, new[]
        {
            ("היום", RelativeDay.Today),
            ("מחר", RelativeDay.Tomorrow),
            ("מחרתיים", RelativeDay.DayAfterTomorrow),
        });

        _dayParts = new(normalizer, new[]
        {
            ("בוקר", WindowKind.Morning),
            ("צהריים", WindowKind.Midday),
            ("צהרים", WindowKind.Midday),
            ("אחה״צ", WindowKind.Afternoon),
        });

        _markers = new(normalizer, new[]
        {
            ("אחרי", MarkerAfter),
            ("אחר", MarkerOther),
            ("יום", MarkerDay),
            ("שבוע", MarkerWeek),
        });

        _weekQualifiers = new(normalizer, new[]
        {
            ("הבא", WeekHint.NextWeek),
            ("הזה", WeekHint.ThisWeek),
        });

        _afternoonTails = new(normalizer, new[]
        {
            ("הצהריים", true),
            ("הצהרים", true),
        });
    }

    public ExtractionResult Extract(string? text)
    {
        var tokens = _normalizer.Tokenize(text);
        if (tokens.Count == 0)
        {
            return ExtractionResult.Empty;
        }

        var categories = new List<Category>();
        var days = new List<DayReference>();
        var windows = new List<TimeWindow>();
        var seen = new HashSet<object>();
        bool thisWeek = false;
        bool nextWeek = false;
        bool ambiguousTime = false;
        string? unknownDayName = null;

        for (int i = 0; i < tokens.Count; i++)
        {
            string token = tokens[i];
            string? next = i + 1 < tokens.Count ? tokens[i + 1] : null;

            // Relative days first: "היום" must not be read as the marker "יום" with the prefix "ה".
            if (_matcher.TryMatch(token, _relativeDays, out var relative))
            {
                AddDistinct(days, seen, DayReference.Of(relative.Value));
                continue;
            }

            if (_matcher.TryMatch(token, _markers, out var marker))
            {
                switch (marker.Value)
                {
                    case MarkerOther when next is not null && _afternoonTails.TryGetValue(next, out _):
                    case MarkerAfter when next is not null && _afternoonTails.TryGetValue(next, out _):
                        AddDistinct(windows, seen, new TimeWindow(WindowKind.Afternoon));
                        i++;
                        break;

                    case MarkerAfter:
                    case MarkerOther when marker.Prefix == "ל": // "לאחר 16:00" means "after 16:00"
                        if (next is not null && StartsWithDigit(next))
                        {
                            i++; // the number belongs to the marker, valid or not
                            if (TryParse24h(next, out var time))
                            {
                                AddDistinct(windows, seen, new TimeWindow(WindowKind.After, time));
                                break;
                            }
                        }

                        ambiguousTime = true;
                        break;

                    case MarkerDay when next is not null:
                        if (_dayLetters.TryGetValue(next, out var weekday))
                        {
                            AddDistinct(days, seen, DayReference.Of(weekday));
                            i++;
                        }
                        else if (IsLetterWithGeresh(next))
                        {
                            unknownDayName ??= next;
                            i++;
                        }

                        break;

                    case MarkerWeek:
                        if (next is not null && _weekQualifiers.TryGetValue(next, out var hint))
                        {
                            thisWeek |= hint == WeekHint.ThisWeek;
                            nextWeek |= hint == WeekHint.NextWeek;
                            i++;
                        }
                        else if (marker.Prefix.EndsWith('ה'))
                        {
                            thisWeek = true; // "השבוע", "והשבוע"
                        }

                        break;
                }

                continue;
            }

            if (_matcher.TryMatch(token, _dayNames, out var dayName))
            {
                AddDistinct(days, seen, DayReference.Of(dayName.Value));
            }
            else if (_matcher.TryMatch(token, _dayParts, out var dayPart))
            {
                AddDistinct(windows, seen, new TimeWindow(dayPart.Value));
            }
            else if (_matcher.TryMatch(token, _categories, out var category))
            {
                AddDistinct(categories, seen, category.Value);
            }
        }

        var weekHint = (thisWeek, nextWeek) switch
        {
            (true, false) => WeekHint.ThisWeek,
            (false, true) => WeekHint.NextWeek,
            _ => WeekHint.None,
        };

        return new ExtractionResult(categories, days, windows, weekHint, ambiguousTime, unknownDayName);
    }

    /// <summary>Adds <paramref name="value"/> unless it was seen before, keeping first-seen order. O(1).</summary>
    private static void AddDistinct<T>(List<T> list, HashSet<object> seen, T value)
        where T : notnull
    {
        if (seen.Add(value))
        {
            list.Add(value);
        }
    }

    private static bool StartsWithDigit(string token) => char.IsAsciiDigit(token[0]);

    /// <summary>"HH:MM" with HH 00–23 and MM 00–59, or a bare hour 13–23. A bare 0–12 is ambiguous (could be AM or PM).</summary>
    private static bool TryParse24h(string token, out TimeOnly time)
    {
        time = default;
        int colon = token.IndexOf(':');
        string hourPart = colon < 0 ? token : token.Substring(0, colon);
        string? minutePart = colon < 0 ? null : token.Substring(colon + 1);

        if (!IsDigits(hourPart, 1, 2) || (minutePart is not null && !IsDigits(minutePart, 2, 2)))
        {
            return false;
        }

        int hour = int.Parse(hourPart, System.Globalization.CultureInfo.InvariantCulture);
        int minute = minutePart is null ? 0 : int.Parse(minutePart, System.Globalization.CultureInfo.InvariantCulture);

        bool valid = minutePart is null ? hour is >= 13 and <= 23 : hour <= 23 && minute <= 59;
        if (valid)
        {
            time = new TimeOnly(hour, minute);
        }

        return valid;
    }

    private static bool IsDigits(string s, int minLength, int maxLength) =>
        s.Length >= minLength && s.Length <= maxLength && s.All(char.IsAsciiDigit);

    private static bool IsLetterWithGeresh(string token) =>
        token.Length == 2 && token[0] is >= 'א' and <= 'ת' && token[1] == HebrewTextNormalizer.Geresh;
}
