using ClinicDemo.Core.Models;

namespace ClinicDemo.Core.Services;

/// <summary>
/// Validates request data in application code before it is stored (FR-20), and staff status changes.
/// Pure and stateless apart from the injected simulation date, so it is safe to share.
/// </summary>
/// <remarks>
/// There are no clinic hours in the demo: any valid <see cref="TimeOnly"/> is accepted for
/// <see cref="WindowKind.After"/> as a stated preference. No time is treated as available or unavailable.
/// Error messages are fixed developer-facing strings and never include the visitor's wording.
/// </remarks>
public sealed class RequestValidator
{
    private readonly HebrewDateResolver _resolver;
    private readonly DemoOptions _options;

    public RequestValidator(HebrewDateResolver resolver, DemoOptions options)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>Validates a review snapshot: ReviewId, contact, wording, category and time preference.</summary>
    public ValidationResult Validate(ReviewSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return ValidationResult.Fail("Snapshot is required.");
        }

        var errors = new List<string>();
        if (snapshot.ReviewId == Guid.Empty)
        {
            errors.Add("ReviewId is required.");
        }

        if (string.IsNullOrWhiteSpace(snapshot.ContactId))
        {
            errors.Add("ContactId is required.");
        }

        if (snapshot.Wording is null)
        {
            errors.Add("Wording is required.");
        }

        errors.AddRange(Validate(snapshot.Category, snapshot.TimePreference).Errors);
        return errors.Count == 0 ? ValidationResult.Ok : new ValidationResult(false, errors);
    }

    /// <summary>Validates the structured fields: a defined category, a day inside the range and a well-formed window.</summary>
    public ValidationResult Validate(Category category, TimePreference? preference)
    {
        var errors = new List<string>();
        if (!Enum.IsDefined(category))
        {
            errors.Add("Category is not a defined value.");
        }

        if (preference is null)
        {
            errors.Add("TimePreference is required.");
        }
        else
        {
            if (!_resolver.IsInRange(preference.Day, _options.SimulationDate))
            {
                errors.Add("Day is outside the allowed range.");
            }

            errors.AddRange(ValidateWindow(preference.Window).Errors);
        }

        return errors.Count == 0 ? ValidationResult.Ok : new ValidationResult(false, errors);
    }

    /// <summary>A defined kind, with a time set if and only if Kind is After. Any time of day is accepted.</summary>
    public ValidationResult ValidateWindow(TimeWindow? window)
    {
        if (window is null)
        {
            return ValidationResult.Fail("Window is required.");
        }

        if (!Enum.IsDefined(window.Kind))
        {
            return ValidationResult.Fail("WindowKind is not a defined value.");
        }

        if (window.Kind == WindowKind.After && window.After is null)
        {
            return ValidationResult.Fail("An After window needs a time.");
        }

        if (window.Kind != WindowKind.After && window.After is not null)
        {
            return ValidationResult.Fail("Only an After window may carry a time.");
        }

        return ValidationResult.Ok;
    }

    /// <summary>Staff may only set <see cref="RequestStatus.Reviewed"/> or <see cref="RequestStatus.NeedsClarification"/> (FR-16).</summary>
    public ValidationResult ValidateStatusChange(RequestStatus target) =>
        target is RequestStatus.Reviewed or RequestStatus.NeedsClarification
            ? ValidationResult.Ok
            : ValidationResult.Fail("Status must be Reviewed or NeedsClarification.");
}
