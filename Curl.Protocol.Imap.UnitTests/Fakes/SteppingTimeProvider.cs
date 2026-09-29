namespace Curl.Protocol.Imap.Fakes;

/// <summary>
/// A <see cref="TimeProvider" /> whose timestamps count milliseconds and advance by
/// <paramref name="stepMilliseconds" /> each time one is read, so an elapsed time is exact.
/// </summary>
/// <param name="stepMilliseconds">How far each read advances the clock.</param>
public sealed class SteppingTimeProvider(long stepMilliseconds) : TimeProvider
{
    private long now;

    /// <inheritdoc />
    public override long TimestampFrequency => 1000;

    /// <inheritdoc />
    public override long GetTimestamp()
    {
        long read = now;
        now += stepMilliseconds;
        return read;
    }
}
