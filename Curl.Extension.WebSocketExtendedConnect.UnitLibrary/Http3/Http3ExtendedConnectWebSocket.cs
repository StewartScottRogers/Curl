using Curl.Http3;
using Curl.Protocol.Abstractions;
using Curl.WebSocket;

namespace Curl.Extension.WebSocketExtendedConnect.Http3;

/// <summary>
/// A WebSocket open on an HTTP/3 request stream (RFC 9220 section 3): WebSocket frames travel both ways as the
/// payload of DATA frames, the stream's FIN is the orderly close of the underlying connection and a stream reset its
/// abortive close. The client masks the frames it sends (RFC 6455 section 5.3).
/// </summary>
internal sealed class Http3ExtendedConnectWebSocket : IAsyncDisposable
{
    private const int ReceiveBufferLength = 16 * 1024;
    private readonly Http3ClientSession session;
    private readonly Http3ClientRequestStream stream;
    private readonly IWebSocketRandomSource randomSource;
    private readonly WsFrameDecoder decoder = new(recordsRuns: true);
    private readonly byte[] receiveBuffer = new byte[ReceiveBufferLength];

    /// <summary>Creates the WebSocket over an accepted extended CONNECT.</summary>
    /// <param name="session">The HTTP/3 session, which the WebSocket disposes with itself.</param>
    /// <param name="stream">The request stream the extended CONNECT was accepted on.</param>
    /// <param name="subprotocol">The subprotocol the server chose, or <see langword="null" />.</param>
    /// <param name="randomSource">The source of the masking keys.</param>
    internal Http3ExtendedConnectWebSocket(Http3ClientSession session, Http3ClientRequestStream stream, string? subprotocol, IWebSocketRandomSource randomSource)
    {
        this.session = session;
        this.stream = stream;
        this.randomSource = randomSource;
        Subprotocol = subprotocol;
    }

    /// <summary>Gets the subprotocol the server chose in <c>sec-websocket-protocol</c>, or <see langword="null" />.</summary>
    internal string? Subprotocol { get; }

    /// <summary>Sends one masked WebSocket frame as one DATA frame.</summary>
    /// <param name="opcode">The frame's opcode.</param>
    /// <param name="isFinal">Whether the frame ends its message.</param>
    /// <param name="payload">The frame's payload, unmasked.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the DATA frame is written.</returns>
    internal ValueTask SendFrameAsync(WsOpcode opcode, bool isFinal, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken) =>
        stream.SendDataAsync(WsFrameEncoder.Encode(opcode, isFinal, payload.Span, randomSource), endStream: false, cancellationToken);

    /// <summary>
    /// Reads the next DATA payload from the server and decodes the WebSocket frames in it, which may be parts of
    /// frames that continue in the next.
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The runs of payload decoded, or <see langword="null" /> once the server ended the stream (FIN).</returns>
    /// <exception cref="Http3ExtendedConnectException">The server reset the stream or sent a malformed WebSocket frame.</exception>
    internal async ValueTask<IReadOnlyList<WsFrameRun>?> ReceiveFramesAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            Http3FrameOrData read = await ReadAsync(cancellationToken).ConfigureAwait(false);
            if (read.IsEndOfStream)
            {
                return null;
            }

            if (read.DataLength > 0)
            {
                WsDecodedBytes decoded = decoder.Decode(receiveBuffer.AsSpan(0, read.DataLength));
                return decoded.ViolationMessage is { } violation
                    ? throw new Http3ExtendedConnectException($"The server sent a malformed WebSocket frame: {violation}")
                    : decoder.TakeRuns();
            }
        }
    }

    /// <summary>Ends the stream with FIN, the orderly close of the underlying connection (RFC 9220 section 3).</summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the FIN is written.</returns>
    internal ValueTask CloseAsync(CancellationToken cancellationToken) => stream.EndRequestAsync(cancellationToken);

    /// <summary>Resets the stream with <c>H3_REQUEST_CANCELLED</c>, the abortive close (RFC 9220 section 3).</summary>
    internal void Abort() => stream.Stream.Abort((long)Http3ErrorCode.RequestCancelled);

    /// <summary>Disposes the HTTP/3 session, its streams and the connection.</summary>
    /// <returns>A task that completes when the session is disposed.</returns>
    public ValueTask DisposeAsync() => session.DisposeAsync();

    private async ValueTask<Http3FrameOrData> ReadAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await stream.ReadFrameOrDataAsync(receiveBuffer, cancellationToken).ConfigureAwait(false);
        }
        catch (MultiplexedStreamResetException reset)
        {
            throw new Http3ExtendedConnectException($"The server reset the WebSocket's stream with error code 0x{reset.ApplicationErrorCode:x} (RFC 9220 section 3).", reset);
        }
    }
}
