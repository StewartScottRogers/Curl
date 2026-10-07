using System.Net;
using System.Text;
using Curl.Http2;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins what an HTTP exchange writes to Curl's own diagnostic log (BL-922, ADR-0222): the
/// component by version, the milestones at <c>info</c>, header names at <c>verbose</c>, the
/// failure at <c>error</c>, and no credential in any line.
/// </summary>
public sealed partial class HttpProtocolHandlerTests
{
    private const string LogUrl = "http://127.0.0.1:18922/a?token=q";

    [TestMethod]
    public async Task ExecuteAsync_Http11GetAtInfo_LogsTheRequestTheReplyAndTheEndUnderHttp()
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Info);
        Diagnostics.Arrange("url, log level", $"{LogUrl}, Info");

        TransferResult result = await Handler(QueueConnector.For(Connection(Head + "hello", 65536)))
            .ExecuteAsync(LogContext(log));

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("every line under Http", true, log.Lines.All(line => line.Component == DiagnosticLogComponents.Http));
        Assert.IsTrue(log.Lines.All(line => line.Component == DiagnosticLogComponents.Http));
        string[] expectedInfo = ["GET /a sent", "reply 200 OK", "body framed by Content-Length 5", "exchange done: status 200, 5 body bytes in 0 ms"];
        WriteExpectedLines("info lines", expectedInfo, log.MessagesAt(DiagnosticLogLevel.Info));
        CollectionAssert.AreEqual(
            new[] { "GET /a sent", "reply 200 OK", "body framed by Content-Length 5", "exchange done: status 200, 5 body bytes in 0 ms" },
            log.MessagesAt(DiagnosticLogLevel.Info));
    }

    [TestMethod]
    public async Task ExecuteAsync_ChunkedBodyAtInfo_LogsItsFraming()
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Info);
        const string chunked = "HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n5\r\nhello\r\n0\r\n\r\n";

        Diagnostics.Arrange("url, log level, response", $"{LogUrl}, Info, chunked 200");

        TransferResult result = await Handler(QueueConnector.For(Connection(chunked, 65536))).ExecuteAsync(LogContext(log));

        WriteResult(result);
        Diagnostics.Assert("info lines contain", "body framed chunked", OneLine(string.Join(" | ", log.MessagesAt(DiagnosticLogLevel.Info))));
        CollectionAssert.Contains(log.MessagesAt(DiagnosticLogLevel.Info), "body framed chunked");
    }

    [TestMethod]
    public async Task ExecuteAsync_AtVerbose_LogsHeaderNamesAndTheVersionButNoAuthorizationValue()
    {
        RecordingDiagnosticLog log = new();
        HttpRequestOptions options = new() { Headers = ["Authorization: Bearer s3cret-token"] };

        Diagnostics.Arrange("url, log level, headers", $"{LogUrl}, Verbose, Authorization: Bearer s3cret-token");

        TransferResult result = await Handler(QueueConnector.For(Connection(Head + "hello", 65536))).ExecuteAsync(LogContext(log, options));

        WriteResult(result);
        string[] verbose = log.MessagesAt(DiagnosticLogLevel.Verbose);
        WriteEvents("verbose lines", verbose);
        Diagnostics.Assert("verbose lines contain", "header sent Authorization: (value not logged)", OneLine(string.Join(" | ", verbose)));
        CollectionAssert.Contains(verbose, "using HTTP/1.x on a new connection");
        CollectionAssert.Contains(verbose, "header sent Host: 127.0.0.1:18922");
        CollectionAssert.Contains(verbose, "header sent Authorization: (value not logged)");
        CollectionAssert.Contains(verbose, "header received Content-Type: text/plain");
        AssertNoLineContains(log, "s3cret-token");
    }

    [TestMethod]
    public async Task ExecuteAsync_Http2Exchange_LogsUnderHttp2()
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Info);
        HpackEncoder server = new();
        byte[] response = Http2Response(
            Http2FrameFactory.CreateHeaders(1, server.Encode([new(":status", "200"), new("content-length", "5")]), isEndStream: false, isEndHeaders: true),
            Http2FrameFactory.CreateData(1, "hello"u8.ToArray(), isEndStream: true));
        TransferContext context = new()
        {
            Url = CurlUrl.Parse(LogUrl),
            Output = new MemoryStream(),
            Http = new HttpRequestOptions { Version = HttpVersionPreference.Http2PriorKnowledge },
            DiagnosticLog = log,
            TimeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch),
        };
        Diagnostics.Arrange("url, log level, version", $"{LogUrl}, Info, Http2PriorKnowledge");

        TransferResult result = await Handler(QueueConnector.For(new ScriptedConnection(response, 65536)))
            .ExecuteAsync(context);

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("line count", ">0", log.Lines.Count);
        Assert.IsNotEmpty(log.Lines);
        Diagnostics.Assert("every line under Http2", true, log.Lines.All(line => line.Component == DiagnosticLogComponents.Http2));
        Assert.IsTrue(log.Lines.All(line => line.Component == DiagnosticLogComponents.Http2));
        CollectionAssert.Contains(log.MessagesAt(DiagnosticLogLevel.Info), "reply 200");
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnectionClosedMidBody_LogsErrorWithTheExitCode()
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Error);
        const string cutShort = "HTTP/1.1 200 OK\r\nContent-Length: 10\r\n\r\nhel";

        Diagnostics.Arrange("url, log level, response", $"{LogUrl}, Error, Content-Length 10 cut after 3 bytes");

        TransferResult result = await Handler(QueueConnector.For(Connection(cutShort, 65536))).ExecuteAsync(LogContext(log));

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.PartialFile, result.ExitCode);
        Assert.AreEqual(CurlExitCode.PartialFile, result.ExitCode);
        (DiagnosticLogLevel level, string component, string message) = log.Lines.Single();
        Diagnostics.Assert("level", DiagnosticLogLevel.Error, level);
        Assert.AreEqual(DiagnosticLogLevel.Error, level);
        Diagnostics.Assert("component", DiagnosticLogComponents.Http, component);
        Assert.AreEqual(DiagnosticLogComponents.Http, component);
        Diagnostics.Assert("message starts with", "exchange failed with PartialFile (exit 18): ", message);
        StringAssert.StartsWith(message, "exchange failed with PartialFile (exit 18): ");
    }

    [TestMethod]
    public async Task ExecuteAsync_AtError_RecordsNoInfoOrVerboseLine()
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Error);

        Diagnostics.Arrange("url, log level", $"{LogUrl}, Error");

        TransferResult result = await Handler(QueueConnector.For(Connection(Head + "hello", 65536))).ExecuteAsync(LogContext(log));

        WriteResult(result);
        Diagnostics.Assert("line count", 0, log.Lines.Count);
        Assert.IsEmpty(log.Lines);
    }

    [TestMethod]
    public async Task ExecuteAsync_BasicCredentialsAtVerbose_LogsNoSecret()
    {
        RecordingDiagnosticLog log = new();
        string basic = "Basic " + Convert.ToBase64String(Encoding.Latin1.GetBytes("user:s3cret"));
        TransferContext context = LogContext(log, credentials: new NetworkCredential("user", "s3cret"));

        Diagnostics.Arrange("url, log level, credentials", $"{LogUrl}, Verbose, user:(secret)");

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(Connection(Head + "hello", 65536)), new ScriptedAuthenticator(basic, null))
            .ExecuteAsync(context);

        WriteResult(result);
        Diagnostics.Assert("verbose lines contain", "header sent Authorization: (value not logged)", OneLine(string.Join(" | ", log.MessagesAt(DiagnosticLogLevel.Verbose))));
        CollectionAssert.Contains(log.MessagesAt(DiagnosticLogLevel.Verbose), "header sent Authorization: (value not logged)");
        AssertNoLineContains(log, "s3cret");
        AssertNoLineContains(log, basic["Basic ".Length..]);
    }

    [TestMethod]
    public async Task ExecuteAsync_BearerHeaderAtVerbose_LogsNoSecret()
    {
        RecordingDiagnosticLog log = new();
        HttpRequestOptions options = new() { Headers = ["Authorization: Bearer s3cret"] };

        Diagnostics.Arrange("url, log level, headers", $"{LogUrl}, Verbose, Authorization: Bearer (secret)");

        TransferResult result = await Handler(QueueConnector.For(Connection(Head + "hello", 65536))).ExecuteAsync(LogContext(log, options));

        WriteResult(result);
        Diagnostics.Assert("a line contains the secret", false, log.Lines.Any(line => line.Message.Contains("s3cret", StringComparison.Ordinal)));
        AssertNoLineContains(log, "s3cret");
    }

    [TestMethod]
    public async Task ExecuteAsync_CookieAtVerbose_LogsNoSecret()
    {
        RecordingDiagnosticLog log = new();
        const string reply = "HTTP/1.1 200 OK\r\nSet-Cookie: back=s3cret-too\r\nContent-Length: 0\r\n\r\n";

        Diagnostics.Arrange("url, log level, stored cookie", $"{LogUrl}, Verbose, name=(secret)");

        TransferResult result = await CookieHandler(QueueConnector.For(Connection(reply, 65536)), new ScriptedCookieStore("name=s3cret"))
            .ExecuteAsync(LogContext(log));

        WriteResult(result);
        Diagnostics.Assert("verbose lines contain", "header sent Cookie: (value not logged)", OneLine(string.Join(" | ", log.MessagesAt(DiagnosticLogLevel.Verbose))));
        CollectionAssert.Contains(log.MessagesAt(DiagnosticLogLevel.Verbose), "header sent Cookie: (value not logged)");
        CollectionAssert.Contains(log.MessagesAt(DiagnosticLogLevel.Verbose), "header received Set-Cookie: (value not logged)");
        AssertNoLineContains(log, "s3cret");
    }

    private static TransferContext LogContext(IDiagnosticLog log, HttpRequestOptions? options = null, NetworkCredential? credentials = null) =>
        new() { Url = CurlUrl.Parse(LogUrl), Output = new MemoryStream(), Http = options, DiagnosticLog = log, Credentials = credentials, TimeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch) };

    private static void AssertNoLineContains(RecordingDiagnosticLog log, string secret) =>
        Assert.IsFalse(log.Lines.Any(line => line.Message.Contains(secret, StringComparison.Ordinal)), string.Join('\n', log.Lines.Select(line => line.Message)));
}
