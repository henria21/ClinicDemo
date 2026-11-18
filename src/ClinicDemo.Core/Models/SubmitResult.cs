namespace ClinicDemo.Core.Models;

/// <summary>Created / Duplicate carry the card; Rejected has Request = null and changed nothing.</summary>
public sealed record SubmitResult(SubmitOutcome Outcome, AppointmentRequest? Request);
