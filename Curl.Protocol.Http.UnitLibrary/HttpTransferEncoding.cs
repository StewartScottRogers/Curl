using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// Reads a response's Transfer-Encoding headers the way curl 8.21.0 does when it was not
/// asked to decode transfer compression (measured, BL-171): whether the body is chunked, and
/// exit 61 for any other coding; and, for <c>--tr-encoding</c>, which codings to decode
/// (<see cref="Requested" />).
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
    /// <summary>
    /// The most codings <c>--tr-encoding</c> accepts across a response's Transfer-Encoding
    /// headers, <c>chunked</c> included: curl 8.21.0's limit (measured, BL-315 Notes).
    /// </summary>
    internal const int MaximumCodings = 5;

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
    /// Reads the Transfer-Encoding headers as curl 8.21.0 does for <c>--tr-encoding</c>
    /// (measured, BL-315 Notes): every coding of every header is accepted, to be decoded or,
    /// for one curl does not know, to fail once the body arrives, but at most
    /// <see cref="MaximumCodings" /> of them, and none after <c>chunked</c>. A repeated
    /// <c>chunked</c> is ignored. Under <c>--raw</c>, <c>chunked</c> is passed through and so
    /// neither counted nor checked, while every other coding is still decoded.
    /// </summary>
    /// <param name="headers">The final response's headers.</param>
    /// <param name="passesChunked"><see langword="true" /> for <c>--raw</c>.</param>
    /// <returns>The codings.</returns>
    /// <exception cref="HttpTransferException">
    /// More than <see cref="MaximumCodings" /> codings, or one after <c>chunked</c> (exit 61), or
    /// a Content-Length header before that one is invalid (exit 8).
    /// </exception>
    internal static HttpTransferCodings Requested(IReadOnlyList<HttpResponseHeader> headers, bool passesChunked)
    {
        List<string> codings = [];
        bool isChunked = false;
        int? firstHeaderIndex = null;
        for (int index = 0; index < headers.Count; index++)
        {
            if (!string.Equals(headers[index].Name, HeaderName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            firstHeaderIndex ??= index;
            HttpTransferException? rejection = AdmitCodings(headers[index].Value, codings, ref isChunked, passesChunked);
            if (rejection is not null)
            {
                HttpContentLength.Find([.. headers.Take(index)]);
                throw rejection;
            }
        }

        return new HttpTransferCodings(isChunked, firstHeaderIndex, codings);
    }

    /// <summary>
    /// Admits one Transfer-Encoding header's codings for <see cref="Requested" />.
    /// </summary>
    /// <returns>The exit 61 failure for the first coding refused, or <see langword="null" />.</returns>
    private static HttpTransferException? AdmitCodings(string value, List<string> codings, ref bool isChunked, bool passesChunked)
    {
        foreach (string coding in Codings(value))
        {
            HttpTransferException? rejection = AdmitCoding(coding, codings, ref isChunked, passesChunked);
            if (rejection is not null)
            {
                return rejection;
            }
        }

        return null;
    }

    /// <summary>
    /// Admits one coding for <see cref="Requested" />: the limit is checked first, then that
    /// nothing follows <c>chunked</c>.
    /// </summary>
    /// <returns>The exit 61 failure, or <see langword="null" /> when the coding is admitted or ignored.</returns>
    private static HttpTransferException? AdmitCoding(string coding, List<string> codings, ref bool isChunked, bool passesChunked)
    {
        bool isChunkedCoding = Is(coding, "chunked");
        if (isChunkedCoding && passesChunked)
        {
            return null;
        }

        return CountOf(codings, isChunked) >= MaximumCodings
            ? Rejected(HttpTransferMessages.TooManyTransferCodings)
            : PlaceCoding(coding, isChunkedCoding, codings, ref isChunked);
    }

    private static int CountOf(List<string> codings, bool isChunked) => codings.Count + (isChunked ? 1 : 0);

    /// <summary>
    /// Adds one coding within the limit for <see cref="Requested" />, refusing any but a repeated
    /// <c>chunked</c> once <c>chunked</c> has been listed.
    /// </summary>
    /// <returns>The exit 61 failure, or <see langword="null" /> when the coding is admitted or ignored.</returns>
    private static HttpTransferException? PlaceCoding(string coding, bool isChunkedCoding, List<string> codings, ref bool isChunked)
    {
        if (isChunked)
        {
            return isChunkedCoding ? null : Rejected(HttpTransferMessages.ChunkedNotLast);
        }

        if (isChunkedCoding)
        {
            isChunked = true;
        }
        else
        {
            codings.Add(coding);
        }

        return null;
    }

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
