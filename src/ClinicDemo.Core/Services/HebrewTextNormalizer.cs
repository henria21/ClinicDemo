using System.Globalization;
using System.Text;

namespace ClinicDemo.Core.Services;

/// <summary>
/// Turns visitor text into matching tokens. The output is for keyword matching only and is never shown to anyone.
/// Deterministic, stateless and linear in the length of the text.
/// </summary>
/// <remarks>
/// Rules, applied in this order:
/// <list type="number">
/// <item>Niqqud and cantillation are removed (canonical decomposition, then every non-spacing mark is dropped,
/// so presentation forms such as U+FB2A also lose their points).</item>
/// <item>Final letters are mapped to their regular forms: ך→כ, ם→מ, ן→נ, ף→פ, ץ→צ. So "יום" becomes "יומ".</item>
/// <item>Geresh variants (׳ U+05F3, ' U+0027, ‘ U+2018, ’ U+2019) all become the Hebrew geresh <see cref="Geresh"/> (׳).
/// Gershayim variants (״ U+05F4, " U+0022, “ U+201C, ” U+201D) all become the Hebrew gershayim <see cref="Gershayim"/> (״).</item>
/// <item>Latin letters are lower-cased (invariant culture).</item>
/// <item>Tokens are maximal runs of letters and digits. Everything else separates tokens and is dropped, with three exceptions:
///   <list type="bullet">
///   <item>A colon between two ASCII digits stays inside the token: "16:00." → "16:00".</item>
///   <item>A geresh stays when it directly follows a Hebrew letter and either that letter is the only Hebrew letter
///   before it in the token ("ג׳", "ג'" → "ג׳") or a Hebrew letter follows ("צ׳יפס"). A quote closing a longer word
///   ("'בוקר'") is dropped, so the word still matches.</item>
///   <item>A gershayim stays only between two Hebrew letters ("ד"ר" → "ד״ר"); quotation marks around words are dropped.</item>
///   </list>
/// </item>
/// </list>
/// </remarks>
public sealed class HebrewTextNormalizer
{
    /// <summary>The single geresh representation after normalisation (U+05F3).</summary>
    public const char Geresh = '׳';

    /// <summary>The single gershayim representation after normalisation (U+05F4).</summary>
    public const char Gershayim = '״';

    /// <summary>Splits <paramref name="text"/> into normalised tokens. Null or empty text gives no tokens.</summary>
    public IReadOnlyList<string> Tokenize(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Array.Empty<string>();
        }

        string clean = Clean(text);
        var tokens = new List<string>();
        var current = new StringBuilder();

        for (int i = 0; i < clean.Length; i++)
        {
            char c = clean[i];
            char next = i + 1 < clean.Length ? clean[i + 1] : '\0';

            if (char.IsLetterOrDigit(c) || KeepsSeparator(c, current, next))
            {
                current.Append(c);
            }
            else
            {
                Flush(current, tokens);
            }
        }

        Flush(current, tokens);
        return tokens;
    }

    /// <summary>The normalised tokens of <paramref name="text"/> joined by single spaces. Used to normalise keywords.</summary>
    public string Normalize(string? text) => string.Join(' ', Tokenize(text));

    private static bool KeepsSeparator(char c, StringBuilder current, char next)
    {
        if (current.Length == 0)
        {
            return false;
        }

        char previous = current[current.Length - 1];
        return c switch
        {
            ':' => char.IsAsciiDigit(previous) && char.IsAsciiDigit(next),
            Geresh => IsHebrewLetter(previous) && (IsHebrewLetter(next) || TrailingHebrewLetters(current) == 1),
            Gershayim => IsHebrewLetter(previous) && IsHebrewLetter(next),
            _ => false,
        };
    }

    private static int TrailingHebrewLetters(StringBuilder current)
    {
        int count = 0;
        for (int i = current.Length - 1; i >= 0 && IsHebrewLetter(current[i]); i--)
        {
            count++;
        }

        return count;
    }

    private static bool IsHebrewLetter(char c) => c is >= 'א' and <= 'ת';

    /// <summary>Removes marks, maps final letters and unifies geresh/gershayim, keeping everything else for tokenising.</summary>
    private static string Clean(string text)
    {
        string decomposed = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);

        foreach (char c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            sb.Append(c switch
            {
                'ך' => 'כ',
                'ם' => 'מ',
                'ן' => 'נ',
                'ף' => 'פ',
                'ץ' => 'צ',
                '׳' or '\'' or '‘' or '’' => Geresh,
                '״' or '"' or '“' or '”' => Gershayim,
                _ => char.ToLowerInvariant(c),
            });
        }

        return sb.ToString();
    }

    private static void Flush(StringBuilder current, List<string> tokens)
    {
        if (current.Length > 0)
        {
            tokens.Add(current.ToString());
            current.Clear();
        }
    }
}
