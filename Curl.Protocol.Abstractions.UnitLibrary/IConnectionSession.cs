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
}
