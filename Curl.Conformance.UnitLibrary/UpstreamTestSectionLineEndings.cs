using System.Text;

namespace Curl.Conformance;

/// <summary>
/// The body transforms upstream's <c>runtests.pl</c> and <c>testutil.pm</c> at <c>curl-8_21_0</c>
/// apply for a test-file part's line-ending attributes: <c>nonewline</c>, <c>crlf="yes"</c>,
/// <c>crlf="headers"</c> and <c>mode="text"</c>. Each works on bytes read as Latin-1, so every
/// byte survives.
/// </summary>
/// <remarks>
/// Upstream applies them where it uses a part, after variable substitution and in a
/// different order per part, so the caller chooses the order; see <see cref="UpstreamTestSection"/>.
/// </remarks>
public static class UpstreamTestSectionLineEndings
{
    /// <summary>
    /// <c>nonewline</c>: drops one final line feed, as Perl's <c>chomp</c> does; a carriage return
    /// before it stays. A body not ending in a line feed is returned unchanged.
    /// </summary>
    /// <param name="body">The body.</param>
    /// <returns>The body without its final line feed.</returns>
    public static byte[] CutFinalNewline(ReadOnlySpan<byte> body) =>
        body.EndsWith("\n"u8) ? body[..^1].ToArray() : body.ToArray();

    /// <summary>
    /// <c>crlf="yes"</c>: ends every line with CRLF, turning any run of carriage returns before
    /// a line feed into one (<c>s/\x0d*\x0a/\x0d\x0a/</c> per line). A final line without a line
    /// feed is unchanged.
    /// </summary>
    /// <param name="body">The body.</param>
    /// <returns>The body with CRLF line endings.</returns>
    public static byte[] ForceCrlf(ReadOnlySpan<byte> body) =>
        TransformLines(body, (line, _) => (EndWithCrlf(line), false));

    /// <summary>
    /// <c>crlf="headers"</c>: ends with CRLF only the lines <c>subnewlines</c> guesses are headers
    /// (an HTTP status line, an HTTP or RTSP request line, or a <c>name: value</c> line that is
    /// not a <c>curl: (N) </c> error) and a bare empty line straight after one of them.
    /// </summary>
    /// <param name="body">The body.</param>
    /// <returns>The body with CRLF on its header lines.</returns>
    /// <remarks>
    /// Upstream keeps "the previous line was a header" in a module-level variable that carries
    /// from one part to the next; here it starts cleared for every body.
    /// </remarks>
    public static byte[] ForceHeaderCrlf(ReadOnlySpan<byte> body) =>
        TransformLines(body, ForceHeaderLineCrlf);

    /// <summary>
    /// <c>mode="text"</c>: what <c>normalize_text</c> does to both sides of a comparison; every
    /// CRLF becomes a line feed, then every line feed becomes CRLF.
    /// </summary>
    /// <param name="body">The body.</param>
    /// <returns>The body with every line feed as CRLF.</returns>
    public static byte[] NormalizeText(ReadOnlySpan<byte> body) =>
        Encoding.Latin1.GetBytes(Encoding.Latin1.GetString(body).Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n", "\r\n", StringComparison.Ordinal));

    private static (string Line, bool WasHeader) ForceHeaderLineCrlf(string line, bool previousWasHeader)
    {
        if (UpstreamTestHeaderLine.LooksLikeHeader(line))
        {
            return (EndWithCrlf(line), true);
        }

        return (line == "\n" && previousWasHeader ? "\r\n" : line, false);
    }

    private static byte[] TransformLines(ReadOnlySpan<byte> body, Func<string, bool, (string Line, bool WasHeader)> transform)
    {
        StringBuilder result = new(body.Length);
        bool previousWasHeader = false;
        foreach (string line in SplitAfterLineFeeds(Encoding.Latin1.GetString(body)))
        {
            (string transformed, previousWasHeader) = transform(line, previousWasHeader);
            result.Append(transformed);
        }

        return Encoding.Latin1.GetBytes(result.ToString());
    }

    private static IEnumerable<string> SplitAfterLineFeeds(string text)
    {
        int start = 0;
        while (start < text.Length)
        {
            int lineFeed = text.IndexOf('\n', start);
            int end = lineFeed < 0 ? text.Length : lineFeed + 1;
            yield return text[start..end];
            start = end;
        }
    }

    private static string EndWithCrlf(string line) =>
        line.EndsWith('\n') ? line.TrimEnd('\n').TrimEnd('\r') + "\r\n" : line;
}
