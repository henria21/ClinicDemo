using Bunit;
using ClinicDemo.Core.Session;
using ClinicDemo.Web.Components;
using Microsoft.Extensions.DependencyInjection;

namespace ClinicDemo.Web.Tests.Components;

public class ChatPaneTests : TestContext
{
    private const string Banner = "דמו בלבד. אין להזין פרטים אישיים או רפואיים.";
    private const string SimulationDate = "תאריך סימולציה: יום ד׳, 18.11";

    public ChatPaneTests()
    {
        Services.AddClinicDemo();
    }

    private DemoAppState State => Services.GetRequiredService<DemoAppState>();

    private static void Send(IRenderedComponent<ChatPane> cut, string text)
    {
        cut.Find("input.chat-text").Input(text);
        cut.Find("form.chat-input").Submit();
    }

    private static IReadOnlyList<string> BubbleTexts(IRenderedComponent<ChatPane> cut) =>
        cut.FindAll(".bubble-text").Select(e => e.TextContent).ToList();

    // ---- FR-2 / FR-3 ----

    [Fact]
    public void BannerAndSimulationDate_RenderBeforeAContactIsPicked()
    {
        var cut = RenderComponent<ChatPane>();

        Assert.Equal(Banner, cut.Find(".demo-banner").TextContent);
        Assert.Equal(SimulationDate, cut.Find(".simulation-date").TextContent);
    }

    [Fact]
    public void BannerAndSimulationDate_StayDuringTheChat_AndAfterAHandoff()
    {
        var cut = RenderComponent<ChatPane>();
        cut.Find("button[data-contact='DEMO-A']").Click();
        Send(cut, "בדיקה");
        Send(cut, "יש לי כאב חזק, מה לקחת?");

        Assert.Equal(Banner, cut.Find(".demo-banner").TextContent);
        Assert.Equal(SimulationDate, cut.Find(".simulation-date").TextContent);
    }

    // ---- FR-4 ----

    [Fact]
    public void ChatIsDisabledUntilAContactIsPicked()
    {
        var cut = RenderComponent<ChatPane>();

        Assert.True(cut.Find("input.chat-text").HasAttribute("disabled"));
        Assert.True(cut.Find("button.send-button").HasAttribute("disabled"));
        Assert.Equal(3, cut.FindAll("button.contact-option").Count);

        cut.Find("button[data-contact='DEMO-A']").Click();

        Assert.False(cut.Find("input.chat-text").HasAttribute("disabled"));
        Assert.False(cut.Find("button.send-button").HasAttribute("disabled"));
        Assert.Empty(cut.FindAll("button.contact-option"));
        Assert.Contains("איש קשר לדוגמה א׳", cut.Find(".chat-contact").TextContent);
    }

    [Fact]
    public void ThePickerHasNoPersonalDataFields()
    {
        var cut = RenderComponent<ChatPane>();

        Assert.Empty(cut.FindAll("input[type=email], input[type=tel], input[name]"));
        Assert.Single(cut.FindAll("input"));
    }

    // ---- Sending ----

    [Fact]
    public void Sending_RendersTheVisitorBubbleAndTheEnginesReplies()
    {
        var cut = RenderComponent<ChatPane>();
        cut.Find("button[data-contact='DEMO-A']").Click();

        Send(cut, "שלום, אפשר לקבוע תור לבדיקה בשבוע הבא, אחר הצהריים?");

        Assert.Equal(
            new[]
            {
                "שלום, אפשר לקבוע תור לבדיקה בשבוע הבא, אחר הצהריים?",
                "זהו דמו בלבד. אין להזין פרטים אישיים או רפואיים.",
                "באיזה יום מועדף לך להגיע?",
            },
            BubbleTexts(cut));
        Assert.Single(cut.FindAll(".bubble-visitor"));
        Assert.Equal(2, cut.FindAll(".bubble-assistant").Count);
        Assert.Equal(string.Empty, cut.Find("input.chat-text").GetAttribute("value") ?? string.Empty);
    }

    [Fact]
    public void AnEmptyMessage_IsNotSent()
    {
        var cut = RenderComponent<ChatPane>();
        cut.Find("button[data-contact='DEMO-A']").Click();

        Send(cut, "   ");

        Assert.Empty(cut.FindAll(".bubble"));
        Assert.Empty(State.Transcript);
    }

    // ---- FR-12: medical handoff ----

    [Fact]
    public void AfterAMedicalMessage_TheInputIsReplacedByNewConversation()
    {
        var cut = RenderComponent<ChatPane>();
        cut.Find("button[data-contact='DEMO-A']").Click();

        Send(cut, "יש לי כאב חזק, מה לקחת?");

        Assert.Empty(cut.FindAll("form.chat-input"));
        var restart = cut.Find("button.restart-button");
        Assert.Equal("שיחה חדשה", restart.TextContent);
        Assert.Equal(
            "הדמו הזה לא יכול לטפל בשאלות רפואיות או בפניות דחופות, ואין להשתמש בו כדי לקבל טיפול או ייעוץ.",
            BubbleTexts(cut).Last());

        restart.Click();

        Assert.Equal(3, cut.FindAll("button.contact-option").Count);
        Assert.Empty(cut.FindAll(".bubble"));
        Assert.True(cut.Find("input.chat-text").HasAttribute("disabled"));
    }

    // ---- Chat text is text, not markup ----

    [Fact]
    public void ChatText_IsRenderedAsText_NotMarkup()
    {
        var cut = RenderComponent<ChatPane>();
        cut.Find("button[data-contact='DEMO-A']").Click();

        Send(cut, "<b>בדיקה</b><script>alert(1)</script>");

        Assert.Empty(cut.FindAll(".bubble b"));
        Assert.Empty(cut.FindAll(".bubble script"));
        Assert.Equal("<b>בדיקה</b><script>alert(1)</script>", cut.Find(".bubble-visitor .bubble-text").TextContent);
        Assert.Contains("&lt;b&gt;", cut.Markup);
    }

    // ---- Observer ----

    [Fact]
    public void ChangesMadeThroughTheFacade_ReRenderThePane()
    {
        var cut = RenderComponent<ChatPane>();

        cut.InvokeAsync(() => State.SelectContact("DEMO-B"));
        cut.InvokeAsync(() => State.Send("בדיקה"));

        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll(".bubble").Count));
        Assert.Contains("איש קשר לדוגמה ב׳", cut.Find(".chat-contact").TextContent);
    }
}
