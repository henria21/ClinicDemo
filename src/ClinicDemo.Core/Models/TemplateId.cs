namespace ClinicDemo.Core.Models;

/// <summary>One value per assistant message template in MessageCatalog.</summary>
public enum TemplateId
{
    Disclaimer,
    AskCategory,
    AskDay,
    AskWindow,
    ClarifyCategory,
    ClarifyDay,
    ClarifyWindow,
    AskTime24h,
    ExactTimeNotSupported,
    DayOutOfRange,
    Summary,
    Submitted,
    NotABookingBeforeSubmit,
    NotABookingAfterSubmit,
    MedicalHandoff,
    OfferForm,
    NoChangeDetected,
    RestartRequired,
    AlreadySent,
}
