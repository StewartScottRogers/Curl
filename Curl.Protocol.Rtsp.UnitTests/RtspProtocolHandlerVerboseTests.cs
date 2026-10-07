using System.Net.Sockets;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Rtsp.Fakes;
using Curl.Testing;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Rtsp;

/// <summary>
/// Pins what an <c>rtsp://</c> transfer reports for <c>-v</c> and <c>--trace</c>, and whether it
/// hands its connection back for reuse, as curl 8.21.0 does (measured with
/// <c>Record-CurlExchange.ps1</c>, BL-593 Notes): the request as one header event and
/// <c>Request completely sent off</c>, each head line, the blank line after any <c>-f</c>
/// refusal, the body as received data, each failure's message, then <c>left intact</c>,
/// <c>shutting down</c> or <c>closing</c>.
/// </summary>
[TestClass]
public sealed class RtspProtocolHandlerVerboseTests
{
    private const string Request = "OPTIONS * RTSP/1.0\r\nCSeq: 1\r\nUser-Agent: curl/8.21.0\r\n\r\n";

    private const string RequestSent = "* Request completely sent off";

    private const string LeftIntact = "* Connection #0 to host 127.0.0.1:47950 left intact";

    /// <summary>Gets or sets the running test's context, which carries its diagnostics.</summary>
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ExecuteAsync_OkReply_ReportsRequestHeadAndLeftIntactAndMarksReusable()
    {
        ScriptedConnection server = Server("RTSP/1.0 200 OK\r\nCSeq: 1\r\nPublic: OPTIONS, DESCRIBE\r\n\r\n");

        (TransferResult result, List<string> transcript) = await RunAsync(server);

        Diagnostics.AssertExitCode(CurlExitCode.Ok, result);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "> " + Request, RequestSent, "< RTSP/1.0 200 OK\r\n", "< CSeq: 1\r\n", "< Public: OPTIONS, DESCRIBE\r\n", "< \r\n", LeftIntact },
            transcript);
        Assert.IsTrue(server.IsMarkedReusable);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReusedConnection_SendsCSeq0AndNamesItsConnectionNumber()
    {
        ScriptedConnection server = Server("RTSP/1.0 200 OK\r\nCSeq: 0\r\n\r\n");
        ConnectResult reused = ConnectResult.Connected(server, null, isReused: true, connectionNumber: 3);

        (TransferResult result, List<string> transcript) = await RunAsync(reused);

        Diagnostics.AssertExitCode(CurlExitCode.Ok, result);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("OPTIONS * RTSP/1.0\r\nCSeq: 0\r\nUser-Agent: curl/8.21.0\r\n\r\n", Encoding.Latin1.GetString(server.Sent));
        Assert.AreEqual("* Connection #3 to host 127.0.0.1:47950 left intact", transcript[^1]);
        Assert.IsTrue(server.IsMarkedReusable);
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnectTarget_IsPooledUnderRtsp()
    {
        var connector = new RecordingConnector(ConnectResult.Connected(Server("RTSP/1.0 200 OK\r\nCSeq: 1\r\n\r\n")));

        TransferResult result = await new RtspProtocolHandler(connector, new RecordingAuthenticator()).ExecuteAsync(Context(new RecordingTransferEvents()));

        Diagnostics.ActResult(result);
        Diagnostics.Act("connect target", connector.Targets.Single());
        Diagnostics.Assert("pool scheme", "rtsp", connector.Targets.Single().PoolScheme);
        Assert.AreEqual("rtsp", connector.Targets.Single().PoolScheme);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReplyWithBody_ReportsEachPieceAndShutsTheConnectionDown()
    {
        ScriptedConnection server = new(Bytes("RTSP/1.0 200 OK\r\nCSeq: 1\r\nContent-Length: 2\r\n\r\no"), Bytes("k"));

        (TransferResult result, List<string> transcript) = await RunAsync(server);

        Diagnostics.AssertExitCode(CurlExitCode.Ok, result);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(new[] { "< \r\n", "{ 1", "{ 1", "* shutting down connection #0" }, transcript[^4..]);
        Assert.IsFalse(server.IsMarkedReusable);
    }

    [TestMethod]
    public async Task ExecuteAsync_FailOn404_ReportsTheRefusalBeforeTheBlankLineThenClosing()
    {
        ScriptedConnection server = Server("RTSP/1.0 404 Not Found\r\nCSeq: 1\r\n\r\n");

        (TransferResult result, List<string> transcript) = await RunAsync(server, new HttpRequestOptions { Fail = HttpFailMode.Fail });

        Diagnostics.AssertExitCode(CurlExitCode.HttpReturnedError, result);
        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "< CSeq: 1\r\n", "* The requested URL returned error: 404", "< \r\n", "* closing connection #0" },
            transcript[^4..]);
        Assert.IsFalse(server.IsMarkedReusable);
    }

    [TestMethod]
    public async Task ExecuteAsync_StatusBelow100_ReportsTheBlankLineThenTheMessageThenClosing()
    {
        ScriptedConnection server = Server("RTSP/1.0 099 X\r\nCSeq: 1\r\n\r\n");

        (TransferResult result, List<string> transcript) = await RunAsync(server);

        Diagnostics.AssertExitCode(CurlExitCode.UnsupportedProtocol, result);
        Assert.AreEqual(CurlExitCode.UnsupportedProtocol, result.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "< \r\n", "* Unsupported response code in HTTP response", "* closing connection #0" },
            transcript[^3..]);
    }

    [TestMethod]
    public async Task ExecuteAsync_CSeqMismatch_ReportsTheMessageAndLeavesTheConnectionIntact()
    {
        ScriptedConnection server = Server("RTSP/1.0 200 OK\r\nCSeq: 7\r\n\r\n");

        (TransferResult result, List<string> transcript) = await RunAsync(server);

        Diagnostics.AssertExitCode(CurlExitCode.RtspCseqError, result);
        Assert.AreEqual(CurlExitCode.RtspCseqError, result.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "< \r\n", "* The CSeq of this request 1 did not match the response 7", LeftIntact },
            transcript[^3..]);
        Assert.IsTrue(server.IsMarkedReusable);
    }

    [TestMethod]
    public async Task ExecuteAsync_CSeqMismatchWithBody_ShutsTheConnectionDown()
    {
        ScriptedConnection server = Server("RTSP/1.0 200 OK\r\nCSeq: 7\r\nContent-Length: 2\r\n\r\nok");

        (TransferResult result, List<string> transcript) = await RunAsync(server);

        Diagnostics.AssertExitCode(CurlExitCode.RtspCseqError, result);
        Assert.AreEqual(CurlExitCode.RtspCseqError, result.ExitCode);
        Assert.AreEqual("* shutting down connection #0", transcript[^1]);
        Assert.IsFalse(server.IsMarkedReusable);
    }

    [TestMethod]
    public async Task ExecuteAsync_HeadCutShort_ReportsTheUnreadBytesAsALineAndLeavesTheConnectionIntact()
    {
        ScriptedConnection server = Server("RTSP/1.0 200 OK\r\nCSeq: 1\r\n");

        (TransferResult result, List<string> transcript) = await RunAsync(server);

        Diagnostics.AssertExitCode(CurlExitCode.RtspCseqError, result);
        Assert.AreEqual(CurlExitCode.RtspCseqError, result.ExitCode);
        CollectionAssert.AreEqual(
            new[] { RequestSent, "< RTSP/1.0 200 OK\r\n", "< CSeq: 1\r\n", "* The CSeq of this request 1 did not match the response 0", LeftIntact },
            transcript[1..]);
    }

    [TestMethod]
    public async Task ExecuteAsync_NotRtsp_ReportsEmptyReplyAndShutsTheConnectionDown()
    {
        ScriptedConnection server = Server("HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n");

        (TransferResult result, List<string> transcript) = await RunAsync(server);

        Diagnostics.AssertExitCode(CurlExitCode.GotNothing, result);
        Assert.AreEqual(CurlExitCode.GotNothing, result.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "> " + Request, RequestSent, "* Empty reply from server", "* shutting down connection #0" },
            transcript);
        Assert.IsFalse(server.IsMarkedReusable);
    }

    [TestMethod]
    public async Task ExecuteAsync_SessionContradicted_ReportsTheKeptLinesThenTheMessageThenClosing()
    {
        ScriptedConnection server = Server("RTSP/1.0 200 OK\r\nCSeq: 1\r\nSession: a\r\nSession: b\r\n\r\n");

        (TransferResult result, List<string> transcript) = await RunAsync(server);

        Diagnostics.AssertExitCode(CurlExitCode.RtspSessionError, result);
        Assert.AreEqual(CurlExitCode.RtspSessionError, result.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "< Session: a\r\n", "* Got RTSP Session ID Line [b\r\n], but wanted ID [a]", "* closing connection #0" },
            transcript[^3..]);
    }

    [TestMethod]
    [DataRow("CSeq: 5", CurlExitCode.RtspCseqError, "CSeq cannot be set as a custom header.")]
    [DataRow("Session: 5", CurlExitCode.BadFunctionArgument, "Session ID cannot be set as a custom header.")]
    public async Task ExecuteAsync_RefusedCustomHeader_ReportsTheMessageAndLeavesTheConnectionIntact(string header, CurlExitCode exitCode, string message)
    {
        ScriptedConnection server = Server("RTSP/1.0 200 OK\r\nCSeq: 1\r\n\r\n");

        (TransferResult result, List<string> transcript) = await RunAsync(server, new HttpRequestOptions { Headers = [header] });

        Diagnostics.AssertExitCode(exitCode, result);
        Assert.AreEqual(exitCode, result.ExitCode);
        CollectionAssert.AreEqual(new[] { "* " + message, LeftIntact }, transcript);
        Assert.IsTrue(server.IsMarkedReusable);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DataRow(SocketError.ConnectionReset, "Send failure: Connection was reset")]
    [DataRow(SocketError.ConnectionAborted, "Send failure: Connection was aborted")]
    public async Task ExecuteAsync_SendSocketError_ReportsTheRequestTheWinsockWordsFailedSendingRtspRequestAndClosing(SocketError error, string message)
    {
        var failure = new IOException("send", new SocketException((int)error));

        (TransferResult result, List<string> transcript) = await RunAsync(ConnectResult.Connected(new FailingConnection(writeFailure: failure)));

        Diagnostics.AssertExitCode(CurlExitCode.SendError, result);
        Assert.AreEqual(CurlExitCode.SendError, result.ExitCode);
        Assert.AreEqual(message, result.ErrorMessage);
        CollectionAssert.AreEqual(
            new[] { "> " + Request, "* " + message, "* Failed sending RTSP request", "* closing connection #0" },
            transcript);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    [DataRow(SocketError.ConnectionReset)]
    [DataRow(SocketError.ConnectionAborted)]
    public async Task ExecuteAsync_SendSocketError_ReportsTheRequestTheSocketErrorsOwnMessageFailedSendingRtspRequestAndClosing(SocketError error)
    {
        var socketError = new SocketException((int)error);
        string message = "Send failure: " + socketError.Message;

        (TransferResult result, List<string> transcript) = await RunAsync(
            ConnectResult.Connected(new FailingConnection(writeFailure: new IOException("send", socketError))));

        Diagnostics.AssertExitCode(CurlExitCode.SendError, result);
        Assert.AreEqual(CurlExitCode.SendError, result.ExitCode);
        Assert.AreEqual(message, result.ErrorMessage);
        CollectionAssert.AreEqual(
            new[] { "> " + Request, "* " + message, "* Failed sending RTSP request", "* closing connection #0" },
            transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_SendFailsOtherwise_ReportsTheMessageFailedSendingRtspRequestAndClosing()
    {
        var broken = new IOException("broken pipe");

        (TransferResult result, List<string> transcript) = await RunAsync(ConnectResult.Connected(new FailingConnection(writeFailure: broken)));

        Diagnostics.AssertExitCode(CurlExitCode.SendError, result);
        Assert.AreEqual(CurlExitCode.SendError, result.ExitCode);
        Assert.AreEqual("Failed sending data to the peer", result.ErrorMessage);
        CollectionAssert.AreEqual(
            new[] { "> " + Request, "* Failed sending data to the peer", "* Failed sending RTSP request", "* closing connection #0" },
            transcript);
    }

    private Task<(TransferResult Result, List<string> Transcript)> RunAsync(ScriptedConnection server, HttpRequestOptions? http = null) =>
        RunAsync(ConnectResult.Connected(server), http);

    private async Task<(TransferResult Result, List<string> Transcript)> RunAsync(ConnectResult connect, HttpRequestOptions? http = null)
    {
        var events = new RecordingTransferEvents();
        Diagnostics.Arrange("connection", $"{connect.Connection?.GetType().Name}, reused {connect.IsReused}, number {connect.ConnectionNumber}");
        TransferResult result = await new RtspProtocolHandler(new RecordingConnector(connect), new RecordingAuthenticator())
            .ExecuteAsync(Context(events, http));
        Diagnostics.ActResult(result);
        if (connect.Connection is ScriptedConnection server)
        {
            Diagnostics.ActSent(server.Sent);
            Diagnostics.Act("marked reusable", server.IsMarkedReusable);
        }

        Diagnostics.ActTranscript(events.Transcript);
        return (result, events.Transcript);
    }

    private TransferContext Context(ITransferEvents events, HttpRequestOptions? http = null)
    {
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("rtsp://127.0.0.1:47950/media"),
            Output = new MemoryStream(),
            Http = http,
            Events = events,
        };
        Diagnostics.ArrangeContext(context);
        return context;
    }

    private ScriptedConnection Server(string reply)
    {
        Diagnostics.ArrangeReply(reply);
        return new(Bytes(reply));
    }

    private static byte[] Bytes(string text) => Encoding.Latin1.GetBytes(text);
}
