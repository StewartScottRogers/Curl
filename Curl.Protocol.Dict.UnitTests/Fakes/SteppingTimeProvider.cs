namespace Curl.Protocol.Dict.Fakes;

/// <summary>
/// A <see cref="TimeProvider" /> whose clock moves on by <paramref name="step" /> each time a
/// timestamp is read, the first read being zero, so a measured duration is known exactly
/// and no test reads the real clock.
/// </summary>
/// <param name="step">How far the clock moves between one timestamp and the next.</param>
public sealed class SteppingTimeProvider(TimeSpan step) : TimeProvider
{
    private long reads;

    /// <inheritdoc />
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    /// <inheritdoc />
    public override long GetTimestamp() => step.Ticks * reads++;
}
