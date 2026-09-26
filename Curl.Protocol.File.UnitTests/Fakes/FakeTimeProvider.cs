namespace Curl.Protocol.File.Fakes;

/// <summary>
/// A <see cref="TimeProvider" /> stopped at one instant, so no test reads the real clock.
/// </summary>
/// <param name="utcNow">The instant this provider always reports.</param>
public sealed class FakeTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => utcNow;
}
