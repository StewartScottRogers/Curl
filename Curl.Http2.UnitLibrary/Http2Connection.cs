using System.Buffers;

namespace Curl.Http2;

/// <summary>
/// The client end of one HTTP/2 connection (RFC 9113) over a byte stream the caller owns:
/// in production the <c>IConnection</c>'s stream, handed in by the HTTP handler. It sends
/// the connection preface with the SETTINGS and WINDOW_UPDATE curl 8.21.0's library sends,
/// allocates client stream identifiers, splits header blocks and bodies into frames,
/// answers SETTINGS and PING, tracks every flow-control window, and turns GOAWAY,
/// RST_STREAM and protocol errors into typed exceptions.
/// </summary>
/// <remarks>
/// One caller at a time: reads and writes are not synchronized with each other.
/// Sending DATA never waits for window: <see cref="WriteDataAsync" /> sends what the
/// windows allow and returns the count, and the caller reads frames (which applies the
/// peer's WINDOW_UPDATE) before sending the rest. Receive windows are topped back up with
/// WINDOW_UPDATE once half of one is used, as nghttp2 does.
/// </remarks>
public sealed class Http2Connection
{
    /// <summary>The SETTINGS_INITIAL_WINDOW_SIZE this client advertises: each stream's receive window.</summary>
    public const int ClientInitialWindowSize = 65536;

    /// <summary>
    /// The connection-level WINDOW_UPDATE sent with the preface. It grows the connection's
    /// receive window from 65535 to 1,048,576,000 bytes (1000 MiB), as measured from curl.
    /// </summary>
    public const int ClientConnectionWindowIncrement = 1048510465;

    /// <summary>
    /// The most CONTINUATION frames one header block may take, against CONTINUATION floods:
    /// nghttp2's default (NGHTTP2_DEFAULT_MAX_CONTINUATIONS), which curl's library uses.
    /// </summary>
    public const int MaximumContinuationFrames = 8;

    private static readonly byte[] ClientPrefaceBytes = "PRI * HTTP/2.0\r\n\r\nSM\r\n\r\n"u8.ToArray();

    private readonly Stream stream;
    private readonly Dictionary<int, Http2Stream> streams = [];
    private long nextStreamId;
    private readonly int streamReceiveWindowSize;
    private long connectionReceiveWindowTarget;
    private PendingHeaderBlock? pendingHeaderBlock;
    private int highestStartedStreamId;
    private Http2ProtocolException? connectionError;

    /// <summary>
    /// Initializes a new instance of the <see cref="Http2Connection" /> class.
    /// </summary>
    /// <param name="stream">The connection's byte stream; the caller keeps ownership.</param>
    public Http2Connection(Stream stream)
        : this(stream, 1, Http2Settings.DefaultInitialWindowSize, ClientInitialWindowSize)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Http2Connection" /> class whose first
    /// stream is <paramref name="firstStreamId" /> and whose receive windows start smaller
    /// than any frame, so tests can reach identifier exhaustion and window overruns.
    /// </summary>
    internal Http2Connection(Stream stream, long firstStreamId, int connectionReceiveWindowSize, int streamReceiveWindowSize)
    {
        ArgumentNullException.ThrowIfNull(stream);
        this.stream = stream;
        nextStreamId = firstStreamId;
        ConnectionReceiveWindow = new Http2FlowControlWindow(connectionReceiveWindowSize);
        connectionReceiveWindowTarget = connectionReceiveWindowSize;
        this.streamReceiveWindowSize = streamReceiveWindowSize;
    }

    /// <summary>Gets the client connection preface (RFC 9113 section 3.4).</summary>
    public static ReadOnlyMemory<byte> ClientPreface => ClientPrefaceBytes;

    /// <summary>
    /// Gets the SETTINGS sent with the preface, in curl's order: MAX_CONCURRENT_STREAMS 100,
    /// INITIAL_WINDOW_SIZE 65536, ENABLE_PUSH 0.
    /// </summary>
    public static IReadOnlyList<Http2Setting> ClientSettings { get; } =
    [
        new(Http2SettingIdentifier.MaxConcurrentStreams, 100),
        new(Http2SettingIdentifier.InitialWindowSize, ClientInitialWindowSize),
        new(Http2SettingIdentifier.EnablePush, 0),
    ];

    /// <summary>Gets the peer's SETTINGS as applied so far.</summary>
    public Http2Settings PeerSettings { get; } = new();

    /// <summary>Gets how many DATA bytes the connection may still send.</summary>
    public Http2FlowControlWindow ConnectionSendWindow { get; } = new(Http2Settings.DefaultInitialWindowSize);

    /// <summary>Gets how many DATA bytes the peer may still send on the connection.</summary>
    public Http2FlowControlWindow ConnectionReceiveWindow { get; }

    /// <summary>Gets whether the peer's first SETTINGS has arrived (and been acknowledged).</summary>
    public bool IsPeerSettingsReceived { get; private set; }

    /// <summary>Gets whether the peer has acknowledged <see cref="ClientSettings" />.</summary>
    public bool IsClientSettingsAcknowledged { get; private set; }

    /// <summary>Gets the GOAWAY the peer sent, or <see langword="null" /> if none.</summary>
    public Http2GoAwayPayload? PeerGoAway { get; private set; }

    /// <summary>Gets how many streams are open or half closed; a closed or reset stream is forgotten.</summary>
    public int OpenStreamCount => streams.Count;

    /// <summary>
    /// Sends the client preface, <see cref="ClientSettings" /> and the connection
    /// WINDOW_UPDATE of <see cref="ClientConnectionWindowIncrement" />, in one write.
    /// </summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the preface is written.</returns>
    public async Task SendPrefaceAsync(CancellationToken cancellationToken)
    {
        byte[] bytes =
        [
            .. ClientPrefaceBytes,
            .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateSettings(ClientSettings)),
            .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateWindowUpdate(0, ClientConnectionWindowIncrement)),
        ];
        await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        _ = ConnectionReceiveWindow.TryAdjust(ClientConnectionWindowIncrement);
        connectionReceiveWindowTarget = ConnectionReceiveWindow.Size;
    }

    /// <summary>
    /// Allocates the next client stream identifier (1, 3, 5, …) and opens that stream.
    /// Nothing is sent until <see cref="WriteHeadersAsync" />.
    /// </summary>
    /// <returns>The stream identifier.</returns>
    /// <exception cref="InvalidOperationException">
    /// The connection failed, the peer sent GOAWAY, the identifiers are exhausted, or the
    /// peer's SETTINGS_MAX_CONCURRENT_STREAMS is reached.
    /// </exception>
    public int OpenStream()
    {
        ThrowIfFailed();
        if (PeerGoAway is not null)
        {
            throw new InvalidOperationException("The peer sent GOAWAY; no new HTTP/2 stream may be opened.");
        }

        if (nextStreamId > int.MaxValue)
        {
            throw new InvalidOperationException("The HTTP/2 stream identifiers are exhausted; a new connection is needed.");
        }

        if (OpenStreamCount >= PeerSettings.MaxConcurrentStreams)
        {
            throw new InvalidOperationException($"The peer allows {PeerSettings.MaxConcurrentStreams} concurrent HTTP/2 streams, and that many are open.");
        }

        var streamId = (int)nextStreamId;
        nextStreamId += 2;
        streams[streamId] = new Http2Stream(PeerSettings.InitialWindowSize, streamReceiveWindowSize);
        return streamId;
    }

    /// <summary>
    /// Returns how many DATA bytes may be sent on a stream now: the smaller of its window and
    /// the connection's.
    /// </summary>
    /// <param name="streamId">An open stream.</param>
    /// <returns>The byte count, 0 when either window is used up.</returns>
    /// <exception cref="InvalidOperationException">The stream is not open.</exception>
    public int GetAvailableSendWindow(int streamId) =>
        Math.Min(ConnectionSendWindow.Available, GetOpenStream(streamId).SendWindow.Available);

    /// <summary>
    /// Sends a header block as one HEADERS frame and as many CONTINUATION frames as the
    /// peer's SETTINGS_MAX_FRAME_SIZE requires, in one write so nothing comes between them.
    /// </summary>
    /// <param name="streamId">A stream this endpoint has not ended.</param>
    /// <param name="headerBlock">The HPACK-encoded header block.</param>
    /// <param name="isEndStream">Whether to end the stream: no body follows.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the frames are written.</returns>
    /// <exception cref="InvalidOperationException">
    /// The stream is not open, this endpoint already ended it, or a stream with a higher
    /// identifier has already started, which RFC 9113 section 5.1.1 forbids.
    /// </exception>
    public async Task WriteHeadersAsync(int streamId, ReadOnlyMemory<byte> headerBlock, bool isEndStream, CancellationToken cancellationToken)
    {
        var sendingStream = GetSendingStream(streamId);
        if (!sendingStream.IsHeadersSent && streamId < highestStartedStreamId)
        {
            throw new InvalidOperationException($"HTTP/2 stream {streamId} cannot start after stream {highestStartedStreamId} has.");
        }

        var maximumFrameSize = PeerSettings.MaxFrameSize;
        var fragment = headerBlock[..Math.Min(maximumFrameSize, headerBlock.Length)];
        var remaining = headerBlock[fragment.Length..];
        var buffer = new ArrayBufferWriter<byte>();
        buffer.Write(Http2FrameCodec.Serialize(Http2FrameFactory.CreateHeaders(streamId, fragment, isEndStream, remaining.IsEmpty)));
        while (!remaining.IsEmpty)
        {
            fragment = remaining[..Math.Min(maximumFrameSize, remaining.Length)];
            remaining = remaining[fragment.Length..];
            buffer.Write(Http2FrameCodec.Serialize(Http2FrameFactory.CreateContinuation(streamId, fragment, remaining.IsEmpty)));
        }

        await stream.WriteAsync(buffer.WrittenMemory, cancellationToken).ConfigureAwait(false);
        sendingStream.IsHeadersSent = true;
        highestStartedStreamId = Math.Max(highestStartedStreamId, streamId);
        EndLocally(streamId, sendingStream, isEndStream);
    }

    /// <summary>
    /// Sends as much of <paramref name="data" /> as the stream's and the connection's
    /// windows allow, in DATA frames no larger than the peer's SETTINGS_MAX_FRAME_SIZE.
    /// END_STREAM is set only when all of it went.
    /// </summary>
    /// <param name="streamId">A stream this endpoint has not ended.</param>
    /// <param name="data">The body bytes; empty with <paramref name="isEndStream" /> sends an empty DATA ending the stream.</param>
    /// <param name="isEndStream">Whether this is the end of the body.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>How many bytes were sent; the caller sends the rest once the peer grows the window.</returns>
    /// <exception cref="InvalidOperationException">The stream is not open, its HEADERS has not been sent, or this endpoint already ended it.</exception>
    public async Task<int> WriteDataAsync(int streamId, ReadOnlyMemory<byte> data, bool isEndStream, CancellationToken cancellationToken)
    {
        var sendingStream = GetSendingStream(streamId);
        if (!sendingStream.IsHeadersSent)
        {
            throw new InvalidOperationException($"HTTP/2 stream {streamId} has no HEADERS yet; DATA cannot come first.");
        }

        var count = Math.Min(data.Length, Math.Min(ConnectionSendWindow.Available, sendingStream.SendWindow.Available));
        var isLastFrameEndStream = isEndStream && count == data.Length;
        if (count == 0 && !isLastFrameEndStream)
        {
            return 0;
        }

        await stream.WriteAsync(SerializeDataFrames(streamId, data[..count], isLastFrameEndStream), cancellationToken).ConfigureAwait(false);
        _ = ConnectionSendWindow.TryConsume(count);
        _ = sendingStream.SendWindow.TryConsume(count);
        EndLocally(streamId, sendingStream, isLastFrameEndStream);
        return count;
    }

    /// <summary>
    /// Grows a stream's receive window beyond <see cref="ClientInitialWindowSize" /> with a
    /// WINDOW_UPDATE; the window is kept topped up to its new size from then on.
    /// </summary>
    /// <param name="streamId">An open stream.</param>
    /// <param name="increment">How many bytes to grow it by.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the WINDOW_UPDATE is written.</returns>
    /// <exception cref="InvalidOperationException">The stream is not open.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="increment" /> is not positive, or would grow the window past 2^31 - 1.</exception>
    public async Task IncreaseStreamReceiveWindowAsync(int streamId, int increment, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(increment);
        var openStream = GetOpenStream(streamId);
        if (openStream.ReceiveWindow.Size + increment > Http2FlowControlWindow.MaximumSize)
        {
            throw new ArgumentOutOfRangeException(nameof(increment), increment, "The increment would grow the window past 2^31 - 1.");
        }

        await WriteFrameAsync(Http2FrameFactory.CreateWindowUpdate(streamId, increment), cancellationToken).ConfigureAwait(false);
        _ = openStream.ReceiveWindow.TryAdjust(increment);
        openStream.ReceiveWindowTarget = openStream.ReceiveWindow.Size;
    }

    /// <summary>
    /// Ends a stream with RST_STREAM. Frames the peer still sends on it are ignored.
    /// </summary>
    /// <param name="streamId">The stream.</param>
    /// <param name="errorCode">Why, usually <see cref="Http2ErrorCode.Cancel" />.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the frame is written.</returns>
    /// <exception cref="InvalidOperationException">The stream was never opened: RST_STREAM on an idle stream is a protocol error.</exception>
    public async Task ResetStreamAsync(int streamId, Http2ErrorCode errorCode, CancellationToken cancellationToken)
    {
        if (IsIdle(streamId))
        {
            throw new InvalidOperationException($"HTTP/2 stream {streamId} was never opened.");
        }

        _ = streams.Remove(streamId);
        await WriteFrameAsync(Http2FrameFactory.CreateRstStream(streamId, errorCode), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends a PING, which the peer echoes with ACK.
    /// </summary>
    /// <param name="opaqueData">The eight opaque bytes, as a big-endian number.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the frame is written.</returns>
    public async Task SendPingAsync(ulong opaqueData, CancellationToken cancellationToken) =>
        await WriteFrameAsync(Http2FrameFactory.CreatePing(opaqueData, isAcknowledgement: false), cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Sends GOAWAY. A client accepts no peer-initiated stream, so the last stream is 0.
    /// </summary>
    /// <param name="errorCode">Why the connection is closing.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the frame is written.</returns>
    public async Task SendGoAwayAsync(Http2ErrorCode errorCode, CancellationToken cancellationToken) =>
        await WriteFrameAsync(Http2FrameFactory.CreateGoAway(0, errorCode, ReadOnlyMemory<byte>.Empty), cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Reads frames until one carries something for a stream: DATA, or a complete header
    /// block. Every other frame is handled on the way: SETTINGS applied and acknowledged,
    /// PING answered, WINDOW_UPDATE applied, PRIORITY and unknown types ignored. A header
    /// block is returned even for a stream this endpoint reset, because the HPACK decoder
    /// must see every block.
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The stream frame, or <see langword="null" /> when the peer closed the connection between frames.</returns>
    /// <exception cref="Http2ProtocolException">The peer broke RFC 9113; GOAWAY has been sent with the error code.</exception>
    /// <exception cref="Http2StreamResetException">The peer reset a stream this endpoint has open.</exception>
    /// <exception cref="Http2GoAwayException">The peer sent GOAWAY with an error, or one leaving an open stream unprocessed.</exception>
    /// <exception cref="EndOfStreamException">The peer closed the connection part way through a frame or header block.</exception>
    /// <exception cref="InvalidOperationException">The connection already failed with a protocol error.</exception>
    public async Task<Http2StreamFrame?> ReadStreamFrameAsync(CancellationToken cancellationToken)
    {
        ThrowIfFailed();
        try
        {
            return await ReadUntilStreamFrameAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Http2ProtocolException exception)
        {
            connectionError = exception;
        }

        await SendGoAwayAfterErrorAsync(connectionError.ErrorCode, cancellationToken).ConfigureAwait(false);
        throw connectionError;
    }

    private async Task SendGoAwayAfterErrorAsync(Http2ErrorCode errorCode, CancellationToken cancellationToken)
    {
        try
        {
            await SendGoAwayAsync(errorCode, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            // The transport is already gone; the protocol error is still what the caller hears.
        }
    }

    private ReadOnlyMemory<byte> SerializeDataFrames(int streamId, ReadOnlyMemory<byte> data, bool isEndStream)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var offset = 0;
        do
        {
            var size = Math.Min(data.Length - offset, PeerSettings.MaxFrameSize);
            var chunk = data.Slice(offset, size);
            offset += size;
            buffer.Write(Http2FrameCodec.Serialize(Http2FrameFactory.CreateData(streamId, chunk, isEndStream && offset == data.Length)));
        }
        while (offset < data.Length);

        return buffer.WrittenMemory;
    }

    private void ThrowIfFailed()
    {
        if (connectionError is not null)
        {
            throw new InvalidOperationException("The HTTP/2 connection failed with a protocol error; a new connection is needed.", connectionError);
        }
    }

    private void EndLocally(int streamId, Http2Stream sendingStream, bool isEndStream)
    {
        sendingStream.IsLocalEnded = isEndStream;
        ForgetIfClosed(streamId, sendingStream);
    }

    private void ForgetIfClosed(int streamId, Http2Stream closingStream)
    {
        if (closingStream.IsClosed)
        {
            _ = streams.Remove(streamId);
        }
    }

    private static Http2ProtocolException ProtocolError(string detail) => new(Http2ErrorCode.ProtocolError, detail);

    private static Http2ProtocolException FlowControlError(string detail) => new(Http2ErrorCode.FlowControlError, detail);

    private Http2StreamFrame? EndOfConnection() => pendingHeaderBlock is null
        ? null
        : throw new EndOfStreamException($"The connection closed part way through stream {pendingHeaderBlock.StreamId}'s header block.");

    private async Task<Http2StreamFrame?> ReadUntilStreamFrameAsync(CancellationToken cancellationToken)
    {
        Http2StreamFrame? streamFrame = null;
        while (streamFrame is null)
        {
            var frame = await Http2FrameCodec.ReadAsync(stream, Http2FrameCodec.DefaultMaximumFrameSize, cancellationToken).ConfigureAwait(false);
            if (frame is null)
            {
                return EndOfConnection();
            }

            streamFrame = await ReceiveFrameAsync(frame, cancellationToken).ConfigureAwait(false);
        }

        return streamFrame;
    }

    private Task<Http2StreamFrame?> ReceiveFrameAsync(Http2Frame frame, CancellationToken cancellationToken)
    {
        if (pendingHeaderBlock is not null)
        {
            return Task.FromResult(ContinueHeaderBlock(pendingHeaderBlock, frame));
        }

        if (frame.Type == Http2FrameType.Data)
        {
            return ReceiveDataAsync(frame, cancellationToken);
        }

        if (frame.Type == Http2FrameType.Headers)
        {
            return Task.FromResult(ReceiveHeaders(frame));
        }

        if (frame.Type == Http2FrameType.Continuation)
        {
            throw ProtocolError($"CONTINUATION on stream {frame.StreamId} with no header block open");
        }

        if (frame.Type == Http2FrameType.PushPromise)
        {
            throw ProtocolError($"PUSH_PROMISE on stream {frame.StreamId} though SETTINGS_ENABLE_PUSH is 0");
        }

        return ReceiveConnectionFrameAsync(frame, cancellationToken);
    }

    private async Task<Http2StreamFrame?> ReceiveConnectionFrameAsync(Http2Frame frame, CancellationToken cancellationToken)
    {
        switch (frame.Type)
        {
            case Http2FrameType.Priority:
                _ = Http2FramePayloadParser.ParsePriority(frame);
                break;
            case Http2FrameType.RstStream:
                ReceiveRstStream(frame);
                break;
            case Http2FrameType.Settings:
                await ReceiveSettingsAsync(frame, cancellationToken).ConfigureAwait(false);
                break;
            case Http2FrameType.Ping:
                await ReceivePingAsync(frame, cancellationToken).ConfigureAwait(false);
                break;
            case Http2FrameType.GoAway:
                ReceiveGoAway(frame);
                break;
            case Http2FrameType.WindowUpdate:
                ReceiveWindowUpdate(frame);
                break;
        }

        return null;
    }

    private async Task<Http2StreamFrame?> ReceiveDataAsync(Http2Frame frame, CancellationToken cancellationToken)
    {
        var data = Http2FramePayloadParser.ParseData(frame);
        var receivingStream = FindReceivingStream(frame.StreamId);
        if (!ConnectionReceiveWindow.TryConsume(frame.Payload.Length))
        {
            throw FlowControlError($"DATA of {frame.Payload.Length} bytes overran the connection's receive window of {ConnectionReceiveWindow.Size}");
        }

        await TopUpReceiveWindowAsync(0, ConnectionReceiveWindow, connectionReceiveWindowTarget, cancellationToken).ConfigureAwait(false);
        if (receivingStream is null)
        {
            return null;
        }

        if (!receivingStream.ReceiveWindow.TryConsume(frame.Payload.Length))
        {
            throw FlowControlError($"DATA of {frame.Payload.Length} bytes overran stream {frame.StreamId}'s receive window of {receivingStream.ReceiveWindow.Size}");
        }

        var isEndStream = frame.HasFlag(Http2FrameFlags.EndStream);
        if (!isEndStream)
        {
            await TopUpReceiveWindowAsync(frame.StreamId, receivingStream.ReceiveWindow, receivingStream.ReceiveWindowTarget, cancellationToken).ConfigureAwait(false);
        }

        EndRemotely(frame.StreamId, receivingStream, isEndStream);
        return new Http2StreamFrame(Http2FrameType.Data, frame.StreamId, data, isEndStream);
    }

    private async Task TopUpReceiveWindowAsync(int streamId, Http2FlowControlWindow window, long targetSize, CancellationToken cancellationToken)
    {
        var increment = targetSize - window.Size;
        if (increment < targetSize / 2)
        {
            return;
        }

        _ = window.TryAdjust(increment);
        await WriteFrameAsync(Http2FrameFactory.CreateWindowUpdate(streamId, (int)increment), cancellationToken).ConfigureAwait(false);
    }

    private Http2StreamFrame? ReceiveHeaders(Http2Frame frame)
    {
        var payload = Http2FramePayloadParser.ParseHeaders(frame);
        var receivingStream = FindReceivingStream(frame.StreamId);
        var isEndStream = frame.HasFlag(Http2FrameFlags.EndStream);
        if (frame.HasFlag(Http2FrameFlags.EndHeaders))
        {
            return CompleteHeaderBlock(frame.StreamId, receivingStream, payload.Fragment, isEndStream);
        }

        pendingHeaderBlock = new PendingHeaderBlock(frame.StreamId, receivingStream, isEndStream);
        pendingHeaderBlock.Fragments.Write(payload.Fragment.Span);
        return null;
    }

    private Http2StreamFrame? ContinueHeaderBlock(PendingHeaderBlock pending, Http2Frame frame)
    {
        if (frame.Type != Http2FrameType.Continuation || frame.StreamId != pending.StreamId)
        {
            throw ProtocolError($"{frame.Type} on stream {frame.StreamId} interrupted stream {pending.StreamId}'s header block");
        }

        if (++pending.ContinuationFrameCount > MaximumContinuationFrames)
        {
            throw new Http2ProtocolException(Http2ErrorCode.EnhanceYourCalm, $"stream {pending.StreamId}'s header block takes more than {MaximumContinuationFrames} CONTINUATION frames");
        }

        pending.Fragments.Write(frame.Payload.Span);
        if (!frame.HasFlag(Http2FrameFlags.EndHeaders))
        {
            return null;
        }

        pendingHeaderBlock = null;
        return CompleteHeaderBlock(pending.StreamId, pending.Stream, pending.Fragments.WrittenMemory, pending.IsEndStream);
    }

    private Http2StreamFrame CompleteHeaderBlock(int streamId, Http2Stream? receivingStream, ReadOnlyMemory<byte> headerBlock, bool isEndStream)
    {
        if (receivingStream is not null)
        {
            EndRemotely(streamId, receivingStream, isEndStream);
        }

        return new Http2StreamFrame(Http2FrameType.Headers, streamId, headerBlock, isEndStream);
    }

    private void EndRemotely(int streamId, Http2Stream receivingStream, bool isEndStream)
    {
        receivingStream.IsRemoteEnded = isEndStream;
        ForgetIfClosed(streamId, receivingStream);
    }

    /// <summary>
    /// A RST_STREAM with NO_ERROR after the whole response arrived is the server declining
    /// the rest of the request body (RFC 9113 section 8.1): the response stands.
    /// </summary>
    private void ReceiveRstStream(Http2Frame frame)
    {
        var errorCode = Http2FramePayloadParser.ParseRstStream(frame);
        if (streams.Remove(frame.StreamId, out var resetStream))
        {
            if (errorCode == Http2ErrorCode.NoError && resetStream.IsRemoteEnded)
            {
                return;
            }

            throw new Http2StreamResetException(frame.StreamId, errorCode);
        }

        if (IsIdle(frame.StreamId))
        {
            throw ProtocolError($"RST_STREAM on stream {frame.StreamId}, which was never opened");
        }
    }

    private async Task ReceiveSettingsAsync(Http2Frame frame, CancellationToken cancellationToken)
    {
        var settings = Http2FramePayloadParser.ParseSettings(frame);
        if (frame.HasFlag(Http2FrameFlags.Acknowledgement))
        {
            IsClientSettingsAcknowledged = true;
            return;
        }

        foreach (var setting in settings)
        {
            ApplyPeerSetting(setting);
        }

        IsPeerSettingsReceived = true;
        await WriteFrameAsync(Http2FrameFactory.CreateSettingsAcknowledgement(), cancellationToken).ConfigureAwait(false);
    }

    private void ApplyPeerSetting(Http2Setting setting)
    {
        if (setting is { Identifier: Http2SettingIdentifier.EnablePush, Value: 1 })
        {
            throw ProtocolError("a server sent SETTINGS_ENABLE_PUSH of 1");
        }

        var previousInitialWindowSize = PeerSettings.InitialWindowSize;
        PeerSettings.Apply(setting);
        var delta = (long)PeerSettings.InitialWindowSize - previousInitialWindowSize;
        foreach (var (streamId, openStream) in streams)
        {
            if (!openStream.SendWindow.TryAdjust(delta))
            {
                throw FlowControlError($"SETTINGS_INITIAL_WINDOW_SIZE grew stream {streamId}'s window past 2^31 - 1");
            }
        }
    }

    private async Task ReceivePingAsync(Http2Frame frame, CancellationToken cancellationToken)
    {
        var opaqueData = Http2FramePayloadParser.ParsePing(frame);
        if (!frame.HasFlag(Http2FrameFlags.Acknowledgement))
        {
            await WriteFrameAsync(Http2FrameFactory.CreatePing(opaqueData, isAcknowledgement: true), cancellationToken).ConfigureAwait(false);
        }
    }

    private void ReceiveGoAway(Http2Frame frame)
    {
        var goAway = Http2FramePayloadParser.ParseGoAway(frame);
        PeerGoAway = goAway;
        if (goAway.ErrorCode != Http2ErrorCode.NoError || streams.Keys.Any(streamId => streamId > goAway.LastStreamId))
        {
            throw new Http2GoAwayException(goAway);
        }
    }

    private void ReceiveWindowUpdate(Http2Frame frame)
    {
        var increment = Http2FramePayloadParser.ParseWindowUpdate(frame);
        var window = frame.StreamId == 0 ? ConnectionSendWindow : FindSendWindow(frame.StreamId);
        if (window is not null && !window.TryAdjust(increment))
        {
            throw FlowControlError($"WINDOW_UPDATE grew stream {frame.StreamId}'s window past 2^31 - 1");
        }
    }

    private Http2FlowControlWindow? FindSendWindow(int streamId)
    {
        if (streams.TryGetValue(streamId, out var openStream))
        {
            return openStream.SendWindow;
        }

        return IsIdle(streamId) ? throw ProtocolError($"WINDOW_UPDATE on stream {streamId}, which was never opened") : null;
    }

    private Http2Stream? FindReceivingStream(int streamId)
    {
        if (streams.TryGetValue(streamId, out var openStream))
        {
            return openStream.IsRemoteEnded
                ? throw new Http2ProtocolException(Http2ErrorCode.StreamClosed, $"a frame on stream {streamId} after its END_STREAM")
                : openStream;
        }

        return IsIdle(streamId) ? throw ProtocolError($"a frame on stream {streamId}, which was never opened") : null;
    }

    private bool IsIdle(int streamId) => streamId % 2 == 0 || streamId >= nextStreamId;

    private Http2Stream GetOpenStream(int streamId)
    {
        ThrowIfFailed();
        return streams.TryGetValue(streamId, out var openStream)
            ? openStream
            : throw new InvalidOperationException($"HTTP/2 stream {streamId} is not open.");
    }

    private Http2Stream GetSendingStream(int streamId)
    {
        var openStream = GetOpenStream(streamId);
        return openStream.IsLocalEnded
            ? throw new InvalidOperationException($"HTTP/2 stream {streamId} has already been ended by this endpoint.")
            : openStream;
    }

    private async Task WriteFrameAsync(Http2Frame frame, CancellationToken cancellationToken) =>
        await Http2FrameCodec.WriteAsync(stream, frame, cancellationToken).ConfigureAwait(false);

    /// <summary>A header block whose HEADERS frame has arrived and whose CONTINUATION frames are still coming.</summary>
    private sealed record PendingHeaderBlock(int StreamId, Http2Stream? Stream, bool IsEndStream)
    {
        public ArrayBufferWriter<byte> Fragments { get; } = new();

        public int ContinuationFrameCount { get; set; }
    }
}
