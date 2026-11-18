using ClinicDemo.Core.Models;

namespace ClinicDemo.Core.Services;

/// <summary>
/// Classifies one visitor message as <see cref="VisitorIntent.Medical"/>, <see cref="VisitorIntent.DemandsConfirmation"/>,
/// <see cref="VisitorIntent.AsksIfBooked"/> or <see cref="VisitorIntent.Preference"/> (the default).
/// </summary>
/// <remarks>
/// <para>
/// <b>LIMITATION (FR-12, A6): this is a demonstration, not reliable medical detection.</b> The medical list below is a
/// handful of synthetic keywords chosen to cover the demo's own synthetic examples (pain, medication, test results,
/// urgency). Any phrasing that uses none of them is missed and treated as an ordinary preference message. Never describe
/// this class as detecting medical input in general.
/// </para>
/// <para>Priority, first match wins:</para>
/// <list type="number">
/// <item><b>Medical</b>: any token matches <see cref="MedicalKeywords"/>. It wins over everything else, so health text
/// always leads to the handoff and is never processed as a booking question.</item>
/// <item><b>DemandsConfirmation</b>: "תאשר"/"תאשרי", "תקבע לי"/"תקבעי לי", or "תתעלם"/"תתעלמי" followed by a word
/// starting with מ ("תתעלם מהכללים"). It is checked before AsksIfBooked because an instruction to confirm or to
/// ignore the rules is the stronger signal (FR-13); a message with both ("תאשר לי שזה נקבע") gets the same
/// not-a-booking reply from the engine either way.</item>
/// <item><b>AsksIfBooked</b>: "נקבע", "זה סגור", "יש לי תור".</item>
/// <item><b>Preference</b>: everything else.</item>
/// </list>
/// <para>
/// Matching runs on <see cref="HebrewTextNormalizer"/> tokens. Single keywords and the first word of a phrase may carry a
/// common one-letter prefix (<see cref="HebrewPrefixMatcher"/>: "ותאשר", "שנקבע", "ויש לי תור"); the remaining
/// words of a phrase must follow directly and match exactly. Note that "לקבוע" ("אפשר לקבוע תור") is not "תקבע",
/// so an ordinary request stays a preference. Stateless and linear in the number of tokens.
/// </para>
/// </remarks>
public sealed class VisitorIntentDetector
{
    /// <summary>Synthetic demo keywords only (see the limitation above).</summary>
    private static readonly string[] MedicalKeywords =
    {
        "כאב", "כאבים", "כואב", "כואבת",
        "תרופה", "תרופות",
        "תוצאות",
        "דחוף", "דחופה", "חירום",
        "סחרחורת",
    };

    private static readonly string[] ConfirmationKeywords = { "תאשר", "תאשרי" };

    private static readonly (string First, string Following)[] ConfirmationPhrases =
    {
        ("תקבע", "לי"),
        ("תקבעי", "לי"),
    };

    /// <summary>"תתעלם מ...": the next token must start with מ.</summary>
    private static readonly string[] IgnoreKeywords = { "תתעלם", "תתעלמי" };

    private static readonly string[] BookedKeywords = { "נקבע" };

    private static readonly (string First, string Following)[] BookedPhrases =
    {
        ("זה", "סגור"),
        ("יש", "לי תור"),
    };

    private const char IgnoreObjectPrefix = 'מ';

    private readonly HebrewTextNormalizer _normalizer;
    private readonly HebrewPrefixMatcher _matcher;
    private readonly HebrewKeywordDictionary<bool> _medical;
    private readonly HebrewKeywordDictionary<bool> _confirmation;
    private readonly HebrewKeywordDictionary<string[]> _confirmationPhrases;
    private readonly HebrewKeywordDictionary<bool> _ignore;
    private readonly HebrewKeywordDictionary<bool> _booked;
    private readonly HebrewKeywordDictionary<string[]> _bookedPhrases;

    public VisitorIntentDetector(HebrewTextNormalizer normalizer, HebrewPrefixMatcher matcher)
    {
        ArgumentNullException.ThrowIfNull(normalizer);
        ArgumentNullException.ThrowIfNull(matcher);
        _normalizer = normalizer;
        _matcher = matcher;

        _medical = Words(MedicalKeywords);
        _confirmation = Words(ConfirmationKeywords);
        _confirmationPhrases = Phrases(ConfirmationPhrases);
        _ignore = Words(IgnoreKeywords);
        _booked = Words(BookedKeywords);
        _bookedPhrases = Phrases(BookedPhrases);
    }

    /// <summary>Classifies <paramref name="text"/>. Empty text is a <see cref="VisitorIntent.Preference"/>.</summary>
    public VisitorIntent Detect(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var tokens = _normalizer.Tokenize(text);

        if (AnyWord(tokens, _medical))
        {
            return VisitorIntent.Medical;
        }

        if (AnyWord(tokens, _confirmation) || AnyPhrase(tokens, _confirmationPhrases) || AnyIgnoreInstruction(tokens))
        {
            return VisitorIntent.DemandsConfirmation;
        }

        if (AnyWord(tokens, _booked) || AnyPhrase(tokens, _bookedPhrases))
        {
            return VisitorIntent.AsksIfBooked;
        }

        return VisitorIntent.Preference;
    }

    private HebrewKeywordDictionary<bool> Words(IEnumerable<string> keywords) =>
        new(_normalizer, keywords.Select(k => (k, true)));

    private HebrewKeywordDictionary<string[]> Phrases(IEnumerable<(string First, string Following)> phrases) =>
        new(_normalizer, phrases.Select(p => (p.First, _normalizer.Tokenize(p.Following).ToArray())));

    private bool AnyWord(IReadOnlyList<string> tokens, HebrewKeywordDictionary<bool> dictionary)
    {
        foreach (string token in tokens)
        {
            if (_matcher.TryMatch(token, dictionary, out _))
            {
                return true;
            }
        }

        return false;
    }

    private bool AnyPhrase(IReadOnlyList<string> tokens, HebrewKeywordDictionary<string[]> phrases)
    {
        for (int i = 0; i < tokens.Count; i++)
        {
            if (_matcher.TryMatch(tokens[i], phrases, out var match) && RestFollows(tokens, i + 1, match.Value))
            {
                return true;
            }
        }

        return false;
    }

    private static bool RestFollows(IReadOnlyList<string> tokens, int start, string[] rest)
    {
        if (start + rest.Length > tokens.Count)
        {
            return false;
        }

        for (int j = 0; j < rest.Length; j++)
        {
            if (!string.Equals(tokens[start + j], rest[j], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private bool AnyIgnoreInstruction(IReadOnlyList<string> tokens)
    {
        for (int i = 0; i + 1 < tokens.Count; i++)
        {
            string next = tokens[i + 1];
            if (next.Length > 1 && next[0] == IgnoreObjectPrefix && _matcher.TryMatch(tokens[i], _ignore, out _))
            {
                return true;
            }
        }

        return false;
    }
}
