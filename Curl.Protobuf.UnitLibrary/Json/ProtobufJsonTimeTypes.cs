using System.Globalization;
using System.Text.Json;
using Curl.Protobuf.DynamicMessages;

namespace Curl.Protobuf.Json;

/// <summary>
/// The JSON string forms of the well-known types <c>google.protobuf.Timestamp</c> (RFC 3339,
/// written in UTC with "Z", read with any offset) and <c>google.protobuf.Duration</c> (seconds
/// followed by "s"), written with 0, 3, 6 or 9 fractional digits. A type is recognised by its
/// full name, wherever its descriptor came from.
/// </summary>
internal static class ProtobufJsonTimeTypes
{
    private const string TimestampName = "google.protobuf.Timestamp";
    private const string DurationName = "google.protobuf.Duration";
    private const long MinimumTimestampSeconds = -62135596800; // 0001-01-01T00:00:00Z
    private const long MaximumTimestampSeconds = 253402300799; // 9999-12-31T23:59:59Z
    private const long MaximumDurationSeconds = 315576000000; // 10,000 years
    private const int NanosPerSecond = 1_000_000_000;
    private const string TimestampLayout = "yyyy-MM-dd'T'HH:mm:ss";
    private const int TimestampLayoutLength = 19;

    /// <summary>Whether messages of this type have a string form here.</summary>
    public static bool HasStringForm(DynamicMessageType type) => type.FullName is TimestampName or DurationName;

    /// <summary>The string form of a Timestamp or Duration; throws <see cref="FormatException" /> when its value is out of the type's range.</summary>
    public static string Format(DynamicMessage message)
    {
        var seconds = (long)message.GetValue("seconds");
        var nanos = (int)message.GetValue("nanos");
        return message.Type.FullName == TimestampName ? FormatTimestamp(seconds, nanos) : FormatDuration(seconds, nanos);
    }

    /// <summary>Reads the string form of a Timestamp or Duration into the message.</summary>
    public static ProtobufJsonError Read(JsonElement element, DynamicMessage message)
    {
        if (element.ValueKind != JsonValueKind.String)
        {
            return ProtobufJsonError.WrongType;
        }

        var text = element.GetString()!;
        var parsed = message.Type.FullName == TimestampName
            ? TryParseTimestamp(text, out var seconds, out var nanos)
            : TryParseDuration(text, out seconds, out nanos);
        if (!parsed)
        {
            return ProtobufJsonError.InvalidValue;
        }

        message.SetValue("seconds", seconds);
        message.SetValue("nanos", nanos);
        return ProtobufJsonError.None;
    }

    private static string FormatTimestamp(long seconds, int nanos)
    {
        if (!IsTimestampInRange(seconds) || !IsNanosInRange(nanos))
        {
            throw new FormatException($"Timestamp {seconds}s {nanos}ns is out of range.");
        }

        var time = DateTime.UnixEpoch.AddSeconds(seconds);
        return time.ToString(TimestampLayout, CultureInfo.InvariantCulture) + Fraction(nanos) + "Z";
    }

    private static string FormatDuration(long seconds, int nanos)
    {
        if (!IsDurationInRange(seconds, nanos))
        {
            throw new FormatException($"Duration {seconds}s {nanos}ns is out of range.");
        }

        var sign = seconds < 0 || nanos < 0 ? "-" : string.Empty;
        return sign + long.Abs(seconds).ToString(CultureInfo.InvariantCulture) + Fraction(int.Abs(nanos)) + "s";
    }

    private static bool IsTimestampInRange(long seconds) => seconds >= MinimumTimestampSeconds && seconds <= MaximumTimestampSeconds;

    private static bool IsNanosInRange(int nanos) => nanos >= 0 && nanos < NanosPerSecond;

    private static bool IsDurationInRange(long seconds, int nanos) =>
        seconds >= -MaximumDurationSeconds && seconds <= MaximumDurationSeconds && nanos > -NanosPerSecond && nanos < NanosPerSecond && HaveOneSign(seconds, nanos);

    private static bool HaveOneSign(long seconds, int nanos) => (seconds <= 0 && nanos <= 0) || (seconds >= 0 && nanos >= 0);

    private static string Fraction(int nanos) => nanos switch
    {
        0 => string.Empty,
        _ when nanos % 1_000_000 == 0 => "." + (nanos / 1_000_000).ToString("D3", CultureInfo.InvariantCulture),
        _ when nanos % 1_000 == 0 => "." + (nanos / 1_000).ToString("D6", CultureInfo.InvariantCulture),
        _ => "." + nanos.ToString("D9", CultureInfo.InvariantCulture),
    };

    // yyyy-MM-ddTHH:mm:ss, then an optional fraction, then Z or an offset ±HH:mm.
    private static bool TryParseTimestamp(string text, out long seconds, out int nanos)
    {
        seconds = 0;
        nanos = 0;
        if (text.Length <= TimestampLayoutLength
            || !DateTime.TryParseExact(text[..TimestampLayoutLength], TimestampLayout, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var time)
            || !TrySplitFraction(text.AsSpan(TimestampLayoutLength), out nanos, out var zone)
            || !TryReadOffset(zone, out var offsetSeconds))
        {
            return false;
        }

        seconds = ((time - DateTime.UnixEpoch).Ticks / TimeSpan.TicksPerSecond) - offsetSeconds;
        return IsTimestampInRange(seconds);
    }

    // The fraction of a Timestamp: none, or "." and 1 to 9 digits, before the zone.
    private static bool TrySplitFraction(ReadOnlySpan<char> rest, out int nanos, out ReadOnlySpan<char> zone)
    {
        nanos = 0;
        zone = rest;
        if (rest[0] != '.')
        {
            return true;
        }

        var digits = rest[1..].IndexOfAnyExceptInRange('0', '9');
        zone = digits < 0 ? [] : rest[(digits + 1)..];
        return TryParseFraction(rest[1..(digits < 0 ? rest.Length : digits + 1)], out nanos);
    }

    private static bool TryReadOffset(ReadOnlySpan<char> zone, out long offsetSeconds)
    {
        offsetSeconds = 0;
        return zone is "Z" || TryReadNumericOffset(zone, out offsetSeconds);
    }

    // ±HH:mm
    private static bool TryReadNumericOffset(ReadOnlySpan<char> zone, out long offsetSeconds)
    {
        offsetSeconds = 0;
        var sign = zone.IsEmpty ? 0 : SignOf(zone[0]);
        if (sign == 0 || !TimeSpan.TryParseExact(zone[1..], @"hh\:mm", CultureInfo.InvariantCulture, out var offset))
        {
            return false;
        }

        offsetSeconds = sign * (long)offset.TotalSeconds;
        return true;
    }

    private static int SignOf(char sign) => sign switch
    {
        '+' => 1,
        '-' => -1,
        _ => 0,
    };

    // [-]seconds[.fraction]s, with at most 315,576,000,000 seconds and 1 to 9 digits of fraction.
    private static bool TryParseDuration(string text, out long seconds, out int nanos)
    {
        var negative = text.StartsWith('-');
        var parsed = TryParseUnsignedDuration(text.AsSpan(negative ? 1 : 0), out seconds, out nanos);
        var sign = negative ? -1 : 1;
        seconds *= sign;
        nanos *= sign;
        return parsed;
    }

    private static bool TryParseUnsignedDuration(ReadOnlySpan<char> text, out long seconds, out int nanos)
    {
        seconds = 0;
        nanos = 0;
        if (!text.EndsWith('s'))
        {
            return false;
        }

        var body = text[..^1];
        var point = body.IndexOf('.');
        return point < 0
            ? TryParseDurationSeconds(body, out seconds)
            : TryParseDurationSeconds(body[..point], out seconds) && TryParseFraction(body[(point + 1)..], out nanos);
    }

    private static bool TryParseDurationSeconds(ReadOnlySpan<char> digits, out long magnitude) =>
        long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out magnitude) && magnitude <= MaximumDurationSeconds;

    private static bool TryParseFraction(ReadOnlySpan<char> digits, out int nanos)
    {
        nanos = 0;
        if (digits.Length is 0 or > 9 || !TryParseDigits(digits, out var value))
        {
            return false;
        }

        nanos = value * (int)Math.Pow(10, 9 - digits.Length);
        return true;
    }

    private static bool TryParseDigits(ReadOnlySpan<char> digits, out int value) =>
        int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out value);
}
