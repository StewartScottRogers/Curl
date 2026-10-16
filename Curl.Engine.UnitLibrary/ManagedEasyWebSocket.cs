using Curl.Api.Codes;
using Curl.Api.EngineContracts.WebSocket;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ws;

namespace Curl.Engine;

/// <summary>
/// libcurl's WebSocket API over the connection a <see cref="ManagedEasyTransfer" /> perform with
/// <c>CURLOPT_CONNECT_ONLY</c> 2 upgraded and left open (ADR-0616). A receive decodes the server's frames with
/// <see cref="WsFrameMetadataDecoder" /> and hands their payload over a run at a time, as much as the buffer holds,
/// with the frame's <c>CURLWS_*</c> bits, offset and bytes left; it never waits: it starts one read and answers
/// <see cref="CurlCode.Again" /> while that read runs or while only part of a frame has come. Unless
/// <paramref name="answersPings" /> says no (<c>CURLWS_NOAUTOPONG</c>), each ping received is answered with a pong
/// carrying its payload; the ping is still handed over. A send writes one masked frame from the bits given
/// (<see cref="WebSocketFrameFlags.TryFrameOf" />). With options no longer connect-only both answer
/// <see cref="CurlCode.UnsupportedProtocol" />, and with no upgraded connection <see cref="CurlCode.BadFunctionArgument" />.
/// </summary>
/// <param name="isConnectOnly">Whether the handle's options set <c>CURLOPT_CONNECT_ONLY</c> now.</param>
/// <param name="answersPings">Whether a received ping is answered with a pong now.</param>
/// <param name="randomSource">Supplies each sent frame's mask.</param>
internal sealed class ManagedEasyWebSocket(Func<bool> isConnectOnly, Func<bool> answersPings, IWebSocketRandomSource randomSource) : IEasyWebSocket
{
    /// <summary>How many bytes one read asks the connection for.</summary>
    internal const int ReadSize = 16 * 1024;

    private readonly Queue<WsFrameRun> runs = new();

    private IConnection? connection;

    private WsFrameMetadataDecoder decoder = new();

    private int deliveredOfRun;

    private CurlWebSocketFlags messageType;

    private bool continuesSentMessage;

    private CurlCode? streamFailure;

    private Task<int>? pendingRead;

    private byte[] readBuffer = [];

    /// <summary>
    /// <c>curl_ws_meta</c>: the frame of the data the last receive handed over, as libcurl's <c>ws->recvframe</c>, or
    /// <see langword="null" /> before any since the connection was upgraded.
    /// </summary>
    public CurlWebSocketFrame? CurrentFrame { get; private set; }

    /// <summary>
    /// Closes the connection open now, if any, keeps <paramref name="upgraded" /> in its place and decodes
    /// <paramref name="alreadyReceived" />, the bytes that came with the <c>101</c>, answering a ping among them.
    /// </summary>
    /// <param name="upgraded">The connection the upgrade left open.</param>
    /// <param name="alreadyReceived">The bytes received after the reply head.</param>
    /// <returns>A task that completes once the earlier connection is closed and the bytes are decoded.</returns>
    internal async ValueTask OpenAsync(IConnection upgraded, ReadOnlyMemory<byte> alreadyReceived)
    {
        await CloseAsync().ConfigureAwait(false);
        connection = upgraded;
        await DecodeAsync(upgraded, alreadyReceived).ConfigureAwait(false);
    }

    /// <summary>Closes the connection open now, if any, forgetting every frame not yet handed over.</summary>
    /// <returns>A task that completes once the connection is closed.</returns>
    internal async ValueTask CloseAsync()
    {
        IConnection? open = connection;
        connection = null;
        decoder = new WsFrameMetadataDecoder();
        runs.Clear();
        deliveredOfRun = 0;
        messageType = CurlWebSocketFlags.None;
        continuesSentMessage = false;
        streamFailure = null;
        pendingRead = null;
        CurrentFrame = null;
        if (open is not null)
        {
            await open.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async ValueTask<CurlWebSocketReceiveResult> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CurlCode usable = UsableConnection(out IConnection? live);
        if (usable == CurlCode.Ok && runs.Count == 0)
        {
            usable = await ReadMoreAsync(live!).ConfigureAwait(false);
        }

        return usable == CurlCode.Ok ? Deliver(buffer.Span) : new CurlWebSocketReceiveResult(usable, 0, default);
    }

    /// <inheritdoc />
    /// <remarks><c>CURLWS_OFFSET</c> is refused with <see cref="CurlCode.BadFunctionArgument" /> until BL-2481.</remarks>
    public async ValueTask<CurlWebSocketSendResult> SendAsync(ReadOnlyMemory<byte> buffer, long fragmentSize, CurlWebSocketFlags flags, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CurlCode usable = UsableConnection(out IConnection? live);
        if (usable != CurlCode.Ok)
        {
            return new CurlWebSocketSendResult(usable, 0);
        }

        if ((flags & CurlWebSocketFlags.Offset) != 0 || !WebSocketFrameFlags.TryFrameOf(flags, continuesSentMessage, out WsOpcode opcode, out bool isFinal))
        {
            return new CurlWebSocketSendResult(CurlCode.BadFunctionArgument, 0);
        }

        if (!await TrySendFrameAsync(live!, opcode, isFinal, buffer).ConfigureAwait(false))
        {
            return new CurlWebSocketSendResult(CurlCode.SendError, 0);
        }

        continuesSentMessage = opcode is WsOpcode.Close or WsOpcode.Ping or WsOpcode.Pong ? continuesSentMessage : !isFinal;
        return new CurlWebSocketSendResult(CurlCode.Ok, buffer.Length);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <c>curl_ws_start_frame</c> belongs inside a read callback of a WebSocket upload, which the engine does not run
    /// yet (BL-2481), so it answers <see cref="CurlCode.BadFunctionArgument" />.
    /// </remarks>
    public ValueTask<CurlCode> StartFrameAsync(CurlWebSocketFlags flags, long frameLength, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(CurlCode.BadFunctionArgument);
    }

    /// <summary>The connection a receive or send may use, or why there is none.</summary>
    /// <param name="live">The upgraded connection when the answer is <see cref="CurlCode.Ok" />.</param>
    /// <returns><see cref="CurlCode.Ok" />, <see cref="CurlCode.UnsupportedProtocol" /> or <see cref="CurlCode.BadFunctionArgument" />.</returns>
    private CurlCode UsableConnection(out IConnection? live)
    {
        live = connection;
        if (!isConnectOnly())
        {
            return CurlCode.UnsupportedProtocol;
        }

        return live is null ? CurlCode.BadFunctionArgument : CurlCode.Ok;
    }

    /// <summary>
    /// Takes the read that has completed and decodes its bytes, or starts one: <see cref="CurlCode.Ok" /> once a run of
    /// payload is waiting, <see cref="CurlCode.Again" /> while none is, <see cref="CurlCode.GotNothing" /> when the
    /// server closed the connection, <see cref="CurlCode.RecvError" /> when the read failed or the frames broke the
    /// protocol, and the same failure on every receive after it.
    /// </summary>
    /// <param name="live">The upgraded connection.</param>
    /// <returns>The receive's code.</returns>
    private async ValueTask<CurlCode> ReadMoreAsync(IConnection live)
    {
        if (streamFailure is { } failed)
        {
            return failed;
        }

        pendingRead ??= StartRead(live);
        if (!pendingRead.IsCompleted)
        {
            return CurlCode.Again;
        }

        Task<int> read = pendingRead;
        pendingRead = null;
        return await TakeReadAsync(live, read).ConfigureAwait(false);
    }

    /// <summary>Decodes the bytes of a completed read, answering as <see cref="ReadMoreAsync" /> does.</summary>
    /// <param name="live">The upgraded connection.</param>
    /// <param name="read">The completed read.</param>
    /// <returns>The receive's code.</returns>
    private async ValueTask<CurlCode> TakeReadAsync(IConnection live, Task<int> read)
    {
        int count;
        try
        {
            count = await read.ConfigureAwait(false);
        }
        catch (IOException)
        {
            return CurlCode.RecvError;
        }

        if (count == 0)
        {
            return CurlCode.GotNothing;
        }

        await DecodeAsync(live, readBuffer.AsMemory(0, count)).ConfigureAwait(false);
        return runs.Count > 0 ? CurlCode.Ok : streamFailure ?? CurlCode.Again;
    }

    private Task<int> StartRead(IConnection live)
    {
        readBuffer = new byte[ReadSize];
        return live.ReadAsync(readBuffer, CancellationToken.None).AsTask();
    }

    /// <summary>
    /// Decodes <paramref name="received" />, queueing its runs of payload, keeping a protocol violation as the stream's
    /// failure and answering the last ping completed in it with a pong unless pings go unanswered; a pong that cannot
    /// be written fails the stream with <see cref="CurlCode.SendError" />.
    /// </summary>
    /// <param name="live">The upgraded connection.</param>
    /// <param name="received">The bytes received.</param>
    /// <returns>A task that completes once the bytes are decoded and any pong written.</returns>
    private async ValueTask DecodeAsync(IConnection live, ReadOnlyMemory<byte> received)
    {
        WsDecodedFrames decoded = decoder.Decode(received.Span);
        foreach (WsFrameRun run in decoded.Runs)
        {
            runs.Enqueue(run);
        }

        if (decoded.ViolationMessage is not null)
        {
            streamFailure = CurlCode.RecvError;
        }

        if (decoded.CompletedPing is { } ping && answersPings() && !await TrySendFrameAsync(live, WsOpcode.Pong, true, ping).ConfigureAwait(false))
        {
            streamFailure ??= CurlCode.SendError;
        }
    }

    /// <summary>Writes and flushes one masked frame.</summary>
    /// <param name="live">The upgraded connection.</param>
    /// <param name="opcode">The frame's opcode.</param>
    /// <param name="isFinal">Whether the frame's FIN bit is set.</param>
    /// <param name="payload">The frame's payload.</param>
    /// <returns><see langword="false" /> when the write failed with an <see cref="IOException" />.</returns>
    private async ValueTask<bool> TrySendFrameAsync(IConnection live, WsOpcode opcode, bool isFinal, ReadOnlyMemory<byte> payload)
    {
        byte[] frame = WsFrameEncoder.Encode(opcode, isFinal, payload.Span, randomSource);
        try
        {
            await live.WriteAsync(frame, CancellationToken.None).ConfigureAwait(false);
            await live.FlushAsync(CancellationToken.None).ConfigureAwait(false);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }

    /// <summary>
    /// Hands over as much of the first waiting run as <paramref name="buffer" /> holds, with its frame's bits, the
    /// offset of the bytes in the frame and the frame's bytes still to come, and keeps that frame for
    /// <see cref="CurrentFrame" />.
    /// </summary>
    /// <param name="buffer">Where the bytes go.</param>
    /// <returns>The receive's result.</returns>
    private CurlWebSocketReceiveResult Deliver(Span<byte> buffer)
    {
        WsFrameRun run = runs.Peek();
        ReadOnlySpan<byte> left = run.Payload.Span[deliveredOfRun..];
        int count = Math.Min(left.Length, buffer.Length);
        left[..count].CopyTo(buffer);
        long offset = run.Offset + deliveredOfRun;
        deliveredOfRun += count;
        int remaining = run.Payload.Length - deliveredOfRun;
        CurlWebSocketFlags flags = WebSocketFrameFlags.Of(run, messageType);
        if ((flags & WebSocketFrameFlags.MessageTypes) != 0)
        {
            messageType = flags & WebSocketFrameFlags.MessageTypes;
        }

        if (remaining == 0)
        {
            runs.Dequeue();
            deliveredOfRun = 0;
        }

        CurlWebSocketFrame frame = new(0, flags, offset, run.BytesLeft + remaining, count);
        CurrentFrame = frame;
        return new CurlWebSocketReceiveResult(CurlCode.Ok, count, frame);
    }
}
