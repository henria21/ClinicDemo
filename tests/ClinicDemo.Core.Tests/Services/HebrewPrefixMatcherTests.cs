using ClinicDemo.Core.Services;

namespace ClinicDemo.Core.Tests.Services;

public class HebrewPrefixMatcherTests
{
    private readonly HebrewTextNormalizer _normalizer = new();
    private readonly HebrewPrefixMatcher _matcher = new();
    private readonly HebrewKeywordDictionary<string> _keywords;

    public HebrewPrefixMatcherTests()
    {
        // Keywords are written in natural Hebrew (final letters, geresh); the dictionary normalises them.
        _keywords = new HebrewKeywordDictionary<string>(_normalizer, new[]
        {
            ("בוקר", "morning"),
            ("בדיקה", "checkup"),
            ("יום", "day"),
            ("שבוע", "week"),
            ("השבוע", "this-week"),
            ("היום", "today"),
            ("מחר", "tomorrow"),
            ("ג'", "tuesday-letter"),
        });
    }

    private string Token(string text) => Assert.Single(_normalizer.Tokenize(text));

    [Theory]
    [InlineData("בוקר", "", "morning")]
    [InlineData("בדיקה", "", "checkup")]
    [InlineData("בבוקר", "ב", "morning")]
    [InlineData("לבדיקה", "ל", "checkup")]
    [InlineData("הבוקר", "ה", "morning")]
    [InlineData("וביום", "וב", "day")]
    [InlineData("ובבוקר", "וב", "morning")]
    [InlineData("ובשבוע", "וב", "week")]
    [InlineData("שבשבוע", "שב", "week")]
    [InlineData("ומחר", "ו", "tomorrow")]
    [InlineData("ג׳", "", "tuesday-letter")]
    [InlineData("ג'", "", "tuesday-letter")]
    public void KnownKeyword_WithAllowedPrefixes_Matches(string text, string prefix, string value)
    {
        string token = Token(text);

        Assert.True(_matcher.TryMatch(token, _keywords, out var match));
        Assert.Equal(prefix, match.Prefix);
        Assert.Equal(value, match.Value);
        Assert.Equal(token, match.Prefix + match.Keyword);
    }

    [Fact]
    public void Bedika_MatchesAsItself_NotSplitAsBetPlusRemainder()
    {
        var dictionary = new HebrewKeywordDictionary<string>(_normalizer, new[]
        {
            ("בדיקה", "checkup"),
            ("דיקה", "decoy"),
        });

        Assert.True(_matcher.TryMatch("בדיקה", dictionary, out var match));
        Assert.Equal("", match.Prefix);
        Assert.Equal("checkup", match.Value);
    }

    [Theory]
    [InlineData("השבוע", "this-week")]
    [InlineData("היום", "today")]
    public void BareKeyword_WinsOverPrefixReading(string text, string value)
    {
        Assert.True(_matcher.TryMatch(Token(text), _keywords, out var match));
        Assert.Equal("", match.Prefix);
        Assert.Equal(value, match.Value);
    }

    [Theory]
    [InlineData("בלבול")]   // ב + לבול: not a keyword
    [InlineData("ובקר")]    // וב + קר: not a keyword
    [InlineData("שלום")]    // ש + לומ: not a keyword
    [InlineData("לבקר")]    // ל + בקר: close to בוקר, but no guessing
    [InlineData("בוקרים")]  // keyword + suffix: no partial match
    [InlineData("בוק")]     // truncated keyword: no partial match
    [InlineData("ב")]       // a lone prefix letter
    [InlineData("16:00")]
    public void UnknownWord_DoesNotMatch(string text)
    {
        Assert.False(_matcher.TryMatch(Token(text), _keywords, out var match));
        Assert.Equal(default, match);
    }

    [Theory]
    [InlineData("כבבוקר")]   // כ is not a conjunction, so two prefixes are not allowed
    [InlineData("ווביום")]   // three prefix letters
    [InlineData("ושבוקר")]   // ש is not allowed after a conjunction
    [InlineData("בלבוקר")]   // two non-conjunction prefixes
    public void DisallowedPrefixCombinations_DoNotMatch(string text)
    {
        Assert.False(_matcher.TryMatch(Token(text), _keywords, out _));
    }

    [Fact]
    public void EmptyToken_DoesNotMatch()
    {
        Assert.False(_matcher.TryMatch("", _keywords, out _));
    }

    [Fact]
    public void Dictionary_NormalisesKeywordsLikeTokens()
    {
        var dictionary = new HebrewKeywordDictionary<int>(_normalizer, new[] { ("מחרתיים", 2), ("שָׁלוֹם", 1) });

        Assert.Equal(new[] { "מחרתיימ", "שלומ" }, dictionary.Keywords.OrderBy(k => k, StringComparer.Ordinal));
        Assert.True(_matcher.TryMatch(Token("ומחרתיים"), dictionary, out var match));
        Assert.Equal(2, match.Value);
    }

    [Fact]
    public void Dictionary_AcceptsVariantsWithTheSameValue()
    {
        var dictionary = new HebrewKeywordDictionary<string>(_normalizer, new[] { ("ג׳", "tue"), ("ג'", "tue") });

        Assert.Equal(1, dictionary.Count);
    }

    [Fact]
    public void Dictionary_RejectsConflictingVariants()
    {
        Assert.Throws<ArgumentException>(() =>
            new HebrewKeywordDictionary<string>(_normalizer, new[] { ("ג׳", "tue"), ("ג'", "wed") }));
    }

    [Theory]
    [InlineData("שבוע הבא")]
    [InlineData("")]
    [InlineData("?!")]
    public void Dictionary_RejectsKeywordsThatAreNotOneToken(string keyword)
    {
        Assert.Throws<ArgumentException>(() =>
            new HebrewKeywordDictionary<string>(_normalizer, new[] { (keyword, "x") }));
    }
}
