namespace Curl.Output;

/// <summary>A clock that reads the next of a list of instants each time it is read, with UTC as its local time.</summary>
/// <param name="readings">The instants <see cref="GetUtcNow"/> returns, in order.</param>
internal sealed class QueuedTimeProvider(params DateTimeOffset[] readings) : TimeProvider
{
    private readonly Queue<DateTimeOffset> readings = new(readings);

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

    public override DateTimeOffset GetUtcNow()
    {
        return readings.Dequeue();
    }
}
