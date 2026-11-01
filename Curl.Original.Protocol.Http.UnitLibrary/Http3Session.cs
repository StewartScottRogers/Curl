using System.Net;
using Curl.Http3;
using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// One QUIC connection speaking HTTP/3 for the HTTP handler: curl's face on an
/// <see cref="Http3ClientSession" /> (BL-4194), which opens the client's control and QPACK
/// encoder and decoder streams with the first request, before its HEADERS, carrying curl's
/// <c>SETTINGS</c> (<see cref="Http3LocalUnidirectionalStreams.CurlSettings" />), and reads the
/// server's unidirectional streams in the background for the connection's life. QPACK runs
/// with the dynamic table capacity and blocked streams curl advertises, 0, so requests are
/// encoded with the static table and literals only (ADR-0144 section 5). This class adds what
/// curl says about it: the server's SETTINGS and GOAWAY in the transfer's log, its exit codes
/// and nghttp3's error names.
/// </summary>
/// <remarks>
/// <para>
/// The session is the <see cref="IConnection" /> the handler holds for the transfer, so its
/// connect result, timings and disposal follow the same path as a TCP connection's; bytes
/// travel only on its streams, so reading or writing the session itself throws
/// <see cref="NotSupportedException" />. Disposing it disposes the core, which stops reading
/// the server's streams, disposes every stream it opened or accepted, closes the connection
/// with <c>H3_NO_ERROR</c> unless a connection error closed it already, and disposes the
/// connection.
/// </para>
/// <para>
/// The first error on any of the server's streams is fatal to the connection, as
/// <c>cf_ngtcp2_h3_err_is_fatal</c> makes it: the core closes the connection with the error's
/// code and this session fails the transfer's next read with <see cref="ConnectionError" />,
/// exit 56 and nghttp3's error name (ADR-0172).
/// </para>
/// </remarks>
internal sealed class Http3Session : IHttpStreamSession, IConnection, IConnectionSession
{
    private readonly Lock gate = new();

    /// <summary>The server's SETTINGS and GOAWAY that arrived before any transfer opened a stream.</summary>
    private readonly List<Http3Frame> unloggedConnectionFrames = [];

    /// <summary>The core's connection error in curl's terms, once there is one.</summary>
    private HttpTransferException? connectionError;

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
        Core = new Http3ClientSession(connection, Http3LocalUnidirectionalStreams.CurlSettings, LogConnectionFrame);
    }

    /// <summary>Gets the HTTP/3 client session this one speaks for.</summary>
    internal Http3ClientSession Core { get; }

    /// <summary>Gets the QUIC connection the session runs on.</summary>
    internal IMultiplexedConnection Connection => Core.Connection;

    /// <summary>
    /// Gets the task that reads the server's unidirectional streams: it ends when the session
    /// is disposed, the connection is lost, or a connection error stops it and closes the
    /// connection.
    /// </summary>
    internal Task PeerStreamsReading => Core.PeerStreamsReading;

    /// <summary>Gets the server's <c>SETTINGS</c>, once its control stream has delivered them.</summary>
    internal Http3SettingsFrame? PeerSettings => Core.PeerSettings;

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
                connectionError ??= ReadStreamFailed(Core.ConnectionError);
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
    public bool AcceptsNewStreams => Core.AcceptsNewStreams;

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
    public IHttpStreamConnection CreateStream(string scheme, long? bodyLength, bool ignoresBody, HttpStreamOpenedLines? openedLines = null, IDiagnosticLog? diagnosticLog = null, ITransferEvents? traceEvents = null) =>
        new Http3StreamConnection(this, scheme, bodyLength, ignoresBody, openedLines, HttpFrameLog.For(diagnosticLog, VersionName), new Http3StreamTrace(traceEvents));

    /// <summary>
    /// Opens a request stream through the core, which opens the client's control stream with
    /// curl's <c>SETTINGS</c> and its QPACK encoder and decoder streams first unless they are
    /// open already, one transfer at a time, as the <c>-Z</c> transfers a pool shares the
    /// session between open theirs concurrently (BL-735). The server's SETTINGS and GOAWAY are
    /// logged to <paramref name="transferLog" /> from now on, those that arrived before it
    /// included (ADR-0345, BL-1155). A stream the connection cannot open, though it is up, is
    /// exit 55 and <c>cannot open bidi streams</c>, as curl 8.21.0 reports it (ADR-0245).
    /// </summary>
    /// <param name="transferLog">The frame log of the transfer opening the stream.</param>
    /// <param name="cancellationToken">Cancels the opens and writes.</param>
    /// <param name="trace">The transfer's <c>--trace-config http/3</c> lines, told of the peer's idle timeout when this is the connection's first request stream (BL-1208), or <see langword="null" />.</param>
    /// <returns>The request stream.</returns>
    /// <exception cref="HttpTransferException">The stream cannot be opened (exit 55).</exception>
    internal async ValueTask<Http3ClientRequestStream> OpenRequestStreamAsync(HttpFrameLog transferLog, CancellationToken cancellationToken, Http3StreamTrace? trace = null)
    {
        UseConnectionLog(transferLog);
        try
        {
            return await Core.OpenRequestStreamAsync(() => trace?.ConnectionReady(Connection.PeerIdleTimeout), cancellationToken).ConfigureAwait(false);
        }
        catch (IOException refused) when (refused is not MultiplexedConnectionFailedException)
        {
            throw new HttpTransferException(CurlExitCode.SendError, HttpTransferMessages.Http3CannotOpenBidiStreams);
        }
    }

    /// <summary>
    /// Marks one transfer's request stream done and says what curl's <c>query conn[&lt;n&gt;]: MAX_CONCURRENT</c>
    /// line reports (BL-1208): how many more request streams the peer allows, or <see langword="null" />
    /// when the connection does not know, and how many request streams are still in use.
    /// </summary>
    /// <returns>The streams left and the streams still in use.</returns>
    internal (long? StreamsLeft, int StreamsInUse) EndRequestStream() => Core.EndRequestStream();

    /// <summary>
    /// Stops the connection carrying another request, as curl marks a connection closed once
    /// the server refuses a stream with <c>H3_REQUEST_REJECTED</c> (ADR-0187).
    /// </summary>
    internal void StopNewStreams() => Core.StopNewStreams();

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
    /// Disposes the core: it stops reading the server's streams, disposes every stream it
    /// opened or accepted, closes the connection with <c>H3_NO_ERROR</c> - unless a connection
    /// error closed it already, or it is lost - and disposes it.
    /// </summary>
    /// <returns>A task that completes when the connection is disposed.</returns>
    public ValueTask DisposeAsync() => Core.DisposeAsync();

    /// <summary>
    /// Logs a SETTINGS or GOAWAY frame from the server's control stream to the transfer that
    /// last opened a stream, or holds it until one does; other control frames are not logged.
    /// </summary>
    /// <param name="frame">A frame read from the server's control stream.</param>
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

    /// <summary>
    /// Gives the exit 56 failure for the core's connection error, with nghttp3's name for it,
    /// or <see langword="null" /> while there is none.
    /// </summary>
    private static HttpTransferException? ReadStreamFailed(Exception? error) => error switch
    {
        null => null,
        Http3Exception http3 => new(CurlExitCode.RecvError, HttpTransferMessages.Http3ReadStreamFailed(Http3StreamConnection.Nghttp3ErrorName(http3.ErrorCode))),
        _ => new(CurlExitCode.RecvError, HttpTransferMessages.Http3ReadStreamFailed("ERR_QPACK_" + Http3StreamConnection.UpperSnakeCase(((QpackException)error).ErrorCode.ToString()))),
    };

    private static void WriteConnectionFrame(HttpFrameLog log, Http3Frame frame)
    {
        if (frame is Http3SettingsFrame settings)
        {
            log.Http3SettingsReceived(settings);
            return;
        }

        log.Http3GoawayReceived(((Http3GoawayFrame)frame).Id);
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
}
