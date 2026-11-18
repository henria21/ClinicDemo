namespace ClinicDemo.Core.Services;

/// <summary>
/// A keyword found by <see cref="HebrewPrefixMatcher"/>.
/// <paramref name="Prefix"/> is "" for a bare match, one letter ("ב") or a conjunction plus a letter ("וב").
/// <paramref name="Keyword"/> is the normalised dictionary keyword, so <c>Prefix + Keyword == Token</c>.
/// </summary>
public readonly record struct PrefixMatch<TValue>(string Token, string Prefix, string Keyword, TValue Value);
