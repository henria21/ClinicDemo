using ClinicDemo.Core.Models;
using ClinicDemo.Core.Repositories;
using ClinicDemo.Core.Services;

namespace ClinicDemo.Core.Session;

/// <summary>
/// The facade the UI talks to (design §4): one call per user action, then <see cref="Changed"/>.
/// Owns the current chat session (null while the contact picker is shown), the chat transcript, and the store.
/// Everything is in memory; a browser refresh resets it too.
/// </summary>
public sealed class DemoAppState
{
    private readonly ConversationEngine _engine;
    private readonly RequestStore _store;
    private readonly List<ChatEntry> _transcript = new();

    public DemoAppState(ConversationEngine engine, RequestStore store, DemoOptions options)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        Options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>Raised after every action, including a rejected submit.</summary>
    public event Action? Changed;

    public DemoOptions Options { get; }

    public IReadOnlyList<DemoContact> Contacts => DemoContacts.All;

    /// <summary>The current chat, or null while the contact picker is shown.</summary>
    public ConversationSession? Session { get; private set; }

    /// <summary>The chat bubbles of the current session, visitor and assistant, in order.</summary>
    public IReadOnlyList<ChatEntry> Transcript => _transcript;

    /// <summary>All submitted request cards, in creation order.</summary>
    public IReadOnlyList<AppointmentRequest> Requests => _store.GetAll();

    /// <summary>Starts a chat for one of the fixed demo contacts (FR-4).</summary>
    public void SelectContact(string contactId)
    {
        var contact = DemoContacts.All.FirstOrDefault(c => c.Id == contactId)
            ?? throw new ArgumentException("Unknown demo contact.", nameof(contactId));
        Session = new ConversationSession(contact);
        _transcript.Clear();
        RaiseChanged();
    }

    public IReadOnlyList<AssistantMessage> Send(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var session = RequireSession();
        _transcript.Add(new ChatEntry(FromVisitor: true, text));
        var replies = Track(session, () => _engine.Handle(session, text));
        RaiseChanged();
        return replies;
    }

    public void OpenEdit()
    {
        var session = RequireSession();
        _engine.OpenEdit(session);
        RaiseChanged();
    }

    public ValidationResult ApplyForm(Category category, DayOfWeek weekday, WeekHint week, TimeWindow window)
    {
        var session = RequireSession();
        ValidationResult result = ValidationResult.Ok;
        Track(session, () => result = _engine.ApplyForm(session, category, weekday, week, window));
        RaiseChanged();
        return result;
    }

    public void CancelEdit()
    {
        var session = RequireSession();
        Track(session, () => _engine.CancelEdit(session));
        RaiseChanged();
    }

    public SubmitResult Submit(Guid reviewId)
    {
        var session = RequireSession();
        SubmitResult result = new(SubmitOutcome.Rejected, null);
        Track(session, () => result = _engine.Submit(session, reviewId));
        RaiseChanged();
        return result;
    }

    /// <summary>"שיחה חדשה": a new chat from the contact picker. The inbox is kept.</summary>
    public void RestartConversation()
    {
        Session = null;
        _transcript.Clear();
        RaiseChanged();
    }

    /// <summary>Staff status change (FR-16). Sends nothing to the chat (FR-17).</summary>
    public AppointmentRequest SetStatus(string requestId, RequestStatus status)
    {
        var card = _store.SetStatus(requestId, status);
        RaiseChanged();
        return card;
    }

    /// <summary>Reset demo (FR-18): clears every card, the ID sequence and the chat, back to the contact picker.</summary>
    public void Reset()
    {
        _store.Clear();
        Session = null;
        _transcript.Clear();
        RaiseChanged();
    }

    private ConversationSession RequireSession() =>
        Session ?? throw new InvalidOperationException("No demo contact is selected.");

    /// <summary>Runs an engine action and copies the assistant messages it added to the transcript.</summary>
    private IReadOnlyList<AssistantMessage> Track(ConversationSession session, Action action)
    {
        var before = session.Messages.Count;
        action();
        var added = session.Messages.Skip(before).ToList();
        foreach (var message in added)
        {
            var reviewId = message.Id == TemplateId.Summary ? session.ActiveReview?.ReviewId : null;
            _transcript.Add(new ChatEntry(FromVisitor: false, message.Text, message.Id, reviewId));
        }

        return added;
    }

    private void RaiseChanged() => Changed?.Invoke();
}
