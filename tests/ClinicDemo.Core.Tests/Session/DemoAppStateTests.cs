using ClinicDemo.Core.Exceptions;
using ClinicDemo.Core.Models;
using ClinicDemo.Core.Session;
using ClinicDemo.Core.Tests.TestData;

namespace ClinicDemo.Core.Tests.Session;

public class DemoAppStateTests
{
    private const string AppendixMessage1 = "שלום, אפשר לקבוע תור לבדיקה בשבוע הבא, אחר הצהריים?";
    private const string AppendixMessage2 = "יום שלישי אחרי 16:00.";

    private readonly DemoAppState _state = EngineFactory.CreateAppState();
    private int _changes;

    public DemoAppStateTests()
    {
        _state.Changed += () => _changes++;
    }

    private Guid ReachReview(string contactId = "DEMO-A")
    {
        _state.SelectContact(contactId);
        _state.Send(AppendixMessage1);
        _state.Send(AppendixMessage2);
        return _state.Session!.ActiveReview!.ReviewId;
    }

    [Fact]
    public void StartsAtTheContactPicker_WithAnEmptyInbox()
    {
        Assert.Null(_state.Session);
        Assert.Empty(_state.Transcript);
        Assert.Empty(_state.Requests);
        Assert.Equal(new[] { "DEMO-A", "DEMO-B", "DEMO-C" }, _state.Contacts.Select(c => c.Id));
    }

    [Fact]
    public void SelectContact_StartsACollectingSession()
    {
        _state.SelectContact("DEMO-B");

        Assert.Equal("DEMO-B", _state.Session!.Contact.Id);
        Assert.Equal(ConversationStage.Collecting, _state.Session.Stage);
    }

    [Fact]
    public void SelectContact_RejectsAnUnknownContact()
    {
        Assert.Throws<ArgumentException>(() => _state.SelectContact("REAL-PERSON"));
        Assert.Null(_state.Session);
    }

    [Fact]
    public void ActionsWithoutAContact_Throw()
    {
        Assert.Throws<InvalidOperationException>(() => _state.Send("בדיקה"));
        Assert.Throws<InvalidOperationException>(() => _state.Submit(Guid.NewGuid()));
        Assert.Throws<InvalidOperationException>(() => _state.OpenEdit());
    }

    [Fact]
    public void Transcript_HasVisitorAndAssistantBubbles_AndTheSummaryCarriesItsReviewId()
    {
        var reviewId = ReachReview();

        Assert.Equal(
            new[] { true, false, false, true, false },
            _state.Transcript.Select(e => e.FromVisitor));
        Assert.Equal(AppendixMessage1, _state.Transcript[0].Text);
        Assert.Equal(TemplateId.Disclaimer, _state.Transcript[1].Template);
        Assert.Equal(TemplateId.AskDay, _state.Transcript[2].Template);
        var summary = _state.Transcript[4];
        Assert.Equal(TemplateId.Summary, summary.Template);
        Assert.Equal(reviewId, summary.ReviewId);
    }

    [Fact]
    public void FormAndSubmitMessages_AreAddedToTheTranscript()
    {
        var firstId = ReachReview();
        _state.OpenEdit();
        _state.CancelEdit();
        var secondId = _state.Session!.ActiveReview!.ReviewId;

        var result = _state.Submit(secondId);

        Assert.Equal(SubmitOutcome.Created, result.Outcome);
        var summaries = _state.Transcript.Where(e => e.Template == TemplateId.Summary).ToList();
        Assert.Equal(new Guid?[] { firstId, secondId }, summaries.Select(s => s.ReviewId));
        Assert.Equal(TemplateId.Submitted, _state.Transcript.Last().Template);
    }

    [Fact]
    public void ApplyForm_ReturnsTheValidationResult()
    {
        _state.SelectContact("DEMO-A");
        _state.Send("בדיקה");
        _state.OpenEdit();

        var invalid = _state.ApplyForm(Category.Checkup, DayOfWeek.Sunday, WeekHint.ThisWeek, new TimeWindow(WindowKind.Morning));
        var valid = _state.ApplyForm(Category.Checkup, DayOfWeek.Sunday, WeekHint.NextWeek, new TimeWindow(WindowKind.Morning));

        Assert.False(invalid.IsValid);
        Assert.True(valid.IsValid);
        Assert.Equal(TemplateId.Summary, _state.Transcript.Last().Template);
        Assert.Equal(new DateOnly(2026, 11, 22), _state.Session!.ActiveReview!.TimePreference.Day);
    }

    // ---- AC-11 (logic) / FR-18 ----

    [Fact]
    public void Reset_EmptiesTheStore_ReturnsToThePicker_AndRestartsTheIdSequence()
    {
        _state.Submit(ReachReview());
        Assert.Single(_state.Requests);

        _state.Reset();

        Assert.Empty(_state.Requests);
        Assert.Null(_state.Session);
        Assert.Empty(_state.Transcript);

        var result = _state.Submit(ReachReview("DEMO-C"));
        Assert.Equal("REQ-0001", result.Request!.Id);
    }

    [Fact]
    public void RestartConversation_KeepsTheCards_AndClearsTheSession()
    {
        _state.Submit(ReachReview());

        _state.RestartConversation();

        Assert.Null(_state.Session);
        Assert.Empty(_state.Transcript);
        var card = Assert.Single(_state.Requests);
        Assert.Equal("REQ-0001", card.Id);

        var second = _state.Submit(ReachReview("DEMO-B"));
        Assert.Equal("REQ-0002", second.Request!.Id);
    }

    [Fact]
    public void RestartAfterAMedicalHandoff_StartsAFreshChat()
    {
        _state.SelectContact("DEMO-A");
        _state.Send(MedicalExamples.All[0]);
        Assert.Equal(ConversationStage.Halted, _state.Session!.Stage);

        _state.RestartConversation();
        _state.SelectContact("DEMO-A");

        Assert.Equal(ConversationStage.Collecting, _state.Session!.Stage);
        Assert.Empty(_state.Session.Draft.OriginalWording);
        Assert.Empty(_state.Transcript);
    }

    // ---- Status (FR-16 / FR-17) ----

    [Fact]
    public void SetStatus_ChangesTheCard_AndSendsNothingToTheChat()
    {
        _state.Submit(ReachReview());
        var transcriptCount = _state.Transcript.Count;

        var card = _state.SetStatus("REQ-0001", RequestStatus.Reviewed);

        Assert.Equal(RequestStatus.Reviewed, card.Status);
        Assert.Equal(RequestStatus.Reviewed, _state.Requests[0].Status);
        Assert.Equal(transcriptCount, _state.Transcript.Count);
    }

    [Fact]
    public void SetStatus_RejectsAnUnknownCard()
    {
        Assert.Throws<InvalidStatusChangeException>(() => _state.SetStatus("REQ-0099", RequestStatus.Reviewed));
    }

    // ---- Changed fires for every action ----

    [Fact]
    public void Changed_FiresForEveryAction()
    {
        var expected = 0;
        void Step(Action action)
        {
            action();
            expected++;
            Assert.Equal(expected, _changes);
        }

        Step(() => _state.SelectContact("DEMO-A"));
        Step(() => _state.Send("בדיקה בשבוע הבא אחר הצהריים"));
        Step(() => _state.Send("חמישי"));
        Step(() => _state.OpenEdit());
        Step(() => _state.ApplyForm(Category.Consultation, DayOfWeek.Monday, WeekHint.NextWeek, new TimeWindow(WindowKind.Morning)));
        Step(() => _state.OpenEdit());
        Step(() => _state.CancelEdit());
        Step(() => _state.Submit(Guid.NewGuid())); // rejected, still raises Changed
        Step(() => _state.Submit(_state.Session!.ActiveReview!.ReviewId));
        Step(() => _state.SetStatus("REQ-0001", RequestStatus.NeedsClarification));
        Step(() => _state.RestartConversation());
        Step(() => _state.Reset());
    }
}
