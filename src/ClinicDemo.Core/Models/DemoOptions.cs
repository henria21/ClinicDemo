namespace ClinicDemo.Core.Models;

public sealed record DemoOptions(DateOnly SimulationDate)
{
    /// <summary>Simulation date: Wednesday 18.11.2026.</summary>
    public static DemoOptions Default { get; } = new(new DateOnly(2026, 11, 18));
}
