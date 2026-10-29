using System.Globalization;

namespace Curl.Extension.HttpCache;

/// <summary>
/// Parses an HTTP-date (RFC 9110 §5.6.7) in any of its three forms: IMF-fixdate,
/// the obsolete RFC 850 form and the obsolete asctime form. Every form is in GMT.
/// </summary>
public static class HttpDate
{
    private static readonly string[] Formats =
    [
        "ddd, dd MMM yyyy HH':'mm':'ss 'GMT'",
        "dddd, dd'-'MMM'-'yy HH':'mm':'ss 'GMT'",
        "ddd MMM d HH':'mm':'ss yyyy",
    ];

    /// <summary>Parses <paramref name="value"/>; false when it is missing or in none of the three forms.</summary>
    public static bool TryParse(string? value, out DateTimeOffset date) =>
        DateTimeOffset.TryParseExact(
            value,
            Formats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal | DateTimeStyles.AllowInnerWhite,
            out date);
}
