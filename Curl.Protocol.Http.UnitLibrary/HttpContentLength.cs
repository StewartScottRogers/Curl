using System.Globalization;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// Finds a response's body length in its Content-Length headers, accepting and refusing
/// the same values curl 8.21.0 does (measured, BL-170).
/// </summary>
/// <remarks>
/// A Content-Length value is a comma-separated list of decimal numbers, each with optional
/// spaces or tabs around it, and every number in every Content-Length header must be the
/// same: <c>5, 5</c> is 5, while <c>5,6</c>, <c>+5</c>, <c>0x5</c>, <c>5 x</c>, an empty
/// value, an empty list item, or a second header saying 3 are exit 8. A number too large
/// for a signed 64-bit integer leaves the length unknown, so the body is read to close,
/// written after <see cref="HttpConnectionInfoLines.OverflowContentLength" /> (BL-1387).
/// </remarks>
internal static class HttpContentLength
{
    private const string HeaderName = "Content-Length";

    private static readonly char[] Blanks = [' ', '\t'];

    /// <summary>
    /// Finds the body length the headers declare.
    /// </summary>
    /// <param name="headers">The final response's headers.</param>
    /// <returns>
    /// The length, or <see langword="null" /> when no Content-Length header is present or
    /// its number is too large, and the body ends when the peer closes.
    /// </returns>
    /// <exception cref="HttpTransferException">
    /// A Content-Length value is not a list of equal decimal numbers, or two disagree
    /// (exit 8).
    /// </exception>
    internal static long? Find(IReadOnlyList<HttpResponseHeader> headers)
    {
        long? length = null;
        foreach (string item in headers.Where(IsContentLength).SelectMany(header => header.Value.Split(',')))
        {
            if (!TryParseItem(item.Trim(Blanks), out long value))
            {
                return null;
            }

            length = Agree(length, value);
        }

        return length;
    }

    /// <summary>
    /// Determines whether any Content-Length header holds a decimal number too large for a
    /// signed 64-bit integer, which curl 8.21.0 answers with
    /// <see cref="HttpConnectionInfoLines.OverflowContentLength" /> and a connection it shuts down,
    /// or, under <c>--max-filesize</c>, with exit 63 (<c>lib/http.c</c>, BL-1387).
    /// </summary>
    /// <param name="headers">The headers to look through.</param>
    /// <returns><see langword="true" /> when one of them overflows.</returns>
    internal static bool Overflows(IReadOnlyList<HttpResponseHeader> headers) =>
        headers.Any(header => IsContentLength(header) && ValueOverflows(header.Value));

    /// <summary>
    /// Determines whether one header line is a Content-Length header holding a decimal number
    /// too large for a signed 64-bit integer (<see cref="Overflows" />).
    /// </summary>
    /// <param name="headerLine">The header line's text, without its line end.</param>
    /// <returns><see langword="true" /> when the line's number overflows.</returns>
    internal static bool OverflowsLine(string headerLine)
    {
        int colon = headerLine.IndexOf(':', StringComparison.Ordinal);
        return colon >= 0
            && string.Equals(headerLine[..colon], HeaderName, StringComparison.OrdinalIgnoreCase)
            && ValueOverflows(headerLine[(colon + 1)..]);
    }

    private static bool ValueOverflows(string value) =>
        value.Split(',').Select(item => item.Trim(Blanks)).Any(item =>
            item.Length > 0 && item.All(char.IsAsciiDigit) && !long.TryParse(item, NumberStyles.None, CultureInfo.InvariantCulture, out _));

    /// <summary>
    /// Checks one number against the ones before it.
    /// </summary>
    /// <param name="length">The number every earlier item gave, or <see langword="null" /> for none yet.</param>
    /// <param name="value">This item's number.</param>
    /// <returns><paramref name="value" />.</returns>
    /// <exception cref="HttpTransferException">The two differ (exit 8).</exception>
    private static long Agree(long? length, long value) =>
        length is null || length == value ? value : throw Invalid();

    private static bool IsContentLength(HttpResponseHeader header) =>
        string.Equals(header.Name, HeaderName, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Parses one list item.
    /// </summary>
    /// <param name="item">The item, without the blanks around it.</param>
    /// <param name="value">The number, when the item is one that fits.</param>
    /// <returns><see langword="false" /> when the number is too large to hold.</returns>
    /// <exception cref="HttpTransferException">The item is not a decimal number (exit 8).</exception>
    private static bool TryParseItem(string item, out long value)
    {
        if (item.Length == 0 || !item.All(char.IsAsciiDigit))
        {
            throw Invalid();
        }

        return long.TryParse(item, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }

    private static HttpTransferException Invalid() =>
        new(CurlExitCode.WeirdServerReply, HttpTransferMessages.InvalidContentLength);
}
