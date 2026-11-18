using ClinicDemo.Core.Models;

namespace ClinicDemo.Core.Session;

/// <summary>
/// State of one chat (design §4 "Session and store"). Mutated only by <see cref="Services.ConversationEngine"/>.
/// Holds chat text in memory only; nothing here is logged or persisted.
/// </summary>
public sealed class ConversationSession
{
    private readonly List<AssistantMessage> _messages = new();
    private readonly HashSet<Guid> _submittedReviewIds = new();

    public ConversationSession(DemoContact contact)
    {
        ArgumentNullException.ThrowIfNull(contact);
        Contact = contact;
    }

    public DemoContact Contact { get; }

    public ConversationStage Stage { get; internal set; } = ConversationStage.Collecting;

    public RequestDraft Draft { get; private set; } = new();

    /// <summary>Every assistant message shown in this chat, in order.</summary>
    public IReadOnlyList<AssistantMessage> Messages => _messages;

    /// <summary>The only review that can be submitted. Cleared only through <see cref="InvalidateReview"/>.</summary>
    public ReviewSnapshot? ActiveReview { get; private set; }

    /// <summary>ReviewIds this session has submitted successfully.</summary>
    public IReadOnlyCollection<Guid> SubmittedReviewIds => _submittedReviewIds;

    /// <summary>Whether the first reply (with its Disclaimer prefix) has been sent.</summary>
    public bool DisclaimerShown { get; internal set; }

    /// <summary>Consecutive not-understood answers while the category was the missing field.</summary>
    public int CategoryMisses { get; internal set; }

    /// <summary>Consecutive not-understood answers while the day was the missing field.</summary>
    public int DayMisses { get; internal set; }

    /// <summary>Consecutive not-understood answers while the window was the missing field.</summary>
    public int WindowMisses { get; internal set; }

    /// <summary>Clears the active review (I-2). The only way <see cref="ActiveReview"/> becomes null.</summary>
    public void InvalidateReview() => ActiveReview = null;

    internal void SetActiveReview(ReviewSnapshot snapshot) => ActiveReview = snapshot;

    internal void MarkSubmitted(Guid reviewId) => _submittedReviewIds.Add(reviewId);

    /// <summary>Discards the draft, including its original wording (used after a medical handoff).</summary>
    internal void DiscardDraft() => Draft = new RequestDraft();

    internal void ResetMisses()
    {
        CategoryMisses = 0;
        DayMisses = 0;
        WindowMisses = 0;
    }

    internal void AddMessages(IEnumerable<AssistantMessage> messages) => _messages.AddRange(messages);
}
