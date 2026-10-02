using System.Net;
using Curl.Http3;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// One QUIC connection speaking HTTP/3 for the HTTP handler: the QPACK contexts every request
/// stream on it shares, the client's control and QPACK encoder and decoder streams, which
/// open with the first request, before its HEADERS, carrying curl's <c>SETTINGS</c>
/// (<see cref="Http3LocalUnidirectionalStreams.CurlSettings" />), and the server's
/// unidirectional streams, read in the background for the connection's life. QPACK runs with
/// the dynamic table capacity and blocked streams curl advertises, 0, so requests are encoded
/// with the static table and literals only (ADR-0144 section 5).
/// </summary>
/// <remarks>
/// <para>
/// The session is the <see cref="IConnection" /> the handler holds for the transfer, so its
/// connect result, timings and disposal follow the same path as a TCP connection's; bytes
/// travel only on its streams, so reading or writing the session itself throws
/// <see cref="NotSupportedException" />. Disposing it stops reading the server's streams,
/// disposes every stream it opened or accepted, closes the connection with <c>H3_NO_ERROR</c>
/// unless a connection error closed it already, and disposes the connection.
/// </para>
/// <para>
/// The server's control stream is read with <see cref="Http3ControlStreamReader" />, its QPACK
/// encoder and decoder streams are fed to <see cref="Decoder" /> and <see cref="Encoder" />, and
/// a stream of an unknown type is abandoned with <c>H3_STREAM_CREATION_ERROR</c>. The first
/// error on any of them is fatal to the connection, as <c>cf_ngtcp2_h3_err_is_fatal</c> makes
/// it: the session closes the connection with the error's code and fails the transfer's next
/// read with <see cref="ConnectionError" />, exit 56 and nghttp3's error name (ADR-0172).
/// </para>
/// </remarks>
internal sealed class Http3Session : IHttpStreamSession, IConnection, IConnectionSession
{
    /// <summary>The size of the buffer each server QPACK stream is read into.</summary>
    private const int QpackStreamBufferSize = 4096;

    private readonly List<IMultiplexedStream> openedStreams = [];

    private readonly List<IMultiplexedStream> acceptedStreams = [];

    private readonly Http3PeerUnidirectionalStreams peerStreamTypes = new();

    private readonly CancellationTokenSource stopReadingPeerStreams = new();

    private readonly Lock gate = new();

    private readonly SemaphoreSlim openingStreams = new(1, 1);

    /// <summary>The server's SETTINGS and GOAWAY that arrived before any transfer opened a stream.</summary>
    private readonly List<Http3Frame> unloggedConnectionFrames = [];

    private volatile Http3ControlStreamReader? controlStream;

    private HttpTransferException? connectionError;

    private long connectionErrorCode;

    private volatile bool isStoppedByRefusedStream;

    /// <summary>
    /// The frame log of the transfer that last opened a stream, which the server's SETTINGS and
    /// GOAWAY go to (ADR-0345 point 3), or <see langword="null" /> until one has.
    /// </summary>
    private HttpFrameLog? connectionLog;

    /// <summary>
    /// Initializes a new instance of the <see cref="Http3Session" /> class and starts reading
    /// the server's unidirectional streams.
    /// </summary>
    /// <param name="connection">The QUIC connection, past its handshake; the session owns it.</param>
    public Http3Session(IMultiplexedConnection connection)
    {
        Connection = connection;
        PeerStreamsReading = ReadPeerStreamsAsync(stopReadingPeerStreams.Token);
    }

    /// <summary>Gets the QUIC connection the session runs on.</summary>
    internal IMultiplexedConnection Connection { get; }

    /// <summary>Gets the encoder every request field section passes through.</summary>
    internal QpackEncoder Encoder { get; } = new(maximumTableCapacity: 0, maximumBlockedStreams: 0);

    /// <summary>Gets the decoder every response field section passes through.</summary>
    internal QpackDecoder Decoder { get; } = new(maximumTableCapacity: 0, maximumBlockedStreams: 0);

    /// <summary>
    /// Gets the task that reads the server's unidirectional streams: it ends when the session
    /// is disposed, the connection is lost, or a connection error stops it and closes the
    /// connection.
    /// </summary>
    internal Task PeerStreamsReading { get; }

    /// <summary>Gets the server's <c>SETTINGS</c>, once its control stream has delivered them.</summary>
    internal Http3SettingsFrame? PeerSettings => controlStream?.PeerSettings;

    /// <summary>
    /// Gets the failure a connection error on one of the server's unidirectional streams
    /// gives the transfer - exit 56 with <c>nghttp3_conn_read_stream returned error: &lt;name&gt;</c> -
    /// or <see langword="null" /> while there is none.
    /// </summary>
    internal HttpTransferException? ConnectionError
    {
        get
        {
            lock (gate)
            {
                return connectionError;
            }
        }
    }

    /// <inheritdoc />
    public string VersionName => "HTTP/3";

    /// <inheritdoc />
    public string UsingLine => HttpConnectionInfoLines.UsingHttp3;

    /// <summary>
    /// Gets a value indicating whether the connection can carry another request: until the
    /// server sends <c>GOAWAY</c> or refuses a request stream (<see cref="StopNewStreams" />),
    /// or a connection error ends it.
    /// </summary>
    public bool AcceptsNewStreams => !isStoppedByRefusedStream && controlStream?.GoawayStreamId is null && ConnectionError is null;

    /// <summary>
    /// Gets how many transfers the connection carries at once, each on a request stream of its
    /// own: the servers latest <c>MAX_STREAMS</c> for the clients bidirectional streams,
    /// unlimited when the connection does not know it, or 0 once it takes no new request
    /// (<see cref="AcceptsNewStreams" />), so a pool shares the session between <c>-Z</c>
    /// transfers and opens a new connection when it is reached (BL-735).
    /// </summary>
    public int? ConcurrentTransferLimit => AcceptsNewStreams
        ? (int)Math.Min(Connection.BidirectionalStreamLimit ?? int.MaxValue, int.MaxValue)
        : 0;

    /// <summary>
    /// Does nothing: disposing the session closes the connection with <c>H3_NO_ERROR</c>.
    /// </summary>
    /// <param name="cancellationToken">Not used.</param>
    /// <returns>A completed task.</returns>
    public ValueTask ShutDownAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    /// <summary>Gets a value indicating that QUIC traffic is always encrypted.</summary>
    public bool IsSecure => true;

    /// <inheritdoc />
    public EndPoint? RemoteEndPoint => Connection.RemoteEndPoint;

    /// <inheritdoc />
    public EndPoint? LocalEndPoint => Connection.LocalEndPoint;

    /// <inheritdoc />
    public IHttpStreamConnection CreateStream(string scheme, long? bodyLength, bool ignoresBody, HttpStreamOpenedLines? openedLines = null, IDiagnosticLog? diagnosticLog = null) =>
        new Http3StreamConnection(this, scheme, bodyLength, ignoresBody, openedLines, HttpFrameLog.For(diagnosticLog, VersionName));

    /// <summary>
    /// Opens the client's control stream with curl's <c>SETTINGS</c> and its QPACK encoder
    /// and decoder streams, unless they are open already, then opens a request stream; one
    /// transfer at a time, as the <c>-Z</c> transfers a pool shares the session between open
    /// theirs concurrently (BL-735). The server's SETTINGS and GOAWAY are logged to
    /// <paramref name="transferLog" /> from now on, those that arrived before it included
    /// (ADR-0345, BL-1155).
    /// </summary>
    /// <param name="transferLog">The frame log of the transfer opening the stream.</param>
    /// <param name="cancellationToken">Cancels the opens and writes.</param>
    /// <returns>The request stream.</returns>
    internal async ValueTask<IMultiplexedStream> OpenRequestStreamAsync(HttpFrameLog transferLog, CancellationToken cancellationToken)
    {
        await openingStreams.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            UseConnectionLog(transferLog);
            return await OpenRequestStreamOneAtATimeAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            openingStreams.Release();
        }
    }

    private async ValueTask<IMultiplexedStream> OpenRequestStreamOneAtATimeAsync(CancellationToken cancellationToken)
    {
        if (openedStreams.Count == 0) // the first request: the three unidirectional streams go first
        {
            await Http3LocalUnidirectionalStreams.OpenControlStreamAsync(await OpenLocalStreamAsync(cancellationToken).ConfigureAwait(false), Http3LocalUnidirectionalStreams.CurlSettings, cancellationToken).ConfigureAwait(false);
            await Http3LocalUnidirectionalStreams.OpenQpackEncoderStreamAsync(await OpenLocalStreamAsync(cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
            await Http3LocalUnidirectionalStreams.OpenQpackDecoderStreamAsync(await OpenLocalStreamAsync(cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
        }

        IMultiplexedStream requestStream = await OpenBidirectionalStreamAsync(cancellationToken).ConfigureAwait(false);
        openedStreams.Add(requestStream);
        return requestStream;
    }

    /// <summary>
    /// Opens the request's bidirectional stream. An <see cref="IOException" /> other than a
    /// lost connection's <see cref="MultiplexedConnectionFailedException" /> means the
    /// connection is up but the stream cannot be opened, which curl 8.21.0 reports as exit 55
    /// and <c>cannot open bidi streams</c> (ADR-0245).
    /// </summary>
    /// <exception cref="HttpTransferException">The stream cannot be opened (exit 55).</exception>
    private async ValueTask<IMultiplexedStream> OpenBidirectionalStreamAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await Connection.OpenBidirectionalStreamAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (IOException refused) when (refused is not MultiplexedConnectionFailedException)
        {
            throw new HttpTransferException(CurlExitCode.SendError, HttpTransferMessages.Http3CannotOpenBidiStreams);
        }
    }

    /// <summary>
    /// Stops the connection carrying another request, as curl marks a connection closed once
    /// the server refuses a stream with <c>H3_REQUEST_REJECTED</c> (ADR-0187).
    /// </summary>
    internal void StopNewStreams() => isStoppedByRefusedStream = true;

    /// <summary>Always throws: bytes travel only on the session's streams.</summary>
    /// <param name="buffer">Not used.</param>
    /// <param name="cancellationToken">Not used.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="NotSupportedException">Always.</exception>
    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <summary>Always throws: bytes travel only on the session's streams.</summary>
    /// <param name="buffer">Not used.</param>
    /// <param name="cancellationToken">Not used.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="NotSupportedException">Always.</exception>
    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <summary>Does nothing: each stream sends what it is given.</summary>
    /// <param name="cancellationToken">Not used.</param>
    /// <returns>A completed task.</returns>
    public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    /// <summary>
    /// Stops reading the server's streams, disposes every stream it opened or accepted, closes
    /// the connection with <c>H3_NO_ERROR</c> - unless a connection error closed it already,
    /// or it is lost - and disposes it.
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

    /// <summary>
    /// Reads a critical stream for as long as it lasts: its reads throw once it ends, so only
    /// an exception ends the loop.
    /// </summary>
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
        return new MultiplexedStreamAdapter(stream);
    }

    /// <summary>
    /// Accepts the server's unidirectional streams and reads each as it arrives, until the
    /// session stops reading or the connection is lost, then waits for every reader and, after
    /// a connection error, closes the connection with its code.
    /// </summary>
    private async Task ReadPeerStreamsAsync(CancellationToken cancellationToken)
    {
        List<Task> readers = [];
        try
        {
            while (true)
            {
                IMultiplexedStream stream = await Connection.AcceptUnidirectionalStreamAsync(cancellationToken).ConfigureAwait(false);
                acceptedStreams.Add(stream);
                readers.Add(ReadPeerStreamAsync(stream, cancellationToken));
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
            // The connection hands out no server streams, as a scripted one in a test may not.
        }

        await Task.WhenAll(readers).ConfigureAwait(false);
        if (ConnectionError is not null)
        {
            await CloseAsync(connectionErrorCode).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Reads one server stream by its type until it fails, the session stops reading or the
    /// connection is lost, recording a failure as the session's connection error. A stream the
    /// server resets before its type arrives is dropped, as its type is unknown.
    /// </summary>
    private async Task ReadPeerStreamAsync(IMultiplexedStream stream, CancellationToken cancellationToken)
    {
        MultiplexedStreamAdapter peer = new(stream);
        Http3UnidirectionalStreamType? type = null;
        try
        {
            type = await peerStreamTypes.AcceptAsync(peer, cancellationToken).ConfigureAwait(false);
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
        catch (MultiplexedStreamResetException)
        {
            // Before its type arrived there is nothing known to read; after, a critical stream closed.
            FailIf(type is not null, (long)Http3ErrorCode.ClosedCriticalStream, Http3StreamConnection.Nghttp3ErrorName(Http3ErrorCode.ClosedCriticalStream));
        }
        catch (Http3Exception error)
        {
            FailIf(true, (long)error.ErrorCode, Http3StreamConnection.Nghttp3ErrorName(error.ErrorCode));
        }
        catch (QpackException error)
        {
            FailIf(true, (long)error.ErrorCode, "ERR_QPACK_" + Http3StreamConnection.UpperSnakeCase(error.ErrorCode.ToString()));
        }
    }

    /// <summary>
    /// Gives the task that reads a server stream of <paramref name="type" /> until it fails, or
    /// abandons a stream of no known type.
    /// </summary>
    private Task ReadPeerStreamOfTypeAsync(IMultiplexedStream stream, Stream peer, Http3UnidirectionalStreamType? type, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[QpackStreamBufferSize];
        switch (type)
        {
            case Http3UnidirectionalStreamType.Control:
                Http3ControlStreamReader reader = new(peer);
                controlStream = reader;
                return ReadUntilFailureAsync(async () => LogConnectionFrame(await reader.ReadFrameAsync(cancellationToken).ConfigureAwait(false)));
            case Http3UnidirectionalStreamType.QpackEncoder:
                return ReadUntilFailureAsync(async () => await Http3PeerQpackStreams.ReadEncoderStreamAsync(peer, Decoder, buffer, cancellationToken).ConfigureAwait(false));
            case Http3UnidirectionalStreamType.QpackDecoder:
                return ReadUntilFailureAsync(async () => await Http3PeerQpackStreams.ReadDecoderStreamAsync(peer, Encoder, buffer, cancellationToken).ConfigureAwait(false));
            default: // unknown or grease: RFC 9114 section 6.2 lets the client stop reading it
                stream.Abort((long)Http3ErrorCode.StreamCreationError);
                return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Records the first connection error, when <paramref name="isError" />, and stops reading
    /// the server's streams; the reading task then closes the connection with
    /// <paramref name="errorCode" />. A later error changes nothing.
    /// </summary>
    private void FailIf(bool isError, long errorCode, string errorName)
    {
        lock (gate)
        {
            if (!isError || connectionError is not null)
            {
                return;
            }

            connectionError = new HttpTransferException(CurlExitCode.RecvError, HttpTransferMessages.Http3ReadStreamFailed(errorName));
            connectionErrorCode = errorCode;
        }

        stopReadingPeerStreams.Cancel();
    }

    /// <summary>
    /// Makes <paramref name="transferLog" /> the log the server's SETTINGS and GOAWAY go to,
    /// and writes there those that arrived while no transfer had opened a stream.
    /// </summary>
    private void UseConnectionLog(HttpFrameLog transferLog)
    {
        lock (gate)
        {
            connectionLog = transferLog;
            foreach (Http3Frame frame in unloggedConnectionFrames)
            {
                WriteConnectionFrame(transferLog, frame);
            }

            unloggedConnectionFrames.Clear();
        }
    }

    /// <summary>
    /// Logs a SETTINGS or GOAWAY frame from the server's control stream to the transfer that
    /// last opened a stream, or holds it until one does; other control frames are not logged.
    /// </summary>
    internal void LogConnectionFrame(Http3Frame frame)
    {
        if (frame is not (Http3SettingsFrame or Http3GoawayFrame))
        {
            return;
        }

        lock (gate)
        {
            if (connectionLog is null)
            {
                unloggedConnectionFrames.Add(frame);
                return;
            }

            WriteConnectionFrame(connectionLog, frame);
        }
    }

    private static void WriteConnectionFrame(HttpFrameLog log, Http3Frame frame)
    {
        if (frame is Http3SettingsFrame settings)
        {
            log.Http3SettingsReceived(settings);
            return;
        }

        log.Http3GoawayReceived(((Http3GoawayFrame)frame).Id);
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
