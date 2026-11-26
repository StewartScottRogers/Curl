using Curl.Http2;
using Curl.Http3;
using Curl.Original.Protocol.Http.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// What the status of an HTTP/3 response does to an upload that asked for <c>100 Continue</c>,
/// as over HTTP/2 (ADR-1411): a 500 that arrives after the wait ran out, while the body is being
/// sent, stops the body, writes the two stop lines, cancels the stream once the response is read
/// and leaves the connection intact (BL-5979). A 500 during the wait leaves the body unsent and
/// cancels the stream too. The clock is a fake one, advanced only to run the wait out.
/// </summary>
[TestClass]
public sealed class Http3ContinueWaitStatusTests
{
    private const long RequestCancelled = 0x10c;

    private const int BodyLength = 200000;

    private const string StopSending = "* HTTP error before end of send, stop sending";

    private const string LeftIntact = "* Connection #0 to host example.com:443 left intact";

    private static readonly byte[] DecoderStreamType = [0x03];

    private static readonly byte[] DecoderStreamCancellingStreamZero = [0x03, 0x40];

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ExecuteAsync_A500AfterTheWaitRanOutWhileTheBodyIsSent_StopsTheUploadAndKeepsTheConnection()
    {
        // Completing this runs the pending read of the response inline, so the status has been
        // taken before the body's second read returns.
        TaskCompletionSource answer = new();
        FakeMultiplexedStream request = new(0, Response("500")) { ReadsAfter = answer.Task };
        FakeMultiplexedConnection quic = new(request);
        AnsweringBody body = new(answer);
        RecordingTransferEvents events = new();
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        Diagnostics.Arrange("server", "500 on stream 0 once the body's second read starts, after the wait ran out");

        Task<TransferResult> transfer = StartAsync(quic, events, time, new StreamBody(body, BodyLength, "application/octet-stream"));
        time.Advance(HttpRequestOptions.DefaultContinueWait);
        TransferResult result = await transfer;

        string all = string.Join('|', events.Events);
        Diagnostics.Act("exit code, body reads, bytes written, abort code, events", $"{result.ExitCode}, {body.Reads}, {request.Written.Length}, {request.AbortCode}, {all}");
        Diagnostics.Assert("abort code", RequestCancelled, request.AbortCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        Assert.AreEqual(2, body.Reads);
        Assert.IsGreaterThan(body.FirstReadLength, (int)request.Written.Length);
        Assert.IsLessThan(2 * body.FirstReadLength, (int)request.Written.Length);
        Assert.IsFalse(request.IsEndedByClient);
        Assert.AreEqual(RequestCancelled, request.AbortCode);
        CollectionAssert.AreEqual(DecoderStreamCancellingStreamZero, quic.UnidirectionalStreams[2].Written.ToArray());
        Assert.IsNull(quic.CloseCode);
        Assert.IsFalse(events.Events.Any(line => line.Contains("sent off", StringComparison.Ordinal)), all);
        AssertInOrder(
            events.Events,
            "* Done waiting for 100-continue",
            "< HTTP/3 500 \r\n",
            StopSending,
            $"* abort upload after having sent {body.FirstReadLength} bytes",
            "< \r\n",
            LeftIntact);
    }

    [TestMethod]
    public async Task ExecuteAsync_A200AfterTheWaitRanOutWhileTheBodyIsSent_SendsTheWholeBody()
    {
        TaskCompletionSource answer = new();
        FakeMultiplexedStream request = new(0, Response("200")) { ReadsAfter = answer.Task };
        FakeMultiplexedConnection quic = new(request);
        AnsweringBody body = new(answer);
        RecordingTransferEvents events = new();
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        Diagnostics.Arrange("server", "200 on stream 0 once the body's second read starts, after the wait ran out");

        Task<TransferResult> transfer = StartAsync(quic, events, time, new StreamBody(body, BodyLength, "application/octet-stream"));
        time.Advance(HttpRequestOptions.DefaultContinueWait);
        TransferResult result = await transfer;

        Diagnostics.Act("exit code, bytes written, abort code", $"{result.ExitCode}, {request.Written.Length}, {request.AbortCode}");
        Diagnostics.Assert("abort code", null, request.AbortCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        Assert.IsGreaterThan(BodyLength, (int)request.Written.Length);
        Assert.IsTrue(request.IsEndedByClient);
        Assert.IsNull(request.AbortCode);
        CollectionAssert.AreEqual(DecoderStreamType, quic.UnidirectionalStreams[2].Written.ToArray());
        CollectionAssert.DoesNotContain(events.Events, StopSending);
    }

    [TestMethod]
    public async Task ExecuteAsync_A500DuringTheWait_LeavesTheBodyUnsentAndCancelsTheStream()
    {
        FakeMultiplexedStream request = new(0, Response("500"));
        FakeMultiplexedConnection quic = new(request);
        RecordingTransferEvents events = new();
        Diagnostics.Arrange("server", "500 on stream 0 at once");

        TransferResult result = await StartAsync(quic, events, new FakeTimeProvider(DateTimeOffset.UnixEpoch), new BytesBody("x=1"u8.ToArray(), "application/x-www-form-urlencoded"));

        string all = string.Join('|', events.Events);
        Diagnostics.Act("exit code, abort code, events", $"{result.ExitCode}, {request.AbortCode}, {all}");
        Diagnostics.Assert("abort code", RequestCancelled, request.AbortCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        Assert.IsFalse(request.IsEndedByClient);
        Assert.AreEqual(RequestCancelled, request.AbortCode);
        Assert.IsNull(quic.CloseCode);
        AssertInOrder(events.Events, "< HTTP/3 500 \r\n", StopSending, "* abort upload", "< \r\n", LeftIntact);
    }

    private static byte[] Response(string status) =>
    [
        .. new Http3HeadersFrame(new QpackEncoder(0, 0).EncodeFieldSection(0, [new HeaderField(":status", status)])).ToBytes(),
        .. new Http3DataFrame("err\n"u8.ToArray()).ToBytes(),
    ];

    private static void AssertInOrder(List<string> events, params string[] expected)
    {
        int[] positions = [.. expected.Select(line => events.IndexOf(line))];
        string all = string.Join('|', events);

        Assert.IsFalse(positions.Contains(-1), all);
        CollectionAssert.AreEqual(positions.Order().ToArray(), positions, all);
    }

    private static Task<TransferResult> StartAsync(FakeMultiplexedConnection quic, RecordingTransferEvents events, FakeTimeProvider time, HttpRequestBody body)
    {
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("https://example.com/"),
            Output = new MemoryStream(),
            Events = events,
            TimeProvider = time,
            ConnectTimeout = TimeSpan.FromSeconds(1),
            Http = new HttpRequestOptions
            {
                Version = HttpVersionPreference.Http3Only,
                Headers = ["Expect: 100-continue"],
                Body = body,
            },
        };

        return new HttpProtocolHandler(new SessionConnector(quic), new SilentAuthenticator())
            .ExecuteAsync(context).AsTask();
    }

    /// <summary>A body of <see cref="BodyLength" /> zero bytes that lets the server answer as its second read starts.</summary>
    private sealed class AnsweringBody(TaskCompletionSource answer) : MemoryStream(new byte[BodyLength])
    {
        public int Reads { get; private set; }

        public int FirstReadLength { get; private set; }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Reads++;
            if (Reads == 2)
            {
                answer.SetResult();
            }

            int count = Read(buffer.Span);
            FirstReadLength = Reads == 1 ? count : FirstReadLength;
            return ValueTask.FromResult(count);
        }
    }

    /// <summary>Connects every transfer to a new HTTP/3 session on <paramref name="quic" />.</summary>
    private sealed class SessionConnector(FakeMultiplexedConnection quic) : IConnector
    {
        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask<ConnectResult> ConnectMultiplexedSessionAsync(ConnectTarget target, Func<IMultiplexedConnection, IConnection> openSession, CancellationToken cancellationToken)
        {
            IConnection session = openSession(quic);
            SessionHoldingConnection pooled = new(session);
            pooled.TryHoldSession((IConnectionSession)session);
            return ValueTask.FromResult(ConnectResult.Connected(pooled));
        }
    }
}
