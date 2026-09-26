namespace Curl.Protocol.Http.Fakes;

/// <summary>
/// A <see cref="TimeProvider" /> whose clock moves only when a test calls
/// <see cref="Advance(TimeSpan)" />, so no test reads the real clock. Hand-written because
/// the solution takes no package beyond MSTest.
/// </summary>
/// <param name="start">The instant the clock starts at.</param>
public sealed class FakeTimeProvider(DateTimeOffset start) : TimeProvider
{
    private TimeSpan elapsed;

    /// <inheritdoc />
    /// <remarks>One timestamp tick is one <see cref="TimeSpan" /> tick.</remarks>
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => start + elapsed;

    /// <inheritdoc />
    public override long GetTimestamp() => elapsed.Ticks;

    /// <summary>
    /// Moves the clock forward.
    /// </summary>
    /// <param name="duration">How far to move it; zero or more.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="duration" /> is negative.</exception>
    public void Advance(TimeSpan duration)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(duration, TimeSpan.Zero);
        elapsed += duration;
    }
}
