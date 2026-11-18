using ClinicDemo.Core.Models;
using ClinicDemo.Core.Services;
using ClinicDemo.Core.Session;
using ClinicDemo.Core.Tests.TestData;

namespace ClinicDemo.Core.Tests.Session;

/// <summary>The week hint persists across messages (simulation date Wed 18.11.2026). Collecting part (T8).</summary>
public class WeekContextTests
{
    private readonly ConversationEngine _engine = EngineFactory.CreateEngine();
    private readonly ConversationSession _session = EngineFactory.CreateSession();

    [Theory]
    [InlineData("חמישי", 26)]
    [InlineData("שלישי", 24)]
    [InlineData("שבת", 28)]
    public void NextWeekHint_PersistsToALaterDayAnswer(string day, int expectedDay)
    {
        _engine.Handle(_session, "בדיקה בשבוע הבא אחר הצהריים");
        _engine.Handle(_session, day);

        Assert.Equal(new DateOnly(2026, 11, expectedDay), _session.ActiveReview!.TimePreference.Day);
    }

    [Fact]
    public void ThursdayWithoutHint_IsTheNearestThursday()
    {
        _engine.Handle(_session, "בדיקה אחר הצהריים");
        _engine.Handle(_session, "חמישי");

        Assert.Equal(new DateOnly(2026, 11, 19), _session.ActiveReview!.TimePreference.Day);
    }

    [Fact]
    public void TomorrowWithNextWeek_AsksClarifyDay()
    {
        _engine.Handle(_session, "בדיקה בשבוע הבא אחר הצהריים");
        var replies = _engine.Handle(_session, "מחר");

        Assert.Equal(TemplateId.ClarifyDay, replies.Last().Id);
        Assert.Equal("הוזכרו כמה ימים: מחר או בשבוע הבא. איזה יום מועדף לך?", replies.Last().Text);
        Assert.Null(_session.Draft.DayReference);
        Assert.Null(_session.ActiveReview);

        _engine.Handle(_session, "מחר");
        Assert.Equal(new DateOnly(2026, 11, 19), _session.ActiveReview!.TimePreference.Day);
    }

    // ---- During review (T9, AC-3) ----

    [Fact]
    public void ChangingTheDayDuringANextWeekReview_KeepsTheHint()
    {
        _engine.Handle(_session, "שלום, אפשר לקבוע תור לבדיקה בשבוע הבא, אחר הצהריים?");
        _engine.Handle(_session, "יום שלישי אחרי 16:00.");
        var firstId = _session.ActiveReview!.ReviewId;

        var replies = _engine.Handle(_session, "בעצם חמישי");

        Assert.Equal(TemplateId.Summary, replies.Last().Id);
        Assert.Equal(new DateOnly(2026, 11, 26), _session.ActiveReview!.TimePreference.Day);
        Assert.NotEqual(firstId, _session.ActiveReview.ReviewId);
        Assert.Contains("ביום חמישי בשבוע הבא אחרי 16:00", replies.Last().Text);
    }

    [Fact]
    public void ChangingOnlyTheWeek_ReResolvesTheSameWeekday()
    {
        _engine.Handle(_session, "שלום, אפשר לקבוע תור לבדיקה בשבוע הבא, אחר הצהריים?");
        _engine.Handle(_session, "יום שלישי אחרי 16:00.");
        _engine.Handle(_session, "בעצם חמישי");
        var thursdayId = _session.ActiveReview!.ReviewId;

        var replies = _engine.Handle(_session, "בעצם בשבוע הזה");

        Assert.Equal(TemplateId.Summary, replies.Last().Id);
        Assert.Equal(new DateOnly(2026, 11, 19), _session.ActiveReview!.TimePreference.Day);
        Assert.NotEqual(thursdayId, _session.ActiveReview.ReviewId);
    }

    [Fact]
    public void EditFormOnANextWeekDraft_KeepsTheHintForTheSelector()
    {
        _engine.Handle(_session, "בדיקה בשבוע הבא אחר הצהריים");
        _engine.Handle(_session, "חמישי");

        _engine.OpenEdit(_session);

        Assert.Equal(WeekHint.NextWeek, _session.Draft.WeekHint);
        Assert.Equal(DayReference.Of(DayOfWeek.Thursday), _session.Draft.DayReference);
    }

    [Fact]
    public void BothWeeksInOneMessage_AsksClarifyDay()
    {
        var replies = _engine.Handle(_session, "בדיקה חמישי השבוע או בשבוע הבא בבוקר");

        Assert.Equal(TemplateId.ClarifyDay, replies.Last().Id);
        Assert.Equal("הוזכרו כמה ימים: יום חמישי, השבוע או בשבוע הבא. איזה יום מועדף לך?", replies.Last().Text);
        Assert.Null(_session.Draft.DayReference);
        Assert.Null(_session.ActiveReview);

        _engine.Handle(_session, "חמישי בשבוע הבא");
        Assert.Equal(new DateOnly(2026, 11, 26), _session.ActiveReview!.TimePreference.Day);
    }
}
