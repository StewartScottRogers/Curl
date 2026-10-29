namespace Curl.Protocol.Abstractions;

/// <summary>
/// A multiplexed connection that also carries unreliable datagrams beside its streams: QUIC's
/// DATAGRAM frames (RFC 9221), which HTTP/3 datagrams (RFC 9297) and so WebTransport and
/// MASQUE ride on. An optional interface an <see cref="IMultiplexedConnection" /> may also
/// implement; a caller tests for it, so no other implementation has to change.
/// </summary>
public interface IDatagramCarryingConnection
{
    /// <summary>
    /// Gets the most application bytes one datagram may carry: 0 when the peer did not
    /// advertise that it accepts datagrams, so none may be sent.
    /// </summary>
    int MaxDatagramPayloadLength { get; }

    /// <summary>Queues one datagram to go to the peer; it may be lost, and is never sent again.</summary>
    /// <param name="datagram">The application data, at most <see cref="MaxDatagramPayloadLength" /> bytes.</param>
    /// <param name="cancellationToken">Cancels the send.</param>
    /// <returns>A task that completes once the datagram is queued.</returns>
    /// <exception cref="InvalidOperationException">The peer did not advertise that it accepts datagrams.</exception>
    /// <exception cref="ArgumentException">The datagram is longer than <see cref="MaxDatagramPayloadLength" />.</exception>
    ValueTask SendDatagramAsync(ReadOnlyMemory<byte> datagram, CancellationToken cancellationToken);

    /// <summary>Waits for the next datagram the peer sent.</summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The application data of the datagram.</returns>
    ValueTask<ReadOnlyMemory<byte>> ReceiveDatagramAsync(CancellationToken cancellationToken);
}
