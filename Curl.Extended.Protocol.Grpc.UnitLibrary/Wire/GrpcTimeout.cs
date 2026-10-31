using System.Globalization;

namespace Curl.Extended.Protocol.Grpc.Wire;

/// <summary>
/// Writes the <c>grpc-timeout</c> header value of PROTOCOL-HTTP2.md "Requests": a TimeoutValue of
/// at most 8 ASCII digits followed by a TimeoutUnit (<c>H</c>, <c>M</c>, <c>S</c>, <c>m</c>,
/// <c>u</c> or <c>n</c>).
/// </summary>
internal static class GrpcTimeout
{
    private const long MaximumValue = 99_999_999;

    private static readonly (char Unit, long Nanoseconds)[] UnitsFinestFirst =
    [
        ('n', 1),
        ('u', 1_000),
        ('m', 1_000_000),
        ('S', 1_000_000_000),
        ('M', 60_000_000_000),
        ('H', 3_600_000_000_000),
    ];

    /// <summary>
    /// Formats <paramref name="timeout" /> in the finest unit whose value fits in 8 digits, rounding
    /// up so the server never sees a shorter deadline than the client asked for (ADR-1133). A timeout
    /// past 99999999 hours is written as that maximum.
    /// </summary>
    /// <param name="timeout">The time the call may take; never negative.</param>
    /// <returns>The header value, for example <c>1500000u</c> for one and a half seconds.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout" /> is negative.</exception>
    public static string Format(TimeSpan timeout)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(timeout, TimeSpan.Zero);
        var nanoseconds = (Int128)timeout.Ticks * 100;
        foreach (var (unit, unitNanoseconds) in UnitsFinestFirst)
        {
            var value = (nanoseconds + unitNanoseconds - 1) / unitNanoseconds;
            if (value <= MaximumValue)
            {
                return string.Create(CultureInfo.InvariantCulture, $"{value}{unit}");
            }
        }

        return string.Create(CultureInfo.InvariantCulture, $"{MaximumValue}H");
    }
}
