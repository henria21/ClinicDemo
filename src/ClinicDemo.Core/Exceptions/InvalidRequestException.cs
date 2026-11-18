namespace ClinicDemo.Core.Exceptions;

/// <summary>
/// <see cref="Repositories.RequestStore.Submit"/> received a snapshot that fails validation.
/// This can't happen through the UI; it guards calls that bypass the normal flow (AC-10).
/// The message lists the validation errors only, never the visitor's wording.
/// </summary>
public sealed class InvalidRequestException : Exception
{
    public InvalidRequestException(IReadOnlyList<string> errors)
        : base("Invalid appointment request: " + string.Join("; ", errors))
    {
        Errors = errors;
    }

    public IReadOnlyList<string> Errors { get; }
}
