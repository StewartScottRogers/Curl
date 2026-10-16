using Curl.Protocol.Abstractions;

namespace Libcurl.Misc.DatesAndLists;

/// <summary>
/// <c>curl_getdate</c>: reads a date the way libcurl's <c>lib/parsedate.c</c> does, over the shared
/// <see cref="CurlDateParser"/> that <c>-z</c> and cookie expiry use (ADR-0074).
/// </summary>
public static class CurlDate
{
    /// <summary>What <c>curl_getdate</c> returns for text that is not a date.</summary>
    public const long Invalid = -1;

    /// <summary>
    /// <c>curl_getdate</c>: <paramref name="text"/> as seconds since the Unix epoch, or
    /// <see cref="Invalid"/> (-1) when it is not a date curl accepts. A date that is really one
    /// second before the epoch answers 0, as upstream moves it off the failure value.
    /// </summary>
    /// <remarks>
    /// Accepts what <see cref="CurlDateParser.TryParse"/> documents: RFC 1123, RFC 850, ANSI C
    /// <c>asctime</c>, <c>yyyyMMdd</c> and the looser orders upstream reads, with a missing time
    /// read as midnight and a missing zone as UTC. Upstream's second argument is unused, so this
    /// has none.
    /// </remarks>
    /// <param name="text">The date text.</param>
    /// <returns>The instant in seconds since the Unix epoch, or -1.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public static long Parse(string text) => CurlDateParser.TryParse(text, out long unixSeconds) ? unixSeconds : Invalid;
}
