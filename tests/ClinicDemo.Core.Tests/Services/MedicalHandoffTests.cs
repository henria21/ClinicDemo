using ClinicDemo.Core.Models;
using ClinicDemo.Core.Repositories;
using ClinicDemo.Core.Services;
using ClinicDemo.Core.Session;
using ClinicDemo.Core.Tests.TestData;

namespace ClinicDemo.Core.Tests.Services;

/// <summary>
/// FR-12 / AC-5 for the listed synthetic examples only. These tests do not claim general detection coverage.
/// </summary>
public class MedicalHandoffTests
{
    private readonly ConversationEngine _engine;
    private readonly RequestStore _store;
    private readonly ConversationSession _session = EngineFactory.CreateSession();

    public MedicalHandoffTests()
    {
        _engine = EngineFactory.CreateEngine(out _store);
    }

    [Theory]
    [MemberData(nameof(MedicalExamples.TheoryData), MemberType = typeof(MedicalExamples))]
    public void EachExample_DuringCollecting_GetsOnlyTheHandoff(string text)
    {
        _engine.Handle(_session, "בדיקה בשבוע הבא");

        var replies = _engine.Handle(_session, text);

        Assert.Equal(new[] { TemplateId.MedicalHandoff }, replies.Select(r => r.Id));
        Assert.Equal(ConversationStage.Halted, _session.Stage);
        Assert.Null(_session.ActiveReview);
        Assert.Empty(_session.Draft.OriginalWording);
        Assert.Null(_session.Draft.Category);
        Assert.DoesNotContain(_session.Messages, m => m.Id == TemplateId.Summary);
    }

    [Theory]
    [MemberData(nameof(MedicalExamples.TheoryData), MemberType = typeof(MedicalExamples))]
    public void EachExample_AsFirstMessage_GetsDisclaimerAndHandoffOnly(string text)
    {
        var replies = _engine.Handle(_session, text);

        Assert.Equal(new[] { TemplateId.Disclaimer, TemplateId.MedicalHandoff }, replies.Select(r => r.Id));
        Assert.Equal(ConversationStage.Halted, _session.Stage);
        Assert.Empty(_session.Draft.OriginalWording);
    }

    [Fact]
    public void ReviewThenMedical_StaleSubmitIsRejected_StoreEmpty_DraftCleared()
    {
        _engine.Handle(_session, "שלום, אפשר לקבוע תור לבדיקה בשבוע הבא, אחר הצהריים?");
        _engine.Handle(_session, "יום שלישי אחרי 16:00.");
        var oldId = _session.ActiveReview!.ReviewId;

        var replies = _engine.Handle(_session, MedicalExamples.All[0]);
        var result = _engine.Submit(_session, oldId);

        Assert.Equal(new[] { TemplateId.MedicalHandoff }, replies.Select(r => r.Id));
        Assert.Equal(SubmitOutcome.Rejected, result.Outcome);
        Assert.Empty(_store.GetAll());
        Assert.Equal(ConversationStage.Halted, _session.Stage);
        Assert.Null(_session.ActiveReview);
        Assert.Empty(_session.Draft.OriginalWording);
        Assert.Null(_session.Draft.DayReference);
    }

    [Theory]
    [InlineData("בדיקה ביום שלישי בבוקר")]
    [InlineData("זה נקבע?")]
    [InlineData("תודה")]
    public void AfterHalt_EveryMessageGetsRestartRequired(string text)
    {
        _engine.Handle(_session, MedicalExamples.All[0]);

        var replies = _engine.Handle(_session, text);

        Assert.Equal(new[] { TemplateId.RestartRequired }, replies.Select(r => r.Id));
        Assert.Equal(ConversationStage.Halted, _session.Stage);
        Assert.Null(_session.ActiveReview);
        Assert.Empty(_session.Draft.OriginalWording);
    }

    [Fact]
    public void AfterHalt_EditIsNotAvailable()
    {
        _engine.Handle(_session, MedicalExamples.All[0]);

        Assert.Throws<InvalidOperationException>(() => _engine.OpenEdit(_session));
    }
}
