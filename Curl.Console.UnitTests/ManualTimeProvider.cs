namespace Curl.Console;

/// <summary>
/// A <see cref="TimeProvider" /> whose timestamp moves only when a test advances it, in
/// milliseconds, so no test reads the real clock.
/// </summary>
public sealed class ManualTimeProvider : TimeProvider
{
    private long elapsedMilliseconds;

    /// <inheritdoc />
    public override long TimestampFrequency => 1000;

    /// <inheritdoc />
    public override long GetTimestamp() => elapsedMilliseconds;

    /// <summary>Moves the timestamp forward.</summary>
    /// <param name="milliseconds">How far to move it.</param>
    public void Advance(long milliseconds) => elapsedMilliseconds += milliseconds;
}
