namespace ClinicDemo.Core.Tests.TestData;

/// <summary>
/// Synthetic, fictional medical example messages that the demo's medical keyword list covers (requirements A6, AC-5).
/// Tests assert behaviour for these listed examples only; they do not claim general detection coverage (FR-12).
/// </summary>
public static class MedicalExamples
{
    public static readonly IReadOnlyList<string> All = new[]
    {
        "יש לי כאב חזק, מה לקחת?",
        "כואב לי הראש כבר יומיים",
        "איזו תרופה כדאי לי לקחת?",
        "קיבלתי תוצאות של בדיקה, אפשר להסביר לי?",
        "זה דחוף, אני צריך עזרה עוד היום",
        "יש לי סחרחורת מהבוקר",
    };

    /// <summary>The examples as xUnit theory data.</summary>
    public static IEnumerable<object[]> TheoryData => All.Select(text => new object[] { text });
}
