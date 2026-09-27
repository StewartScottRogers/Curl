using System.Globalization;

namespace Curl.Protocol.Http;

/// <summary>
/// Reads a response's <c>Content-Range</c> headers the way curl 8.21.0 does to decide whether a
/// <c>-C</c>/<c>--continue-at</c> resume was honoured (measured, BL-178 Notes).
/// </summary>
/// <remarks>
/// curl skips to the value's first digit or asterisk, so <c>bytes 100-104/105</c>,
/// <c>bytes: 100-</c> and <c>100-</c> all start at 100. A value that starts with no digit -
/// <c>*/105</c>, or none at all - on a 2xx response makes curl fetch the whole resource
/// instead of resuming, which it also counts as honoured.
/// </remarks>
internal static class HttpContentRange
{
    private const string HeaderName = "Content-Range";

    /// <summary>
    /// Determines whether any <c>Content-Range</c> header of <paramref name="head" /> honours a
    /// resume from <paramref name="resumeFrom" />.
    /// </summary>
    /// <param name="head">The final response's head.</param>
    /// <param name="resumeFrom">The byte offset the request asked to resume from.</param>
    /// <returns>
    /// <see langword="true" /> when a <c>Content-Range</c> starts at
    /// <paramref name="resumeFrom" />, or names no start on a 2xx response.
    /// </returns>
    internal static bool HonoursResume(HttpResponseHead head, long resumeFrom) =>
        head.Headers
            .Where(header => string.Equals(header.Name, HeaderName, StringComparison.OrdinalIgnoreCase))
            .Any(header => HonoursResume(header.Value, head.StatusLine.StatusCode, resumeFrom));

    private static bool HonoursResume(string value, int statusCode, long resumeFrom)
    {
        int start = value.AsSpan().IndexOfAny("*0123456789");
        if (start < 0 || value[start] == '*')
        {
            return statusCode < 300;
        }

        ReadOnlySpan<char> digits = value.AsSpan(start);
        int length = digits.IndexOfAnyExceptInRange('0', '9');
        digits = length < 0 ? digits : digits[..length];
        return long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out long first) && first == resumeFrom;
    }
}
