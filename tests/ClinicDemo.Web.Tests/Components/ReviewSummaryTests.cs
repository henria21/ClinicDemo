using Bunit;
using ClinicDemo.Core.Models;
using ClinicDemo.Core.Repositories;
using ClinicDemo.Core.Session;
using ClinicDemo.Web.Components;
using Microsoft.Extensions.DependencyInjection;

namespace ClinicDemo.Web.Tests.Components;

/// <summary>T12: the review summary (AC-8, I-5) and the Edit / fallback form.</summary>
public class ReviewSummaryTests : TestContext
{
    private const string AppendixMessage1 = "שלום, אפשר לקבוע תור לבדיקה בשבוע הבא, אחר הצהריים?";
    private const string AppendixMessage2 = "יום שלישי אחרי 16:00.";

    public ReviewSummaryTests()
    {
        Services.AddClinicDemo();
    }

    private DemoAppState State => Services.GetRequiredService<DemoAppState>();

    private RequestStore Store => Services.GetRequiredService<RequestStore>();

    private static void Send(IRenderedComponent<ChatPane> cut, string text)
    {
        cut.Find("input.chat-text").Input(text);
        cut.Find("form.chat-input").Submit();
    }

    private IRenderedComponent<ChatPane> RenderAtReview()
    {
        var cut = RenderComponent<ChatPane>();
        cut.Find("button[data-contact='DEMO-A']").Click();
        Send(cut, AppendixMessage1);
        Send(cut, AppendixMessage2);
        return cut;
    }

    // ---- Summary ----

    [Fact]
    public void TheSummary_ShowsAppendixAText_WithEditAndSubmit()
    {
        var cut = RenderAtReview();

        var summary = cut.Find(".bubble-summary");
        Assert.Equal(
            "לסיכום: בקשה לבדיקה, ביום שלישי בשבוע הבא אחרי 16:00, עבור איש קשר לדוגמה א׳. לשלוח את הבקשה לבדיקה של צוות הדמו?",
            summary.QuerySelector(".bubble-text")!.TextContent);
        Assert.Equal("עריכה", summary.QuerySelector(".edit-button")!.TextContent);
        Assert.Equal("שליחת בקשה", summary.QuerySelector(".submit-button")!.TextContent);
    }

    [Fact]
    public void Submit_CreatesTheCard_AndShowsTheNotABookingMessage()
    {
        var cut = RenderAtReview();

        cut.Find(".submit-button").Click();

        var card = Assert.Single(Store.GetAll());
        Assert.Equal("REQ-0001", card.Id);
        Assert.Equal(
            "בקשת הדמו נשמרה וממתינה לבדיקת הצוות. זה אינו אישור תור. לא נשלחה הודעה למרפאה אמיתית.",
            cut.FindAll(".bubble-text").Last().TextContent);
        Assert.Empty(cut.FindAll(".submit-button"));
        Assert.Empty(cut.FindAll(".edit-button"));
    }

    // ---- AC-8 / FR-19 ----

    [Fact]
    public async Task AFastDoubleClick_CreatesOneCard()
    {
        var cut = RenderAtReview();
        var button = cut.Find(".submit-button");
        var reviewId = State.Session!.ActiveReview!.ReviewId;

        button.Click();
        // The second click lands on the same (now stale) button. In the browser it is disabled or gone;
        // here bUnit may report its handler as gone. Either way it must not create a second card.
        _ = Record.Exception(() => button.Click());
        // Replaying the same submit through the facade is idempotent as well.
        var replay = await cut.InvokeAsync(() => State.Submit(reviewId));

        Assert.Single(Store.GetAll());
        Assert.Equal(SubmitOutcome.Duplicate, replay.Outcome);
        Assert.Empty(cut.FindAll(".submit-button:not([disabled])"));
    }

    [Fact]
    public void TheSubmitButton_DisablesOnTheFirstClick()
    {
        RenderAtReview();
        var review = State.Session!.ActiveReview!;
        var entry = State.Transcript.Single(e => e.Template == TemplateId.Summary);

        // Put the card in the store directly, so the click below is a Duplicate: the review stays active and
        // the bubble keeps its buttons, which lets the test see that the component disabled them on the click.
        var summary = RenderComponent<ReviewSummary>(p => p.Add(s => s.Entry, entry));
        Store.Submit(review);
        summary.Find(".submit-button").Click();

        Assert.True(summary.Find(".submit-button").HasAttribute("disabled"));
        Assert.True(summary.Find(".edit-button").HasAttribute("disabled"));
        Assert.Single(Store.GetAll());
    }

    // ---- I-5 ----

    [Fact]
    public void AfterAChatChange_OnlyTheNewestSummaryHasButtons()
    {
        var cut = RenderAtReview();

        Send(cut, "בעצם חמישי");

        var summaries = cut.FindAll(".bubble-summary");
        Assert.Equal(2, summaries.Count);
        Assert.Null(summaries[0].QuerySelector(".submit-button"));
        Assert.Null(summaries[0].QuerySelector(".edit-button"));
        Assert.NotNull(summaries[1].QuerySelector(".submit-button"));
        Assert.Contains("ביום חמישי בשבוע הבא", summaries[1].QuerySelector(".bubble-text")!.TextContent);

        Assert.Single(cut.FindAll(".submit-button"));
    }

    [Fact]
    public void AfterAConflictDuringReview_NoSummaryHasButtons()
    {
        var cut = RenderAtReview();

        Send(cut, "רביעי או חמישי");

        Assert.Single(cut.FindAll(".bubble-summary"));
        Assert.Empty(cut.FindAll(".submit-button"));
    }

    [Fact]
    public void AfterAMedicalHandoff_NoSubmitExists()
    {
        var cut = RenderAtReview();

        Send(cut, "יש לי כאב חזק, מה לקחת?");

        Assert.Empty(cut.FindAll(".submit-button"));
        Assert.Empty(cut.FindAll(".edit-button"));
        Assert.NotNull(cut.Find("button.restart-button"));
        Assert.Empty(Store.GetAll());
    }

    // ---- Edit form ----

    [Fact]
    public void Edit_OpensTheFormPreFilled_WithTheWeekSelectorOnTheDraftsHint()
    {
        var cut = RenderAtReview();

        cut.Find(".edit-button").Click();

        Assert.Equal(nameof(Category.Checkup), cut.Find("select.form-category").GetAttribute("value"));
        Assert.Equal(nameof(DayOfWeek.Tuesday), cut.Find("select.form-weekday").GetAttribute("value"));
        Assert.Equal(nameof(WeekHint.NextWeek), cut.Find("select.form-week").GetAttribute("value"));
        Assert.Equal(nameof(WindowKind.After), cut.Find("select.form-window").GetAttribute("value"));
        Assert.Equal("16:00", cut.Find("input.form-time").GetAttribute("value"));
        Assert.Empty(cut.FindAll(".submit-button"));
    }

    [Fact]
    public void Edit_WithoutAHint_DefaultsTheWeekSelectorToTheResolvedWeek()
    {
        var cut = RenderComponent<ChatPane>();
        cut.Find("button[data-contact='DEMO-A']").Click();
        Send(cut, "בדיקה ביום שני בבוקר"); // no hint: the nearest Monday is 23.11, next week

        cut.Find(".edit-button").Click();

        Assert.Equal(nameof(DayOfWeek.Monday), cut.Find("select.form-weekday").GetAttribute("value"));
        Assert.Equal(nameof(WeekHint.NextWeek), cut.Find("select.form-week").GetAttribute("value"));
        Assert.Equal(nameof(WindowKind.Morning), cut.Find("select.form-window").GetAttribute("value"));
        Assert.Empty(cut.FindAll("input.form-time"));
    }

    [Fact]
    public void SavingTheForm_GivesANewSummary_ThatSubmitsTheEditedValues()
    {
        var cut = RenderAtReview();
        var oldId = State.Session!.ActiveReview!.ReviewId;
        cut.Find(".edit-button").Click();

        cut.Find("select.form-category").Change(nameof(Category.Consultation));
        cut.Find("select.form-weekday").Change(nameof(DayOfWeek.Thursday));
        cut.Find("select.form-week").Change(nameof(WeekHint.ThisWeek));
        cut.Find("select.form-window").Change(nameof(WindowKind.Morning));
        cut.Find("form.preference-form").Submit();

        Assert.Empty(cut.FindAll("form.preference-form"));
        var summaries = cut.FindAll(".bubble-summary");
        Assert.Equal(2, summaries.Count);
        Assert.Contains("בקשה לייעוץ, ביום חמישי השבוע בבוקר", summaries[1].QuerySelector(".bubble-text")!.TextContent);
        Assert.NotEqual(oldId, State.Session!.ActiveReview!.ReviewId);

        cut.Find(".submit-button").Click();
        var card = Assert.Single(Store.GetAll());
        Assert.Equal(Category.Consultation, card.Category);
        Assert.Equal(new DateOnly(2026, 11, 19), card.TimePreference.Day);
    }

    [Fact]
    public void TheForm_RejectsANon24HourTime_AndStaysOpen()
    {
        var cut = RenderAtReview();
        cut.Find(".edit-button").Click();

        cut.Find("input.form-time").Change("4pm");
        cut.Find("form.preference-form").Submit();

        Assert.Equal("יש לכתוב שעה בפורמט 24 שעות, למשל 16:00.", cut.Find(".form-error").TextContent);
        Assert.NotNull(cut.Find("form.preference-form"));
        Assert.Null(State.Session!.ActiveReview);
    }

    [Fact]
    public void TheForm_RejectsADayOutOfRange_AndStaysOpen()
    {
        var cut = RenderAtReview();
        cut.Find(".edit-button").Click();

        cut.Find("select.form-weekday").Change(nameof(DayOfWeek.Sunday));
        cut.Find("select.form-week").Change(nameof(WeekHint.ThisWeek));
        cut.Find("form.preference-form").Submit();

        Assert.Equal("אפשר לבחור יום מהיום ועד סוף השבוע הבא (28.11).", cut.Find(".form-error").TextContent);
        Assert.NotNull(cut.Find("form.preference-form"));
    }

    [Fact]
    public void CancellingTheForm_GivesAFreshSummaryWithButtons()
    {
        var cut = RenderAtReview();
        var oldId = State.Session!.ActiveReview!.ReviewId;
        cut.Find(".edit-button").Click();

        cut.Find("button.form-cancel").Click();

        Assert.Empty(cut.FindAll("form.preference-form"));
        Assert.Equal(2, cut.FindAll(".bubble-summary").Count);
        Assert.Single(cut.FindAll(".submit-button"));
        Assert.NotEqual(oldId, State.Session!.ActiveReview!.ReviewId);
    }

    [Fact]
    public void TheFallbackForm_OpensAfterTwoMisses_AndLeadsToACard()
    {
        var cut = RenderComponent<ChatPane>();
        cut.Find("button[data-contact='DEMO-A']").Click();
        Send(cut, "שלום");
        Send(cut, "לא יודע");

        Assert.Equal(
            "לא הצלחתי להבין. אפשר למלא את הפרטים בטופס הקצר.",
            cut.FindAll(".bubble-text").Last().TextContent);
        cut.Find("select.form-category").Change(nameof(Category.FollowUp));
        cut.Find("select.form-weekday").Change(nameof(DayOfWeek.Monday));
        cut.Find("select.form-week").Change(nameof(WeekHint.NextWeek));
        cut.Find("select.form-window").Change(nameof(WindowKind.After));
        cut.Find("input.form-time").Change("9:30");
        cut.Find("form.preference-form").Submit();
        cut.Find(".submit-button").Click();

        var card = Assert.Single(Store.GetAll());
        Assert.Equal(Category.FollowUp, card.Category);
        Assert.Equal(new DateOnly(2026, 11, 23), card.TimePreference.Day);
        Assert.Equal(new TimeOnly(9, 30), card.TimePreference.Window.After);
    }
}
