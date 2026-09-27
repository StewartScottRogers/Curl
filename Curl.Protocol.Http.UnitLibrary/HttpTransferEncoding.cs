using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// Reads a response's Transfer-Encoding headers the way curl 8.21.0 does when it was not
/// asked to decode transfer compression (measured, BL-171): whether the body is chunked, and
/// exit 61 for any other coding.
/// </summary>
/// <remarks>
/// Each header is a comma-separated list of codings, compared without regard to case, with
/// blanks around each and empty items ignored. <c>chunked</c> may repeat and
/// <c>identity</c> is ignored, except that any coding but <c>chunked</c> listed after
/// <c>chunked</c> in the same header, <c>identity</c> included, is exit 61
/// <c>A Transfer-Encoding (X) was listed after chunked</c>. Any coding but <c>chunked</c>
/// or <c>identity</c> anywhere else, a later header included, is exit 61
/// <c>Unsolicited Transfer-Encoding (X) found</c>. curl reads headers
/// in order, so an invalid Content-Length before a rejected Transfer-Encoding is reported
/// instead of it.
/// </remarks>
internal static class HttpTransferEncoding
{
    private const string HeaderName = "Transfer-Encoding";

    private static readonly char[] Blanks = [' ', '\t'];

    /// <summary>
    /// Determines whether the headers frame the body as chunked.
    /// </summary>
    /// <param name="headers">The final response's headers.</param>
    /// <returns><see langword="true" /> when a Transfer-Encoding header lists <c>chunked</c>.</returns>
    /// <exception cref="HttpTransferException">
    /// A Transfer-Encoding header lists a coding curl does not decode (exit 61), or a
    /// Content-Length header before it is invalid (exit 8).
    /// </exception>
    internal static bool IsChunked(IReadOnlyList<HttpResponseHeader> headers)
    {
        bool isChunked = false;
        for (int index = 0; index < headers.Count; index++)
        {
            if (!string.Equals(headers[index].Name, HeaderName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            HttpTransferException? rejection = ReadCodings(headers[index].Value, ref isChunked);
            if (rejection is not null)
            {
                HttpContentLength.Find([.. headers.Take(index)]);
                throw rejection;
            }
        }

        return isChunked;
    }

    /// <summary>
    /// Determines whether any Transfer-Encoding header lists <c>chunked</c>, refusing no
    /// coding: how <c>--raw</c>, which decodes none, reads the headers (measured, BL-180 Notes).
    /// </summary>
    /// <param name="headers">The final response's headers.</param>
    /// <returns><see langword="true" /> when a Transfer-Encoding header lists <c>chunked</c>.</returns>
    internal static bool ListsChunked(IReadOnlyList<HttpResponseHeader> headers) =>
        headers
            .Where(header => string.Equals(header.Name, HeaderName, StringComparison.OrdinalIgnoreCase))
            .SelectMany(header => Codings(header.Value))
            .Any(coding => Is(coding, "chunked"));

    /// <summary>
    /// Reads one Transfer-Encoding header's codings.
    /// </summary>
    /// <param name="value">The header's value.</param>
    /// <param name="isChunked">Set when the header lists <c>chunked</c>.</param>
    /// <returns>The exit 61 failure for a coding curl does not decode, or <see langword="null" />.</returns>
    private static HttpTransferException? ReadCodings(string value, ref bool isChunked)
    {
        bool listsChunked = false;
        foreach (string coding in Codings(value))
        {
            if (Is(coding, "chunked"))
            {
                listsChunked = true;
                continue;
            }

            HttpTransferException? rejection = Reject(coding, listsChunked);
            if (rejection is not null)
            {
                return rejection;
            }
        }

        isChunked |= listsChunked;
        return null;
    }

    /// <summary>
    /// Splits a Transfer-Encoding value into its codings, without the blanks around each and
    /// without empty items.
    /// </summary>
    private static IEnumerable<string> Codings(string value) =>
        value.Split(',').Select(item => item.Trim(Blanks)).Where(item => item.Length > 0);

    /// <summary>
    /// Checks one coding other than <c>chunked</c>.
    /// </summary>
    /// <param name="coding">The coding.</param>
    /// <param name="followsChunked">Whether <c>chunked</c> came before it in the same header.</param>
    /// <returns>The exit 61 failure, or <see langword="null" /> for an <c>identity</c> curl ignores.</returns>
    private static HttpTransferException? Reject(string coding, bool followsChunked)
    {
        if (followsChunked)
        {
            return Rejected(HttpTransferMessages.CodingListedAfterChunked(coding));
        }

        return Is(coding, "identity") ? null : Rejected(HttpTransferMessages.UnsolicitedTransferCoding(coding));
    }

    private static bool Is(string coding, string name) =>
        string.Equals(coding, name, StringComparison.OrdinalIgnoreCase);

    private static HttpTransferException Rejected(string message) =>
        new(CurlExitCode.BadContentEncoding, message);
}
