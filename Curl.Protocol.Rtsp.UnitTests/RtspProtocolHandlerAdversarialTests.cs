using System.Globalization;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Rtsp.Fakes;
using Curl.Testing;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Rtsp;

/// <summary>
/// Attacks <see cref="RtspProtocolHandler" /> through its public surface only, by the method in
/// <c>Documentation/Wiki/Adversarial-Testing.md</c> (BL-1515): <c>CSeq</c>, status, length and
/// head-size boundaries, malformed header lines, every request method, and replies cut at every
/// offset, handlers reused and run on many tasks at once. The oracle is the handler's documented
/// contract (ADR-0169), itself measured against curl 8.21.0.
/// </summary>
[TestClass]
public sealed class RtspProtocolHandlerAdversarialTests
{
    private const string Request = "OPTIONS * RTSP/1.0\r\nCSeq: 1\r\nUser-Agent: curl/8.21.0\r\n\r\n";

    private const string Ok = "RTSP/1.0 200 OK\r\nCSeq: 1\r\n\r\n";

    // The longest reply head the handler reads, in bytes (RtspProtocolHandler's documented limit).
    private const int MaximumHeadLength = 102400;

    /// <summary>Gets or sets the running test's context, which carries its diagnostics.</summary>
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ExecuteAsync_ReplyCSeqZeroOnAFreshConnection_FailsWith85NamingZero()
    {
        TransferResult result = await Handler(Server("RTSP/1.0 200 OK\r\nCSeq: 0\r\n\r\n")).ExecuteAsync(Context());

        Diagnostics.ActResult(result);
        Diagnostics.AssertExitCode(CurlExitCode.RtspCseqError, result);
        Assert.AreEqual(CurlExitCode.RtspCseqError, result.ExitCode);
        Assert.AreEqual("The CSeq of this request 1 did not match the response 0", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReusedConnectionAnsweredWithCSeqZero_Succeeds()
    {
        ScriptedConnection server = Server("RTSP/1.0 200 OK\r\nCSeq: 0\r\n\r\n");

        TransferResult result = await ReusedHandler(server).ExecuteAsync(Context());

        Diagnostics.ActResult(result);
        Diagnostics.AssertExitCode(CurlExitCode.Ok, result);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        StringAssert.Contains(Encoding.Latin1.GetString(server.Sent), "\r\nCSeq: 0\r\n");
    }

    [TestMethod]
    public async Task ExecuteAsync_ReusedConnectionAnsweredWithCSeqOne_FailsWith85()
    {
        TransferResult result = await ReusedHandler(Server(Ok)).ExecuteAsync(Context());

        Diagnostics.ActResult(result);
        Diagnostics.AssertExitCode(CurlExitCode.RtspCseqError, result);
        Assert.AreEqual(CurlExitCode.RtspCseqError, result.ExitCode);
        Assert.AreEqual("The CSeq of this request 0 did not match the response 1", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReplyCSeqAtLongMaxValue_FailsWith85NamingIt()
    {
        TransferResult result = await Handler(Server("RTSP/1.0 200 OK\r\nCSeq: 9223372036854775807\r\n\r\n")).ExecuteAsync(Context());

        Diagnostics.ActResult(result);
        Diagnostics.AssertExitCode(CurlExitCode.RtspCseqError, result);
        Assert.AreEqual("The CSeq of this request 1 did not match the response 9223372036854775807", result.ErrorMessage);
    }

    [TestMethod]
    [DataRow("099", CurlExitCode.UnsupportedProtocol)]
    [DataRow("100", CurlExitCode.Ok)]
    [DataRow("399", CurlExitCode.Ok)]
    [DataRow("400", CurlExitCode.Ok)]
    [DataRow("999", CurlExitCode.Ok)]
    public async Task ExecuteAsync_StatusAtEachBoundaryWithoutFail_EndsWithItsExitCode(string status, CurlExitCode expected)
    {
        TransferResult result = await Handler(Server("RTSP/1.0 " + status + " X\r\nCSeq: 1\r\n\r\n")).ExecuteAsync(Context());

        Diagnostics.ActResult(result);
        Diagnostics.AssertExitCode(expected, result);
        Assert.AreEqual(expected, result.ExitCode);
        Assert.AreEqual(int.Parse(status, CultureInfo.InvariantCulture), result.Report!.ResponseCode);
    }

    [TestMethod]
    [DataRow("399", CurlExitCode.Ok)]
    [DataRow("400", CurlExitCode.HttpReturnedError)]
    [DataRow("999", CurlExitCode.HttpReturnedError)]
    public async Task ExecuteAsync_StatusAtEachBoundaryUnderFail_EndsWithItsExitCode(string status, CurlExitCode expected)
    {
        TransferResult result = await Handler(Server("RTSP/1.0 " + status + " X\r\nCSeq: 1\r\n\r\n"))
            .ExecuteAsync(Context(http: new HttpRequestOptions { Fail = HttpFailMode.Fail }));

        Diagnostics.ActResult(result);
        Diagnostics.AssertExitCode(expected, result);
        Assert.AreEqual(expected, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_ContentLengthZeroWithBytesAfterTheHead_ReadsNoBody()
    {
        TransferResult result = await Handler(Server("RTSP/1.0 200 OK\r\nCSeq: 1\r\nContent-Length: 0\r\n\r\nextra")).ExecuteAsync(Context());

        Diagnostics.ActResult(result);
        Diagnostics.AssertExitCode(CurlExitCode.Ok, result);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(0L, result.Report!.DownloadSize);
    }

    [TestMethod]
    public async Task ExecuteAsync_ContentLengthAtLongMaxValueThenClose_SucceedsWithTheBytesReceived()
    {
        TransferResult result = await Handler(Server("RTSP/1.0 200 OK\r\nCSeq: 1\r\nContent-Length: 9223372036854775807\r\n\r\nabcde")).ExecuteAsync(Context());

        Diagnostics.ActResult(result);
        Diagnostics.AssertExitCode(CurlExitCode.Ok, result);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(5L, result.Report!.DownloadSize);
    }

    [TestMethod]
    public async Task ExecuteAsync_ContentLengthOnePastLongMaxValue_LeavesNoBodyAndDoesNotKeepTheConnection()
    {
        ScriptedConnection server = Server("RTSP/1.0 200 OK\r\nCSeq: 1\r\nContent-Length: 9223372036854775808\r\n\r\nabcde");
        var events = new RecordingTransferEvents();

        TransferResult result = await Handler(server).ExecuteAsync(Context(events: events));

        Diagnostics.ActResult(result);
        Diagnostics.AssertExitCode(CurlExitCode.Ok, result);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(0L, result.Report!.DownloadSize);
        Assert.IsFalse(server.IsMarkedReusable);
        CollectionAssert.Contains(events.Info, "Overflow Content-Length: value");
    }

    [TestMethod]
    [DataRow(4L, CurlExitCode.Ok)]
    [DataRow(5L, CurlExitCode.Ok)]
    [DataRow(6L, CurlExitCode.FilesizeExceeded)]
    public async Task ExecuteAsync_ContentLengthAroundMaxFileSize_FailsOnlyOverTheLimit(long contentLength, CurlExitCode expected)
    {
        string reply = "RTSP/1.0 200 OK\r\nCSeq: 1\r\nContent-Length: " + contentLength.ToString(CultureInfo.InvariantCulture) + "\r\n\r\n" + new string('b', (int)contentLength);
        TransferContext context = Context(maxFileSize: 5);

        TransferResult result = await Handler(Server(reply)).ExecuteAsync(context);

        Diagnostics.ActResult(result);
        Diagnostics.AssertExitCode(expected, result);
        Assert.AreEqual(expected, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_HeadOfExactlyTheLimit_IsReadWhole()
    {
        string reply = HeadOfLength(MaximumHeadLength);

        TransferResult result = await Handler(Server(reply)).ExecuteAsync(Context());

        Diagnostics.ActResult(result);
        Diagnostics.AssertExitCode(CurlExitCode.Ok, result);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual((long)MaximumHeadLength, result.Report!.HeaderSize);
    }

    [TestMethod]
    public async Task ExecuteAsync_HeadOneByteOverTheLimit_FailsWith100()
    {
        TransferResult result = await Handler(Server(HeadOfLength(MaximumHeadLength + 1))).ExecuteAsync(Context());

        Diagnostics.ActResult(result);
        Diagnostics.AssertExitCode(CurlExitCode.TooLarge, result);
        Assert.AreEqual(CurlExitCode.TooLarge, result.ExitCode);
        Assert.AreEqual("A value or data field grew larger than allowed", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_ManyShortHeaderLinesUnderTheLimit_AreReadWhole()
    {
        var reply = new StringBuilder("RTSP/1.0 200 OK\r\nCSeq: 1\r\n");
        for (int index = 0; index < 5000; index++)
        {
            reply.Append("X: y\r\n");
        }

        TransferResult result = await Handler(Server(reply.Append("\r\n").ToString())).ExecuteAsync(Context());

        Diagnostics.ActResult(result);
        Diagnostics.AssertExitCode(CurlExitCode.Ok, result);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    [TestMethod]
    [DataRow("NoColonHere\r\n", "Header without colon")]
    [DataRow("X: a\rb\r\n", "Carriage return found in header")]
    [DataRow("X: a\0b\r\n", "Nul byte in header")]
    [DataRow("Content-Length: 2, 3\r\n", "Invalid Content-Length: value")]
    [DataRow("Content-Length: 2\r\nContent-Length: 3\r\n", "Invalid Content-Length: value")]
    [DataRow("Content-Length: -1\r\n", "Invalid Content-Length: value")]
    [DataRow("Location: /a\r\nLocation: /b\r\n", "Multiple Location headers")]
    public async Task ExecuteAsync_MalformedHeaderLine_FailsWith8(string lines, string message)
    {
        TransferResult result = await Handler(Server("RTSP/1.0 200 OK\r\nCSeq: 1\r\n" + lines + "\r\n")).ExecuteAsync(Context());

        Diagnostics.ActResult(result);
        Diagnostics.AssertExitCode(CurlExitCode.WeirdServerReply, result);
        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        Assert.AreEqual(message, result.ErrorMessage);
    }

    [TestMethod]
    [DataRow("Content-Length: 2, 2\r\n")]
    [DataRow("Content-Length: 2\r\nContent-Length: 2\r\n")]
    [DataRow("Location: /a\r\nLocation: /a\r\n")]
    public async Task ExecuteAsync_RepeatedHeaderThatAgrees_Succeeds(string lines)
    {
        TransferResult result = await Handler(Server("RTSP/1.0 200 OK\r\nCSeq: 1\r\n" + lines + "\r\nab")).ExecuteAsync(Context());

        Diagnostics.ActResult(result);
        Diagnostics.AssertExitCode(CurlExitCode.Ok, result);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_SessionIdChangingCaseOfTheHeaderName_FailsWith86()
    {
        TransferResult result = await Handler(Server("RTSP/1.0 200 OK\r\nCSeq: 1\r\nSESSION: a\r\nsession: b\r\n\r\n")).ExecuteAsync(Context());

        Diagnostics.ActResult(result);
        Diagnostics.AssertExitCode(CurlExitCode.RtspSessionError, result);
        Assert.AreEqual(CurlExitCode.RtspSessionError, result.ExitCode);
        Assert.AreEqual("Got RTSP Session ID Line [b\r\n], but wanted ID [a]", result.ErrorMessage);
    }

    [TestMethod]
    [DataRow("OPTIONS")]
    [DataRow("DESCRIBE")]
    [DataRow("ANNOUNCE")]
    [DataRow("SETUP")]
    [DataRow("PLAY")]
    [DataRow("PAUSE")]
    [DataRow("TEARDOWN")]
    [DataRow("GET_PARAMETER")]
    [DataRow("SET_PARAMETER")]
    [DataRow("RECORD")]
    [DataRow("RECEIVE")]
    [DataRow("")]
    [DataRow("BOGUS METHOD")]
    public async Task ExecuteAsync_EveryRequestMethod_StillSendsOptionsStar(string method)
    {
        ScriptedConnection server = Server(Ok);

        TransferResult result = await Handler(server).ExecuteAsync(Context(http: new HttpRequestOptions { CustomMethod = method }));

        Diagnostics.ActResult(result);
        Diagnostics.DiffText("request sent", Request, server.Sent);
        Assert.AreEqual(Request, Encoding.Latin1.GetString(server.Sent));
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReplyWithBodySplitInTwoAtEveryOffset_ReadsTheSameHeadAndBody()
    {
        const string reply = "RTSP/1.0 200 OK\r\nCSeq: 1\r\nSession: s;timeout=60\r\nContent-Length: 4\r\n\r\nbody";
        byte[] bytes = Encoding.Latin1.GetBytes(reply);
        for (int offset = 1; offset < bytes.Length; offset++)
        {
            var headers = new MemoryStream();
            var server = new ScriptedConnection(bytes[..offset], bytes[offset..]);

            TransferResult result = await Handler(server).ExecuteAsync(Context(headerOutput: headers));

            Diagnostics.Act("split offset", offset);
            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"split at {offset}");
            Assert.AreEqual(4L, result.Report!.DownloadSize, $"split at {offset}");
            Assert.AreEqual(reply[..^4], Encoding.Latin1.GetString(headers.ToArray()), $"split at {offset}");
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_ReplyCutAtEveryOffset_NeverThrows()
    {
        byte[] bytes = Encoding.Latin1.GetBytes("RTSP/1.0 200 OK\r\nCSeq: 1\r\nSession: s\r\nContent-Length: 4\r\n\r\nbody");
        for (int length = 0; length <= bytes.Length; length++)
        {
            ScriptedConnection server = length == 0 ? new ScriptedConnection() : new ScriptedConnection(bytes[..length]);

            TransferResult result = await Handler(server).ExecuteAsync(Context());

            Diagnostics.Act("cut after", $"{length} bytes: exit {result.ExitCode}");
            Assert.IsTrue(
                result.ExitCode is CurlExitCode.Ok or CurlExitCode.GotNothing or CurlExitCode.WeirdServerReply or CurlExitCode.RtspCseqError,
                $"cut after {length} bytes ended with {result.ExitCode}");
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_SameHandlerForTwoTransfers_StartsEachAtCSeqOne()
    {
        var first = new ScriptedConnection(Encoding.Latin1.GetBytes("RTSP/1.0 200 OK\r\nCSeq: 1\r\nSession: a\r\n\r\n"));
        var second = new ScriptedConnection(Encoding.Latin1.GetBytes("RTSP/1.0 200 OK\r\nCSeq: 1\r\nSession: b\r\n\r\n"));
        var connector = new QueuedConnector(first, second);
        var handler = new RtspProtocolHandler(connector, new RecordingAuthenticator());

        TransferResult firstResult = await handler.ExecuteAsync(Context());
        TransferResult secondResult = await handler.ExecuteAsync(Context());

        Diagnostics.ActResult(secondResult);
        Assert.AreEqual(CurlExitCode.Ok, firstResult.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, secondResult.ExitCode);
        Assert.AreEqual(Request, Encoding.Latin1.GetString(second.Sent));
    }

    [TestMethod]
    public async Task ExecuteAsync_SixteenTransfersAtOnceOnOneHandler_AllMatchTheirOwnReplies()
    {
        const int count = 16;
        var servers = new ScriptedConnection[count];
        for (int index = 0; index < count; index++)
        {
            string cseq = index % 2 == 0 ? "1" : "2";
            servers[index] = new ScriptedConnection(Encoding.Latin1.GetBytes("RTSP/1.0 200 OK\r\nCSeq: " + cseq + "\r\nSession: s" + index.ToString(CultureInfo.InvariantCulture) + "\r\n\r\n"));
        }

        var handler = new RtspProtocolHandler(new QueuedConnector(servers), new RecordingAuthenticator());
        TransferContext[] contexts = [.. Enumerable.Range(0, count).Select(_ => Context())];

        TransferResult[] results = await Task.WhenAll(contexts.Select(context => Task.Run(async () => await handler.ExecuteAsync(context))));

        Diagnostics.Act("exit codes", string.Join(", ", results.Select(result => result.ExitCode)));
        Assert.AreEqual(count / 2, results.Count(result => result.ExitCode == CurlExitCode.Ok));
        Assert.AreEqual(count / 2, results.Count(result => result.ExitCode == CurlExitCode.RtspCseqError));
        Assert.IsTrue(servers.All(server => Encoding.Latin1.GetString(server.Sent) == Request));
    }

    [TestMethod]
    public async Task ExecuteAsync_CancelledWhileTheReplyIsAwaited_ThrowsAndDisposesTheConnection()
    {
        using var cancellation = new CancellationTokenSource();
        var server = new CancellingConnection(cancellation);
        TransferContext context = Context(cancellationToken: cancellation.Token);

        OperationCanceledException thrown = await Assert.ThrowsAsync<OperationCanceledException>(async () => await Handler(server).ExecuteAsync(context));

        Diagnostics.Act("thrown", thrown.GetType().Name);
        Assert.IsTrue(server.IsDisposed);
    }

    // A head of exactly length bytes: the status line, CSeq and one filler header.
    private static string HeadOfLength(int length)
    {
        const string start = "RTSP/1.0 200 OK\r\nCSeq: 1\r\nX: ";
        const string end = "\r\n\r\n";
        return start + new string('a', length - start.Length - end.Length) + end;
    }

    private RtspProtocolHandler Handler(IConnection connection)
    {
        Diagnostics.Arrange("connection", connection.GetType().Name);
        return new(new RecordingConnector(ConnectResult.Connected(connection)), new RecordingAuthenticator());
    }

    private RtspProtocolHandler ReusedHandler(IConnection connection)
    {
        Diagnostics.Arrange("connection", $"{connection.GetType().Name}, reused");
        return new(new RecordingConnector(ConnectResult.Connected(connection, null, isReused: true)), new RecordingAuthenticator());
    }

    private ScriptedConnection Server(string reply)
    {
        Diagnostics.ArrangeReply(reply);
        return new ScriptedConnection(Encoding.Latin1.GetBytes(reply));
    }

    private TransferContext Context(
        Stream? headerOutput = null,
        HttpRequestOptions? http = null,
        RecordingTransferEvents? events = null,
        long? maxFileSize = null,
        CancellationToken cancellationToken = default)
    {
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("rtsp://127.0.0.1:47950/media"),
            Output = new MemoryStream(),
            HeaderOutput = headerOutput,
            Http = http,
            Events = events ?? (ITransferEvents)NoTransferEvents.Instance,
            MaxFileSize = maxFileSize,
            CancellationToken = cancellationToken,
        };
        Diagnostics.ArrangeContext(context);
        return context;
    }

    /// <summary>Hands out one connection per call, in order, from any thread.</summary>
    private sealed class QueuedConnector(params IConnection[] connections) : IConnector
    {
        private int next = -1;

        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            ValueTask.FromResult(ConnectResult.Connected(connections[Interlocked.Increment(ref next)]));
    }

    /// <summary>Takes the request, then cancels the transfer when the reply is read for.</summary>
    private sealed class CancellingConnection(CancellationTokenSource cancellation) : IConnection
    {
        public bool IsDisposed { get; private set; }

        public bool IsSecure => false;

        public System.Net.EndPoint? RemoteEndPoint => null;

        public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            await cancellation.CancelAsync();
            cancellationToken.ThrowIfCancellationRequested();
            return 0;
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync()
        {
            IsDisposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
