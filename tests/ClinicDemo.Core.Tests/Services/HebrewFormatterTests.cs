using ClinicDemo.Core.Models;
using ClinicDemo.Core.Services;

namespace ClinicDemo.Core.Tests.Services;

public class HebrewFormatterTests
{
    private static readonly DateOnly SimDate = new(2026, 11, 18);
    private readonly HebrewFormatter _formatter = new();

    [Fact]
    public void TimePreference_NextWeekTuesdayAfter16_MatchesAppendixA()
    {
        var preference = new TimePreference(new DateOnly(2026, 11, 24), new TimeWindow(WindowKind.After, new TimeOnly(16, 0)));

        Assert.Equal("ביום שלישי בשבוע הבא אחרי 16:00", _formatter.TimePreferencePhrase(preference, SimDate));
    }

    [Fact]
    public void CardDate_Is_ShortWeekdayWithGeresh_AndDayMonth()
    {
        Assert.Equal("יום ג׳ 24.11", _formatter.CardDate(new DateOnly(2026, 11, 24)));
    }

    [Fact]
    public void CardTimePreference_MatchesAppendixACard()
    {
        var preference = new TimePreference(new DateOnly(2026, 11, 24), new TimeWindow(WindowKind.After, new TimeOnly(16, 0)));

        Assert.Equal("יום ג׳ 24.11, אחרי 16:00", _formatter.CardTimePreference(preference));
    }

    [Theory]
    [InlineData(Category.Checkup, "בדיקה")]
    [InlineData(Category.FollowUp, "ביקור מעקב")]
    [InlineData(Category.Consultation, "ייעוץ")]
    public void CategoryLabel(Category category, string expected)
    {
        Assert.Equal(expected, _formatter.CategoryLabel(category));
    }

    [Fact]
    public void EveryCategory_HasALabel()
    {
        Assert.All(Enum.GetValues<Category>(), c => Assert.False(string.IsNullOrWhiteSpace(_formatter.CategoryLabel(c))));
    }

    [Theory]
    [InlineData(WindowKind.Morning, "בבוקר", "בוקר")]
    [InlineData(WindowKind.Midday, "בצהריים", "צהריים")]
    [InlineData(WindowKind.Afternoon, "אחר הצהריים", "אחר הצהריים")]
    public void Window_PhraseAndLabel(WindowKind kind, string phrase, string label)
    {
        var window = new TimeWindow(kind);
        Assert.Equal(phrase, _formatter.WindowPhrase(window));
        Assert.Equal(label, _formatter.WindowLabel(window));
    }

    [Theory]
    [InlineData(9, 30, "אחרי 09:30")]
    [InlineData(16, 0, "אחרי 16:00")]
    [InlineData(0, 5, "אחרי 00:05")]
    [InlineData(21, 0, "אחרי 21:00")]
    public void AfterWindow_Is24Hour(int hour, int minute, string expected)
    {
        var window = new TimeWindow(WindowKind.After, new TimeOnly(hour, minute));
        Assert.Equal(expected, _formatter.WindowPhrase(window));
        Assert.Equal(expected, _formatter.WindowLabel(window));
    }

    [Fact]
    public void AfterWindow_WithoutTime_Throws()
    {
        Assert.Throws<ArgumentException>(() => _formatter.WindowPhrase(new TimeWindow(WindowKind.After)));
    }

    [Theory]
    [InlineData(2026, 11, 18, WindowKind.Morning, "ביום רביעי השבוע בבוקר")]
    [InlineData(2026, 11, 19, WindowKind.Midday, "ביום חמישי השבוע בצהריים")]
    [InlineData(2026, 11, 21, WindowKind.Afternoon, "ביום שבת השבוע אחר הצהריים")]
    [InlineData(2026, 11, 22, WindowKind.Morning, "ביום ראשון בשבוע הבא בבוקר")]
    [InlineData(2026, 11, 26, WindowKind.Afternoon, "ביום חמישי בשבוע הבא אחר הצהריים")]
    [InlineData(2026, 11, 28, WindowKind.Midday, "ביום שבת בשבוע הבא בצהריים")]
    [InlineData(2026, 12, 1, WindowKind.Morning, "ביום שלישי 1.12 בבוקר")]
    public void TimePreference_WeekWording_SundayToSaturdayWeeks(int y, int m, int d, WindowKind kind, string expected)
    {
        var preference = new TimePreference(new DateOnly(y, m, d), new TimeWindow(kind));
        Assert.Equal(expected, _formatter.TimePreferencePhrase(preference, SimDate));
    }

    [Theory]
    [InlineData(2026, 11, 22, "יום א׳ 22.11")]
    [InlineData(2026, 11, 23, "יום ב׳ 23.11")]
    [InlineData(2026, 11, 18, "יום ד׳ 18.11")]
    [InlineData(2026, 11, 19, "יום ה׳ 19.11")]
    [InlineData(2026, 11, 20, "יום ו׳ 20.11")]
    [InlineData(2026, 11, 28, "יום ש׳ 28.11")]
    [InlineData(2026, 12, 1, "יום ג׳ 1.12")]
    public void CardDate_AllWeekdays(int y, int m, int d, string expected)
    {
        Assert.Equal(expected, _formatter.CardDate(new DateOnly(y, m, d)));
    }

    [Fact]
    public void DayReferenceLabel_WeekdayAndRelative()
    {
        Assert.Equal("יום שלישי", _formatter.DayReferenceLabel(DayReference.Of(DayOfWeek.Tuesday)));
        Assert.Equal("היום", _formatter.DayReferenceLabel(DayReference.Of(RelativeDay.Today)));
        Assert.Equal("מחר", _formatter.DayReferenceLabel(DayReference.Of(RelativeDay.Tomorrow)));
        Assert.Equal("מחרתיים", _formatter.DayReferenceLabel(DayReference.Of(RelativeDay.DayAfterTomorrow)));
    }

    [Fact]
    public void OrList_JoinsWithCommasAndFinalOr()
    {
        Assert.Equal("א", _formatter.OrList(new[] { "א" }));
        Assert.Equal("א או ב", _formatter.OrList(new[] { "א", "ב" }));
        Assert.Equal("א, ב או ג", _formatter.OrList(new[] { "א", "ב", "ג" }));
        Assert.Throws<ArgumentException>(() => _formatter.OrList(Array.Empty<string>()));
    }
}
