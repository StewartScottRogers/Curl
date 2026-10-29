namespace Curl.Protocol.Abstractions;

/// <summary>
/// A multiplexed connection that also hands over the bidirectional streams the peer opens,
/// which HTTP/3 itself never uses but extensions such as WebTransport do. An optional
/// interface an <see cref="IMultiplexedConnection" /> may also implement; a caller tests for
/// it, so no other implementation has to change.
/// </summary>
public interface IPeerBidirectionalStreamAccepting
{
    /// <summary>Waits for the next bidirectional stream the peer opens.</summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The peer's stream, which can be read and written.</returns>
    ValueTask<IMultiplexedStream> AcceptBidirectionalStreamAsync(CancellationToken cancellationToken);
}
