namespace ClinicDemo.Core.Models;

/// <summary>Request status. There is deliberately no booked or confirmed value.</summary>
public enum RequestStatus
{
    AwaitingReview,
    Reviewed,
    NeedsClarification,
}
