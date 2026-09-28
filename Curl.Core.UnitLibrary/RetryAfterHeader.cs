using System.Globalization;
using Curl.Protocol.Abstractions;

namespace Curl.Core;

/// <summary>
/// Reads an HTTP <c>Retry-After</c> header value the way libcurl 8.21.0's
/// <c>http_header_r</c> does, into the number of seconds to wait.
/// </summary>
/// <remarks>
/// Leading blanks are skipped. A date is read first, with <see cref="CurlDateParser" />, the
/// port of the <c>Curl_getdate_capped</c> libcurl uses, so every date curl reads is read: the
/// three HTTP-date forms and lenient ones such as <c>27 Sep 2026 05:26:19</c> or
/// <c>20260927 05:26:19</c>. A date gives the seconds from now until it, or zero when it has
/// passed. Anything else is read as the digits it starts with, <c>3abc</c>, <c>2.5</c> and
/// <c>5 Sep</c> giving 3, 2 and 5; no digits, a sign, or a number too large for a 64-bit
/// integer give zero. The result is capped at 21600 seconds (six hours). Zero means the header
/// asks for nothing. Measured with curl 8.21.0 on 2026-09-26 and 2026-09-27; the commands are
/// in BL-208's and BL-393's notes, and ADR-0094 records the decision.
/// </remarks>
public static class RetryAfterHeader
{
    /// <summary>libcurl's undocumented ceiling on a <c>Retry-After</c> wait: six hours, in seconds.</summary>
    public const long MaxSeconds = 21600;

    /// <summary>
    /// Reads <paramref name="value" /> into whole seconds to wait.
    /// </summary>
    /// <param name="value">The header value, as received.</param>
    /// <param name="now">The current time, from the transfer's <see cref="TimeProvider" />.</param>
    /// <returns>The seconds to wait, zero to <see cref="MaxSeconds" />; zero when the value asks for no wait.</returns>
    public static long ParseSeconds(string value, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(value);

        string text = value.TrimStart(' ', '\t');
        long seconds = CurlDateParser.TryParse(text, out long unixSeconds)
            ? Math.Max(0, unixSeconds - now.ToUnixTimeSeconds())
            : LeadingNumber(text);
        return Math.Min(seconds, MaxSeconds);
    }

    private static long LeadingNumber(string text)
    {
        int length = 0;
        while (length < text.Length && char.IsAsciiDigit(text[length]))
        {
            length++;
        }

        return long.TryParse(text.AsSpan(0, length), NumberStyles.None, CultureInfo.InvariantCulture, out long number)
            ? number
            : 0;
    }
}
