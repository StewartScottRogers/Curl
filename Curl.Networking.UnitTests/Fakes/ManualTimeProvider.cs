using System.Net;
using System.Net.Sockets;

using Curl.Protocol.Abstractions;

namespace Curl.Networking.Fakes;

/// <summary>
/// A <see cref="TimeProvider" /> whose timestamp moves only when a test advances it, in
/// milliseconds, so no test reads the real clock.
/// </summary>
public sealed class ManualTimeProvider : TimeProvider
{
    private long _elapsedMilliseconds;

    /// <inheritdoc />
    public override long TimestampFrequency => 1000;

    /// <inheritdoc />
    public override long GetTimestamp() => _elapsedMilliseconds;

    /// <summary>Moves the timestamp forward.</summary>
    /// <param name="milliseconds">How far to move it.</param>
    public void Advance(long milliseconds) => _elapsedMilliseconds += milliseconds;
}
