namespace ClinicDemo.Core.Exceptions;

/// <summary>An unknown request ID or a disallowed target status (AC-10).</summary>
public sealed class InvalidStatusChangeException : Exception
{
    public InvalidStatusChangeException(string message)
        : base(message)
    {
    }
}
