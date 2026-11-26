using Curl.Http2;
using Curl.Http3;
using Curl.Original.Protocol.Http.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// What an HTTP/3 transfer whose stream the server resets does to its connection and to the next
/// URL on it, as curl 8.18.0's ngtcp2 build was measured to do against
/// <c>Record-CurlExchange.ps1 -Http3StalledOrigin -Http3ResetCode 0x102</c> (BL-5997, ADR-1416):
/// the stream's reset lines, the failure, the stream marked done, the connection left intact, and
/// the next URL sent on it after the <c>peer idle timeout</c> line.
/// </summary>
[TestClass]
public sealed class Http3ResetStreamConnectionTests
{
    private const long InternalError = 0x102;

    private const long RequestCancelled = 0x10c;

    private const string IdleLine = "* [HTTP/3] peer idle timeout is 300000ms, set keep-alive to 150000 ms.";

    private const string LeftIntact = "* Connection #0 to host example.com:443 left intact";

    private const string Failure = "HTTP/3 stream 0 reset by server (error 0x102 INTERNAL_ERROR)";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(false, false, CurlExitCode.Http3, DisplayName = "reset before any answer")]
    [DataRow(true, false, CurlExitCode.Http3, DisplayName = "reset after the head, before body bytes")]
    [DataRow(true, true, CurlExitCode.PartialFile, DisplayName = "reset after body bytes")]
    public async Task ExecuteAsync_TheServerResetsAStream_MarksItDoneAndTheNextUrlUsesTheConnection(bool sendsHead, bool sendsBody, CurlExitCode expected)
    {
        byte[] head = sendsHead ? Head(0) : [];
        byte[] body = sendsBody ? Body() : [];
        FakeMultiplexedStream reset = new(0, [.. head, .. body]) { EndException = new MultiplexedStreamResetException(InternalError, "reset") };
        FakeMultiplexedStream answered = new(4, [.. Head(4), .. Body()]);
        FakeMultiplexedConnection quic = new(reset, answered)
        {
            PeerIdleTimeout = TimeSpan.FromMinutes(5),
            BidirectionalStreamLimit = 100,
        };
        PoolingConnector connector = new(quic);
        HttpProtocolHandler handler = new(connector, new SilentAuthenticator()) { TracesHttp3Streams = true };
        RecordingTransferEvents events = new();
        RecordingTransferEvents nextEvents = new();
        Diagnostics.Arrange("server", $"stream 0 reset with 0x102, head sent {sendsHead}, body sent {sendsBody}; answers stream 4 whole");

        TransferResult result = await handler.ExecuteAsync(Context(events));
        TransferResult next = await handler.ExecuteAsync(Context(nextEvents));

        string lines = string.Join('|', events.Events);
        string nextLines = string.Join('|', nextEvents.Events);
        Diagnostics.Act("exit codes, abort code", $"{result.ExitCode} then {next.ExitCode}, {reset.AbortCode}");
        Diagnostics.Act("reset transfer's events", lines);
        Diagnostics.Act("next transfer's events", nextLines);
        Diagnostics.Assert("exit code", expected, result.ExitCode);
        Assert.AreEqual(expected, result.ExitCode);
        Assert.AreEqual(Failure, result.ErrorMessage);
        Assert.AreEqual(CurlExitCode.Ok, next.ExitCode, next.ErrorMessage);
        Assert.IsNull(reset.AbortCode);
        CollectionAssert.AreEqual(
            new[]
            {
                "* [HTTP/3] [0] reset -> 0",
                "* [HTTP/3] [0] RESET: error 258",
                "* [HTTP/3] [0] quic close(app_error=258) -> 0",
                "* " + Failure,
                "* [HTTP/3] [0] easy handle is done",
                "* [HTTP/3] no active streams, unset keep-alive",
                "* [HTTP/3] query conn[0]: MAX_CONCURRENT -> 99 (0 in use)",
                LeftIntact,
            },
            events.Events.TakeLast(8).ToArray(),
            lines);
        int idle = nextEvents.Events.IndexOf(IdleLine);
        int opened = nextEvents.Events.FindIndex(line => line.StartsWith("* [HTTP/3] [4] OPENED stream", StringComparison.Ordinal));
        Assert.IsGreaterThanOrEqualTo(0, idle, nextLines);
        Assert.IsGreaterThan(idle, opened, nextLines);
        Assert.DoesNotContain("* [HTTP/3] [0] quic close(app_error=268) -> 0", nextEvents.Events, nextLines);
        Assert.AreEqual(2, connector.Pooled!.ReturnedReusableCount);
        Assert.IsNull(quic.CloseCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_TheServerResetsAStreamWhoseRequestIsStillUnsent_CancelsTheRequestAndKeepsTheConnection()
    {
        FakeMultiplexedStream reset = new(0, []) { EndException = new MultiplexedStreamResetException(InternalError, "reset") };
        FakeMultiplexedConnection quic = new(reset) { BidirectionalStreamLimit = 100 };
        PoolingConnector connector = new(quic);
        HttpProtocolHandler handler = new(connector, new SilentAuthenticator());
        TransferContext context = Context(new RecordingTransferEvents(), new HttpRequestOptions
        {
            Version = HttpVersionPreference.Http3Only,
            Headers = ["Expect: 100-continue"],
            Body = new BytesBody("x=1"u8.ToArray(), "application/x-www-form-urlencoded"),
        });
        Diagnostics.Arrange("server", "stream 0 reset with 0x102 while the request body waits for 100 Continue");

        TransferResult result = await handler.ExecuteAsync(context);

        Diagnostics.Act("exit code, abort code", $"{result.ExitCode}, {reset.AbortCode}");
        Diagnostics.Assert("abort code", RequestCancelled, reset.AbortCode);
        Assert.AreEqual(CurlExitCode.Http3, result.ExitCode, result.ErrorMessage);
        Assert.AreEqual(RequestCancelled, reset.AbortCode);
        Assert.AreEqual(1, connector.Pooled!.ReturnedReusableCount);
        Assert.IsNull(quic.CloseCode);
    }

    private static byte[] Head(long streamId) =>
        new Http3HeadersFrame(new QpackEncoder(0, 0).EncodeFieldSection(streamId, [new HeaderField(":status", "200")])).ToBytes();

    private static byte[] Body() => new Http3DataFrame("hello\n"u8.ToArray()).ToBytes();

    // A clock no test advances: on the real clock a slow machine can pass the handler's stream
    // cleanup limit and drop the cancellation the test expects (BL-6052).
    private static TransferContext Context(RecordingTransferEvents events, HttpRequestOptions? options = null) => new()
    {
        Url = CurlUrl.Parse("https://example.com/"),
        Output = new MemoryStream(),
        TimeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch),
        ConnectTimeout = TimeSpan.FromSeconds(1),
        Events = events,
        Http = options ?? new HttpRequestOptions { Version = HttpVersionPreference.Http3Only },
    };

    /// <summary>Opens one HTTP/3 session and gives every connect the same pooled connection holding it, as a pool does.</summary>
    private sealed class PoolingConnector(FakeMultiplexedConnection quic) : IConnector
    {
        public SessionHoldingConnection? Pooled { get; private set; }

        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask<ConnectResult> ConnectMultiplexedSessionAsync(ConnectTarget target, Func<IMultiplexedConnection, IConnection> openSession, CancellationToken cancellationToken)
        {
            if (Pooled is null)
            {
                IConnection session = openSession(quic);
                Pooled = new SessionHoldingConnection(session);
                Pooled.TryHoldSession((IConnectionSession)session);
            }

            return ValueTask.FromResult(ConnectResult.Connected(Pooled));
        }
    }
}
