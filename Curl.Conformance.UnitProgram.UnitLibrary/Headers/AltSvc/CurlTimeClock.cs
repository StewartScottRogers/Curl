namespace Curl.Conformance.UnitProgram.Headers.AltSvc;

/// <summary>
/// The clock of the alt-svc cases' <c>CURL_TIME=1548369261</c>: a <see cref="TimeProvider" /> that always
/// answers that second, <c>20190124 22:34:21</c> UTC. The context carries no environment, so the ports
/// inject the epoch the C programs' checks hard-code.
/// </summary>
/// <param name="epochSeconds">The second the clock answers, in seconds since 1970 UTC.</param>
public sealed class CurlTimeClock(long epochSeconds) : TimeProvider
{
    /// <summary>The cases' <c>CURL_TIME</c>, <c>1548369261</c>.</summary>
    public const long UpstreamCaseEpochSeconds = 1548369261;

    /// <summary>The second the clock answers, in seconds since 1970 UTC.</summary>
    public long EpochSeconds => epochSeconds;

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => DateTimeOffset.FromUnixTimeSeconds(epochSeconds);
}
