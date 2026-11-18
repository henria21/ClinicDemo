using ClinicDemo.Core.Models;
using ClinicDemo.Core.Services;
using ClinicDemo.Core.Session;
using ClinicDemo.Core.Tests.TestData;

namespace ClinicDemo.Core.Tests.Services;

public class ConversationEngineTests
{
    private const string AppendixMessage1 = "שלום, אפשר לקבוע תור לבדיקה בשבוע הבא, אחר הצהריים?";
    private const string AppendixMessage2 = "יום שלישי אחרי 16:00.";

    private readonly ConversationEngine _engine = EngineFactory.CreateEngine();
    private readonly ConversationSession _session = EngineFactory.CreateSession();

    private static string Text(IReadOnlyList<AssistantMessage> replies) => string.Join(" ", replies.Select(r => r.Text));

    private static TemplateId[] Ids(IReadOnlyList<AssistantMessage> replies) => replies.Select(r => r.Id).ToArray();

    // ---- AC-1: Appendix A ----

    [Fact]
    public void AppendixA_ProducesTheBriefsTextAndSnapshot()
    {
        var first = _engine.Handle(_session, AppendixMessage1);
        Assert.Equal("זהו דמו בלבד. אין להזין פרטים אישיים או רפואיים. באיזה יום מועדף לך להגיע?", Text(first));
        Assert.Equal(ConversationStage.Collecting, _session.Stage);
        Assert.Null(_session.ActiveReview);

        var second = _engine.Handle(_session, AppendixMessage2);
        Assert.Equal(
            "לסיכום: בקשה לבדיקה, ביום שלישי בשבוע הבא אחרי 16:00, עבור איש קשר לדוגמה א׳. לשלוח את הבקשה לבדיקה של צוות הדמו?",
            Text(second));

        var review = Assert.IsType<ReviewSnapshot>(_session.ActiveReview);
        Assert.Equal(ConversationStage.Reviewing, _session.Stage);
        Assert.Equal("DEMO-A", review.ContactId);
        Assert.Equal(Category.Checkup, review.Category);
        Assert.Equal(new DateOnly(2026, 11, 24), review.TimePreference.Day);
        Assert.Equal(new TimeWindow(WindowKind.After, new TimeOnly(16, 0)), review.TimePreference.Window);
        Assert.Equal(new[] { AppendixMessage1, AppendixMessage2 }, review.Wording);
    }

    [Fact]
    public void Disclaimer_PrefixesOnlyTheFirstReply()
    {
        var first = _engine.Handle(_session, "בדיקה");
        var second = _engine.Handle(_session, "יום שלישי");

        Assert.Equal(new[] { TemplateId.Disclaimer, TemplateId.AskDay }, Ids(first));
        Assert.Equal(new[] { TemplateId.AskWindow }, Ids(second));
        Assert.Equal(3, _session.Messages.Count);
    }

    // ---- AC-2: one follow-up for a missing field ----

    [Fact]
    public void MissingDay_GetsExactlyOneAskDay()
    {
        var replies = _engine.Handle(_session, "בדיקה בשבוע הבא אחר הצהריים");

        Assert.Equal(new[] { TemplateId.Disclaimer, TemplateId.AskDay }, Ids(replies));
    }

    [Fact]
    public void MissingCategory_GetsExactlyOneAskCategory()
    {
        var replies = _engine.Handle(_session, "יום שלישי אחרי 16:00");

        Assert.Equal(new[] { TemplateId.Disclaimer, TemplateId.AskCategory }, Ids(replies));
        Assert.Null(_session.ActiveReview);
    }

    [Fact]
    public void MissingFields_AreAskedInOrder_CategoryDayWindow()
    {
        Assert.Equal(TemplateId.AskCategory, _engine.Handle(_session, "שבוע הבא").Last().Id);
        Assert.Equal(TemplateId.AskDay, _engine.Handle(_session, "ייעוץ").Last().Id);
        Assert.Equal(TemplateId.AskWindow, _engine.Handle(_session, "חמישי").Last().Id);
        Assert.Equal(TemplateId.Summary, _engine.Handle(_session, "בבוקר").Last().Id);
        Assert.Equal(new DateOnly(2026, 11, 26), _session.ActiveReview!.TimePreference.Day);
    }

    // ---- AC-4 / FR-7: conflicts ----

    [Fact]
    public void MorningAfter1600_AsksClarifyWindow_NoSummary()
    {
        var replies = _engine.Handle(_session, "בדיקה יום שלישי בבוקר אחרי 16:00");

        Assert.Equal(TemplateId.ClarifyWindow, replies.Last().Id);
        Assert.Equal("ההעדפות לשעה לא מתאימות זו לזו: בוקר או אחרי 16:00. מה עדיף לך?", replies.Last().Text);
        Assert.Null(_session.ActiveReview);
        Assert.Null(_session.Draft.Window);
    }

    [Fact]
    public void TwoDays_AsksClarifyDay()
    {
        var replies = _engine.Handle(_session, "בדיקה שלישי או רביעי בבוקר");

        Assert.Equal(TemplateId.ClarifyDay, replies.Last().Id);
        Assert.Equal("הוזכרו כמה ימים: יום שלישי או יום רביעי. איזה יום מועדף לך?", replies.Last().Text);
        Assert.Null(_session.Draft.DayReference);
        Assert.Null(_session.ActiveReview);
    }

    [Fact]
    public void TwoCategories_AsksClarifyCategory()
    {
        var replies = _engine.Handle(_session, "בדיקה או ייעוץ ביום שלישי בבוקר");

        Assert.Equal(TemplateId.ClarifyCategory, replies.Last().Id);
        Assert.Equal("הוזכרו כמה סוגי תור: בדיקה או ייעוץ. איזה מהם מבוקש?", replies.Last().Text);
        Assert.Null(_session.Draft.Category);
        Assert.Null(_session.ActiveReview);
    }

    [Fact]
    public void Conflict_IsResolvedByTheNextAnswer()
    {
        _engine.Handle(_session, "בדיקה יום שלישי בבוקר אחרי 16:00");
        var replies = _engine.Handle(_session, "אחרי 16:00");

        Assert.Equal(TemplateId.Summary, replies.Last().Id);
        Assert.Equal(new TimeWindow(WindowKind.After, new TimeOnly(16, 0)), _session.ActiveReview!.TimePreference.Window);
    }

    [Theory]
    [InlineData("בבוקר אחרי 9:30", WindowKind.After)]
    [InlineData("אחר הצהריים אחרי 16:00", WindowKind.After)]
    [InlineData("בצהריים", WindowKind.Midday)]
    public void CompatibleWindows_DoNotConflict(string window, WindowKind expected)
    {
        var replies = _engine.Handle(_session, "בדיקה ביום שלישי " + window);

        Assert.Equal(TemplateId.Summary, replies.Last().Id);
        Assert.Equal(expected, _session.ActiveReview!.TimePreference.Window.Kind);
    }

    [Theory]
    [InlineData("בבוקר או בצהריים")]
    [InlineData("אחר הצהריים אחרי 9:00")]
    [InlineData("אחרי 14:00 או אחרי 16:00")]
    public void IncompatibleWindows_Conflict(string window)
    {
        var replies = _engine.Handle(_session, "בדיקה ביום שלישי " + window);

        Assert.Equal(TemplateId.ClarifyWindow, replies.Last().Id);
    }

    // ---- 24-hour times ----

    [Theory]
    [InlineData("אחרי 4")]
    [InlineData("אחרי ארבע")]
    [InlineData("בבוקר אחרי 4")]
    public void AmbiguousTime_AsksTime24h_AndNeverGuesses(string time)
    {
        var replies = _engine.Handle(_session, "בדיקה ביום שלישי " + time);

        Assert.Equal(TemplateId.AskTime24h, replies.Last().Id);
        Assert.Null(_session.Draft.Window);
        Assert.Null(_session.ActiveReview);
    }

    // ---- Day range ----

    [Fact]
    public void SundayThisWeek_IsOutOfRange_AndTheNextAnswerIsNotTrapped()
    {
        var replies = _engine.Handle(_session, "בדיקה ראשון השבוע בבוקר");

        Assert.Equal(TemplateId.DayOutOfRange, replies.Last().Id);
        Assert.Equal("אפשר לבחור יום מהיום ועד סוף השבוע הבא (28.11). איזה יום מועדף לך?", replies.Last().Text);
        Assert.Null(_session.Draft.DayReference);

        _engine.Handle(_session, "ראשון");
        Assert.Equal(new DateOnly(2026, 11, 22), _session.ActiveReview!.TimePreference.Day);
    }

    // ---- AC-9 (chat side) / FR-14 ----

    [Fact]
    public void TwoUnparseableAnswers_OfferTheForm()
    {
        _engine.Handle(_session, "בדיקה בבוקר");

        var first = _engine.Handle(_session, "לא יודע");
        Assert.Equal(TemplateId.AskDay, first.Last().Id);
        Assert.Equal(ConversationStage.Collecting, _session.Stage);

        var second = _engine.Handle(_session, "אולי");
        Assert.Equal(new[] { TemplateId.OfferForm }, Ids(second));
        Assert.Equal(ConversationStage.Editing, _session.Stage);
    }

    [Fact]
    public void AnUnderstoodAnswer_ResetsTheMissCounter()
    {
        _engine.Handle(_session, "בדיקה");
        _engine.Handle(_session, "לא יודע");
        _engine.Handle(_session, "בבוקר");

        var replies = _engine.Handle(_session, "אולי");

        Assert.Equal(TemplateId.AskDay, replies.Last().Id);
        Assert.Equal(ConversationStage.Collecting, _session.Stage);
    }

    [Fact]
    public void OriginalWording_KeepsEveryPreferenceMessageInOrder()
    {
        _engine.Handle(_session, "בדיקה");
        _engine.Handle(_session, "לא יודע");
        _engine.Handle(_session, "חמישי בבוקר");

        Assert.Equal(new[] { "בדיקה", "לא יודע", "חמישי בבוקר" }, _session.ActiveReview!.Wording);
    }
}
