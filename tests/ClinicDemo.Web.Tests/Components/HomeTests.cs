using Bunit;
using ClinicDemo.Core.Models;
using ClinicDemo.Core.Repositories;
using ClinicDemo.Web.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace ClinicDemo.Web.Tests.Components;

/// <summary>T13: inbox pane, request cards, Reset, and the two-pane Home page.</summary>
public class HomeTests : TestContext
{
    private const string AppendixMessage1 = "שלום, אפשר לקבוע תור לבדיקה בשבוע הבא, אחר הצהריים?";
    private const string AppendixMessage2 = "יום שלישי אחרי 16:00.";

    public HomeTests()
    {
        Services.AddClinicDemo();
    }

    private RequestStore Store => Services.GetRequiredService<RequestStore>();

    private static void Send(IRenderedComponent<Home> cut, string text)
    {
        cut.Find("input.chat-text").Input(text);
        cut.Find("form.chat-input").Submit();
    }

    private static void RunAppendixA(IRenderedComponent<Home> cut)
    {
        cut.Find("button[data-contact='DEMO-A']").Click();
        Send(cut, AppendixMessage1);
        Send(cut, AppendixMessage2);
        cut.Find(".submit-button").Click();
    }

    // ---- End-to-end: Appendix A → one matching card (AC-1) ----

    [Fact]
    public void AppendixA_ThroughTheRenderedPage_ProducesOneMatchingCard()
    {
        var cut = RenderComponent<Home>();

        cut.Find("button[data-contact='DEMO-A']").Click();
        Send(cut, AppendixMessage1);
        Send(cut, AppendixMessage2);
        cut.Find(".submit-button").Click();

        Assert.Equal(
            new[]
            {
                AppendixMessage1,
                "זהו דמו בלבד. אין להזין פרטים אישיים או רפואיים.",
                "באיזה יום מועדף לך להגיע?",
                AppendixMessage2,
                "לסיכום: בקשה לבדיקה, ביום שלישי בשבוע הבא אחרי 16:00, עבור איש קשר לדוגמה א׳. לשלוח את הבקשה לבדיקה של צוות הדמו?",
                "בקשת הדמו נשמרה וממתינה לבדיקת הצוות. זה אינו אישור תור. לא נשלחה הודעה למרפאה אמיתית.",
            },
            cut.FindAll(".chat-pane .bubble-text").Select(e => e.TextContent));

        var card = Assert.Single(cut.FindAll(".request-card"));
        Assert.Equal("REQ-0001", card.QuerySelector(".card-id")!.TextContent);
        Assert.Equal("DEMO-A", card.QuerySelector(".card-contact")!.TextContent);
        Assert.Equal("בדיקה", card.QuerySelector(".card-category")!.TextContent);
        Assert.Equal("יום ג׳ 24.11, אחרי 16:00", card.QuerySelector(".card-time")!.TextContent);
        Assert.Equal("ממתין לבדיקת צוות", card.QuerySelector(".card-status")!.TextContent);
        Assert.Equal(
            new[] { AppendixMessage1, AppendixMessage2 },
            card.QuerySelectorAll(".card-wording-line").Select(e => e.TextContent));
        Assert.Single(Store.GetAll());
    }

    // ---- FR-15: the card appears without a reload ----

    [Fact]
    public void Submitting_MakesTheCardAppearInTheInbox()
    {
        var cut = RenderComponent<Home>();
        Assert.Equal("אין בקשות עדיין.", cut.Find(".inbox-empty").TextContent);

        RunAppendixA(cut);

        Assert.Empty(cut.FindAll(".inbox-empty"));
        Assert.Single(cut.FindAll(".inbox-pane .request-card"));
    }

    // ---- AC-7 / FR-16 ----

    [Fact]
    public void TheOnlyButtonsInTheInbox_AreReviewedAndNeedsClarification()
    {
        var cut = RenderComponent<Home>();
        RunAppendixA(cut);

        var labels = cut.FindAll(".inbox-pane button").Select(b => b.TextContent.Trim()).ToList();

        Assert.Equal(new[] { "נבדק", "דורש הבהרה" }, labels);
        Assert.Empty(cut.FindAll(".inbox-pane input, .inbox-pane select, .inbox-pane form"));
    }

    [Fact]
    public void StatusButtons_ChangeTheCardsStatus_AndSendNothingToTheChat()
    {
        var cut = RenderComponent<Home>();
        RunAppendixA(cut);
        var chatBubbles = cut.FindAll(".chat-pane .bubble").Count;

        cut.Find(".status-reviewed").Click();

        Assert.Equal("נבדק", cut.Find(".card-status").TextContent);
        Assert.Equal(RequestStatus.Reviewed, Store.GetAll()[0].Status);
        Assert.True(cut.Find(".status-reviewed").HasAttribute("disabled"));
        Assert.Equal(chatBubbles, cut.FindAll(".chat-pane .bubble").Count);

        cut.Find(".status-clarify").Click();

        Assert.Equal("דורש הבהרה", cut.Find(".card-status").TextContent);
        Assert.Equal(RequestStatus.NeedsClarification, Store.GetAll()[0].Status);
        Assert.Equal(chatBubbles, cut.FindAll(".chat-pane .bubble").Count);
    }

    // ---- AC-11 / FR-18 ----

    [Fact]
    public void Reset_EmptiesBothPanes_AndReturnsToTheContactPicker()
    {
        var cut = RenderComponent<Home>();
        RunAppendixA(cut);

        cut.Find("button.reset-button").Click();

        Assert.Empty(cut.FindAll(".request-card"));
        Assert.Equal("אין בקשות עדיין.", cut.Find(".inbox-empty").TextContent);
        Assert.Empty(cut.FindAll(".chat-pane .bubble"));
        Assert.Equal(3, cut.FindAll("button.contact-option").Count);
        Assert.Empty(Store.GetAll());
    }

    [Fact]
    public void AfterReset_TheNextCardIsREQ0001Again()
    {
        var cut = RenderComponent<Home>();
        RunAppendixA(cut);
        cut.Find("button.reset-button").Click();

        RunAppendixA(cut);

        Assert.Equal("REQ-0001", cut.Find(".card-id").TextContent);
    }

    [Fact]
    public void ResetIsOutsideTheInbox()
    {
        var cut = RenderComponent<Home>();

        Assert.Null(cut.Find(".inbox-pane").QuerySelector("button.reset-button"));
        Assert.Equal("איפוס הדמו", cut.Find(".app-bar button.reset-button").TextContent);
    }

    // ---- New conversation after submit ----

    [Fact]
    public void NewConversationButton_AppearsOnlyAfterSubmit()
    {
        var cut = RenderComponent<Home>();
        cut.Find("button[data-contact='DEMO-A']").Click();
        Send(cut, AppendixMessage1);
        Send(cut, AppendixMessage2);
        Assert.Empty(cut.FindAll("button.restart-button"));

        cut.Find(".submit-button").Click();

        Assert.Equal("שיחה חדשה", cut.Find("button.restart-after-submit").TextContent);
        Assert.NotNull(cut.Find("form.chat-input"));
    }

    [Fact]
    public void AfterSubmit_TheInputStillAnswersIsItBooked()
    {
        var cut = RenderComponent<Home>();
        RunAppendixA(cut);

        Send(cut, "זה נקבע?");

        Assert.Equal(
            "התור לא נקבע. נשלחה רק בקשה, והיא ממתינה לבדיקה של צוות הדמו.",
            cut.FindAll(".chat-pane .bubble-text").Last().TextContent);
        Assert.NotNull(cut.Find("button.restart-after-submit"));
    }

    [Fact]
    public void NewConversationAfterSubmit_KeepsTheInbox_AndReturnsToThePicker()
    {
        var cut = RenderComponent<Home>();
        RunAppendixA(cut);

        cut.Find("button.restart-after-submit").Click();

        Assert.Equal(3, cut.FindAll("button.contact-option").Count);
        Assert.Empty(cut.FindAll(".chat-pane .bubble"));
        Assert.Empty(cut.FindAll("button.restart-button"));
        Assert.Equal("REQ-0001", Assert.Single(cut.FindAll(".request-card")).QuerySelector(".card-id")!.TextContent);
    }

    // ---- FR-1 ----

    [Fact]
    public void TheRoot_IsHebrewRtl_WithBothPanes()
    {
        var cut = RenderComponent<Home>();

        var root = cut.Find(".app");
        Assert.Equal("rtl", root.GetAttribute("dir"));
        Assert.Equal("he", root.GetAttribute("lang"));
        Assert.NotNull(root.QuerySelector(".panes > .chat-pane"));
        Assert.NotNull(root.QuerySelector(".panes > .inbox-pane"));
    }

    [Fact]
    public void SeveralRequests_ShowInCreationOrder()
    {
        var cut = RenderComponent<Home>();
        RunAppendixA(cut);
        cut.Find("button.restart-after-submit").Click();
        cut.Find("button[data-contact='DEMO-C']").Click();
        Send(cut, "ייעוץ ביום שני בבוקר");
        cut.Find(".submit-button").Click();

        Assert.Equal(new[] { "REQ-0001", "REQ-0002" }, cut.FindAll(".card-id").Select(e => e.TextContent));
        Assert.Equal(new[] { "DEMO-A", "DEMO-C" }, cut.FindAll(".card-contact").Select(e => e.TextContent));
    }
}
