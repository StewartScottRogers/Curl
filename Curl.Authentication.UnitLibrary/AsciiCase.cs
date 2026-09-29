namespace Curl.Authentication;

/// <summary>
/// Changes the case of ASCII letters only, leaving every other character as it is, as curl's
/// <c>Curl_strntolower</c> and <c>Curl_strntoupper</c> do.
/// </summary>
internal static class AsciiCase
{
    /// <summary>Lowercases the ASCII letters in <paramref name="text" />.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The text with <c>A</c>-<c>Z</c> made <c>a</c>-<c>z</c>.</returns>
    internal static string ToLower(string text) => string.Concat(text.Select(c => char.IsAsciiLetterUpper(c) ? (char)(c | 0x20) : c));

    /// <summary>Uppercases the ASCII letters in <paramref name="text" />.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The text with <c>a</c>-<c>z</c> made <c>A</c>-<c>Z</c>.</returns>
    internal static string ToUpper(string text) => string.Concat(text.Select(c => char.IsAsciiLetterLower(c) ? (char)(c & ~0x20) : c));
}
