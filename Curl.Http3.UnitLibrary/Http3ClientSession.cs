using Curl.Protocol.Abstractions;

namespace Curl.Http3;

/// <summary>
/// The client side of one HTTP/3 connection (RFC 9114) over an <see cref="IMultiplexedConnection" />:
/// it opens the local control and QPACK streams before the first request stream, reads the
/// peer's unidirectional streams in the background (SETTINGS and GOAWAY from its control
/// stream, its QPACK encoder and decoder instructions), and opens request streams. The QPACK
/// encoder and decoder use a dynamic table of capacity zero. It writes no text: a peer stream
/// failure is kept as <see cref="ConnectionError" />, every frame read from the peer's
/// control stream is handed to the caller's callback, and so, when the caller asks for them,
/// is every stream the peer opens that HTTP/3 itself does not use.
/// </summary>
public sealed class Http3ClientSession : IAsyncDisposable
{
    private const int QpackStreamBufferSize = 4096;
    private readonly IReadOnlyList<Http3Setting> localSettings;
    private readonly Action<Http3Frame>? controlFrameReceived;
    private readonly Func<Http3UnknownPeerStream, bool>? unknownPeerStreamReceived;
    private readonly List<IMultiplexedStream> openedStreams = [];
    private readonly List<IMultiplexedStream> acceptedStreams = [];
    private readonly Http3PeerUnidirectionalStreams peerStreamTypes = new();
    private readonly CancellationTokenSource stopReadingPeerStreams = new();
    private readonly Lock gate = new();
    private readonly SemaphoreSlim openingStreams = new(1, 1);
    private volatile Http3ControlStreamReader? controlStream;
    private Exception? connectionError;
    private long connectionErrorCode;
    private volatile bool isStoppedByRefusedStream;
    private long requestStreamsOpened;
    private int requestStreamsInUse;

    /// <summary>
    /// Starts a session on <paramref name="connection" /> and starts reading the peer's
    /// unidirectional streams.
    /// </summary>
    /// <param name="connection">The QUIC connection, whose application protocol is <c>h3</c>.</param>
    /// <param name="localSettings">The SETTINGS the local control stream sends.</param>
    /// <param name="controlFrameReceived">
    /// Called with every frame read from the peer's control stream (SETTINGS, GOAWAY and the
    /// rest), on the reading task; <see langword="null" /> when the caller needs none.
    /// </param>
    /// <param name="unknownPeerStreamReceived">
    /// Called, on the reading task, with every unidirectional stream of an unknown or grease type
    /// the peer opens and, when <paramref name="connection" /> is an
    /// <see cref="IPeerBidirectionalStreamAccepting" />, every bidirectional stream it opens; it
    /// returns whether it took the stream, which the session otherwise stops with
    /// <c>H3_STREAM_CREATION_ERROR</c>. The session disposes every such stream with itself.
    /// <see langword="null" />, the default, discards unknown unidirectional streams as RFC 9114
    /// section 6.2 allows and accepts no bidirectional streams.
    /// </param>
    public Http3ClientSession(IMultiplexedConnection connection, IReadOnlyList<Http3Setting> localSettings, Action<Http3Frame>? controlFrameReceived = null, Func<Http3UnknownPeerStream, bool>? unknownPeerStreamReceived = null)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(localSettings);
        Connection = connection;
        this.localSettings = localSettings;
        this.controlFrameReceived = controlFrameReceived;
        this.unknownPeerStreamReceived = unknownPeerStreamReceived;
        PeerStreamsReading = ReadPeerStreamsAsync(stopReadingPeerStreams.Token);
    }

    /// <summary>Gets the QUIC connection the session runs on.</summary>
    public IMultiplexedConnection Connection { get; }

    /// <summary>Gets the QPACK encoder for request field sections (dynamic table capacity zero).</summary>
    public QpackEncoder Encoder { get; } = new(maximumTableCapacity: 0, maximumBlockedStreams: 0);

    /// <summary>Gets the QPACK decoder for response field sections (dynamic table capacity zero).</summary>
    public QpackDecoder Decoder { get; } = new(maximumTableCapacity: 0, maximumBlockedStreams: 0);

    /// <summary>Gets the task reading the streams the peer opens; it ends when the session is disposed or fails.</summary>
    public Task PeerStreamsReading { get; }

    /// <summary>Gets the peer's SETTINGS, or <see langword="null" /> before they arrive.</summary>
    public Http3SettingsFrame? PeerSettings => controlStream?.PeerSettings;

    /// <summary>
    /// Gets whether the peer's SETTINGS carried <c>SETTINGS_ENABLE_CONNECT_PROTOCOL</c> (0x08)
    /// with the value 1, allowing extended CONNECT with a <c>:protocol</c> pseudo-header (RFC 9220).
    /// </summary>
    public bool IsExtendedConnectEnabled => PeerSettings?.GetValueOrDefault(Http3SettingIdentifier.EnableConnectProtocol, 0) == 1;

    /// <summary>Gets the stream ID of the last GOAWAY the peer sent, or <see langword="null" /> before one.</summary>
    public long? GoawayStreamId => controlStream?.GoawayStreamId;

    /// <summary>
    /// Gets the failure that ended reading the peer's streams: an <see cref="Http3Exception" />
    /// (<see cref="Http3ErrorCode.ClosedCriticalStream" /> when a critical stream was reset) or a
    /// <see cref="QpackException" />; <see langword="null" /> while there is none. The session
    /// closes the connection with its error code.
    /// </summary>
    public Exception? ConnectionError
    {
        get
        {
            lock (gate)
            {
                return connectionError;
            }
        }
    }

    /// <summary>Gets whether a new request stream may be opened: no refused stream, no GOAWAY and no connection error.</summary>
    public bool AcceptsNewStreams => !isStoppedByRefusedStream && GoawayStreamId is null && ConnectionError is null;

    /// <summary>
    /// Opens a request stream, opening the local control, QPACK encoder and QPACK decoder streams
    /// first when it is the first. Opens one stream at a time. A connection that refuses another
    /// bidirectional stream throws its <see cref="IOException" />.
    /// </summary>
    /// <param name="beforeFirstStream">Called once, before the local streams of the first request open; may be <see langword="null" />.</param>
    /// <param name="cancellationToken">Cancels the opening.</param>
    /// <returns>The request stream.</returns>
    public async ValueTask<Http3ClientRequestStream> OpenRequestStreamAsync(Action? beforeFirstStream, CancellationToken cancellationToken)
    {
        await openingStreams.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (openedStreams.Count == 0)
            {
                beforeFirstStream?.Invoke();
                await Http3LocalUnidirectionalStreams.OpenControlStreamAsync(await OpenLocalStreamAsync(cancellationToken).ConfigureAwait(false), localSettings, cancellationToken).ConfigureAwait(false);
                await Http3LocalUnidirectionalStreams.OpenQpackEncoderStreamAsync(await OpenLocalStreamAsync(cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
                await Http3LocalUnidirectionalStreams.OpenQpackDecoderStreamAsync(await OpenLocalStreamAsync(cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
            }

            IMultiplexedStream stream = await Connection.OpenBidirectionalStreamAsync(cancellationToken).ConfigureAwait(false);
            openedStreams.Add(stream);
            lock (gate)
            {
                requestStreamsOpened++;
                requestStreamsInUse++;
            }

            return new Http3ClientRequestStream(this, stream);
        }
        finally
        {
            openingStreams.Release();
        }
    }

    /// <summary>Records that one request stream is finished with.</summary>
    /// <returns>
    /// The bidirectional streams the connection still allows (<see langword="null" /> when it
    /// sets no limit) and the request streams still in use.
    /// </returns>
    public (long? StreamsLeft, int StreamsInUse) EndRequestStream()
    {
        lock (gate)
        {
            requestStreamsInUse--;
            return (Connection.BidirectionalStreamLimit - requestStreamsOpened, requestStreamsInUse);
        }
    }

    /// <summary>Stops new request streams, as after the peer refused one with <c>H3_REQUEST_REJECTED</c>.</summary>
    public void StopNewStreams() => isStoppedByRefusedStream = true;

    /// <summary>
    /// Stops reading the peer's streams, disposes every stream, closes the connection with
    /// <c>H3_NO_ERROR</c> unless a connection error closed it already, and disposes it.
    /// </summary>
    /// <returns>A task that completes when the connection is disposed.</returns>
    public async ValueTask DisposeAsync()
    {
        await stopReadingPeerStreams.CancelAsync().ConfigureAwait(false);
        await PeerStreamsReading.ConfigureAwait(false);
        stopReadingPeerStreams.Dispose();
        openingStreams.Dispose();
        foreach (IMultiplexedStream stream in openedStreams.Concat(acceptedStreams))
        {
            await stream.DisposeAsync().ConfigureAwait(false);
        }

        if (ConnectionError is null)
        {
            await CloseAsync((long)Http3ErrorCode.NoError).ConfigureAwait(false);
        }

        await Connection.DisposeAsync().ConfigureAwait(false);
    }

    private static async Task ReadUntilFailureAsync(Func<Task> read)
    {
        while (true)
        {
            await read().ConfigureAwait(false);
        }
    }

    private async ValueTask<Stream> OpenLocalStreamAsync(CancellationToken cancellationToken)
    {
        IMultiplexedStream stream = await Connection.OpenUnidirectionalStreamAsync(cancellationToken).ConfigureAwait(false);
        openedStreams.Add(stream);
        return new Http3MultiplexedStreamAdapter(stream);
    }

    private async Task ReadPeerStreamsAsync(CancellationToken cancellationToken)
    {
        Task bidirectional = unknownPeerStreamReceived is not null && Connection is IPeerBidirectionalStreamAccepting accepting
            ? AcceptPeerStreamsAsync(accepting.AcceptBidirectionalStreamAsync, OfferPeerBidirectionalStreamAsync, cancellationToken)
            : Task.CompletedTask;
        await AcceptPeerStreamsAsync(Connection.AcceptUnidirectionalStreamAsync, ReadPeerStreamAsync, cancellationToken).ConfigureAwait(false);
        await bidirectional.ConfigureAwait(false);
        if (ConnectionError is not null)
        {
            await CloseAsync(connectionErrorCode).ConfigureAwait(false);
        }
    }

    private async Task AcceptPeerStreamsAsync(Func<CancellationToken, ValueTask<IMultiplexedStream>> accept, Func<IMultiplexedStream, CancellationToken, Task> read, CancellationToken cancellationToken)
    {
        List<Task> readers = [];
        try
        {
            while (true)
            {
                IMultiplexedStream stream = await accept(cancellationToken).ConfigureAwait(false);
                lock (gate)
                {
                    acceptedStreams.Add(stream);
                }

                readers.Add(read(stream, cancellationToken));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The session is disposed, or a connection error stopped it.
        }
        catch (MultiplexedConnectionFailedException)
        {
            // The connection is lost; the request stream reports it.
        }
        catch (NotSupportedException)
        {
            // The connection hands out no peer streams, as a scripted one in a test may not.
        }

        await Task.WhenAll(readers).ConfigureAwait(false);
    }

    private async Task OfferPeerBidirectionalStreamAsync(IMultiplexedStream stream, CancellationToken cancellationToken)
    {
        try
        {
            long? signal = await Http3StreamReading.ReadVariableLengthIntegerAsync(new Http3MultiplexedStreamAdapter(stream), cancellationToken).ConfigureAwait(false);
            OfferOrStop(stream, isBidirectional: true, signal);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The session is disposed, or a connection error stopped it.
        }
        catch (MultiplexedConnectionFailedException)
        {
            // The connection is lost; the request stream reports it.
        }
        catch (MultiplexedStreamResetException)
        {
            // Before its first integer arrived there is nothing to offer.
        }
        catch (EndOfStreamException)
        {
            // It ended inside its first integer.
            stream.Abort((long)Http3ErrorCode.StreamCreationError);
        }
    }

    // Hands a stream HTTP/3 does not use to the caller, or stops it when nobody takes it.
    private void OfferOrStop(IMultiplexedStream stream, bool isBidirectional, long? type)
    {
        if (type is not { } known || unknownPeerStreamReceived?.Invoke(new Http3UnknownPeerStream(stream, isBidirectional, known)) != true)
        {
            stream.Abort((long)Http3ErrorCode.StreamCreationError);
        }
    }

    private async Task ReadPeerStreamAsync(IMultiplexedStream stream, CancellationToken cancellationToken)
    {
        Http3MultiplexedStreamAdapter peer = new(stream);
        long? type = null;
        try
        {
            type = await peerStreamTypes.ReadTypeAsync(peer, cancellationToken).ConfigureAwait(false);
            await ReadPeerStreamOfTypeAsync(stream, peer, type, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The session is disposed, or a connection error stopped it.
        }
        catch (MultiplexedConnectionFailedException)
        {
            // The connection is lost; the request stream reports it.
        }
        catch (MultiplexedStreamResetException) when (type is null)
        {
            // Before its type arrived there is nothing known to read.
        }
        catch (MultiplexedStreamResetException)
        {
            Fail(new Http3Exception(Http3ErrorCode.ClosedCriticalStream, $"the server reset its {(Http3UnidirectionalStreamType)type.GetValueOrDefault()} stream"), (long)Http3ErrorCode.ClosedCriticalStream);
        }
        catch (Http3Exception error)
        {
            Fail(error, (long)error.ErrorCode);
        }
        catch (QpackException error)
        {
            Fail(error, (long)error.ErrorCode);
        }
    }

    private Task ReadPeerStreamOfTypeAsync(IMultiplexedStream stream, Stream peer, long? type, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[QpackStreamBufferSize];
        switch (type)
        {
            case (long)Http3UnidirectionalStreamType.Control:
                Http3ControlStreamReader reader = new(peer);
                controlStream = reader;
                return ReadUntilFailureAsync(async () => ReportControlFrame(await reader.ReadFrameAsync(cancellationToken).ConfigureAwait(false)));
            case (long)Http3UnidirectionalStreamType.QpackEncoder:
                return ReadUntilFailureAsync(async () => await Http3PeerQpackStreams.ReadEncoderStreamAsync(peer, Decoder, buffer, cancellationToken).ConfigureAwait(false));
            case (long)Http3UnidirectionalStreamType.QpackDecoder:
                return ReadUntilFailureAsync(async () => await Http3PeerQpackStreams.ReadDecoderStreamAsync(peer, Encoder, buffer, cancellationToken).ConfigureAwait(false));
            default: // unknown or grease: the caller may take it, or RFC 9114 section 6.2 lets the client stop reading it
                OfferOrStop(stream, isBidirectional: false, type);
                return Task.CompletedTask;
        }
    }

    private void ReportControlFrame(Http3Frame frame) => controlFrameReceived?.Invoke(frame);

    private void Fail(Exception error, long errorCode)
    {
        lock (gate)
        {
            if (connectionError is not null)
            {
                return;
            }

            connectionError = error;
            connectionErrorCode = errorCode;
        }

        stopReadingPeerStreams.Cancel();
    }

    private async ValueTask CloseAsync(long errorCode)
    {
        try
        {
            await Connection.CloseAsync(errorCode, CancellationToken.None).ConfigureAwait(false);
        }
        catch (IOException)
        {
            // The connection is lost already; there is nobody to tell.
        }
    }
}
