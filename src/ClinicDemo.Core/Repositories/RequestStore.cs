using ClinicDemo.Core.Exceptions;
using ClinicDemo.Core.Models;
using ClinicDemo.Core.Services;

namespace ClinicDemo.Core.Repositories;

/// <summary>
/// In-memory, thread-safe store of submitted appointment requests (never bookings). Idempotent by ReviewId.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Submit"/> returns a <see cref="SubmitResult"/> whose outcome is only ever
/// <see cref="SubmitOutcome.Created"/> (a new card) or <see cref="SubmitOutcome.Duplicate"/> (this ReviewId was
/// submitted before; the existing card is returned unchanged). The store never returns
/// <see cref="SubmitOutcome.Rejected"/>: deciding that a ReviewId is stale or unknown is the engine's job (I-1).
/// Use <see cref="FindByReviewId"/> for the engine's "already submitted?" check before it rejects.
/// </para>
/// <para>
/// <see cref="Changed"/> fires after a new card is created, after a status change and after <see cref="Clear"/>.
/// It does not fire for a duplicate submit, because nothing changed. It is always raised outside the lock.
/// </para>
/// </remarks>
public sealed class RequestStore
{
    private readonly object _gate = new();
    private readonly RequestValidator _validator;
    private readonly Dictionary<Guid, AppointmentRequest> _byReviewId = new();
    private readonly Dictionary<string, Guid> _reviewIdById = new(StringComparer.Ordinal);
    private readonly List<Guid> _order = new();
    private int _sequence;

    public RequestStore(RequestValidator validator)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
    }

    public event Action? Changed;

    /// <summary>
    /// Stores the snapshot as a new request with status <see cref="RequestStatus.AwaitingReview"/>, or returns the
    /// existing card when this ReviewId was already submitted.
    /// </summary>
    /// <exception cref="InvalidRequestException">The snapshot fails validation.</exception>
    public SubmitResult Submit(ReviewSnapshot snapshot)
    {
        var validation = _validator.Validate(snapshot);
        if (!validation.IsValid)
        {
            throw new InvalidRequestException(validation.Errors);
        }

        SubmitResult result;
        lock (_gate)
        {
            if (_byReviewId.TryGetValue(snapshot.ReviewId, out var existing))
            {
                return new SubmitResult(SubmitOutcome.Duplicate, existing);
            }

            _sequence++;
            var request = new AppointmentRequest(
                $"REQ-{_sequence:D4}",
                snapshot.ContactId,
                snapshot.Category,
                snapshot.TimePreference,
                snapshot.Wording.ToArray(),
                RequestStatus.AwaitingReview,
                snapshot.ReviewId);
            _byReviewId.Add(request.ReviewId, request);
            _reviewIdById.Add(request.Id, request.ReviewId);
            _order.Add(request.ReviewId);
            result = new SubmitResult(SubmitOutcome.Created, request);
        }

        Changed?.Invoke();
        return result;
    }

    /// <summary>The card already submitted for this ReviewId, or null.</summary>
    public AppointmentRequest? FindByReviewId(Guid reviewId)
    {
        lock (_gate)
        {
            return _byReviewId.TryGetValue(reviewId, out var request) ? request : null;
        }
    }

    /// <summary>Sets a staff status and returns the updated card. Nothing is sent to the chat (FR-17).</summary>
    /// <exception cref="InvalidStatusChangeException">Unknown request ID or a disallowed target status.</exception>
    public AppointmentRequest SetStatus(string id, RequestStatus status)
    {
        if (!_validator.ValidateStatusChange(status).IsValid)
        {
            throw new InvalidStatusChangeException($"Status '{status}' is not an allowed target status.");
        }

        AppointmentRequest updated;
        lock (_gate)
        {
            if (id is null || !_reviewIdById.TryGetValue(id, out var reviewId))
            {
                throw new InvalidStatusChangeException($"Unknown request ID '{id}'.");
            }

            updated = _byReviewId[reviewId] with { Status = status };
            _byReviewId[reviewId] = updated;
        }

        Changed?.Invoke();
        return updated;
    }

    /// <summary>A snapshot copy of all requests in creation order.</summary>
    public IReadOnlyList<AppointmentRequest> GetAll()
    {
        lock (_gate)
        {
            return _order.Select(reviewId => _byReviewId[reviewId]).ToArray();
        }
    }

    /// <summary>Empties the store and resets the sequence, so the next request is REQ-0001 (FR-18).</summary>
    public void Clear()
    {
        lock (_gate)
        {
            _byReviewId.Clear();
            _reviewIdById.Clear();
            _order.Clear();
            _sequence = 0;
        }

        Changed?.Invoke();
    }
}
