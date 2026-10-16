using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.WebSocket;
using Curl.Api.Errors;

namespace Curl.Api;

/// <summary>
/// A WebSocket connection opened by <see cref="CurlClient.ConnectWebSocketAsync(string, CancellationToken)"/>:
/// libcurl's <c>curl_ws_recv</c>, <c>curl_ws_send</c> and <c>curl_ws_meta</c> on the easy handle
/// whose <c>CURLOPT_CONNECT_ONLY</c> 2 perform upgraded the connection. Whole messages come from
/// <see cref="ReceiveAsync"/> or <see cref="ReadMessagesAsync"/>, fragments joined; frames from
/// <see cref="ReceiveFrameAsync"/>. A call the engine answers <see cref="CurlCode.Again"/> waits
/// and asks again until it is answered or cancelled; any other failure is a
/// <see cref="CurlException"/>. One receive and one send may run at a time; disposing closes the
/// connection and the handle.
/// </summary>
public sealed class CurlWebSocket : IAsyncDisposable
{
    /// <summary>How long a receive or send the engine answered <see cref="CurlCode.Again"/> waits before asking again.</summary>
    internal static readonly TimeSpan RetryInterval = TimeSpan.FromMilliseconds(1);

    /// <summary>How many bytes one <c>curl_ws_recv</c> asks for while a whole message is received.</summary>
    internal const int ReceiveBufferSize = 16 * 1024;

    /// <summary>The longest close reason, in UTF-8 bytes, that fits a control frame's 125 bytes beside the status code.</summary>
    public const int MaxCloseReasonBytes = 123;

    private readonly IEasyTransfer _easy;
    private readonly Func<CancellationToken, Task> _waitBeforeRetry;
    private readonly byte[] _receiveBuffer = new byte[ReceiveBufferSize];
    private readonly ArrayBufferWriter<byte> _messageSoFar = new();
    private CurlWebSocketMessageType? _messageTypeSoFar;
    private int _disposed;

    /// <summary>A WebSocket over <paramref name="easy"/>'s upgraded connection.</summary>
    /// <param name="easy">The handle whose perform upgraded the connection; owned and disposed by the WebSocket.</param>
    /// <param name="waitBeforeRetry">Waits before a call answered <see cref="CurlCode.Again"/> is made again.</param>
    internal CurlWebSocket(IEasyTransfer easy, Func<CancellationToken, Task> waitBeforeRetry)
    {
        _easy = easy;
        _waitBeforeRetry = waitBeforeRetry;
    }

    /// <summary><c>curl_ws_meta</c>: the frame of the data last received, or <see langword="null"/> before any.</summary>
    public CurlWebSocketFrame? CurrentFrame => _easy.WebSocket.CurrentFrame;

    /// <summary>Waits <see cref="RetryInterval"/> on the system clock.</summary>
    /// <param name="cancellationToken">Stops the wait.</param>
    /// <returns>A task that completes after the interval.</returns>
    internal static Task WaitRetryInterval(CancellationToken cancellationToken) =>
        Task.Delay(RetryInterval, TimeProvider.System, cancellationToken);

    /// <summary>Sends <paramref name="text"/> as one UTF-8 text message.</summary>
    /// <param name="text">The message.</param>
    /// <param name="cancellationToken">Stops the send.</param>
    /// <returns>A task that completes once the whole message is sent.</returns>
    /// <exception cref="CurlException">The engine failed the send.</exception>
    public ValueTask SendTextAsync(string text, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        return SendAsync(Encoding.UTF8.GetBytes(text), CurlWebSocketMessageType.Text, cancellationToken);
    }

    /// <summary>Sends <paramref name="data"/> as one message or control frame of <paramref name="type"/>.</summary>
    /// <param name="data">The payload.</param>
    /// <param name="type">The kind of message to send.</param>
    /// <param name="cancellationToken">Stops the send.</param>
    /// <returns>A task that completes once the whole payload is sent.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> is not a <see cref="CurlWebSocketMessageType"/>.</exception>
    /// <exception cref="CurlException">The engine failed the send.</exception>
    public ValueTask SendAsync(ReadOnlyMemory<byte> data, CurlWebSocketMessageType type, CancellationToken cancellationToken = default) =>
        SendFrameAsync(data, FlagsOf(type), cancellationToken);

    /// <summary>Sends a ping carrying <paramref name="payload"/>.</summary>
    /// <param name="payload">At most 125 bytes the peer's pong echoes.</param>
    /// <param name="cancellationToken">Stops the send.</param>
    /// <returns>A task that completes once the ping is sent.</returns>
    public ValueTask PingAsync(ReadOnlyMemory<byte> payload = default, CancellationToken cancellationToken = default) =>
        SendFrameAsync(payload, CurlWebSocketFlags.Ping, cancellationToken);

    /// <summary>Sends a pong carrying <paramref name="payload"/>, for a ping received with <see cref="CurlWebSocketOptions.AutoPong"/> off.</summary>
    /// <param name="payload">The ping's payload.</param>
    /// <param name="cancellationToken">Stops the send.</param>
    /// <returns>A task that completes once the pong is sent.</returns>
    public ValueTask PongAsync(ReadOnlyMemory<byte> payload = default, CancellationToken cancellationToken = default) =>
        SendFrameAsync(payload, CurlWebSocketFlags.Pong, cancellationToken);

    /// <summary>Sends a close frame with <paramref name="status"/> and <paramref name="reason"/>.</summary>
    /// <param name="status">The close status code; 1000 is a normal closure.</param>
    /// <param name="reason">Why, in at most <see cref="MaxCloseReasonBytes"/> UTF-8 bytes.</param>
    /// <param name="cancellationToken">Stops the send.</param>
    /// <returns>A task that completes once the close frame is sent.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="reason"/> is longer than <see cref="MaxCloseReasonBytes"/> bytes.</exception>
    public ValueTask CloseAsync(ushort status = 1000, string reason = "", CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reason);
        byte[] reasonBytes = Encoding.UTF8.GetBytes(reason);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(reasonBytes.Length, MaxCloseReasonBytes, nameof(reason));
        byte[] payload = new byte[2 + reasonBytes.Length];
        BinaryPrimitives.WriteUInt16BigEndian(payload, status);
        reasonBytes.CopyTo(payload, 2);
        return SendFrameAsync(payload, CurlWebSocketFlags.Close, cancellationToken);
    }

    /// <summary>Under <see cref="CurlWebSocketOptions.RawMode"/>, writes <paramref name="data"/> to the connection unframed.</summary>
    /// <param name="data">The bytes, framing included.</param>
    /// <param name="cancellationToken">Stops the send.</param>
    /// <returns>A task that completes once every byte is sent.</returns>
    /// <exception cref="CurlException">The engine failed the send; <see cref="CurlCode.BadFunctionArgument"/> outside raw mode.</exception>
    public ValueTask SendRawAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default) =>
        SendFrameAsync(data, CurlWebSocketFlags.None, cancellationToken);

    /// <summary>
    /// <c>curl_ws_send</c> until every byte of <paramref name="data"/> is sent: one frame with
    /// <paramref name="flags"/>, its remaining bytes sent again after a partial send.
    /// </summary>
    /// <param name="data">The frame's payload.</param>
    /// <param name="flags">The frame's <c>CURLWS_*</c> bits.</param>
    /// <param name="cancellationToken">Stops the send.</param>
    /// <returns>A task that completes once the whole payload is sent.</returns>
    /// <exception cref="CurlException">The engine failed the send.</exception>
    public async ValueTask SendFrameAsync(ReadOnlyMemory<byte> data, CurlWebSocketFlags flags, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ReadOnlyMemory<byte> left = data;
        do
        {
            CurlWebSocketSendResult sent = await SendOnceAsync(left, flags, cancellationToken).ConfigureAwait(false);
            left = left[sent.BytesSent..];
        }
        while (!left.IsEmpty);
    }

    /// <summary>
    /// <c>curl_ws_recv</c> once data is ready: the next piece of a frame, as much as
    /// <paramref name="buffer"/> holds. Mixing it with <see cref="ReceiveAsync"/> splits messages
    /// between the two.
    /// </summary>
    /// <param name="buffer">Where the received bytes are written.</param>
    /// <param name="cancellationToken">Stops the receive.</param>
    /// <returns>The byte count and the frame they belong to.</returns>
    /// <exception cref="CurlException">The engine failed the receive, as when the peer closed the connection.</exception>
    public async ValueTask<CurlWebSocketFramePiece> ReceiveFrameAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CurlWebSocketReceiveResult received = await _easy.WebSocket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (received.Result != CurlCode.Again)
            {
                CurlException.ThrowIfFailed(received.Result, string.Empty);
                return new CurlWebSocketFramePiece(received.BytesReceived, received.Frame);
            }

            await _waitBeforeRetry(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// The next whole message: a text or binary message with its fragments joined, or a control
    /// frame, which may arrive between a message's fragments without losing them.
    /// </summary>
    /// <param name="cancellationToken">Stops the receive; the fragments received so far are kept for the next one.</param>
    /// <returns>The message.</returns>
    /// <exception cref="CurlException">The engine failed the receive, as when the peer closed the connection.</exception>
    public async ValueTask<CurlWebSocketMessage> ReceiveAsync(CancellationToken cancellationToken = default)
    {
        ArrayBufferWriter<byte> control = new();
        CurlWebSocketMessage? message;
        do
        {
            CurlWebSocketFramePiece piece = await ReceiveFrameAsync(_receiveBuffer, cancellationToken).ConfigureAwait(false);
            message = TakePiece(piece, control);
        }
        while (message is null);

        return message;
    }

    /// <summary>
    /// Every message received, in order, through the peer's close frame, which is the last one
    /// given.
    /// </summary>
    /// <param name="cancellationToken">Stops the enumeration.</param>
    /// <returns>The messages.</returns>
    /// <exception cref="CurlException">The engine failed a receive, as when the peer closed the connection without a close frame.</exception>
    public IAsyncEnumerable<CurlWebSocketMessage> ReadMessagesAsync(CancellationToken cancellationToken = default) =>
        new CurlWebSocketMessageReader(this, cancellationToken);

    /// <summary>Closes the connection and disposes the easy handle. Safe to call more than once.</summary>
    /// <returns>A task that completes once the handle is disposed.</returns>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            await _easy.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>The <c>CURLWS_*</c> bit of a message type.</summary>
    /// <param name="type">The message type.</param>
    /// <returns>Its bit.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> is not a <see cref="CurlWebSocketMessageType"/>.</exception>
    internal static CurlWebSocketFlags FlagsOf(CurlWebSocketMessageType type) => type switch
    {
        CurlWebSocketMessageType.Text => CurlWebSocketFlags.Text,
        CurlWebSocketMessageType.Binary => CurlWebSocketFlags.Binary,
        CurlWebSocketMessageType.Close => CurlWebSocketFlags.Close,
        CurlWebSocketMessageType.Ping => CurlWebSocketFlags.Ping,
        CurlWebSocketMessageType.Pong => CurlWebSocketFlags.Pong,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Not a WebSocket message type."),
    };

    /// <summary>The control frame type of <paramref name="flags"/>, or <see langword="null"/> for a data frame.</summary>
    /// <param name="flags">A received frame's bits.</param>
    /// <returns>Close, ping, pong or <see langword="null"/>.</returns>
    internal static CurlWebSocketMessageType? ControlTypeOf(CurlWebSocketFlags flags) =>
        (flags & CurlWebSocketFlags.Close) != 0 ? CurlWebSocketMessageType.Close
        : (flags & CurlWebSocketFlags.Ping) != 0 ? CurlWebSocketMessageType.Ping
        : (flags & CurlWebSocketFlags.Pong) != 0 ? CurlWebSocketMessageType.Pong
        : null;

    /// <summary>
    /// Adds one received piece to the control frame or message it belongs to, and gives the whole
    /// message once its last byte has come.
    /// </summary>
    /// <param name="piece">The piece in the receive buffer.</param>
    /// <param name="control">The control frame received so far during this receive.</param>
    /// <returns>The completed message, or <see langword="null"/> while more is to come.</returns>
    private CurlWebSocketMessage? TakePiece(CurlWebSocketFramePiece piece, ArrayBufferWriter<byte> control)
    {
        ReadOnlySpan<byte> bytes = _receiveBuffer.AsSpan(0, piece.BytesReceived);
        CurlWebSocketFrame frame = piece.Frame;
        if (ControlTypeOf(frame.Flags) is { } controlType)
        {
            control.Write(bytes);
            return frame.BytesLeft == 0 ? new CurlWebSocketMessage(controlType, control.WrittenSpan.ToArray()) : null;
        }

        return TakeDataPiece(frame, bytes);
    }

    /// <summary>
    /// Adds one received piece of a text or binary frame to the message received so far, and gives
    /// the whole message once its last fragment's last byte has come.
    /// </summary>
    /// <param name="frame">The frame the piece belongs to.</param>
    /// <param name="bytes">The piece's bytes.</param>
    /// <returns>The completed message, or <see langword="null"/> while more is to come.</returns>
    private CurlWebSocketMessage? TakeDataPiece(CurlWebSocketFrame frame, ReadOnlySpan<byte> bytes)
    {
        _messageSoFar.Write(bytes);
        _messageTypeSoFar ??= (frame.Flags & CurlWebSocketFlags.Text) != 0 ? CurlWebSocketMessageType.Text : CurlWebSocketMessageType.Binary;
        if (frame.BytesLeft != 0 || (frame.Flags & CurlWebSocketFlags.Continuation) != 0)
        {
            return null;
        }

        CurlWebSocketMessage message = new(_messageTypeSoFar.Value, _messageSoFar.WrittenSpan.ToArray());
        _messageSoFar.Clear();
        _messageTypeSoFar = null;
        return message;
    }

    /// <summary>One <c>curl_ws_send</c> once the engine takes data.</summary>
    /// <param name="data">The bytes still to send.</param>
    /// <param name="flags">The frame's bits.</param>
    /// <param name="cancellationToken">Stops the send.</param>
    /// <returns>The send's result, never <see cref="CurlCode.Again"/>.</returns>
    /// <exception cref="CurlException">The engine failed the send.</exception>
    private async ValueTask<CurlWebSocketSendResult> SendOnceAsync(ReadOnlyMemory<byte> data, CurlWebSocketFlags flags, CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CurlWebSocketSendResult sent = await _easy.WebSocket.SendAsync(data, 0, flags, cancellationToken).ConfigureAwait(false);
            if (sent.Result != CurlCode.Again)
            {
                CurlException.ThrowIfFailed(sent.Result, string.Empty);
                return sent;
            }

            await _waitBeforeRetry(cancellationToken).ConfigureAwait(false);
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
}
