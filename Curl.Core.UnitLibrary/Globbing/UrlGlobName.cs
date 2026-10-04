namespace Curl.Core.Globbing;

/// <summary>
/// Reads the <c>&lt;name&gt;</c> curl 8.21.0 accepts after a glob's <c>{</c> or <c>[</c>
/// and after an output name's <c>#</c>: a <c>&lt;</c>, at most <see cref="MaxLength" />
/// characters, and the first <c>&gt;</c> after it.
/// </summary>
internal static class UrlGlobName
{
    /// <summary>curl's <c>MAX_GLOBNAME_LEN</c>: the longest name a glob may have.</summary>
    public const int MaxLength = 64;

    /// <summary>
    /// Gets the name whose <c>&lt;</c> is at <paramref name="start" />, or
    /// <see langword="null" /> when there is no <c>&lt;</c> there, no <c>&gt;</c> after it,
    /// or the name between them is longer than <see cref="MaxLength" />.
    /// </summary>
    /// <remarks>A name spans <see cref="string.Length" /> plus two characters of the text.</remarks>
    public static string? Read(string text, int start)
    {
        if (start >= text.Length || text[start] != '<')
        {
            return null;
        }

        int close = text.IndexOf('>', start + 1);
        return close < 0 || close - start - 1 > MaxLength ? null : text[(start + 1)..close];
    }
}
