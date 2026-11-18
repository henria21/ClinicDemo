using ClinicDemo.Core.Exceptions;
using ClinicDemo.Core.Models;
using ClinicDemo.Core.Repositories;
using ClinicDemo.Core.Services;

namespace ClinicDemo.Core.Tests.Repositories;

public class RequestStoreTests
{
    private readonly RequestStore _store = new(new RequestValidator(new HebrewDateResolver(), DemoOptions.Default));

    private static ReviewSnapshot Snapshot(Guid? reviewId = null, Category category = Category.Checkup) =>
        new(reviewId ?? Guid.NewGuid(), "DEMO-A", category,
            new TimePreference(new DateOnly(2026, 11, 24), new TimeWindow(WindowKind.After, new TimeOnly(16, 0))),
            new[] { "line one", "line two" });

    [Fact]
    public void Submit_CreatesAwaitingReviewCard_WithOriginalWording()
    {
        var snapshot = Snapshot();

        var result = _store.Submit(snapshot);

        Assert.Equal(SubmitOutcome.Created, result.Outcome);
        var card = Assert.IsType<AppointmentRequest>(result.Request);
        Assert.Equal("REQ-0001", card.Id);
        Assert.Equal(RequestStatus.AwaitingReview, card.Status);
        Assert.Equal(snapshot.ReviewId, card.ReviewId);
        Assert.Equal("DEMO-A", card.ContactId);
        Assert.Equal(snapshot.Category, card.Category);
        Assert.Equal(snapshot.TimePreference, card.TimePreference);
        Assert.Equal(snapshot.Wording, card.OriginalWording);
        Assert.Single(_store.GetAll());
    }

    [Fact]
    public void Submit_CopiesWording_SoLaterMutationDoesNotLeakIntoCard()
    {
        var wording = new List<string> { "original" };
        var snapshot = Snapshot() with { Wording = wording };

        var card = _store.Submit(snapshot).Request!;
        wording.Add("added later");

        Assert.Equal(new[] { "original" }, card.OriginalWording);
    }

    [Fact]
    public void AC8_SameReviewIdTwice_OneRequest_SecondReportsDuplicateWithExistingCard()
    {
        var snapshot = Snapshot();

        var first = _store.Submit(snapshot);
        var second = _store.Submit(snapshot);

        Assert.Equal(SubmitOutcome.Created, first.Outcome);
        Assert.Equal(SubmitOutcome.Duplicate, second.Outcome);
        Assert.Same(first.Request, second.Request);
        Assert.Single(_store.GetAll());
    }

    [Fact]
    public void Duplicate_ReturnsCurrentCard_AfterStatusChange()
    {
        var snapshot = Snapshot();
        _store.Submit(snapshot);
        _store.SetStatus("REQ-0001", RequestStatus.Reviewed);

        var again = _store.Submit(snapshot);

        Assert.Equal(SubmitOutcome.Duplicate, again.Outcome);
        Assert.Equal(RequestStatus.Reviewed, again.Request!.Status);
    }

    [Fact]
    public void FindByReviewId_ReturnsCardOrNull()
    {
        var snapshot = Snapshot();
        Assert.Null(_store.FindByReviewId(snapshot.ReviewId));

        var card = _store.Submit(snapshot).Request;

        Assert.Same(card, _store.FindByReviewId(snapshot.ReviewId));
        Assert.Null(_store.FindByReviewId(Guid.NewGuid()));
    }

    [Fact]
    public void Ids_AreSequential_AndGetAllIsInCreationOrder()
    {
        for (var i = 0; i < 3; i++)
        {
            _store.Submit(Snapshot());
        }

        Assert.Equal(new[] { "REQ-0001", "REQ-0002", "REQ-0003" }, _store.GetAll().Select(r => r.Id));
    }

    [Fact]
    public void GetAll_ReturnsSnapshotCopy()
    {
        _store.Submit(Snapshot());
        var before = _store.GetAll();

        _store.Submit(Snapshot());

        Assert.Single(before);
        Assert.Equal(2, _store.GetAll().Count);
    }

    [Fact]
    public void Stress_200ParallelSubmitsOfOneReviewId_OneRequest()
    {
        const int n = 200;
        var snapshot = Snapshot();
        var results = new SubmitResult[n];
        using var barrier = new Barrier(n);

        var threads = Enumerable.Range(0, n).Select(i => new Thread(() =>
        {
            barrier.SignalAndWait();
            results[i] = _store.Submit(snapshot);
        })).ToList();
        threads.ForEach(t => t.Start());
        threads.ForEach(t => t.Join());

        Assert.Single(_store.GetAll());
        Assert.Equal(1, results.Count(r => r.Outcome == SubmitOutcome.Created));
        Assert.Equal(n - 1, results.Count(r => r.Outcome == SubmitOutcome.Duplicate));
        Assert.All(results, r => Assert.Equal("REQ-0001", r.Request!.Id));
    }

    [Fact]
    public void Stress_200ParallelDistinctSubmits_UniqueGapFreeIds()
    {
        const int n = 200;
        var snapshots = Enumerable.Range(0, n).Select(_ => Snapshot()).ToArray();
        var results = new SubmitResult[n];
        using var barrier = new Barrier(n);

        var threads = Enumerable.Range(0, n).Select(i => new Thread(() =>
        {
            barrier.SignalAndWait();
            results[i] = _store.Submit(snapshots[i]);
        })).ToList();
        threads.ForEach(t => t.Start());
        threads.ForEach(t => t.Join());

        Assert.All(results, r => Assert.Equal(SubmitOutcome.Created, r.Outcome));
        var expected = Enumerable.Range(1, n).Select(i => $"REQ-{i:D4}").ToArray();
        Assert.Equal(expected, results.Select(r => r.Request!.Id).OrderBy(id => id, StringComparer.Ordinal));
        Assert.Equal(expected, _store.GetAll().Select(r => r.Id));
        Assert.Equal(n, _store.GetAll().Select(r => r.ReviewId).Distinct().Count());
    }

    [Fact]
    public void AC10_InvalidSnapshot_ThrowsInvalidRequestException_AndStoresNothing()
    {
        var changed = 0;
        _store.Changed += () => changed++;

        Assert.Throws<InvalidRequestException>(() => _store.Submit(Snapshot(category: (Category)99)));
        Assert.Throws<InvalidRequestException>(() => _store.Submit(Snapshot() with
        {
            TimePreference = new TimePreference(new DateOnly(2026, 11, 29), new TimeWindow(WindowKind.Morning)),
        }));
        Assert.Throws<InvalidRequestException>(() => _store.Submit(null!));

        Assert.Empty(_store.GetAll());
        Assert.Equal(0, changed);
        Assert.Equal("REQ-0001", _store.Submit(Snapshot()).Request!.Id);
    }

    [Theory]
    [InlineData(RequestStatus.Reviewed)]
    [InlineData(RequestStatus.NeedsClarification)]
    public void SetStatus_AllowedStatus_UpdatesCard(RequestStatus status)
    {
        _store.Submit(Snapshot());

        var updated = _store.SetStatus("REQ-0001", status);

        Assert.Equal(status, updated.Status);
        Assert.Equal(status, _store.GetAll().Single().Status);
    }

    [Theory]
    [InlineData("REQ-0002")]
    [InlineData("req-0001")]
    [InlineData("")]
    [InlineData(null)]
    public void AC10_SetStatus_UnknownId_Throws(string? id)
    {
        _store.Submit(Snapshot());

        Assert.Throws<InvalidStatusChangeException>(() => _store.SetStatus(id!, RequestStatus.Reviewed));
    }

    [Theory]
    [InlineData(RequestStatus.AwaitingReview)]
    [InlineData((RequestStatus)99)]
    public void AC10_SetStatus_DisallowedStatus_Throws_AndCardUnchanged(RequestStatus status)
    {
        _store.Submit(Snapshot());
        var changed = 0;
        _store.Changed += () => changed++;

        Assert.Throws<InvalidStatusChangeException>(() => _store.SetStatus("REQ-0001", status));

        Assert.Equal(RequestStatus.AwaitingReview, _store.GetAll().Single().Status);
        Assert.Equal(0, changed);
    }

    [Fact]
    public void Clear_EmptiesStore_AndResetsSequence()
    {
        var old = Snapshot();
        _store.Submit(old);
        _store.Submit(Snapshot());

        _store.Clear();

        Assert.Empty(_store.GetAll());
        Assert.Null(_store.FindByReviewId(old.ReviewId));
        Assert.Equal("REQ-0001", _store.Submit(Snapshot()).Request!.Id);
        Assert.Throws<InvalidStatusChangeException>(() => _store.SetStatus("REQ-0002", RequestStatus.Reviewed));
    }

    [Fact]
    public void Changed_FiresOnSubmitStatusChangeAndClear_NotOnDuplicate()
    {
        var changed = 0;
        _store.Changed += () => changed++;
        var snapshot = Snapshot();

        _store.Submit(snapshot);
        Assert.Equal(1, changed);

        _store.Submit(snapshot);
        Assert.Equal(1, changed);

        _store.SetStatus("REQ-0001", RequestStatus.NeedsClarification);
        Assert.Equal(2, changed);

        _store.Clear();
        Assert.Equal(3, changed);
    }

    [Fact]
    public void Changed_IsRaisedOutsideTheLock_HandlerCanReadFromAnotherThread()
    {
        // If Changed were raised while holding the lock, a handler that waits on another thread
        // calling GetAll() would deadlock. The timeout turns a deadlock into a test failure.
        var readOk = false;
        _store.Changed += () =>
        {
            var reader = new Thread(() => _store.GetAll());
            reader.Start();
            readOk = reader.Join(TimeSpan.FromSeconds(5));
        };

        _store.Submit(Snapshot());

        Assert.True(readOk);
    }
}
