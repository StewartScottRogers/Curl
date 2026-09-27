using System.Text;

namespace Curl.Authentication;

/// <summary>
/// Reads the <c>key=value</c> parameters after <c>Digest</c> in a challenge, as curl
/// 8.21.0's <c>Curl_auth_decode_digest_http_message</c> and
/// <c>Curl_auth_digest_get_pair</c> read them.
/// </summary>
/// <remarks>
/// A key is everything up to <c>=</c>, at most 255 characters. A value in double quotes
/// runs to the closing quote, a backslash escaping the character after it; a missing
/// closing quote ends it at the end of the text. A value without quotes runs to a comma, a
/// line break or the end, trailing blanks kept. At most 1023 characters of a value are
/// read. Blanks and one comma between parameters are skipped. The first text that is not a
/// well-formed pair - no <c>=</c>, a key too long, a line break inside quotes, a double
/// quote inside an unquoted value, a trailing backslash - ends the list without error.
/// </remarks>
internal static class DigestChallengeParameters
{
    private const int MaximumKeyLength = 255;

    private const int MaximumValueCharacters = 1023;

    private const int MaximumQopTokenLength = 32;

    // The characters that can end or escape a value, and what each means inside and
    // outside quotes; every other character is content.
    private const string SpecialCharacters = "\\,\r\n\"";

    private static readonly ValueCharacter[] QuotedMeanings =
        [ValueCharacter.Escape, ValueCharacter.Content, ValueCharacter.Malformed, ValueCharacter.Malformed, ValueCharacter.End];

    private static readonly ValueCharacter[] UnquotedMeanings =
        [ValueCharacter.Content, ValueCharacter.End, ValueCharacter.End, ValueCharacter.End, ValueCharacter.Malformed];

    // End and Malformed come last: each finishes the value.
    private enum ValueCharacter
    {
        Content,
        Escape,
        End,
        Malformed,
    }

    /// <summary>
    /// Skips curl's blanks, space and tab.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="index">Where to start.</param>
    /// <returns>The index of the first character that is not a blank, or the length.</returns>
    internal static int SkipBlanks(string text, int index)
    {
        while (IsBlankAt(text, index))
        {
            index++;
        }

        return index;
    }

    /// <summary>
    /// Reads the parameters of a Digest challenge.
    /// </summary>
    /// <param name="challenge">The header value.</param>
    /// <param name="index">The index just after the scheme name <c>Digest</c>.</param>
    /// <returns>The challenge; <see langword="null" /> when curl rejects it.</returns>
    internal static DigestChallenge? Read(string challenge, int index)
    {
        if (!IsBlankAt(challenge, index))
        {
            return null;
        }

        DigestChallengeBuilder builder = new();
        index = SkipBlanks(challenge, index);
        while (TryReadPair(challenge, ref index, out string key, out string value))
        {
            if (!builder.TryApply(key, value))
            {
                return null;
            }

            index = SkipSeparator(challenge, index);
        }

        return builder.Build();
    }

    /// <summary>
    /// Reads the <c>qop</c> list: <c>auth</c> when it holds that token, in any case, else
    /// <c>auth-int</c> when it holds that. Tokens are split at commas with leading blanks
    /// skipped and compared whole; an empty token or one over 32 characters ends the list.
    /// </summary>
    /// <param name="list">The <c>qop</c> value.</param>
    /// <returns><c>auth</c>, <c>auth-int</c>, or <see langword="null" />.</returns>
    internal static string? ReadQop(string list)
    {
        string[] tokens =
        [
            .. list.Split(',')
                .Select(part => part.TrimStart(' ', '\t'))
                .TakeWhile(token => token.Length is > 0 and <= MaximumQopTokenLength),
        ];

        return tokens.Contains("auth", StringComparer.OrdinalIgnoreCase) ? "auth"
            : tokens.Contains("auth-int", StringComparer.OrdinalIgnoreCase) ? "auth-int"
            : null;
    }

    private static bool IsBlankAt(string text, int index) =>
        index < text.Length && text[index] is ' ' or '\t';

    // Blanks, at most one comma, and the blanks after it.
    private static int SkipSeparator(string challenge, int index)
    {
        index = SkipBlanks(challenge, index);
        return challenge.AsSpan(index).StartsWith(",") ? SkipBlanks(challenge, index + 1) : index;
    }

    // Curl_auth_digest_get_pair: false when there is no well-formed pair here.
    private static bool TryReadPair(string challenge, ref int index, out string key, out string value)
    {
        key = string.Empty;
        value = string.Empty;
        int keyLength = challenge.AsSpan(index).IndexOf('=');
        if (keyLength is < 0 or > MaximumKeyLength)
        {
            return false;
        }

        key = challenge.Substring(index, keyLength);
        index += keyLength + 1;
        bool quoted = challenge.AsSpan(index).StartsWith("\"");
        index += quoted ? 1 : 0;
        string? content = ReadValue(challenge, ref index, quoted);
        value = content ?? string.Empty;
        return content is not null;
    }

    private static string? ReadValue(string challenge, ref int index, bool quoted)
    {
        StringBuilder content = new();
        bool escaped = false;
        int end = Math.Min(challenge.Length, index + MaximumValueCharacters);
        while (index < end)
        {
            char character = challenge[index++];
            ValueCharacter meaning = Meaning(character, quoted, escaped);
            if (meaning >= ValueCharacter.End)
            {
                return meaning == ValueCharacter.End ? content.ToString() : null;
            }

            escaped = meaning == ValueCharacter.Escape;
            content.Append(escaped ? string.Empty : character);
        }

        return escaped ? null : content.ToString();
    }

    // An escaped character is always content.
    private static ValueCharacter Meaning(char character, bool quoted, bool escaped)
    {
        int special = escaped ? -1 : SpecialCharacters.IndexOf(character);
        return special < 0 ? ValueCharacter.Content : (quoted ? QuotedMeanings : UnquotedMeanings)[special];
    }
}
