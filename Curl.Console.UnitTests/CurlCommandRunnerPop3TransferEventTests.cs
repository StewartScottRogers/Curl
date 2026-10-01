using System.Net;
using System.Text;
using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins <c>-v</c> and <c>--trace-ascii</c> for a POP3 transfer end to end, through the
/// production handler set over a scripted connection, against curl 8.21.0 (mingw, Schannel)
/// recorded on 2026-09-28 with <c>Record-CurlExchange.ps1 -Pop3</c>, curl running
/// <c>-sv -u user:secret [-k --ssl-reqd] pop3://127.0.0.1:port/1</c> against the recorder's
/// default replies and message (BL-552 Notes). The connector reports the <c>Trying</c> and
/// <c>Established connection</c> lines with the ports curl used, as <c>TcpConnector</c> does.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerPop3TransferEventTests
{
    private const string InfoEnd = "\r\n";

    private const string HeaderEnd = "\r\r\n";

    private const string Greeting = "+OK POP3 ready <1896.697170952@localhost>\r\n";

    private const string CapaReply = "+OK Capability list follows\r\nUSER\r\nSASL PLAIN LOGIN\r\nSTLS\r\nTOP\r\nUIDL\r\n.\r\n";

    private const string SecureCapaReply = "+OK Capability list follows\r\nUSER\r\nSASL PLAIN LOGIN\r\nTOP\r\nUIDL\r\n.\r\n";

    private const string RetrReply =
        "+OK 133 octets\r\nFrom: sender@example.com\r\nTo: recipient@example.com\r\nSubject: Recorded\r\n\r\n"
        + "Hello from the recorder.\r\n..A line that starts with a dot.\r\n.\r\n";

    private const string Message =
        "From: sender@example.com\r\nTo: recipient@example.com\r\nSubject: Recorded\r\n\r\nHello from the recorder.\r\n.A line that starts with a dot.\r\n";

    private const string Bye = "+OK Bye\r\n";

    private static readonly string[] AuthPlainReplies = ["+ \r\n", "+OK Authenticated\r\n"];

    private readonly MemoryStream standardOutput = new();

    private readonly MemoryStream standardError = new();

    [TestMethod]
    public async Task RunAsync_VerboseRetrWithAuthPlain_WritesTheMeasuredLines()
    {
        int exitCode = await RunAsync(["-sv"], 18110, 64805, [Greeting, CapaReply, .. AuthPlainReplies, RetrReply, Bye]);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(
            Opened(18110, 64805, Greeting)
            + Headers("< ", CapaReply)
            + AuthPlainAndRetr(18110),
            Encoding.ASCII.GetString(standardError.ToArray()));
        Assert.AreEqual(Message, Encoding.ASCII.GetString(standardOutput.ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_VerboseRetrWithUserAndPass_WritesThePasswordUnmasked()
    {
        const string greeting = "+OK POP3 ready\r\n";
        const string capaReply = "+OK\r\nUSER\r\n.\r\n";

        int exitCode = await RunAsync(
            ["-sv"], 18114, 52451, [greeting, capaReply, "+OK User accepted\r\n", "+OK Logged in\r\n", RetrReply, Bye]);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(
            Opened(18114, 52451, greeting)
            + Headers("< ", capaReply)
            + "> USER user" + HeaderEnd
            + "< +OK User accepted" + HeaderEnd
            + "> PASS secret" + HeaderEnd
            + "< +OK Logged in" + HeaderEnd
            + RetrTail(18114),
            Encoding.ASCII.GetString(standardError.ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_VerboseLoginWithNoMechanism_WritesTheSaslLineAndClosesWithExit67()
    {
        // Recorded on 2026-09-30 with -Pop3Reply 'GREETING=+OK POP3 ready','CAPA=+OK\r\nTOP\r\n.' (BL-810 Notes).
        const string greeting = "+OK POP3 ready\r\n";
        const string capaReply = "+OK\r\nTOP\r\n.\r\n";

        int exitCode = await RunAsync(["-sv"], 18411, 49971, [greeting, capaReply]);

        Assert.AreEqual(67, exitCode);
        Assert.AreEqual(
            Opened(18411, 49971, greeting)
            + Headers("< ", capaReply)
            + "* SASL: no auth mechanism was offered or recognized" + InfoEnd
            + "* closing connection #0" + InfoEnd,
            Encoding.ASCII.GetString(standardError.ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_VerboseRetrWithStls_WritesTheSessionLinesAroundTheUpgrade()
    {
        // curl also writes two "schannel:" lines and a second "Established connection" line
        // between "< +OK Begin TLS negotiation" and the second CAPA; BL-806 adds them.
        int exitCode = await RunAsync(
            ["-sv", "-k", "--ssl-reqd"],
            18112,
            64807,
            [Greeting, CapaReply, "+OK Begin TLS negotiation\r\n", SecureCapaReply, .. AuthPlainReplies, RetrReply, Bye]);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(
            Opened(18112, 64807, Greeting)
            + Headers("< ", CapaReply)
            + "> STLS" + HeaderEnd
            + "< +OK Begin TLS negotiation" + HeaderEnd
            + "> CAPA" + HeaderEnd
            + Headers("< ", SecureCapaReply)
            + AuthPlainAndRetr(18112),
            Encoding.ASCII.GetString(standardError.ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_TraceAsciiRetr_WritesTheMeasuredDumpBetweenTheBodyPiecesOnStandardOutput()
    {
        int exitCode = await RunAsync(["--trace-ascii", "-"], 18113, 64808, [Greeting, CapaReply, .. AuthPlainReplies, RetrReply, Bye]);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(
            "*   Trying 127.0.0.1:18113...\n"
            + "* Established connection to 127.0.0.1 (127.0.0.1 port 18113) from 127.0.0.1 port 64808 \n"
            + "<= Recv header, 43 bytes (0x2b)\n0000: +OK POP3 ready <1896.697170952@localhost>\n"
            + "=> Send header, 6 bytes (0x6)\n0000: CAPA\n"
            + "<= Recv header, 29 bytes (0x1d)\n0000: +OK Capability list follows\n"
            + "<= Recv header, 6 bytes (0x6)\n0000: USER\n"
            + "<= Recv header, 18 bytes (0x12)\n0000: SASL PLAIN LOGIN\n"
            + "<= Recv header, 6 bytes (0x6)\n0000: STLS\n"
            + "<= Recv header, 5 bytes (0x5)\n0000: TOP\n"
            + "<= Recv header, 6 bytes (0x6)\n0000: UIDL\n"
            + "<= Recv header, 3 bytes (0x3)\n0000: .\n"
            + "=> Send header, 12 bytes (0xc)\n0000: AUTH PLAIN\n"
            + "<= Recv header, 4 bytes (0x4)\n0000: + \n"
            + "=> Send header, 18 bytes (0x12)\n0000: AHVzZXIAc2VjcmV0\n"
            + "<= Recv header, 19 bytes (0x13)\n0000: +OK Authenticated\n"
            + "=> Send header, 8 bytes (0x8)\n0000: RETR 1\n"
            + "<= Recv header, 16 bytes (0x10)\n0000: +OK 133 octets\n"
            + "<= Recv data, 24 bytes (0x18)\n0000: From: sender@example.com\nFrom: sender@example.com"
            + "<= Recv data, 2 bytes (0x2)\n0000: \n\r\n"
            + "<= Recv data, 25 bytes (0x19)\n0000: To: recipient@example.com\nTo: recipient@example.com"
            + "<= Recv data, 2 bytes (0x2)\n0000: \n\r\n"
            + "<= Recv data, 17 bytes (0x11)\n0000: Subject: Recorded\nSubject: Recorded"
            + "<= Recv data, 2 bytes (0x2)\n0000: \n\r\n"
            + "<= Recv data, 2 bytes (0x2)\n0000: \n\r\n"
            + "<= Recv data, 24 bytes (0x18)\n0000: Hello from the recorder.\nHello from the recorder."
            + "<= Recv data, 2 bytes (0x2)\n0000: \n\r\n"
            + "<= Recv data, 31 bytes (0x1f)\n0000: .A line that starts with a dot.\n.A line that starts with a dot."
            + "<= Recv data, 2 bytes (0x2)\n0000: \n\r\n"
            + "* Connection #0 to host 127.0.0.1:18113 left intact\n",
            Encoding.ASCII.GetString(standardOutput.ToArray()));
    }

    private static string Opened(int port, int localPort, string greeting) =>
        $"*   Trying 127.0.0.1:{port}..." + InfoEnd
        + $"* Established connection to 127.0.0.1 (127.0.0.1 port {port}) from 127.0.0.1 port {localPort} " + InfoEnd
        + "< " + greeting[..^2] + HeaderEnd
        + "> CAPA" + HeaderEnd;

    /// <summary>Writes each CRLF-ended line of <paramref name="lines" /> as curl's <c>-v</c> does.</summary>
    private static string Headers(string prefix, string lines) =>
        string.Concat(lines.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Select(line => prefix + line + HeaderEnd));

    private static string AuthPlainAndRetr(int port) =>
        "> AUTH PLAIN" + HeaderEnd
        + "< + " + HeaderEnd
        + "> AHVzZXIAc2VjcmV0" + HeaderEnd
        + "< +OK Authenticated" + HeaderEnd
        + RetrTail(port);

    private static string RetrTail(int port) =>
        "> RETR 1" + HeaderEnd
        + "< +OK 133 octets" + HeaderEnd
        + "{ [24 bytes data]" + InfoEnd
        + $"* Connection #0 to host 127.0.0.1:{port} left intact" + InfoEnd;

    private Task<int> RunAsync(IReadOnlyList<string> options, int port, int localPort, IReadOnlyList<string> replies)
    {
        var connector = new ReportingConnector(new ScriptedConnector([.. replies.Select(Encoding.ASCII.GetBytes)]), localPort);
        var files = new InMemoryFileSystem();

        return new CurlCommandRunner(
                _ => new TransferDispatch(
                    new ProtocolDispatcher(CurlComposition.CreateProtocolHandlers(
                        connector, new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"), new PassThroughTlsProvider(), new LoopbackDnsResolver()))),
                files,
                files,
                standardOutput,
                standardError,
                new MemoryStream(),
                runsOnWindows: true)
            .RunAsync([.. options, "-u", "user:secret", $"pop3://127.0.0.1:{port}/1"]);
    }

    /// <summary>
    /// Reports the <c>Trying</c> and <c>Established connection</c> lines for each connect, as
    /// <c>TcpConnector</c> does, then connects through <paramref name="inner" />.
    /// </summary>
    private sealed class ReportingConnector(IConnector inner, int localPort) : IConnector
    {
        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
        {
            target.Events.ReportInfo($"  Trying {target.Host}:{target.Port}...");
            target.Events.ReportConnectionOpened(new ConnectionOpenedEvent
            {
                HostName = target.Host,
                RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, target.Port),
                LocalEndPoint = new IPEndPoint(IPAddress.Loopback, localPort),
                ConnectionNumber = 0,
            });
            return inner.ConnectAsync(target, cancellationToken);
        }
    }
}
