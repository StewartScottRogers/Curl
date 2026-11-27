using Curl.Http2;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// One open DNS-over-HTTPS connection, which <see cref="DohDnsResolver" /> keeps for later queries
/// (ADR-1441). One query or pair of queries holds it at a time.
/// </summary>
/// <param name="transport">The connection to the DoH server.</param>
/// <param name="number">The number <c>-v</c> gives the connection.</param>
/// <param name="isHttp2">Whether the TLS handshake picked <c>h2</c>.</param>
internal sealed class DohConnection(IConnection transport, long number, bool isHttp2)
{
    private Http2Session? _http2Session;

    /// <summary>Gets the connection to the DoH server.</summary>
    public IConnection Transport { get; } = transport;

    /// <summary>Gets the number <c>-v</c> gives the connection.</summary>
    public long Number { get; } = number;

    /// <summary>Gets whether the connection speaks HTTP/2.</summary>
    public bool IsHttp2 { get; } = isHttp2;

    /// <summary>
    /// Gets the connection's HTTP/2 state, sending the client preface the first time it is asked for,
    /// so a later query goes on the next stream with the same header compression tables.
    /// </summary>
    /// <param name="cancellationToken">Cancels the preface's write.</param>
    /// <returns>The HTTP/2 state.</returns>
    public async ValueTask<Http2Session> StartHttp2Async(CancellationToken cancellationToken)
    {
        if (_http2Session is null)
        {
            var http2 = new Http2Connection(new ConnectionStream(Transport));
            await http2.SendPrefaceAsync(cancellationToken).ConfigureAwait(false);
            _http2Session = new Http2Session(http2, new HpackEncoder(), new HpackDecoder());
        }

        return _http2Session;
    }

    /// <summary>What one HTTP/2 connection carries from each query to the next.</summary>
    /// <param name="Connection">The frames and streams.</param>
    /// <param name="Encoder">Compresses the requests' header fields.</param>
    /// <param name="Decoder">Decompresses the responses' header blocks.</param>
    internal sealed record Http2Session(Http2Connection Connection, HpackEncoder Encoder, HpackDecoder Decoder);
}
