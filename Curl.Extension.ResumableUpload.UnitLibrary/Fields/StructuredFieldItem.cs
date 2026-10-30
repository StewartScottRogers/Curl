using System.Globalization;

namespace Curl.Extension.ResumableUpload.Fields;

/// <summary>
/// Formats and parses the Structured Field items the resumable upload draft's header fields carry (RFC 9651): a non-negative
/// sf-integer (§3.3.1) and an sf-boolean (§3.3.6). An item's parameters (§3.1.2) are skipped, since the draft defines none.
/// </summary>
internal static class StructuredFieldItem
{
    /// <summary>The largest sf-integer, fifteen decimal digits (RFC 9651 §3.3.1).</summary>
    public const long MaxInteger = 999_999_999_999_999;

    /// <summary>Formats a non-negative sf-integer.</summary>
    /// <param name="value">The integer, from zero to <see cref="MaxInteger"/>.</param>
    /// <param name="paramName">The caller's parameter name, for the exception.</param>
    /// <returns>The decimal digits.</returns>
    public static string FormatInteger(long value, string paramName)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value, paramName);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value, MaxInteger, paramName);
        return value.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>Parses a non-negative sf-integer: one to fifteen decimal digits, no sign.</summary>
    /// <param name="value">The field value or dictionary member value.</param>
    /// <param name="result">The integer, or zero when the value is malformed.</param>
    /// <returns>Whether the value is a non-negative sf-integer.</returns>
    public static bool TryParseInteger(ReadOnlySpan<char> value, out long result)
    {
        var item = BareItem(value);
        result = 0;
        return item.Length is >= 1 and <= 15 && item.IndexOfAnyExceptInRange('0', '9') < 0
            && long.TryParse(item, NumberStyles.None, CultureInfo.InvariantCulture, out result);
    }

    /// <summary>Parses an sf-boolean, <c>?0</c> or <c>?1</c>.</summary>
    /// <param name="value">The field value.</param>
    /// <param name="result">The boolean, or <see langword="false"/> when the value is malformed.</param>
    /// <returns>Whether the value is an sf-boolean.</returns>
    public static bool TryParseBoolean(ReadOnlySpan<char> value, out bool result)
    {
        var item = BareItem(value);
        result = item.SequenceEqual("?1");
        return result || item.SequenceEqual("?0");
    }

    private static ReadOnlySpan<char> BareItem(ReadOnlySpan<char> value)
    {
        var parameters = value.IndexOf(';');
        return (parameters < 0 ? value : value[..parameters]).Trim(' ');
    }
}
