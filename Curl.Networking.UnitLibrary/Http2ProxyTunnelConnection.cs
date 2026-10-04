using System.Globalization;
using System.Net;

using Curl.Http2;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// A tunnel through an HTTPS proxy that agreed on <c>h2</c> by ALPN: one HTTP/2 <c>CONNECT</c>
/// stream (RFC 9113 section 8.5) on the proxy's connection, whose DATA frames carry the
/// target's bytes both ways (ADR-0408 decision 4). Presented as an <see cref="IConnection" />,
/// so the target's TLS and every protocol run over it as over the HTTP/1.1 tunnel.
/// </summary>
internal sealed class Http2ProxyTunnelConnection : IConnection
{
    private readonly IConnection _proxyConnection;
    private readonly Http2Connection _http2;
    private readonly int _streamId;
    private ReadOnlyMemory<byte> _received;
    private bool _ended;

    private Http2ProxyTunnelConnection(IConnection proxyConnection, Http2Connection http2, int streamId)
    {
        _proxyConnection = proxyConnection;
        _http2 = http2;
        _streamId = streamId;
    }

    /// <inheritdoc />
    public bool IsSecure => _proxyConnection.IsSecure;

    /// <inheritdoc />
    public EndPoint? RemoteEndPoint => _proxyConnection.RemoteEndPoint;

    /// <inheritdoc />
    public EndPoint? LocalEndPoint => _proxyConnection.LocalEndPoint;

    /// <summary>
    /// Gets the CONNECT request's header fields: <c>:method</c> and <c>:authority</c> only, no
    /// <c>:scheme</c> or <c>:path</c> (RFC 9113 section 8.5), then <c>proxy-authorization</c>
    /// when there is one and <c>user-agent</c> when there is one, as curl 8.18.0 sent them
    /// (measured, BL-1413 Notes).
    /// </summary>
    /// <param name="authority">The target as <c>host:port</c>.</param>
    /// <param name="proxyAuthorization">The <c>Proxy-Authorization</c> value, or <see langword="null" />.</param>
    /// <param name="userAgent">The <c>User-Agent</c> value, or <see langword="null" />.</param>
    /// <returns>The header fields in the order they are sent.</returns>
    internal static IReadOnlyList<HeaderField> ConnectHeaderFields(string authority, string? proxyAuthorization, string? userAgent)
    {
        List<HeaderField> fields = [new(":method", "CONNECT"), new(":authority", authority)];
        if (proxyAuthorization is not null)
        {
            fields.Add(new("proxy-authorization", proxyAuthorization));
        }

        if (userAgent is not null)
        {
            fields.Add(new("user-agent", userAgent));
        }

        return fields;
    }

    /// <summary>
    /// Sends the HTTP/2 preface and the CONNECT on stream 1 of <paramref name="proxyConnection" />,
    /// then reads the proxy's final status.
    /// </summary>
    /// <param name="proxyConnection">The proxy's TLS connection, which agreed on <c>h2</c>.</param>
    /// <param name="headerFields">The CONNECT's header fields (<see cref="ConnectHeaderFields" />).</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>
    /// The proxy's status code, <c>0</c> when it closed the connection before one, and the
    /// tunnel when the status is <c>2xx</c>, else <see langword="null" />.
    /// </returns>
    internal static async ValueTask<(int StatusCode, Http2ProxyTunnelConnection? Tunnel)> OpenAsync(
        IConnection proxyConnection,
        IReadOnlyList<HeaderField> headerFields,
        CancellationToken cancellationToken)
    {
        var http2 = new Http2Connection(new ConnectionStream(proxyConnection));
        await http2.SendPrefaceAsync(cancellationToken).ConfigureAwait(false);
        var streamId = http2.OpenStream();
        await http2.WriteHeadersAsync(streamId, new HpackEncoder().Encode(headerFields), isEndStream: false, cancellationToken).ConfigureAwait(false);
        await proxyConnection.FlushAsync(cancellationToken).ConfigureAwait(false);
        var statusCode = await ReadFinalStatusAsync(http2, cancellationToken).ConfigureAwait(false);
        return statusCode is >= 200 and < 300
            ? (statusCode, new Http2ProxyTunnelConnection(proxyConnection, http2, streamId))
            : (statusCode, null);
    }

    /// <summary>
    /// Reads header blocks until one holds a final (non-<c>1xx</c>) <c>:status</c>, decoding every
    /// block so the HPACK table stays in step.
    /// </summary>
    private static async ValueTask<int> ReadFinalStatusAsync(Http2Connection http2, CancellationToken cancellationToken)
    {
        var decoder = new HpackDecoder();
        while (await http2.ReadStreamFrameAsync(cancellationToken).ConfigureAwait(false) is { } frame)
        {
            if (frame.Type == Http2FrameType.Headers && StatusOf(decoder.Decode(frame.Content.Span)) is >= 200 and var statusCode)
            {
                return statusCode;
            }
        }

        return 0;
    }

    private static int StatusOf(IReadOnlyList<HeaderField> fields) =>
        fields.FirstOrDefault(field => field.Name == ":status") is { Name: not null } status
            && int.TryParse(status.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var code)
            ? code
            : 0;

    /// <inheritdoc />
    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        while (_received.IsEmpty && !_ended)
        {
            var frame = await _http2.ReadStreamFrameAsync(cancellationToken).ConfigureAwait(false);
            _ended = frame is null;
            Accept(frame);
        }

        var count = Math.Min(buffer.Length, _received.Length);
        _received[..count].CopyTo(buffer);
        _received = _received[count..];
        return count;
    }

    /// <summary>
    /// Sends <paramref name="buffer" /> in DATA frames, as much as the flow-control windows let
    /// through at a time, reading the proxy's frames for its WINDOW_UPDATE when they are spent
    /// and keeping any DATA that arrives meanwhile for <see cref="ReadAsync" />.
    /// </summary>
    /// <exception cref="IOException">The proxy closed the connection while the windows were spent.</exception>
    public async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        while (!buffer.IsEmpty)
        {
            var sent = await _http2.WriteDataAsync(_streamId, buffer, isEndStream: false, cancellationToken).ConfigureAwait(false);
            buffer = buffer[sent..];
            if (sent == 0)
            {
                Accept(await _http2.ReadFrameAsync(cancellationToken).ConfigureAwait(false));
                ThrowIfClosedByPeer();
            }
        }
    }

    private void ThrowIfClosedByPeer()
    {
        if (_http2.IsClosedByPeer)
        {
            throw new IOException("The HTTPS proxy closed the HTTP/2 tunnel while data was waiting to be sent.");
        }
    }

    private void Accept(Http2StreamFrame? frame)
    {
        if (frame is { Type: Http2FrameType.Data })
        {
            _received = _received.IsEmpty ? frame.Content : (byte[])[.. _received.Span, .. frame.Content.Span];
        }

        _ended |= frame is { IsEndStream: true };
    }

    /// <inheritdoc />
    public ValueTask FlushAsync(CancellationToken cancellationToken) => _proxyConnection.FlushAsync(cancellationToken);

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _proxyConnection.DisposeAsync();
}
