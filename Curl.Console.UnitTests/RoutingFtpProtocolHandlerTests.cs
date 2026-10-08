using System.Text;
using Curl.Networking;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ftp;
using Curl.Testing;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Console;

/// <summary>
/// Pins which <c>ftp</c> transfers <see cref="RoutingFtpProtocolHandler" /> hands to the HTTP
/// handler (ADR-0056, rule 3): those through an HTTP or HTTP/1.0 proxy without <c>-p</c>; and
/// that every other goes to the FTP handler (ADR-0323).
/// </summary>
[TestClass]
public sealed class RoutingFtpProtocolHandlerTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(ProxyKind.Http)]
    [DataRow(ProxyKind.Http10)]
    public async Task ExecuteAsync_HttpProxyWithoutProxyTunnel_IsPerformedByTheHttpHandler(ProxyKind kind)
    {
        RecordingProtocolHandler http = RecordingProtocolHandler.WritingPath("http");
        RecordingProtocolHandler ftp = RecordingProtocolHandler.WritingPath("ftp");
        TransferContext context = ContextWith(new HttpRequestOptions { ForwardProxy = ProxyOf(kind) });

        TransferResult result = await new RoutingFtpProtocolHandler(http, ftp).ExecuteAsync(context);
        ActRun(http, ftp, result);

        Diagnostics.Assert("http / ftp handler calls", "1 / 0", $"{http.Contexts.Count} / {ftp.Contexts.Count}");
        Assert.IsTrue(result.IsSuccess);
        Assert.AreSame(context, http.Contexts.Single());
        Assert.IsEmpty(ftp.Contexts);
    }

    [TestMethod]
    [DataRow(ProxyKind.Http, true)]
    [DataRow(ProxyKind.Http10, true)]
    [DataRow(ProxyKind.Https, false)]
    [DataRow(ProxyKind.Socks5, false)]
    public async Task ExecuteAsync_ProxyThatIsNotForwardedThrough_IsPerformedByTheFtpHandler(ProxyKind kind, bool proxyTunnel)
    {
        TransferContext context = ContextWith(new HttpRequestOptions { ForwardProxy = ProxyOf(kind), ProxyTunnel = proxyTunnel });
        Diagnostics.Arrange("proxy tunnel", proxyTunnel);

        AssertPerformedByTheFtpHandler(context, await ExecuteWithRecordingHandlersAsync(context));
    }

    [TestMethod]
    public async Task ExecuteAsync_NoProxy_IsPerformedByTheFtpHandler()
    {
        TransferContext context = ContextWith(new HttpRequestOptions());

        AssertPerformedByTheFtpHandler(context, await ExecuteWithRecordingHandlersAsync(context));
    }

    [TestMethod]
    public async Task ExecuteAsync_NoHttpOptions_IsPerformedByTheFtpHandler()
    {
        TransferContext context = ContextWith(null);

        AssertPerformedByTheFtpHandler(context, await ExecuteWithRecordingHandlersAsync(context));
    }

    [TestMethod]
    public async Task ExecuteAsync_NoProxyWithTheFtpProtocolHandler_WritesTheServedFileAndSucceeds()
    {
        // The control replies arrive in one read, then the data connection serves the file
        // (the ADR-0323 conversation, as FtpProtocolHandlerTests records it).
        const string ControlReplies =
            "220 Recorder ready\r\n331 Password required\r\n230 Logged in\r\n257 \"/\" is current directory\r\n"
            + "229 Entering Extended Passive Mode (|||61744|)\r\n200 Type set\r\n213 5\r\n"
            + "150 Opening BINARY mode data connection\r\n226 Transfer complete\r\n221 Bye\r\n";
        ScriptedConnector server = new([Encoding.Latin1.GetBytes(ControlReplies), "hello"u8.ToArray()]);
        MemoryStream output = new();
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("ftp://127.0.0.1/file.txt"),
            Output = output,
        };
        RecordingProtocolHandler http = RecordingProtocolHandler.WritingPath("http");
        Diagnostics.Arrange("url", "ftp://127.0.0.1/file.txt");
        Diagnostics.Arrange("control replies", ControlReplies.ReplaceLineEndings("\n"));
        Diagnostics.Arrange("data connection serves", "hello");

        TransferResult result = await new RoutingFtpProtocolHandler(http, new FtpProtocolHandler(server)).ExecuteAsync(context);
        Diagnostics.Act("exit code", (int)result.ExitCode);
        Diagnostics.Act("output", Encoding.Latin1.GetString(output.ToArray()));
        Diagnostics.ActWritten(server);
        Diagnostics.Act("ports", string.Join(", ", server.Targets.Select(target => target.Port)));

        const string ExpectedCommands = "USER anonymous\r\nPASS ftp@example.com\r\nPWD\r\nEPSV\r\nTYPE I\r\nSIZE file.txt\r\nRETR file.txt\r\nQUIT\r\n";
        Diagnostics.AssertWritten(ExpectedCommands, server);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("hello", Encoding.Latin1.GetString(output.ToArray()));
        Assert.AreEqual(
            ExpectedCommands,
            Encoding.Latin1.GetString(server.Written));
        Assert.AreEqual(21, server.Targets[0].Port);
        Assert.AreEqual(61744, server.Targets[1].Port);
        Assert.IsEmpty(http.Contexts);
    }

    [TestMethod]
    public async Task ExecuteAsync_FtpsThroughHttpProxyWithoutProxyTunnel_IsPerformedByTheFtpHandler()
    {
        // curl 8.21.0 tunnels ftps://h/f through -x http://p with CONNECT h:990 (BL-458).
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("ftps://example.com/f.txt"),
            Output = new MemoryStream(),
            Http = new HttpRequestOptions { ForwardProxy = ProxyOf(ProxyKind.Http) },
        };
        ArrangeContext(context);

        AssertPerformedByTheFtpHandler(context, await ExecuteWithRecordingHandlersAsync(context));
    }

    [TestMethod]
    public void SupportedSchemes_IsTheFtpHandlersSchemes()
    {
        RoutingFtpProtocolHandler handler = new(
            RecordingProtocolHandler.WritingPath("http"),
            new FtpProtocolHandler(new ScriptedConnector([]), new TcpConnectionListener(), new PassThroughTlsProvider()));
        Diagnostics.Arrange("handlers", "recording http, real ftp");
        string schemes = string.Join(",", handler.SupportedSchemes);
        Diagnostics.Act("supported schemes", schemes);

        Diagnostics.Assert("supported schemes", "ftp,ftps", schemes);
        CollectionAssert.AreEqual(new[] { "ftp", "ftps" }, handler.SupportedSchemes.ToArray());
    }

    private async Task<(RecordingProtocolHandler Http, RecordingProtocolHandler Ftp, TransferResult Result)> ExecuteWithRecordingHandlersAsync(TransferContext context)
    {
        RecordingProtocolHandler http = RecordingProtocolHandler.WritingPath("http");
        RecordingProtocolHandler ftp = RecordingProtocolHandler.WritingPath("ftp");

        TransferResult result = await new RoutingFtpProtocolHandler(http, ftp).ExecuteAsync(context);
        ActRun(http, ftp, result);

        return (http, ftp, result);
    }

    private void AssertPerformedByTheFtpHandler(
        TransferContext context,
        (RecordingProtocolHandler Http, RecordingProtocolHandler Ftp, TransferResult Result) run)
    {
        Diagnostics.Assert("http / ftp handler calls", "0 / 1", $"{run.Http.Contexts.Count} / {run.Ftp.Contexts.Count}");
        Assert.IsTrue(run.Result.IsSuccess);
        Assert.AreSame(context, run.Ftp.Contexts.Single());
        Assert.IsEmpty(run.Http.Contexts);
    }

    private void ActRun(RecordingProtocolHandler http, RecordingProtocolHandler ftp, TransferResult result)
    {
        Diagnostics.Act("exit code", (int)result.ExitCode);
        Diagnostics.Act("http / ftp handler calls", $"{http.Contexts.Count} / {ftp.Contexts.Count}");
    }

    private static ProxyEndpoint ProxyOf(ProxyKind kind) => new(kind, "127.0.0.1", 1, null);

    private TransferContext ContextWith(HttpRequestOptions? http)
    {
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("ftp://example.com/f.txt"),
            Output = new MemoryStream(),
            Http = http,
        };
        ArrangeContext(context);
        return context;
    }

    private void ArrangeContext(TransferContext context)
    {
        Diagnostics.Arrange("url", context.Url.OriginalString);
        Diagnostics.Arrange("http options", context.Http is null ? "none" : "given");
        Diagnostics.Arrange("forward proxy", context.Http?.ForwardProxy?.Kind.ToString() ?? "none");
    }
}
