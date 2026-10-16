namespace Curl.Engine;

/// <summary>
/// A <see cref="TimeProvider" /> whose wall clock stands at one instant while its timestamps and
/// timers stay the system's: the HSTS clock of curl's debug build under <c>CURL_TIME</c>, which
/// <c>lib/hsts.c</c> reads in place of <c>time()</c> (ADR-0493, BL-2234).
/// </summary>
/// <param name="utcNow">The instant <see cref="GetUtcNow" /> always returns.</param>
internal sealed class FixedWallClock(DateTimeOffset utcNow) : TimeProvider
{
    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => utcNow;
}
