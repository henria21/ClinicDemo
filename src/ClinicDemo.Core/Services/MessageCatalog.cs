using ClinicDemo.Core.Models;

namespace ClinicDemo.Core.Services;

/// <summary>
/// The only source of visitor-facing assistant text (NFR-1). Template text and "asserts" notes are
/// the approved v1 catalog (design §5). Any change here must be re-approved in
/// tests/ClinicDemo.Core.Tests/Approved/MessageCatalog.approved.txt. Stateless.
/// </summary>
public sealed class MessageCatalog
{
    private static readonly IReadOnlyDictionary<TemplateId, MessageTemplate> TemplateMap = new[]
    {
        new MessageTemplate(TemplateId.Disclaimer,
            "זהו דמו בלבד. אין להזין פרטים אישיים או רפואיים.",
            "Demo only, no personal data"),
        new MessageTemplate(TemplateId.AskCategory,
            "איזה סוג תור מבוקש: בדיקה, ביקור מעקב או ייעוץ?",
            "Question"),
        new MessageTemplate(TemplateId.AskDay,
            "באיזה יום מועדף לך להגיע?",
            "Question"),
        new MessageTemplate(TemplateId.AskWindow,
            "באיזה חלק של היום מועדף לך: בוקר, צהריים או אחר הצהריים?",
            "Question"),
        new MessageTemplate(TemplateId.ClarifyCategory,
            "הוזכרו כמה סוגי תור: {categories}. איזה מהם מבוקש?",
            "Question"),
        new MessageTemplate(TemplateId.ClarifyDay,
            "הוזכרו כמה ימים: {days}. איזה יום מועדף לך?",
            "Question"),
        new MessageTemplate(TemplateId.ClarifyWindow,
            "ההעדפות לשעה לא מתאימות זו לזו: {windows}. מה עדיף לך?",
            "Question"),
        new MessageTemplate(TemplateId.AskTime24h,
            "כדי לא לטעות בשעה, אפשר לכתוב אותה בפורמט 24 שעות, למשל 16:00?",
            "Question"),
        new MessageTemplate(TemplateId.DayOutOfRange,
            "אפשר לבחור יום מהיום ועד סוף השבוע הבא ({lastDate}). איזה יום מועדף לך?",
            "Range limit, question"),
        new MessageTemplate(TemplateId.Summary,
            "לסיכום: בקשה ל{category}, {time}, עבור {contact}. לשלוח את הבקשה לבדיקה של צוות הדמו?",
            "Restates the request, asks to send"),
        new MessageTemplate(TemplateId.Submitted,
            "בקשת הדמו נשמרה וממתינה לבדיקת הצוות. זה אינו אישור תור. לא נשלחה הודעה למרפאה אמיתית.",
            "Saved; not a booking; nothing sent"),
        new MessageTemplate(TemplateId.NotABookingBeforeSubmit,
            "אני לא יכול לקבוע או לאשר תורים. אפשר לשלוח בקשה, והיא תעבור לבדיקה של צוות הדמו.",
            "Cannot book"),
        new MessageTemplate(TemplateId.NotABookingAfterSubmit,
            "התור לא נקבע. נשלחה רק בקשה, והיא ממתינה לבדיקה של צוות הדמו.",
            "Not booked"),
        new MessageTemplate(TemplateId.MedicalHandoff,
            "הדמו הזה לא יכול לטפל בשאלות רפואיות או בפניות דחופות, ואין להשתמש בו כדי לקבל טיפול או ייעוץ.",
            "Cannot handle medical"),
        new MessageTemplate(TemplateId.OfferForm,
            "לא הצלחתי להבין. אפשר למלא את הפרטים בטופס הקצר.",
            "Fallback offer"),
        new MessageTemplate(TemplateId.NoChangeDetected,
            "לא זיהיתי שינוי בבקשה. זה הסיכום העדכני:",
            "No change; summary follows"),
        new MessageTemplate(TemplateId.RestartRequired,
            "השיחה הזו הופסקה. כדי להמשיך, אפשר להתחיל שיחה חדשה.",
            "Conversation stopped; restart needed"),
        new MessageTemplate(TemplateId.AlreadySent,
            "הבקשה כבר נשלחה. כדי להתחיל מחדש, אפשר לאפס את הדמו.",
            "Already sent"),
    }.ToDictionary(t => t.Id);

    private readonly HebrewFormatter _formatter;
    private readonly DemoOptions _options;

    public MessageCatalog(HebrewFormatter formatter, DemoOptions options)
    {
        _formatter = formatter ?? throw new ArgumentNullException(nameof(formatter));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>All approved templates, in <see cref="TemplateId"/> order.</summary>
    public IReadOnlyList<MessageTemplate> Templates { get; } =
        Enum.GetValues<TemplateId>().Where(TemplateMap.ContainsKey).Select(id => TemplateMap[id]).ToList();

    public MessageTemplate GetTemplate(TemplateId id) =>
        TemplateMap.TryGetValue(id, out var template)
            ? template
            : throw new ArgumentOutOfRangeException(nameof(id), id, "No template for this TemplateId.");

    /// <summary>Renders a template without arguments.</summary>
    public AssistantMessage Render(TemplateId id) => Render(id, MessageArgs.None);

    /// <summary>
    /// Renders a template, filling its placeholders from <paramref name="args"/>.
    /// Throws <see cref="ArgumentException"/> if a placeholder the template needs has no value.
    /// </summary>
    public AssistantMessage Render(TemplateId id, MessageArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var text = GetTemplate(id).Text;

        text = Fill(text, "{categories}", () =>
            _formatter.OrList(Required(args.Categories, nameof(args.Categories)).Select(_formatter.CategoryLabel)));
        text = Fill(text, "{days}", () => _formatter.OrList(DayOptions(args)));
        text = Fill(text, "{windows}", () =>
            _formatter.OrList(Required(args.Windows, nameof(args.Windows)).Select(_formatter.WindowLabel)));
        text = Fill(text, "{lastDate}", () =>
            _formatter.ShortDate(RequiredValue(args.LastDate, nameof(args.LastDate))));
        text = Fill(text, "{category}", () =>
            _formatter.CategoryLabel(RequiredValue(args.Category, nameof(args.Category))));
        text = Fill(text, "{time}", () =>
            _formatter.TimePreferencePhrase(Required(args.Time, nameof(args.Time)), _options.SimulationDate));
        text = Fill(text, "{contact}", () =>
            Required(args.Contact, nameof(args.Contact)).DisplayName);

        return new AssistantMessage(id, text);
    }

    private IEnumerable<string> DayOptions(MessageArgs args)
    {
        if (args.Days is null && args.Weeks is null)
        {
            throw new ArgumentException("Missing message argument: Days.", nameof(args.Days));
        }

        return (args.Days ?? Array.Empty<DayReference>()).Select(_formatter.DayReferenceLabel)
            .Concat((args.Weeks ?? Array.Empty<WeekHint>()).Select(_formatter.WeekHintLabel));
    }

    private static string Fill(string text, string placeholder, Func<string> value) =>
        text.Contains(placeholder, StringComparison.Ordinal)
            ? text.Replace(placeholder, value(), StringComparison.Ordinal)
            : text;

    private static T Required<T>(T? value, string name)
        where T : class =>
        value ?? throw new ArgumentException($"Missing message argument: {name}.", name);

    private static T RequiredValue<T>(T? value, string name)
        where T : struct =>
        value ?? throw new ArgumentException($"Missing message argument: {name}.", name);
}
