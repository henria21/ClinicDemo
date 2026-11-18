namespace ClinicDemo.Core.Models;

/// <summary>The fixed fictional contacts (FR-4). No real personal data.</summary>
public static class DemoContacts
{
    public static IReadOnlyList<DemoContact> All { get; } = new[]
    {
        new DemoContact("DEMO-A", "איש קשר לדוגמה א׳"),
        new DemoContact("DEMO-B", "איש קשר לדוגמה ב׳"),
        new DemoContact("DEMO-C", "איש קשר לדוגמה ג׳"),
    };
}
