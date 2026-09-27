namespace Curl.Protocol.Http.Fakes;

/// <summary>
/// A <see cref="TimeProvider" /> whose timestamp moves forward by 10 after every read, so
/// each timestamp a test sees is distinct and in the order it was taken, without reading the
/// real clock. Its timers are the system's.
/// </summary>
/// <param name="first">The first timestamp returned.</param>
public sealed class SteppingTimeProvider(long first) : TimeProvider
{
    private long next = first;

    /// <inheritdoc />
    public override long TimestampFrequency => 1000;

    /// <inheritdoc />
    public override long GetTimestamp()
    {
        long timestamp = next;
        next += 10;
        return timestamp;
    }
}
