using System.Runtime.ExceptionServices;
using Curl.Http2;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// One connection speaking HTTP/2 for the HTTP handler: the frame layer over the
/// <see cref="IConnection" /> and the two HPACK contexts every stream on it shares. The client
/// preface goes out with the first request, as curl 8.21.0 sends it before the first HEADERS
/// without waiting for the server's SETTINGS (measured, BL-658 Notes).
/// </summary>
/// <remarks>
/// <para>
/// The session outlives its transfer when the connection is pooled (BL-817, ADR-0159 point 5):
/// the handler hands it to the connection (<see cref="IConnection.TryHoldSession" />) and the
/// next transfer to the origin continues it - the next odd stream, the same HPACK tables, no
/// second preface. It keeps reading and writing through the connection it was created on,
/// whose later leases reach the same socket. Whoever closes the connection first calls
/// <see cref="ShutDownAsync" />, which sends curl's closing GOAWAY.
/// </para>
/// <para>
/// Several transfers may run on it at once, each on a stream of its own, as curl multiplexes
/// <c>-Z</c> transfers (BL-717): every use of the frame layer holds one lock, a stream waits
/// for the connection's next bytes without it (<see cref="ReadAheadConnectionStream" />), and
/// whichever stream then reads a frame hands it to the stream it belongs to. A failure of the
/// connection itself is kept and given to every stream that reads after it.
/// </para>
/// </remarks>
internal sealed class Http2Session : IHttpStreamSession, IConnectionSession
{
    /// <summary>
    /// The receive window curl grows each stream to right after its HEADERS: 10 MiB
    /// (nghttp2's <c>H2_STREAM_WINDOW_SIZE_MAX</c> in curl; measured, BL-817 Notes).
    /// </summary>
    internal const int StreamReceiveWindowSize = 10 * 1024 * 1024;

    /// <summary>
    /// How many times curl 8.18.0 sends the stream's WINDOW_UPDATE after HEADERS, each with
    /// the same increment: twice (measured, BL-817 Notes).
    /// </summary>
    private const int StreamWindowUpdateCount = 2;

    /// <summary>The debug data of curl's closing GOAWAY: <c>shutdown</c> and its terminating NUL (measured, BL-817 Notes).</summary>
    private static readonly byte[] ShutdownDebugData = "shutdown\0"u8.ToArray();

    /// <summary>
    /// The SETTINGS curl 8.18.0 sends once after an h2c upgrade, right before the first stream it
    /// opens on the connection, <c>000006 04 00 00000000 0004 00010000</c>: INITIAL_WINDOW_SIZE
    /// 65536 again (measured, BL-970 Notes).
    /// </summary>
    private static readonly byte[] UpgradedStreamSettingsFrame = Http2FrameCodec.Serialize(
        Http2FrameFactory.CreateSettings([new(Http2SettingIdentifier.InitialWindowSize, Http2Connection.ClientInitialWindowSize)]));

    private readonly HpackEncoder encoder = new();

    private readonly HpackDecoder decoder = new();

    private readonly SemaphoreSlim frameLock = new(1, 1);

    private readonly Dictionary<int, Http2StreamConnection> openStreams = [];

    private readonly ReadAheadConnectionStream incoming;

    private uint peerHeaderTableSize = HpackEncoder.DefaultMaximumTableSize;

    private bool isPrefaceSent;

    private bool isUpgradeSettingsDue;

    private ExceptionDispatchInfo? connectionFailure;

    /// <summary>
    /// Where the connection's own frames are logged: the log of the transfer that last opened a
    /// stream on the session, which a pooled session hands on to each transfer it carries (ADR-0345).
    /// </summary>
    private HttpFrameLog connectionLog = HttpFrameLog.Silent;

    /// <summary>Initializes a new instance of the <see cref="Http2Session" /> class.</summary>
    /// <param name="connection">The connection; the handler keeps ownership.</param>
    public Http2Session(IConnection connection)
    {
        Connection = connection;
        incoming = new ReadAheadConnectionStream(connection);
        Frames = new Http2Connection(incoming);
    }

    /// <summary>
    /// Gets the <c>HTTP2-Settings</c> value an h2c upgrade request carries: the payload of the
    /// SETTINGS frame the preface sends (<see cref="Http2Connection.ClientSettings" />) in
    /// base64url with no padding, <c>AAMAAABkAAQAAQAAAAIAAAAA</c> as curl sends it (measured,
    /// BL-716 Notes).
    /// </summary>
    internal static string UpgradeSettings { get; } = Convert.ToBase64String(Http2FrameFactory.CreateSettings(Http2Connection.ClientSettings).Payload.Span)
        .TrimEnd('=')
        .Replace('+', '-')
        .Replace('/', '_');

    /// <summary>Gets the connection the session runs on.</summary>
    internal IConnection Connection { get; }

    /// <summary>Gets the frame layer; it is used only holding the session's frame lock.</summary>
    internal Http2Connection Frames { get; }

    /// <inheritdoc />
    public string VersionName => "HTTP/2";

    /// <inheritdoc />
    public string UsingLine => HttpConnectionInfoLines.UsingHttp2;

    /// <summary>
    /// Gets a value indicating whether the connection can carry another request: the peer has
    /// sent no GOAWAY and has not closed it.
    /// </summary>
    public bool AcceptsNewStreams => Frames.PeerGoAway is null && !Frames.IsClosedByPeer;

    /// <summary>
    /// Gets how many transfers the connection carries at once: the peer's
    /// SETTINGS_MAX_CONCURRENT_STREAMS, unlimited until it sends one, or 0 once it takes no new
    /// stream (<see cref="AcceptsNewStreams" />), so a pool shares the connection between
    /// <c>-Z</c> transfers (BL-717).
    /// </summary>
    public int? ConcurrentTransferLimit => AcceptsNewStreams
        ? (int)Math.Min(Frames.PeerSettings.MaxConcurrentStreams ?? int.MaxValue, int.MaxValue)
        : 0;

    /// <summary>
    /// Creates the stream a request is sent and its response read on; nothing is sent until
    /// its head is written.
    /// </summary>
    /// <param name="scheme">The URL's scheme, sent as <c>:scheme</c>.</param>
    /// <param name="bodyLength">
    /// The request body's length, 0 when there is none, or <see langword="null" /> when it is
    /// unknown.
    /// </param>
    /// <param name="ignoresBody">Not used: HTTP/2 fails a reset stream whatever the request wanted.</param>
    /// <param name="openedLines">Reports curl's <c>-v</c> lines for the stream once its HEADERS are sent, or <see langword="null" /> for none.</param>
    /// <param name="diagnosticLog">The transfer's diagnostic log, which the stream's frames are written to, or <see langword="null" /> for none.</param>
    /// <returns>The stream.</returns>
    public IHttpStreamConnection CreateStream(string scheme, long? bodyLength, bool ignoresBody, HttpStreamOpenedLines? openedLines = null, IDiagnosticLog? diagnosticLog = null) =>
        new Http2StreamConnection(this, scheme, bodyLength, openedLines, HttpFrameLog.For(diagnosticLog, VersionName));

    /// <summary>
    /// Sends GOAWAY with NO_ERROR, last stream 0 and debug data <c>shutdown</c> and a NUL, as
    /// curl 8.18.0 does when it closes an HTTP/2 connection (measured, BL-817 Notes); nothing
    /// when no preface went out, when a GOAWAY already has, or when the peer closed the
    /// connection. A connection that fails the write is closing anyway, so that is not an error.
    /// </summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the frame is written, or at once when none is due.</returns>
    public async ValueTask ShutDownAsync(CancellationToken cancellationToken)
    {
        await frameLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!isPrefaceSent || Frames.IsGoAwaySent || Frames.IsClosedByPeer)
            {
                return;
            }

            await Frames.SendGoAwayAsync(Http2ErrorCode.NoError, ShutdownDebugData, cancellationToken).ConfigureAwait(false);
            await Connection.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            // The transport is already gone; there is no one left to say goodbye to.
        }
        finally
        {
            frameLock.Release();
        }
    }

    /// <summary>
    /// Sends the client preface if it has not gone yet, then opens a stream for
    /// <paramref name="receiver" /> and sends <paramref name="fields" /> on it as one header
    /// block. While as many streams are open as the peer's SETTINGS_MAX_CONCURRENT_STREAMS
    /// allows, it reads frames for the other streams until one closes, as nghttp2 holds a new
    /// stream back until it may open.
    /// </summary>
    /// <param name="receiver">The stream the response's frames are handed to.</param>
    /// <param name="fields">The request's header list.</param>
    /// <param name="isEndStream">Whether the request has no body.</param>
    /// <param name="cancellationToken">Cancels the writes.</param>
    /// <returns>The stream's identifier.</returns>
    /// <exception cref="InvalidOperationException">The frame layer can open no stream.</exception>
    internal async ValueTask<int> StartStreamAsync(Http2StreamConnection receiver, IReadOnlyList<HeaderField> fields, bool isEndStream, CancellationToken cancellationToken)
    {
        while (true)
        {
            await frameLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (MayOpenStream())
                {
                    return await OpenStreamAsync(receiver, fields, isEndStream, cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                frameLock.Release();
            }

            await ReceiveAsync(null, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Sends the client preface and opens stream 1 for <paramref name="receiver" /> without
    /// sending anything on it: the stream an HTTP/1.1 request upgraded to h2c becomes, half
    /// closed by the client, whose response arrives on it (RFC 7540 section 3.2), as curl sends
    /// the preface once the <c>101</c> has arrived (measured, BL-716 Notes). Once the response
    /// ends the stream is closed, so later requests on the session open streams 3, 5, ... within
    /// the peer's concurrency limit (BL-866); the first of them is preceded by curl's
    /// post-upgrade SETTINGS (<see cref="UpgradedStreamSettingsFrame" />, BL-970).
    /// </summary>
    /// <param name="receiver">The stream the response's frames are handed to.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The stream's identifier, 1.</returns>
    internal async ValueTask<int> StartUpgradedStreamAsync(Http2StreamConnection receiver, CancellationToken cancellationToken)
    {
        await frameLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await Frames.SendPrefaceAsync(cancellationToken).ConfigureAwait(false);
            isPrefaceSent = true;
            isUpgradeSettingsDue = true;
            return Register(Frames.OpenUpgradedStream(), receiver);
        }
        finally
        {
            frameLock.Release();
        }
    }

    /// <summary>
    /// Sends curl's WINDOW_UPDATEs for a new stream. The increment is what takes the stream's
    /// window to <see cref="StreamReceiveWindowSize" /> from the initial size in force: the
    /// default 65535 until the peer acknowledges this client's SETTINGS, 65536 after, as
    /// nghttp2 counts it - 10420225 on the first stream of a prior-knowledge connection,
    /// 10420224 on the next (measured, BL-817 Notes).
    /// </summary>
    /// <param name="streamId">The stream whose window grows.</param>
    /// <param name="cancellationToken">Cancels the writes.</param>
    /// <returns>A task that completes when both frames are written.</returns>
    internal async ValueTask GrowStreamReceiveWindowAsync(int streamId, CancellationToken cancellationToken)
    {
        await frameLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            int initialWindowSize = Frames.IsClientSettingsAcknowledged ? Http2Connection.ClientInitialWindowSize : Http2Settings.DefaultInitialWindowSize;
            for (int sent = 0; sent < StreamWindowUpdateCount; sent++)
            {
                await Frames.IncreaseStreamReceiveWindowAsync(streamId, StreamReceiveWindowSize - initialWindowSize, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            frameLock.Release();
        }
    }

    /// <summary>Sends DATA on a stream within the flow-control windows (<see cref="Http2Connection.WriteDataAsync" />).</summary>
    /// <param name="streamId">The stream.</param>
    /// <param name="data">The bytes to send.</param>
    /// <param name="isEndStream">Whether the last byte sent ends the stream.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>How many bytes went out, 0 when the windows are used up.</returns>
    internal async ValueTask<int> WriteDataAsync(int streamId, ReadOnlyMemory<byte> data, bool isEndStream, CancellationToken cancellationToken)
    {
        await frameLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Frames.WriteDataAsync(streamId, data, isEndStream, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            frameLock.Release();
        }
    }

    /// <summary>Resets a stream with <paramref name="errorCode" />.</summary>
    /// <param name="streamId">The stream.</param>
    /// <param name="errorCode">The RST_STREAM error code.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the frame is written.</returns>
    internal async ValueTask ResetStreamAsync(int streamId, Http2ErrorCode errorCode, CancellationToken cancellationToken)
    {
        await frameLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _ = openStreams.Remove(streamId);
            await Frames.ResetStreamAsync(streamId, errorCode, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            frameLock.Release();
        }
    }

    /// <summary>
    /// Waits for the connection's next bytes, then, unless another stream has read them or
    /// already handed <paramref name="receiver" /> a frame, reads one frame and hands it to the
    /// stream it belongs to: the header blocks of every stream are decoded here, in the order
    /// they arrive, as HPACK requires, and frames for a stream no longer open are dropped. A
    /// reset of another stream goes to that stream while it is open, and is dropped after.
    /// </summary>
    /// <param name="receiver">The stream reading, or <see langword="null" /> for none.</param>
    /// <param name="cancellationToken">Cancels the wait and the read.</param>
    /// <returns>A task that completes when a frame was read, or when there was none to read.</returns>
    /// <exception cref="Http2StreamResetException">The peer reset <paramref name="receiver" />'s stream.</exception>
    /// <exception cref="HttpTransferException">A header block did not decode (exit 16).</exception>
    internal async ValueTask ReceiveAsync(Http2StreamConnection? receiver, CancellationToken cancellationToken)
    {
        connectionFailure?.Throw();
        await incoming.WaitForBytesAsync(cancellationToken).ConfigureAwait(false);
        await frameLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            connectionFailure?.Throw();
            if (incoming.HasBytesOrEnded && receiver?.HasReceived != true)
            {
                await ReadAndRouteFrameAsync(receiver, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            frameLock.Release();
        }
    }

    private bool MayOpenStream() =>
        Frames.OpenStreamCount < (Frames.PeerSettings.MaxConcurrentStreams ?? uint.MaxValue)
            || !AcceptsNewStreams;

    private async ValueTask<int> OpenStreamAsync(Http2StreamConnection receiver, IReadOnlyList<HeaderField> fields, bool isEndStream, CancellationToken cancellationToken)
    {
        if (!isPrefaceSent)
        {
            await Frames.SendPrefaceAsync(cancellationToken).ConfigureAwait(false);
            isPrefaceSent = true;
        }

        if (isUpgradeSettingsDue)
        {
            await Connection.WriteAsync(UpgradedStreamSettingsFrame, cancellationToken).ConfigureAwait(false);
            isUpgradeSettingsDue = false;
        }

        int streamId = Register(Frames.OpenStream(), receiver);
        byte[] headerBlock = Encode(fields);
        await Frames.WriteHeadersAsync(streamId, headerBlock, isEndStream, cancellationToken).ConfigureAwait(false);
        receiver.FrameLog.FrameSent("HEADERS", streamId, headerBlock.Length);
        return streamId;
    }

    /// <summary>
    /// Registers <paramref name="receiver" /> as the owner of <paramref name="streamId" />, and
    /// makes its transfer's log the one the connection's own frames - the server's SETTINGS and
    /// GOAWAY - are written to from now on (ADR-0345).
    /// </summary>
    private int Register(int streamId, Http2StreamConnection receiver)
    {
        openStreams[streamId] = receiver;
        connectionLog = receiver.FrameLog;
        return streamId;
    }

    private async ValueTask ReadAndRouteFrameAsync(Http2StreamConnection? receiver, CancellationToken cancellationToken)
    {
        bool wasSettingsReceived = Frames.IsPeerSettingsReceived;
        bool wasGoAwayReceived = Frames.PeerGoAway is not null;
        Http2StreamFrame? frame;
        try
        {
            frame = await Frames.ReadFrameAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Http2StreamResetException reset) when (reset.StreamId != receiver?.StreamId)
        {
            if (openStreams.Remove(reset.StreamId, out Http2StreamConnection? owner))
            {
                owner.FrameLog.ResetReceived(reset);
                owner.TakeReset(reset);
            }

            return;
        }
        catch (Http2StreamResetException reset)
        {
            receiver!.FrameLog.ResetReceived(reset);
            throw;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            connectionFailure = ExceptionDispatchInfo.Capture(exception);
            throw;
        }
        finally
        {
            LogPeerConnectionFrames(wasSettingsReceived, wasGoAwayReceived);
        }

        if (frame is not null)
        {
            Route(frame);
        }
    }

    /// <summary>
    /// Logs the server's first SETTINGS and its GOAWAY when the frame just read brought them;
    /// the frame layer applies both itself, so their arrival shows as a change of its state.
    /// </summary>
    private void LogPeerConnectionFrames(bool wasSettingsReceived, bool wasGoAwayReceived)
    {
        if (!wasSettingsReceived && Frames.IsPeerSettingsReceived)
        {
            connectionLog.SettingsReceived(Frames.PeerSettings);
        }

        if (!wasGoAwayReceived && Frames.PeerGoAway is { } goAway)
        {
            connectionLog.GoAwayReceived(goAway);
        }
    }

    private void Route(Http2StreamFrame frame)
    {
        IReadOnlyList<HeaderField>? fields = frame.Type == Http2FrameType.Headers ? Decode(frame.Content) : null;
        if (!openStreams.TryGetValue(frame.StreamId, out Http2StreamConnection? owner))
        {
            return;
        }

        owner.FrameLog.FrameReceived(frame.Type.ToString().ToUpperInvariant(), frame.StreamId, frame.Content.Length);
        owner.Take(frame, fields);
        if (frame.IsEndStream)
        {
            _ = openStreams.Remove(frame.StreamId);
        }
    }

    /// <summary>
    /// Decodes a header block; one that does not decode fails the connection with exit 16, as
    /// curl reports nghttp2's COMPRESSION_ERROR.
    /// </summary>
    private IReadOnlyList<HeaderField> Decode(ReadOnlyMemory<byte> block)
    {
        try
        {
            return decoder.Decode(block.Span);
        }
        catch (HpackDecodingException)
        {
            HttpTransferException failure = new(CurlExitCode.Http2, HttpTransferMessages.Http2ShutsDownConnection(Http2ErrorCode.CompressionError));
            connectionFailure = ExceptionDispatchInfo.Capture(failure);
            throw failure;
        }
    }

    /// <summary>
    /// Encodes a header list, first taking a SETTINGS_HEADER_TABLE_SIZE the peer changed since
    /// the last block, so the block opens with the size update it calls for.
    /// </summary>
    private byte[] Encode(IReadOnlyList<HeaderField> fields)
    {
        uint headerTableSize = Frames.PeerSettings.HeaderTableSize;
        if (headerTableSize != peerHeaderTableSize)
        {
            encoder.SetPeerMaximumTableSize((int)Math.Min(headerTableSize, int.MaxValue));
            peerHeaderTableSize = headerTableSize;
        }

        return encoder.Encode(fields);
    }
}
