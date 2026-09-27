using System.Text;

namespace Curl.Conformance;

/// <summary>
/// Reads a request's first line the way <c>sws_ProcessRequest</c> in upstream's
/// <c>tests/server/sws.c</c> at <c>curl-8_21_0</c> does, for the path and the part number in it.
/// </summary>
internal static class SwsHttpRequestLine
{
    private const int PartNumberBase = 10000;

    /// <summary>
    /// The path between the method and <c>HTTP/x.y</c>, trimmed, or <see langword="null"/> when
    /// the first line has no method, no non-empty path or no <c>HTTP/</c> version.
    /// </summary>
    /// <param name="request">The request's bytes.</param>
    /// <returns>The path, or <see langword="null"/>.</returns>
    public static string? FindPath(ReadOnlySpan<byte> request)
    {
        // Framing has found the empty line after the headers, so the first line ends in CRLF.
        string line = Encoding.Latin1.GetString(request[..request.IndexOf("\r\n"u8)]);
        int methodEnd = line.IndexOf(' ', StringComparison.Ordinal);
        int version = methodEnd < 0 ? -1 : line.IndexOf("HTTP/", methodEnd, StringComparison.Ordinal);
        if (version < 0 || !IsVersion(line[(version + 5)..]))
        {
            return null;
        }

        string path = line[methodEnd..version].Trim();
        return path.Length > 0 ? path : null;
    }

    /// <summary>
    /// The part number the path selects: the number that starts its last segment, modulo
    /// 10000 when it is over 10000, and 0 when it is not, when there is none or when the path
    /// has no slash.
    /// </summary>
    /// <param name="path">The request path.</param>
    /// <returns>The part number.</returns>
    public static int PartNumber(string path)
    {
        int lastSlash = path.LastIndexOf('/');
        string digits = lastSlash < 0 ? string.Empty : LeadingDigits(path[(lastSlash + 1)..]);
        return int.TryParse(digits, out int number) && number > PartNumberBase ? number % PartNumberBase : 0;
    }

    // sscanf "HTTP/%d.%d": a number, a dot, a number.
    private static bool IsVersion(string text)
    {
        int major = LeadingDigits(text).Length;
        return major > 0 && text.Length > major + 1 && text[major] == '.' && char.IsAsciiDigit(text[major + 1]);
    }

    private static string LeadingDigits(string text)
    {
        int count = 0;
        while (count < text.Length && char.IsAsciiDigit(text[count]))
        {
            count++;
        }

        return text[..count];
    }
}
