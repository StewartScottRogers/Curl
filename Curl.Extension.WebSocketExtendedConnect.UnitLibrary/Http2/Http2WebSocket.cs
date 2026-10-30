using Curl.Http2;
using Curl.WebSocket;

namespace Curl.Extension.WebSocketExtendedConnect.Http2;

/// <summary>
/// A WebSocket open on one HTTP/2 stream (RFC 8441 section 5): the DATA frames on the stream carry the
/// WebSocket's bytes both ways, END_STREAM is its orderly close (TCP's FIN) and RST_STREAM its abrupt one
/// (TCP's RST). Made by <see cref="Http2WebSocketClient.OpenAsync" />.
/// </summary>
internal sealed class Http2WebSocket
{
    private readonly Http2Connection connection;

    private readonly IWebSocketRandomSource randomSource;

    private readonly WsFrameDecoder decoder = new(recordsRuns: true);

    // DATA read while a send waited for the server's WINDOW_UPDATE, handed out by ReceiveAsync first.
    private readonly Queue<Http2StreamFrame> received = new();

    /// <summary>Creates the WebSocket on a stream whose extended CONNECT the server accepted.</summary>
    /// <param name="connection">The HTTP/2 connection.</param>
    /// <param name="streamId">The stream the WebSocket runs on.</param>
    /// <param name="randomSource">The source of each sent frame's mask.</param>
    internal Http2WebSocket(Http2Connection connection, int streamId, IWebSocketRandomSource randomSource)
    {
        this.connection = connection;
        StreamId = streamId;
        this.randomSource = randomSource;
    }

    /// <summary>Gets the HTTP/2 stream the WebSocket runs on.</summary>
    internal int StreamId { get; }

    /// <summary>Gets whether the server ended the stream with END_STREAM, closing its side (RFC 8441 section 5).</summary>
    internal bool IsClosedByServer { get; private set; }

    /// <summary>
    /// Sends one masked WebSocket frame (RFC 6455 section 5) in DATA frames on the stream, waiting for the server
    /// to grow the flow-control windows when they are short.
    /// </summary>
    /// <param name="opcode">The frame's opcode.</param>
    /// <param name="payload">The frame's payload.</param>
    /// <param name="cancellationToken">Cancels the send.</param>
    /// <returns>A task that completes when the whole frame is written.</returns>
    /// <exception cref="Http2WebSocketException">The server reset the stream or closed the connection while the send waited.</exception>
    internal async Task SendAsync(WsOpcode opcode, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        ReadOnlyMemory<byte> frame = WsFrameEncoder.Encode(opcode, payload.Span, randomSource);
        while (!frame.IsEmpty)
        {
            var sent = await connection.WriteDataAsync(StreamId, frame, isEndStream: false, cancellationToken).ConfigureAwait(false);
            frame = frame[sent..];
            if (!frame.IsEmpty)
            {
                await TakeInFrameAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Reads the next DATA frame on the stream and decodes the WebSocket frames in it; when it carries END_STREAM,
    /// <see cref="IsClosedByServer" /> becomes <see langword="true" /> (RFC 8441 section 5).
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The runs of WebSocket payload the DATA held, with their frames' metadata.</returns>
    /// <exception cref="Http2WebSocketException">
    /// The server reset the stream, closed the connection, sent HEADERS after the response or broke RFC 6455.
    /// </exception>
    internal async Task<IReadOnlyList<WsFrameRun>> ReceiveAsync(CancellationToken cancellationToken)
    {
        var frame = received.Count > 0
            ? received.Dequeue()
            : await ReadTranslatingResetAsync(connection.ReadStreamFrameAsync(cancellationToken)).ConfigureAwait(false) ?? throw ConnectionClosed();
        if (frame.Type != Http2FrameType.Data)
        {
            throw new Http2WebSocketException("The server sent HEADERS on the WebSocket's stream after the response; only DATA may follow (RFC 8441 section 5).");
        }

        IsClosedByServer = frame.IsEndStream;
        var decoded = decoder.Decode(frame.Content.Span);
        if (decoded.ViolationMessage is { } violation)
        {
            throw new Http2WebSocketException(violation);
        }

        return decoder.TakeRuns();
    }

    /// <summary>Ends the client's side of the stream with an empty DATA frame carrying END_STREAM, TCP's FIN (RFC 8441 section 5).</summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the frame is written.</returns>
    internal async Task CloseAsync(CancellationToken cancellationToken) =>
        _ = await connection.WriteDataAsync(StreamId, ReadOnlyMemory<byte>.Empty, isEndStream: true, cancellationToken).ConfigureAwait(false);

    /// <summary>Awaits an HTTP/2 read, turning the server's RST_STREAM into the WebSocket's abrupt close (RFC 8441 section 5).</summary>
    /// <param name="read">The read.</param>
    /// <returns>What the read returned.</returns>
    /// <exception cref="Http2WebSocketException">The server reset the stream.</exception>
    internal static async Task<Http2StreamFrame?> ReadTranslatingResetAsync(Task<Http2StreamFrame?> read)
    {
        try
        {
            return await read.ConfigureAwait(false);
        }
        catch (Http2StreamResetException reset)
        {
            throw new Http2WebSocketException(
                $"The server reset the WebSocket's HTTP/2 stream with {reset.ErrorCode}, closing it abruptly (RFC 8441 section 5).",
                reset);
        }
    }

    private static Http2WebSocketException ConnectionClosed() =>
        new("The server closed the HTTP/2 connection under the WebSocket (RFC 8441 section 5).");

    private async Task TakeInFrameAsync(CancellationToken cancellationToken)
    {
        var frame = await ReadTranslatingResetAsync(connection.ReadFrameAsync(cancellationToken)).ConfigureAwait(false);
        if (frame is not null)
        {
            received.Enqueue(frame);
        }
        else if (connection.IsClosedByPeer)
        {
            throw ConnectionClosed();
        }
    }
}
