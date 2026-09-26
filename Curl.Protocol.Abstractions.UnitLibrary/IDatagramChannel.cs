using System.Net;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// A datagram socket opened for one transfer: the seam that keeps TFTP off the network
/// during tests.
/// </summary>
/// <remarks>
/// Each send and receive is one whole datagram, and each names its endpoint, so a handler
/// can send its first request to <see cref="ServerEndPoint" />, learn the server's
/// transfer identifier from the source of the first reply, and then address and enforce
/// it (RFC 1350 section 4). A unit test supplies an implementation that replays recorded
/// datagrams with their source endpoints. The handler that was given the channel disposes
/// it.
/// </remarks>
public interface IDatagramChannel : IAsyncDisposable
{
    /// <summary>
    /// Gets the resolved endpoint the first datagram goes to.
    /// </summary>
    EndPoint ServerEndPoint { get; }

    /// <summary>
    /// Sends <paramref name="datagram" /> as one datagram to <paramref name="destination" />.
    /// </summary>
    /// <param name="datagram">The whole datagram to send.</param>
    /// <param name="destination">
    /// Where to send it: <see cref="ServerEndPoint" /> first, then the server's transfer
    /// identifier once a reply has revealed it.
    /// </param>
    /// <param name="cancellationToken">Cancels the send.</param>
    /// <returns>A task that completes when the datagram has been handed to the transport.</returns>
    ValueTask SendAsync(ReadOnlyMemory<byte> datagram, EndPoint destination, CancellationToken cancellationToken);

    /// <summary>
    /// Receives one datagram into <paramref name="buffer" />.
    /// </summary>
    /// <param name="buffer">The destination for the datagram's bytes.</param>
    /// <param name="cancellationToken">Cancels the receive.</param>
    /// <returns>
    /// How many bytes of <paramref name="buffer" /> the datagram filled, and the endpoint
    /// it came from.
    /// </returns>
    ValueTask<DatagramReceived> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken);
}
