using Curl.Http2;
using Curl.Http3;
using Curl.Original.Protocol.Http.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// What an HTTP/3 transfer <c>-f</c>, <c>--max-filesize</c> or a failed write ends does to its
/// stream and to the next URL on the connection, as curl 8.18.0's ngtcp2 build was measured to
/// do against <c>Record-CurlExchange.ps1 -Http3StalledOrigin</c> (BL-5996, ADR-1415): the stream
/// is marked done, the connection is left intact, and the next URL writes the
/// <c>peer idle timeout</c> line again before it opens its stream. A stream left open is cancelled
/// with <c>H3_REQUEST_CANCELLED</c>; one whose end has arrived is not.
/// </summary>
[TestClass]
public sealed class Http3FailedTransferStreamTests
{
    private const long RequestCancelled = 0x10c;

    private const string IdleLine = "* [HTTP/3] peer idle timeout is 300000ms, set keep-alive to 150000 ms.";

    private const string LeftIntact = "* Connection #0 to host example.com:443 left intact";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(CurlExitCode.HttpReturnedError, true, DisplayName = "-f, stream left open")]
    [DataRow(CurlExitCode.FilesizeExceeded, true, DisplayName = "--max-filesize, stream left open")]
    [DataRow(CurlExitCode.FilesizeExceeded, false, DisplayName = "--max-filesize, stream ended")]
    [DataRow(CurlExitCode.WriteError, true, DisplayName = "failed write, stream left open")]
    [DataRow(CurlExitCode.WriteError, false, DisplayName = "failed write, stream ended")]
    public async Task ExecuteAsync_ATransferFailsOnAStream_MarksItDoneAndTheNextUrlUsesTheIdleConnection(CurlExitCode failure, bool staysOpen)
    {
        string status = failure == CurlExitCode.HttpReturnedError ? "500" : "200";
        FakeMultiplexedStream failed = new(0, Response(0, status)) { StaysOpen = staysOpen };
        FakeMultiplexedStream answered = new(4, Response(4, "200"));
        FakeMultiplexedConnection quic = new(failed, answered)
        {
            PeerIdleTimeout = TimeSpan.FromMinutes(5),
            BidirectionalStreamLimit = 100,
        };
        PoolingConnector connector = new(quic);
        HttpProtocolHandler handler = new(connector, new SilentAuthenticator()) { TracesHttp3Streams = true };
        RecordingTransferEvents events = new();
        RecordingTransferEvents nextEvents = new();
        Diagnostics.Arrange("server", $"HEADERS :status {status} and DATA on stream 0, left open {staysOpen}; answers stream 4 whole");

        TransferResult result = await handler.ExecuteAsync(Context(events, failure));
        TransferResult next = await handler.ExecuteAsync(Context(nextEvents, CurlExitCode.Ok));

        string lines = string.Join('|', events.Events);
        string nextLines = string.Join('|', nextEvents.Events);
        Diagnostics.Act("exit codes, abort code", $"{result.ExitCode} then {next.ExitCode}, {failed.AbortCode}");
        Diagnostics.Act("failed transfer's events", lines);
        Diagnostics.Act("next transfer's events", nextLines);
        Diagnostics.Assert("last line", LeftIntact, events.Events[^1]);
        Assert.AreEqual(failure, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, next.ExitCode);
        Assert.AreEqual(staysOpen ? RequestCancelled : null, failed.AbortCode);
        CollectionAssert.AreEqual(
            new[]
            {
                "* [HTTP/3] [0] easy handle is done",
                "* [HTTP/3] no active streams, unset keep-alive",
                "* [HTTP/3] query conn[0]: MAX_CONCURRENT -> 99 (0 in use)",
                LeftIntact,
            },
            events.Events.TakeLast(4).ToArray(),
            lines);
        int idle = nextEvents.Events.IndexOf(IdleLine);
        int opened = nextEvents.Events.FindIndex(line => line.StartsWith("* [HTTP/3] [4] OPENED stream", StringComparison.Ordinal));
        Assert.IsGreaterThanOrEqualTo(0, idle, nextLines);
        Assert.IsGreaterThan(idle, opened, nextLines);
        Assert.Contains("* [HTTP/3] no active streams, unset keep-alive", nextEvents.Events, nextLines);
        Assert.AreEqual(2, connector.Pooled!.ReturnedReusableCount);
        Assert.IsNull(quic.CloseCode);
    }

    private static byte[] Response(long streamId, string status) =>
    [
        .. new Http3HeadersFrame(new QpackEncoder(0, 0).EncodeFieldSection(streamId, [new HeaderField(":status", status)])).ToBytes(),
        .. new Http3DataFrame("hello\n"u8.ToArray()).ToBytes(),
    ];

    private static TransferContext Context(RecordingTransferEvents events, CurlExitCode failure) => new()
    {
        Url = CurlUrl.Parse("https://example.com/"),
        Output = failure == CurlExitCode.WriteError ? new FailingWriteStream(1, new IOException("disk full")) : new MemoryStream(),
        TimeProvider = TimeProvider.System,
        ConnectTimeout = TimeSpan.FromSeconds(1),
        MaxFileSize = failure == CurlExitCode.FilesizeExceeded ? 1 : null,
        Events = events,
        Http = new HttpRequestOptions
        {
            Version = HttpVersionPreference.Http3Only,
            Fail = failure == CurlExitCode.HttpReturnedError ? HttpFailMode.Fail : HttpFailMode.None,
        },
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
