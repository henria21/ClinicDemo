namespace ClinicDemo.Core.Services;

/// <summary>
/// Finds a dictionary keyword inside one normalised token, allowing the common Hebrew one-letter prefixes.
/// Candidates are tried in this order, and the first exact dictionary hit wins:
/// <list type="number">
/// <item>the bare token ("בדיקה", "בוקר", "היום");</item>
/// <item>one prefix from {ב, ל, מ, ה, כ, ו, ש} ("לבדיקה", "בבוקר");</item>
/// <item>a conjunction {ו, ש} followed by one of {ב, ל, מ, ה, כ} ("וביום", "ובבוקר", "שבשבוע").</item>
/// </list>
/// The remainder must be exactly a keyword: there are no partial, fuzzy or guessed matches, and a token that matches
/// nothing is simply unknown. Because the bare token is tried first, a keyword that starts with a prefix-like letter
/// ("בדיקה") is never split. Stateless, at most three O(1) lookups per token.
/// </summary>
public sealed class HebrewPrefixMatcher
{
    private const string SinglePrefixes = "בלמהכוש";
    private const string Conjunctions = "וש";
    private const string PrefixesAfterConjunction = "בלמהכ";

    /// <summary>
    /// Tries to match <paramref name="token"/> against <paramref name="dictionary"/>.
    /// The token must come from <see cref="HebrewTextNormalizer.Tokenize"/> (final letters mapped, niqqud removed).
    /// </summary>
    public bool TryMatch<TValue>(string token, HebrewKeywordDictionary<TValue> dictionary, out PrefixMatch<TValue> match)
    {
        ArgumentNullException.ThrowIfNull(dictionary);
        match = default;

        if (string.IsNullOrEmpty(token))
        {
            return false;
        }

        if (dictionary.TryGetValue(token, out var bare))
        {
            match = new PrefixMatch<TValue>(token, string.Empty, token, bare);
            return true;
        }

        if (token.Length > 1 && SinglePrefixes.Contains(token[0]))
        {
            string remainder = token.Substring(1);
            if (dictionary.TryGetValue(remainder, out var value))
            {
                match = new PrefixMatch<TValue>(token, token.Substring(0, 1), remainder, value);
                return true;
            }
        }

        if (token.Length > 2 && Conjunctions.Contains(token[0]) && PrefixesAfterConjunction.Contains(token[1]))
        {
            string remainder = token.Substring(2);
            if (dictionary.TryGetValue(remainder, out var value))
            {
                match = new PrefixMatch<TValue>(token, token.Substring(0, 2), remainder, value);
                return true;
            }
        }

        return false;
    }
}
