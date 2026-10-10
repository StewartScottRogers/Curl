using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ftp.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Ftp;

/// <summary>
/// Pins the control-connection lines <c>-v</c> and <c>--trace</c> see for FTP against curl
/// 8.21.0 (the Schannel build), measured on 2026-09-29 with <c>Record-CurlExchange.ps1 -Ftp</c>
/// (BL-930): every command sent is a request header with its CRLF, the password in clear,
/// and every reply line read is a response header with its line end, each line of a
/// multi-line reply on its own. <c>QUIT</c> and its reply are never reported.
/// </summary>
[TestClass]
public sealed class FtpProtocolHandlerHeaderEventTests
{
    public TestContext TestContext { get; set; } = null!;

    private const string Url = "ftp://127.0.0.1:18931/f.txt";

    private const string Greeting = "220 Recorder ready\r\n";

    private const string Epsv = "229 Entering Extended Passive Mode (|||60573|)\r\n";

    private const string Bye = "221 Bye\r\n";

    /// <summary>The replies from <c>PWD</c> through <c>RETR</c>'s <c>150</c> for the six-byte file.</summary>
    private const string PwdToOpened = "257 \"/\" is current directory\r\n" + Epsv + "200 Type set\r\n213 6\r\n150 Opening BINARY mode data connection\r\n";

    /// <summary>What curl reported from <c>PWD</c> through <c>RETR</c>'s <c>150</c>.</summary>
    private static readonly string[] PwdToOpenedHeaders =
    [
        "> PWD\r\n",
        "< 257 \"/\" is current directory\r\n",
        "> EPSV\r\n",
        "< " + Epsv,
        "> TYPE I\r\n",
        "< 200 Type set\r\n",
        "> SIZE f.txt\r\n",
        "< 213 6\r\n",
        "> RETR f.txt\r\n",
        "< 150 Opening BINARY mode data connection\r\n",
    ];

    [TestMethod]
    public async Task ExecuteAsync_AnonymousRetr_ReportsEveryCommandAndReplyButQuit()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("test", "ExecuteAsync_AnonymousRetr_ReportsEveryCommandAndReplyButQuit");

        // curl -v ftp://127.0.0.1:18931/f.txt
        var events = new RecordingTransferEvents();
        FtpRun run = await FtpRun.ExecuteAsync(
            Url,
            Greeting + "331 Password required\r\n230 Logged in\r\n" + PwdToOpened + "226 Transfer complete\r\n" + Bye,
            "hello\n",
            adjust: c => With(c, events));

        string[] expected =
        [
            "< 220 Recorder ready\r\n",
            "> USER anonymous\r\n",
            "< 331 Password required\r\n",
            "> PASS ftp@example.com\r\n",
            "< 230 Logged in\r\n",
            .. PwdToOpenedHeaders,
            "< 226 Transfer complete\r\n",
        ];
        diagnostics.Act("steps before the checks", "completed");
        diagnostics.Assert("outcome checked by the assertions below", "passes", "checking");
        CollectionAssert.AreEqual(expected, events.Headers);
        StringAssert.EndsWith(run.Sent, "QUIT\r\n", StringComparison.Ordinal);
        Assert.AreEqual(TransferResult.Success(6), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_UserAndPassword_ReportsThePasswordInClear()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("test", "ExecuteAsync_UserAndPassword_ReportsThePasswordInClear");

        // curl -v -u user:pw ftp://127.0.0.1:18932/f.txt
        var events = new RecordingTransferEvents();
        await FtpRun.ExecuteAsync(
            Url,
            Greeting + "331 Password required\r\n230 Logged in\r\n" + PwdToOpened + "226 Transfer complete\r\n" + Bye,
            "hello\n",
            c => With(c, events, new NetworkCredential("user", "pw")));

        diagnostics.Act("steps before the checks", "completed");
        diagnostics.Assert("outcome checked by the assertions below", "passes", "checking");
        CollectionAssert.AreEqual(
            new[] { "< 220 Recorder ready\r\n", "> USER user\r\n", "< 331 Password required\r\n", "> PASS pw\r\n", "< 230 Logged in\r\n" },
            events.Headers.Take(5).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_MultiLineReply_ReportsEachLine()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("test", "ExecuteAsync_MultiLineReply_ReportsEachLine");

        // curl -v ftp://127.0.0.1:18933/f.txt, PASS answered 230-Welcome, 230-Second line, 230 Logged in
        var events = new RecordingTransferEvents();
        await FtpRun.ExecuteAsync(
            Url,
            Greeting + "331 Password required\r\n230-Welcome\r\n230-Second line\r\n230 Logged in\r\n" + PwdToOpened + "226 Transfer complete\r\n" + Bye,
            "hello\n",
            adjust: c => With(c, events));

        diagnostics.Act("steps before the checks", "completed");
        diagnostics.Assert("outcome checked by the assertions below", "passes", "checking");
        CollectionAssert.AreEqual(
            new[] { "> PASS ftp@example.com\r\n", "< 230-Welcome\r\n", "< 230-Second line\r\n", "< 230 Logged in\r\n", "> PWD\r\n" },
            events.Headers.Skip(3).Take(5).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_Retr550_ReportsTheRefusalAndNotQuit()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("test", "ExecuteAsync_Retr550_ReportsTheRefusalAndNotQuit");

        // curl -v ftp://127.0.0.1:18934/f.txt, RETR answered 550 No such file: exit 78.
        var events = new RecordingTransferEvents();
        FtpRun run = await FtpRun.ExecuteAsync(
            Url,
            Greeting + "331 Password required\r\n230 Logged in\r\n" + PwdToOpened.Replace("150 Opening BINARY mode data connection", "550 No such file", StringComparison.Ordinal) + Bye,
            adjust: c => With(c, events));

        diagnostics.Act("steps before the checks", "completed");
        diagnostics.Assert("outcome checked by the assertions below", "passes", "checking");
        Assert.AreEqual("> RETR f.txt\r\n", events.Headers[^2]);
        Assert.AreEqual("< 550 No such file\r\n", events.Headers[^1]);
        Assert.HasCount(15, events.Headers);
        Assert.AreEqual(CurlExitCode.RemoteFileNotFound, run.Result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_SslReqdAndAuthRefused_ReportsBothAuthAttempts()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("test", "ExecuteAsync_SslReqdAndAuthRefused_ReportsBothAuthAttempts");

        // curl -v --ssl-reqd ftp://127.0.0.1:18935/f.txt, AUTH answered 500: exit 64.
        var events = new RecordingTransferEvents();
        FtpRun run = await FtpRun.ExecuteAsync(
            Url,
            Greeting + "500 AUTH not understood\r\n" + ScriptedConnection.NextRead + "500 AUTH not understood\r\n",
            adjust: c => MutableContext.Build(c, m =>
            {
                m.SslLevel = TransportSecurityLevel.Required;
                m.Events = events;
            }));

        diagnostics.Act("steps before the checks", "completed");
        diagnostics.Assert("outcome checked by the assertions below", "passes", "checking");
        CollectionAssert.AreEqual(
            new[] { "< 220 Recorder ready\r\n", "> AUTH SSL\r\n", "< 500 AUTH not understood\r\n", "> AUTH TLS\r\n", "< 500 AUTH not understood\r\n" },
            events.Headers);
        Assert.AreEqual(CurlExitCode.UseSslFailed, run.Result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_AuthAccepted_ReportsTheCommandsOnTheSecuredControl()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("test", "ExecuteAsync_AuthAccepted_ReportsTheCommandsOnTheSecuredControl");

        // curl -k -v --ssl-reqd: the login after AUTH SSL is reported as in plaintext.
        var events = new RecordingTransferEvents();
        var securedControl = new ScriptedConnection(Encoding.Latin1.GetBytes("331 Password required\r\n230 Logged in\r\n"));
        var connector = new QueuedConnector(ConnectResult.Connected(new ScriptedConnection(Encoding.Latin1.GetBytes(Greeting + "234 AUTH accepted\r\n"))));
        var tls = new QueuedTlsProvider(ConnectResult.Connected(securedControl));
        TransferContext context = MutableContext.Build(
            new TransferContext { Url = CurlUrl.Parse(Url), Output = new MemoryStream() },
            m =>
            {
                m.SslLevel = TransportSecurityLevel.Required;
                m.Events = events;
            });

        await new FtpProtocolHandler(connector, new QueuedListener(), tls).ExecuteAsync(context);

        diagnostics.Act("steps before the checks", "completed");
        diagnostics.Assert("outcome checked by the assertions below", "passes", "checking");
        CollectionAssert.AreEqual(
            new[] { "< 220 Recorder ready\r\n", "> AUTH SSL\r\n", "< 234 AUTH accepted\r\n", "> USER anonymous\r\n", "< 331 Password required\r\n", "> PASS ftp@example.com\r\n", "< 230 Logged in\r\n", "> PBSZ 0\r\n" },
            events.Headers.Take(8).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_AuthAccepted_ReportsTheHandshakeAndTheConnectionOpenedAgainBeforeUser()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("test", "ExecuteAsync_AuthAccepted_ReportsTheHandshakeAndTheConnectionOpenedAgainBeforeUser");

        // curl 8.21.0 -k -v --ssl-reqd: between "< 234 AUTH accepted" and USER it writes the TLS
        // lines and the connect's "Established connection" line again (measured, BL-1084).
        var events = new RecordingTransferEvents();
        var securedControl = new ScriptedConnection(Encoding.Latin1.GetBytes("331 Password required\r\n230 Logged in\r\n"));
        var connector = new OpenedReportingConnector(ConnectResult.Connected(new ScriptedConnection(Encoding.Latin1.GetBytes(Greeting + "234 AUTH accepted\r\n"))));
        var tls = new QueuedTlsProvider(ConnectResult.Connected(securedControl));
        TransferContext context = MutableContext.Build(
            new TransferContext { Url = CurlUrl.Parse(Url), Output = new MemoryStream() },
            m =>
            {
                m.SslLevel = TransportSecurityLevel.Required;
                m.Events = events;
            });

        await new FtpProtocolHandler(connector, new QueuedListener(), tls).ExecuteAsync(context);

        diagnostics.Act("steps before the checks", "completed");
        diagnostics.Assert("outcome checked by the assertions below", "passes", "checking");
        Assert.AreSame(events, tls.HandshakeEvents.Single());
        Assert.HasCount(2, events.ConnectionsOpened);
        Assert.AreSame(events.ConnectionsOpened[0], events.ConnectionsOpened[1]);
    }

    [TestMethod]
    public async Task ExecuteAsync_RangeEndsWithAbor_ReportsAborAndItsReplyButNotQuit()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("test", "ExecuteAsync_RangeEndsWithAbor_ReportsAborAndItsReplyButNotQuit");

        // curl -v -r 0-2 ftp://127.0.0.1:18990/f.txt: ABOR is reported, read 226; QUIT is not.
        var events = new RecordingTransferEvents();
        FtpRun run = await FtpRun.ExecuteAsync(
            Url,
            Greeting + "331 Password required\r\n230 Logged in\r\n" + PwdToOpened + "226 Transfer complete\r\n502 Command not implemented\r\n" + Bye,
            "hello\n",
            c => new TransferContext { Url = c.Url, Output = c.Output, Range = ByteRange.Bounded(0, 2), Events = events });

        diagnostics.Act("steps before the checks", "completed");
        diagnostics.Assert("outcome checked by the assertions below", "passes", "checking");
        Assert.AreEqual("> ABOR\r\n", events.Headers[^2]);
        Assert.AreEqual("< 226 Transfer complete\r\n", events.Headers[^1]);
        StringAssert.EndsWith(run.Sent, "ABOR\r\nQUIT\r\n", StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReplyCutOffMidLine_ReportsNoPartialLine()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("test", "ExecuteAsync_ReplyCutOffMidLine_ReportsNoPartialLine");

        var events = new RecordingTransferEvents();
        await FtpRun.ExecuteAsync(Url, "220 Recor", adjust: c => With(c, events));

        diagnostics.Act("steps before the checks", "completed");
        diagnostics.Assert("outcome checked by the assertions below", "passes", "checking");
        Assert.IsEmpty(events.Headers);
    }

    private static TransferContext With(TransferContext context, ITransferEvents events, NetworkCredential? credentials = null) =>
        new() { Url = context.Url, Output = context.Output, Events = events, Credentials = credentials };
}
