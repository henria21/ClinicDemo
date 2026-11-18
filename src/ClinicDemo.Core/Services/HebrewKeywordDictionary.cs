using System.Diagnostics.CodeAnalysis;

namespace ClinicDemo.Core.Services;

/// <summary>
/// An immutable keyword → value map whose keys are normalised with <see cref="HebrewTextNormalizer"/>,
/// so callers can write keywords in natural Hebrew ("יום", "ג'", "מחרתיים") and still match normalised tokens.
/// Each keyword must normalise to exactly one token; multi-word phrases are matched by the caller over adjacent tokens.
/// </summary>
public sealed class HebrewKeywordDictionary<TValue>
{
    private readonly Dictionary<string, TValue> _entries;

    /// <exception cref="ArgumentException">
    /// A keyword normalises to zero or several tokens, or two keywords normalise to the same key with different values.
    /// </exception>
    public HebrewKeywordDictionary(HebrewTextNormalizer normalizer, IEnumerable<(string Keyword, TValue Value)> keywords)
    {
        ArgumentNullException.ThrowIfNull(normalizer);
        ArgumentNullException.ThrowIfNull(keywords);

        _entries = new Dictionary<string, TValue>(StringComparer.Ordinal);
        var comparer = EqualityComparer<TValue>.Default;

        foreach (var (keyword, value) in keywords)
        {
            var tokens = normalizer.Tokenize(keyword);
            if (tokens.Count != 1)
            {
                throw new ArgumentException(
                    $"A keyword must normalise to exactly one token, but '{keyword}' gives {tokens.Count}.", nameof(keywords));
            }

            string key = tokens[0];
            if (_entries.TryGetValue(key, out var existing))
            {
                if (!comparer.Equals(existing, value))
                {
                    throw new ArgumentException(
                        $"The keyword '{keyword}' normalises to '{key}', which already has a different value.", nameof(keywords));
                }

                continue;
            }

            _entries.Add(key, value);
        }
    }

    /// <summary>The number of distinct normalised keywords.</summary>
    public int Count => _entries.Count;

    /// <summary>The normalised keywords.</summary>
    public IEnumerable<string> Keywords => _entries.Keys;

    /// <summary>Exact lookup of an already-normalised token. No prefix handling (see <see cref="HebrewPrefixMatcher"/>).</summary>
    public bool TryGetValue(string normalizedToken, [MaybeNullWhen(false)] out TValue value) =>
        _entries.TryGetValue(normalizedToken, out value);
}
