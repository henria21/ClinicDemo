namespace ClinicDemo.Core.Models;

/// <summary>A submitted appointment request (never a booking).</summary>
public sealed record AppointmentRequest(
    string Id,
    string ContactId,
    Category Category,
    TimePreference TimePreference,
    IReadOnlyList<string> OriginalWording,
    RequestStatus Status,
    Guid ReviewId);
