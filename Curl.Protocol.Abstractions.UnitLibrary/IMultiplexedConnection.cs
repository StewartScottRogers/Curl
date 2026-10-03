using System.Net;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// One QUIC connection that carries many independent streams: the seam HTTP/3
/// (<c>Curl.Http3</c> and the HTTP handler) talks through, so it is tested over in-memory
/// streams with no QUIC (ADR-0144).
/// </summary>
/// <remarks>
/// <see cref="IConnector.ConnectMultiplexedAsync(ConnectTarget, CancellationToken)" />
/// yields it, already past its handshake; <c>Curl.Networking</c> implements it over
/// <c>Curl.Quic</c>. The handler that was given the connection disposes it.
/// </remarks>
public interface IMultiplexedConnection : IAsyncDisposable
{
    /// <summary>
    /// Gets the server's endpoint, or <see langword="null" /> when the implementation has
    /// no meaningful address, as is the case for a fake used in a test.
    /// </summary>
    EndPoint? RemoteEndPoint { get; }

    /// <summary>
    /// Gets the local endpoint of the connection's socket, the source of
    /// <c>%{local_ip}</c> and <c>%{local_port}</c>, or <see langword="null" /> when the
    /// implementation has no meaningful address.
    /// </summary>
    EndPoint? LocalEndPoint { get; }

    /// <summary>
    /// Gets the application protocol the server chose in the handshake's ALPN, such as
    /// <c>h3</c>.
    /// </summary>
    string ApplicationProtocol { get; }

    /// <summary>
    /// Gets how many client-initiated bidirectional streams the peer allows: its latest
    /// <c>MAX_STREAMS</c>, or <see langword="null" />, the default, when the implementation does
    /// not know it. A pool shares the connection between at most this many transfers (BL-735).
    /// </summary>
    long? BidirectionalStreamLimit => null;

    /// <summary>
    /// Gets the idle timeout the peer declared in its <c>max_idle_timeout</c> transport
    /// parameter, which curl's <c>--trace-config http/3</c> line <c>peer idle timeout is &lt;n&gt;ms</c>
    /// names (BL-1208), or <see langword="null" />, the default, when it declared none or the
    /// implementation does not know it.
    /// </summary>
    TimeSpan? PeerIdleTimeout => null;

    /// <summary>
    /// Opens a client-initiated bidirectional stream, which HTTP/3 carries one request and
    /// its response on.
    /// </summary>
    /// <param name="cancellationToken">Cancels waiting for the peer to allow another stream.</param>
    /// <returns>The open stream.</returns>
    ValueTask<IMultiplexedStream> OpenBidirectionalStreamAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Opens a client-initiated unidirectional stream, which HTTP/3 uses for its control
    /// and QPACK encoder and decoder streams.
    /// </summary>
    /// <param name="cancellationToken">Cancels waiting for the peer to allow another stream.</param>
    /// <returns>The open stream, which can only be written.</returns>
    ValueTask<IMultiplexedStream> OpenUnidirectionalStreamAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Waits for the next unidirectional stream the server opens, such as HTTP/3's server
    /// control stream.
    /// </summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The server's stream, which can only be read.</returns>
    ValueTask<IMultiplexedStream> AcceptUnidirectionalStreamAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Closes the connection with a <c>CONNECTION_CLOSE</c> carrying
    /// <paramref name="applicationErrorCode" />.
    /// </summary>
    /// <param name="applicationErrorCode">
    /// The application's error code, such as HTTP/3's <c>H3_NO_ERROR</c> (<c>0x100</c>).
    /// </param>
    /// <param name="cancellationToken">Cancels the close.</param>
    /// <returns>A task that completes when the close has been sent.</returns>
    ValueTask CloseAsync(long applicationErrorCode, CancellationToken cancellationToken);
}
