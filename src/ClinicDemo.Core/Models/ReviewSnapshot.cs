namespace ClinicDemo.Core.Models;

/// <summary>Immutable copy shown on the review summary. ReviewId is the idempotency key.</summary>
public sealed record ReviewSnapshot(
    Guid ReviewId,
    string ContactId,
    Category Category,
    TimePreference TimePreference,
    IReadOnlyList<string> Wording);
