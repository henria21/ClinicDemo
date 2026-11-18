namespace ClinicDemo.Core.Models;

/// <summary>A preferred part of the day. <see cref="After"/> is set only when Kind is <see cref="WindowKind.After"/>, always as an unambiguous 24-hour time.</summary>
public sealed record TimeWindow(WindowKind Kind, TimeOnly? After = null);
