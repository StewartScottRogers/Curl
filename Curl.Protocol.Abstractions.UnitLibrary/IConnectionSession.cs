namespace Curl.Protocol.Abstractions;

/// <summary>
/// A protocol's state for one connection that outlives a single transfer, such as an HTTP/2
/// session's HPACK tables and stream identifiers: it stays with the connection while the
/// connection is pooled, so the next transfer on the connection continues it (BL-817).
/// </summary>
/// <remarks>
/// A handler hands its session to the connection with
/// <see cref="IConnection.TryHoldSession" />; the next transfer reads it back from
/// <see cref="IConnection.Session" />. A connection that holds a session calls
/// <see cref="ShutDownAsync" /> once, before it closes.
/// </remarks>
public interface IConnectionSession
{
    /// <summary>
    /// Writes what the protocol sends on a connection it is done with, such as HTTP/2's
    /// closing GOAWAY. It does not close the connection, and a connection that can no longer
    /// be written to is not an error.
    /// </summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the bytes are written, or when there is nothing to write.</returns>
    ValueTask ShutDownAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Gets how many transfers the connection can carry at once, each on a stream of its own,
    /// such as an HTTP/2 peer's <c>SETTINGS_MAX_CONCURRENT_STREAMS</c>; 0 once it can carry no
    /// new one; or <see langword="null" />, the default, for a protocol that carries one
    /// transfer at a time. A pool shares a connection whose session gives a number between the
    /// transfers to its origin, as curl multiplexes <c>-Z</c> transfers (BL-717).
    /// </summary>
    int? ConcurrentTransferLimit => null;
}
