namespace Curl.Networking.Fakes;

/// <summary>
/// A <see cref="TimeProvider" /> whose timestamp moves forward by <see cref="Step" />
/// after every read, so each timestamp a test sees is distinct and in the order it was
/// taken, without reading the real clock.
/// </summary>
/// <param name="first">The first timestamp returned.</param>
public sealed class SteppingTimeProvider(long first) : TimeProvider
{
    private long _next = first;

    /// <summary>Gets how far the timestamp moves after each read.</summary>
    public long Step { get; init; } = 10;

    /// <inheritdoc />
    public override long TimestampFrequency => 1000;

    /// <inheritdoc />
    public override long GetTimestamp()
    {
        var timestamp = _next;
        _next += Step;
        return timestamp;
    }
}
