using ClinicDemo.Core.Models;
using ClinicDemo.Core.Repositories;
using ClinicDemo.Core.Services;
using ClinicDemo.Core.Session;
using ClinicDemo.Core.Tests.TestData;

namespace ClinicDemo.Core.Tests.Services;

/// <summary>I-1: the three submit outcomes.</summary>
public class SubmitOutcomeTests
{
    private readonly ConversationEngine _engine;
    private readonly RequestStore _store;
    private readonly ConversationSession _session = EngineFactory.CreateSession();

    public SubmitOutcomeTests()
    {
        _engine = EngineFactory.CreateEngine(out _store);
    }

    private Guid ReachReview()
    {
        _engine.Handle(_session, "שלום, אפשר לקבוע תור לבדיקה בשבוע הבא, אחר הצהריים?");
        _engine.Handle(_session, "יום שלישי אחרי 16:00.");
        return _session.ActiveReview!.ReviewId;
    }

    [Fact]
    public void ActiveId_IsCreated_AsAppendixACard()
    {
        var id = ReachReview();

        var result = _engine.Submit(_session, id);

        Assert.Equal(SubmitOutcome.Created, result.Outcome);
        var card = Assert.Single(_store.GetAll());
        Assert.Equal(card, result.Request);
        Assert.Equal("REQ-0001", card.Id);
        Assert.Equal("DEMO-A", card.ContactId);
        Assert.Equal(Category.Checkup, card.Category);
        Assert.Equal(new DateOnly(2026, 11, 24), card.TimePreference.Day);
        Assert.Equal(new TimeWindow(WindowKind.After, new TimeOnly(16, 0)), card.TimePreference.Window);
        Assert.Equal(RequestStatus.AwaitingReview, card.Status);
        Assert.Equal(2, card.OriginalWording.Count);

        Assert.Equal(ConversationStage.Submitted, _session.Stage);
        Assert.Null(_session.ActiveReview);
        Assert.Contains(id, _session.SubmittedReviewIds);
        Assert.Equal(
            "בקשת הדמו נשמרה וממתינה לבדיקת הצוות. זה אינו אישור תור. לא נשלחה הודעה למרפאה אמיתית.",
            _session.Messages.Last().Text);
    }

    [Fact]
    public void SameIdAgain_IsDuplicate_WithTheSameCard()
    {
        var id = ReachReview();
        var first = _engine.Submit(_session, id);
        var messageCount = _session.Messages.Count;

        var second = _engine.Submit(_session, id);

        Assert.Equal(SubmitOutcome.Duplicate, second.Outcome);
        Assert.Equal(first.Request, second.Request);
        Assert.Single(_store.GetAll());
        Assert.Equal(messageCount, _session.Messages.Count);
    }

    [Fact]
    public void StaleIdAfterEdit_IsRejected_AndTheFreshReviewIsUntouched()
    {
        var id1 = ReachReview();
        _engine.OpenEdit(_session);
        _engine.CancelEdit(_session);
        var id2 = _session.ActiveReview!.ReviewId;
        Assert.NotEqual(id1, id2);
        var messageCount = _session.Messages.Count;

        var stale = _engine.Submit(_session, id1);

        Assert.Equal(SubmitOutcome.Rejected, stale.Outcome);
        Assert.Null(stale.Request);
        Assert.Empty(_store.GetAll());
        Assert.Equal(id2, _session.ActiveReview!.ReviewId);
        Assert.Equal(ConversationStage.Reviewing, _session.Stage);
        Assert.Equal(messageCount, _session.Messages.Count);

        Assert.Equal(SubmitOutcome.Created, _engine.Submit(_session, id2).Outcome);
        Assert.Equal(SubmitOutcome.Duplicate, _engine.Submit(_session, id2).Outcome);
        Assert.Single(_store.GetAll());
    }

    [Fact]
    public void UnknownId_IsRejected()
    {
        ReachReview();

        var result = _engine.Submit(_session, Guid.NewGuid());

        Assert.Equal(SubmitOutcome.Rejected, result.Outcome);
        Assert.Empty(_store.GetAll());
        Assert.Equal(ConversationStage.Reviewing, _session.Stage);
    }

    [Fact]
    public void SubmittedId_AfterStoreClear_IsRejected()
    {
        var id = ReachReview();
        _engine.Submit(_session, id);
        _store.Clear();

        var result = _engine.Submit(_session, id);

        Assert.Equal(SubmitOutcome.Rejected, result.Outcome);
        Assert.Empty(_store.GetAll());
    }

    [Fact]
    public void SubmitWhileCollecting_IsRejected()
    {
        _engine.Handle(_session, "בדיקה");

        Assert.Equal(SubmitOutcome.Rejected, _engine.Submit(_session, Guid.NewGuid()).Outcome);
        Assert.Equal(ConversationStage.Collecting, _session.Stage);
    }
}
