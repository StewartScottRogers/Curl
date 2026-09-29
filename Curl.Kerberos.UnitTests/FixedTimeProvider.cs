namespace Curl.Kerberos;

/// <summary>A <see cref="TimeProvider" /> that always gives the same time, so timestamps and nonces reproduce.</summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
