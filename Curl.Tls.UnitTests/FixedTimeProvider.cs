namespace Curl.Tls;

/// <summary>A <see cref="TimeProvider" /> whose clock stands still at <paramref name="now" />.</summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
