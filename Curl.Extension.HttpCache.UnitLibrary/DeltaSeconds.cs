using System.Globalization;

namespace Curl.Extension.HttpCache;

/// <summary>
/// Parses RFC 9111 §1.2.2 delta-seconds: one or more ASCII digits. A value too large to
/// represent reads as 2147483648 seconds, the greatest value the section requires a cache to keep.
/// </summary>
public static class DeltaSeconds
{
    /// <summary>The greatest delta-seconds value kept, 2^31 seconds (RFC 9111 §1.2.2).</summary>
    public const long Greatest = 2147483648;

    /// <summary>Parses <paramref name="value"/> as delta-seconds; false when it is missing, empty or holds a non-digit.</summary>
    public static bool TryParse(string? value, out TimeSpan seconds)
    {
        seconds = TimeSpan.Zero;
        if (string.IsNullOrEmpty(value) || value.AsSpan().ContainsAnyExceptInRange('0', '9'))
        {
            return false;
        }

        var parsed = long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number < Greatest ? number : Greatest;
        seconds = TimeSpan.FromSeconds(parsed);
        return true;
    }
}
