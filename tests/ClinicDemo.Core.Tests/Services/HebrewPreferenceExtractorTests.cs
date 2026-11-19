using ClinicDemo.Core.Models;
using ClinicDemo.Core.Services;

namespace ClinicDemo.Core.Tests.Services;

public class HebrewPreferenceExtractorTests
{
    private readonly HebrewPreferenceExtractor _extractor = new(new HebrewTextNormalizer(), new HebrewPrefixMatcher());

    private static readonly TimeWindow After1600 = new(WindowKind.After, new TimeOnly(16, 0));

    // ---- Appendix A ----

    [Fact]
    public void AppendixA_Message1_Checkup_NextWeek_Afternoon_NoDay()
    {
        var r = _extractor.Extract("שלום, אפשר לקבוע תור לבדיקה בשבוע הבא, אחר הצהריים?");

        Assert.Equal(new[] { Category.Checkup }, r.Categories);
        Assert.Empty(r.Days);
        Assert.Equal(new[] { new TimeWindow(WindowKind.Afternoon) }, r.Windows);
        Assert.Equal(WeekHint.NextWeek, r.WeekHint);
        Assert.False(r.AmbiguousTime);
        Assert.Null(r.UnknownDayName);
    }

    [Fact]
    public void AppendixA_Message2_Tuesday_After1600()
    {
        var r = _extractor.Extract("יום שלישי אחרי 16:00.");

        Assert.Empty(r.Categories);
        Assert.Equal(new[] { DayReference.Of(DayOfWeek.Tuesday) }, r.Days);
        Assert.Equal(new[] { After1600 }, r.Windows);
        Assert.Equal(WeekHint.None, r.WeekHint);
        Assert.False(r.AmbiguousTime);
        Assert.Null(r.UnknownDayName);
    }

    // ---- Times: 24-hour only ----

    [Theory]
    [InlineData("אחרי 16:00", 16, 0)]
    [InlineData("אחרי 16", 16, 0)]
    [InlineData("אחרי 9:30", 9, 30)]
    [InlineData("אחרי 09:30", 9, 30)]
    [InlineData("אחרי 13", 13, 0)]
    [InlineData("אחרי 23", 23, 0)]
    [InlineData("אחרי 12:00", 12, 0)]
    [InlineData("אחרי 00:00", 0, 0)]
    [InlineData("אחרי 23:59", 23, 59)]
    [InlineData("ואחרי 16:30", 16, 30)]
    [InlineData("לאחר 17:00", 17, 0)]
    public void After_24HourTime_GivesAfterWindow(string text, int hour, int minute)
    {
        var r = _extractor.Extract(text);

        Assert.Equal(new[] { new TimeWindow(WindowKind.After, new TimeOnly(hour, minute)) }, r.Windows);
        Assert.False(r.AmbiguousTime);
    }

    // ---- Exact times: flagged, never a window ----

    [Theory]
    [InlineData("ב-20:00", 20, 0)]
    [InlineData("בשעה 20:00", 20, 0)]
    [InlineData("ב20:00", 20, 0)]
    [InlineData("16:00", 16, 0)]
    [InlineData("ל-9:30", 9, 30)]
    public void ExactTime_IsFlagged_AndYieldsNoWindow(string text, int hour, int minute)
    {
        var r = _extractor.Extract(text);

        Assert.Equal(new TimeOnly(hour, minute), r.ExactTime);
        Assert.Empty(r.Windows);
        Assert.False(r.AmbiguousTime);
    }

    [Theory]
    [InlineData("אחרי 16:00")]
    [InlineData("לאחר 16:00")]
    [InlineData("ב-16")]
    [InlineData("ב-25:00")]
    public void NotAnExactTime(string text)
    {
        Assert.Null(_extractor.Extract(text).ExactTime);
    }

    [Fact]
    public void ExactTime_FirstOneWins()
    {
        Assert.Equal(new TimeOnly(18, 0), _extractor.Extract("ב-18:00 או ב-20:00").ExactTime);
    }

    [Theory]
    [InlineData("אחרי 4")]
    [InlineData("אחרי 12")]
    [InlineData("אחרי 0")]
    [InlineData("אחרי 04")]
    [InlineData("אחרי ארבע")]
    [InlineData("אחרי ארבע וחצי")]
    [InlineData("אחרי 24")]
    [InlineData("אחרי 24:00")]
    [InlineData("אחרי 16:60")]
    [InlineData("אחרי 16:5")]
    [InlineData("אחרי 160")]
    [InlineData("אחרי העבודה")]
    [InlineData("אחרי")]
    [InlineData("לאחר 4")]
    public void After_NotA24HourTime_IsAmbiguous_AndGivesNoWindow(string text)
    {
        var r = _extractor.Extract(text);

        Assert.True(r.AmbiguousTime);
        Assert.Empty(r.Windows);
    }

    [Fact]
    public void After4_IsNeverReadAs1600()
    {
        var r = _extractor.Extract("יום שלישי אחרי 4");

        Assert.True(r.AmbiguousTime);
        Assert.DoesNotContain(r.Windows, w => w.After == new TimeOnly(16, 0));
        Assert.DoesNotContain(r.Windows, w => w.After == new TimeOnly(4, 0));
        Assert.Equal(new[] { DayReference.Of(DayOfWeek.Tuesday) }, r.Days);
    }

    [Fact]
    public void BareNumberWithoutAfter_IsIgnored()
    {
        var r = _extractor.Extract("16:00");

        Assert.Empty(r.Windows);
        Assert.False(r.AmbiguousTime);
    }

    // ---- Days ----

    [Theory]
    [InlineData("יום א׳", DayOfWeek.Sunday)]
    [InlineData("יום ב'", DayOfWeek.Monday)]
    [InlineData("יום ג׳", DayOfWeek.Tuesday)]
    [InlineData("יום ג'", DayOfWeek.Tuesday)]
    [InlineData("יום ג’", DayOfWeek.Tuesday)]
    [InlineData("ביום ד׳", DayOfWeek.Wednesday)]
    [InlineData("יום ה׳", DayOfWeek.Thursday)]
    [InlineData("יום ו׳", DayOfWeek.Friday)]
    [InlineData("יום ש׳", DayOfWeek.Saturday)]
    [InlineData("ראשון", DayOfWeek.Sunday)]
    [InlineData("שני", DayOfWeek.Monday)]
    [InlineData("שלישי", DayOfWeek.Tuesday)]
    [InlineData("רביעי", DayOfWeek.Wednesday)]
    [InlineData("חמישי", DayOfWeek.Thursday)]
    [InlineData("שישי", DayOfWeek.Friday)]
    [InlineData("שבת", DayOfWeek.Saturday)]
    [InlineData("בשבת", DayOfWeek.Saturday)]
    [InlineData("וביום שלישי", DayOfWeek.Tuesday)]
    [InlineData("בעצם חמישי", DayOfWeek.Thursday)]
    public void Weekday_IsRecognised(string text, DayOfWeek expected)
    {
        var r = _extractor.Extract(text);

        Assert.Equal(new[] { DayReference.Of(expected) }, r.Days);
        Assert.Null(r.UnknownDayName);
    }

    [Theory]
    [InlineData("היום", RelativeDay.Today)]
    [InlineData("מחר", RelativeDay.Tomorrow)]
    [InlineData("ומחר", RelativeDay.Tomorrow)]
    [InlineData("מחרתיים", RelativeDay.DayAfterTomorrow)]
    public void RelativeDay_IsRecognised(string text, RelativeDay expected)
    {
        var r = _extractor.Extract(text);

        Assert.Equal(new[] { DayReference.Of(expected) }, r.Days);
        Assert.Null(r.UnknownDayName);
    }

    [Fact]
    public void DayWithUnknownLetter_SetsUnknownDayName()
    {
        var r = _extractor.Extract("יום ז׳");

        Assert.Empty(r.Days);
        Assert.Equal("ז׳", r.UnknownDayName);
    }

    [Theory]
    [InlineData("יום ב-16:00")]
    [InlineData("יום טוב")]
    [InlineData("יום ג")]
    public void DayFollowedByOtherWords_IsNotADay_AndNotUnknown(string text)
    {
        var r = _extractor.Extract(text);

        Assert.Empty(r.Days);
        Assert.Null(r.UnknownDayName);
    }

    // ---- Week hints ----

    [Theory]
    [InlineData("שבוע הבא", WeekHint.NextWeek)]
    [InlineData("בשבוע הבא", WeekHint.NextWeek)]
    [InlineData("ובשבוע הבא", WeekHint.NextWeek)]
    [InlineData("לשבוע הבא", WeekHint.NextWeek)]
    [InlineData("השבוע", WeekHint.ThisWeek)]
    [InlineData("בשבוע הזה", WeekHint.ThisWeek)]
    [InlineData("בעצם בשבוע הזה", WeekHint.ThisWeek)]
    [InlineData("בשבוע", WeekHint.None)]
    [InlineData("השבוע או בשבוע הבא", WeekHint.None)]
    public void WeekHint_IsRecognised(string text, WeekHint expected)
    {
        Assert.Equal(expected, _extractor.Extract(text).WeekHint);
    }

    [Theory]
    [InlineData("השבוע או בשבוע הבא", true)]
    [InlineData("בשבוע הבא או השבוע, יום חמישי", true)]
    [InlineData("בשבוע הבא", false)]
    [InlineData("השבוע", false)]
    [InlineData("יום חמישי", false)]
    public void BothWeeksInOneMessage_SetsWeekHintConflict(string text, bool expected)
    {
        var r = _extractor.Extract(text);

        Assert.Equal(expected, r.WeekHintConflict);
        if (expected)
        {
            Assert.Equal(WeekHint.None, r.WeekHint);
        }
    }

    [Fact]
    public void NextWeekThursday_GivesBothDayAndHint()
    {
        var r = _extractor.Extract("שבוע הבא חמישי");

        Assert.Equal(WeekHint.NextWeek, r.WeekHint);
        Assert.Equal(new[] { DayReference.Of(DayOfWeek.Thursday) }, r.Days);
    }

    // ---- Day parts ----

    [Theory]
    [InlineData("בוקר", WindowKind.Morning)]
    [InlineData("בבוקר", WindowKind.Morning)]
    [InlineData("צהריים", WindowKind.Midday)]
    [InlineData("בצהריים", WindowKind.Midday)]
    [InlineData("אחר הצהריים", WindowKind.Afternoon)]
    [InlineData("ואחר הצהריים", WindowKind.Afternoon)]
    [InlineData("אחרי הצהריים", WindowKind.Afternoon)]
    [InlineData("אחה\"צ", WindowKind.Afternoon)]
    public void DayPart_IsRecognised(string text, WindowKind expected)
    {
        var r = _extractor.Extract(text);

        Assert.Equal(new[] { new TimeWindow(expected) }, r.Windows);
        Assert.False(r.AmbiguousTime);
    }

    // ---- Categories ----

    [Theory]
    [InlineData("בדיקה", Category.Checkup)]
    [InlineData("לבדיקה", Category.Checkup)]
    [InlineData("ביקור מעקב", Category.FollowUp)]
    [InlineData("למעקב", Category.FollowUp)]
    [InlineData("ייעוץ", Category.Consultation)]
    [InlineData("ליעוץ", Category.Consultation)]
    public void Category_IsRecognised(string text, Category expected)
    {
        Assert.Equal(new[] { expected }, _extractor.Extract(text).Categories);
    }

    // ---- Conflicts: sets with more than one entry ----

    [Fact]
    public void TwoCategories_GiveTwoCandidates()
    {
        Assert.Equal(new[] { Category.Checkup, Category.Consultation }, _extractor.Extract("בדיקה או ייעוץ").Categories);
    }

    [Fact]
    public void TwoDays_GiveTwoCandidates()
    {
        Assert.Equal(
            new[] { DayReference.Of(DayOfWeek.Tuesday), DayReference.Of(DayOfWeek.Wednesday) },
            _extractor.Extract("שלישי או רביעי").Days);
    }

    [Fact]
    public void TwoWindows_GiveTwoCandidates()
    {
        var r = _extractor.Extract("יום שלישי בבוקר אחרי 16:00");

        Assert.Equal(new[] { new TimeWindow(WindowKind.Morning), After1600 }, r.Windows);
        Assert.Equal(new[] { DayReference.Of(DayOfWeek.Tuesday) }, r.Days);
    }

    [Fact]
    public void RelativeAndWeekday_GiveTwoCandidates()
    {
        Assert.Equal(
            new[] { DayReference.Of(RelativeDay.Tomorrow), DayReference.Of(DayOfWeek.Friday) },
            _extractor.Extract("מחר או שישי").Days);
    }

    [Theory]
    [InlineData("בדיקה, כן בדיקה")]
    [InlineData("יום שלישי, שלישי")]
    [InlineData("יום ג׳ ביום שלישי")]
    [InlineData("אחרי 16:00 או אחרי 16")]
    public void SameValueTwice_IsNotAConflict(string text)
    {
        var r = _extractor.Extract(text);

        Assert.True(r.Categories.Count <= 1);
        Assert.True(r.Days.Count <= 1);
        Assert.True(r.Windows.Count <= 1);
    }

    // ---- Nothing to extract ----

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("שלום, תודה רבה")]
    [InlineData("זה נקבע?")]
    public void UnrelatedText_IsEmpty(string? text)
    {
        var r = _extractor.Extract(text);

        Assert.Empty(r.Categories);
        Assert.Empty(r.Days);
        Assert.Empty(r.Windows);
        Assert.Equal(WeekHint.None, r.WeekHint);
        Assert.False(r.AmbiguousTime);
        Assert.Null(r.UnknownDayName);
    }
}
