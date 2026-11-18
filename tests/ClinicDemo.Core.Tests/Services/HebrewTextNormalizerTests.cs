using ClinicDemo.Core.Services;

namespace ClinicDemo.Core.Tests.Services;

public class HebrewTextNormalizerTests
{
    private readonly HebrewTextNormalizer _normalizer = new();

    [Fact]
    public void After1600WithSentenceDot_KeepsColon_DropsDot()
    {
        Assert.Equal(new[] { "אחרי", "16:00" }, _normalizer.Tokenize("אחרי 16:00."));
    }

    [Theory]
    [InlineData("16:00,")]
    [InlineData("16:00.")]
    [InlineData("16:00?")]
    [InlineData("(16:00)")]
    [InlineData("  16:00!  ")]
    public void TimeWithSurroundingPunctuation_IsOneToken(string input)
    {
        Assert.Equal(new[] { "16:00" }, _normalizer.Tokenize(input));
    }

    [Theory]
    [InlineData("שעה: 16", new[] { "שעה", "16" })]
    [InlineData("16: 00", new[] { "16", "00" })]
    [InlineData("16:", new[] { "16" })]
    [InlineData(":00", new[] { "00" })]
    [InlineData("9:30", new[] { "9:30" })]
    public void Colon_IsKeptOnlyBetweenDigits(string input, string[] expected)
    {
        Assert.Equal(expected, _normalizer.Tokenize(input));
    }

    [Fact]
    public void AppendixAFirstMessage_PunctuationRemoved_FinalLettersMapped()
    {
        var tokens = _normalizer.Tokenize("שלום, אפשר לקבוע תור לבדיקה בשבוע הבא, אחר הצהריים?");

        Assert.Equal(
            new[] { "שלומ", "אפשר", "לקבוע", "תור", "לבדיקה", "בשבוע", "הבא", "אחר", "הצהריימ" },
            tokens);
    }

    [Theory]
    [InlineData("ך", "כ")]
    [InlineData("ם", "מ")]
    [InlineData("ן", "נ")]
    [InlineData("ף", "פ")]
    [InlineData("ץ", "צ")]
    public void FinalLetters_MapToRegularForms(string final, string regular)
    {
        Assert.Equal(new[] { "א" + regular }, _normalizer.Tokenize("א" + final));
    }

    [Theory]
    [InlineData("יום ג׳")]
    [InlineData("יום ג'")]
    [InlineData("יום ג’")]
    [InlineData("יום ג‘")]
    [InlineData("יום ג׳.")]
    public void GereshVariants_AreUnifiedToHebrewGeresh(string input)
    {
        Assert.Equal(new[] { "יומ", "ג׳" }, _normalizer.Tokenize(input));
    }

    [Theory]
    [InlineData("ד״ר")]
    [InlineData("ד\"ר")]
    [InlineData("ד“ר")]
    [InlineData("ד”ר")]
    public void GershayimVariants_AreUnifiedToHebrewGershayim(string input)
    {
        Assert.Equal(new[] { "ד״ר" }, _normalizer.Tokenize(input));
    }

    [Theory]
    [InlineData("\"בוקר\"")]
    [InlineData("'בוקר'")]
    [InlineData("״בוקר״")]
    [InlineData("׳בוקר׳")]
    public void QuotesAroundAWord_AreDropped(string input)
    {
        Assert.Equal(new[] { "בוקר" }, _normalizer.Tokenize(input));
    }

    [Fact]
    public void Niqqud_IsRemoved()
    {
        Assert.Equal(new[] { "בבוקר" }, _normalizer.Tokenize("בַּבּוֹקֶר"));
        Assert.Equal(new[] { "בבקר" }, _normalizer.Tokenize("בַּבֹּקֶר")); // defective spelling: only the points go
        Assert.Equal(new[] { "שלומ" }, _normalizer.Tokenize("שָׁלוֹם"));
    }

    [Fact]
    public void PresentationFormsWithPoints_AreDecomposedAndStripped()
    {
        // U+FB2A is SHIN WITH SHIN DOT; U+FB31 is BET WITH DAGESH.
        Assert.Equal(new[] { "שב" }, _normalizer.Tokenize("שׁבּ"));
    }

    [Fact]
    public void Maqaf_AndHyphen_SeparateTokens()
    {
        Assert.Equal(new[] { "ב", "16:00" }, _normalizer.Tokenize("ב-16:00"));
        Assert.Equal(new[] { "בית", "ספר" }, _normalizer.Tokenize("בית־ספר"));
    }

    [Fact]
    public void Latin_IsLowerCased()
    {
        Assert.Equal(new[] { "demo", "a" }, _normalizer.Tokenize("DEMO-A"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("?!.,")]
    public void EmptyOrPunctuationOnly_GivesNoTokens(string? input)
    {
        Assert.Empty(_normalizer.Tokenize(input));
    }

    [Theory]
    [InlineData("יום ג' אחרי 16:00.")]
    [InlineData("שָׁלוֹם, ד\"ר!")]
    public void Normalize_IsIdempotent(string input)
    {
        string once = _normalizer.Normalize(input);
        Assert.Equal(once, _normalizer.Normalize(once));
    }

    [Fact]
    public void Normalize_JoinsTokensWithSingleSpaces()
    {
        Assert.Equal("שבוע הבא", _normalizer.Normalize("  שבוע,   הבא! "));
    }
}
