namespace Curl.Conformance.UnitProgram.Headers.Hsts;

/// <summary>
/// The clock a debug build of curl runs on under <c>CURL_TIME</c>: a fixed second that moves only when
/// <see cref="Advance" /> is called, as a unit program's <c>deltatime++</c> moves it.
/// </summary>
/// <param name="unixSeconds">The second the clock starts at, the case's <c>CURL_TIME</c>.</param>
public sealed class CurlTimeClock(long unixSeconds) : TimeProvider
{
    private long now = unixSeconds;

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => DateTimeOffset.FromUnixTimeSeconds(now);

    /// <summary>Moves the clock one second on.</summary>
    public void Advance() => now++;
}
