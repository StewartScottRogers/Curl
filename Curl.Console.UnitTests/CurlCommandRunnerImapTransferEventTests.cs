using System.Net;
using System.Text;
using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins <c>-v</c> and <c>--trace-ascii</c> for an IMAP transfer end to end, through the
/// production handler set over a scripted connection, against curl 8.21.0 (mingw, Schannel)
/// recorded on 2026-09-28 with <c>Record-CurlExchange.ps1 -Imap</c>, curl running
/// <c>-sv -u user:secret [-k --ssl-reqd] [-T mail.txt] imap://127.0.0.1:port/...</c> against
/// the recorder's default replies and message (BL-559 Notes). The connector reports the
/// <c>Trying</c> and <c>Established connection</c> lines with the ports curl used, as
/// <c>TcpConnector</c> does.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerImapTransferEventTests
{
    private const string InfoEnd = "\r\n";

    private const string HeaderEnd = "\r\r\n";

    private const string Greeting = "* OK [CAPABILITY IMAP4rev1 STARTTLS AUTH=PLAIN AUTH=LOGIN] ready\r\n";

    private const string CapabilityReply = "* CAPABILITY IMAP4rev1 STARTTLS AUTH=PLAIN AUTH=LOGIN\r\nA001 OK CAPABILITY completed\r\n";

    private const string Message =
        "From: sender@example.com\r\nTo: recipient@example.com\r\nSubject: Recorded\r\n\r\nHello from the recorder.\r\n";

    private const string Upload = "From: a@b\r\nSubject: hi\r\n\r\nbody text\r\n";

    private readonly MemoryStream standardOutput = new();

    private readonly MemoryStream standardError = new();

    private readonly InMemoryFileSystem files = new();

    [TestMethod]
    public async Task RunAsync_VerboseUidFetchWithAuthPlain_WritesTheMeasuredLines()
    {
        int exitCode = await RunAsync(["-sv"], "INBOX;UID=1", 18143, 59446, [Greeting, CapabilityReply, .. AuthPlain("A002"), Select("A003"), Fetch("A004"), Logout("A005")]);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(
            Opened(18143, 59446, Greeting)
            + Headers("< ", CapabilityReply)
            + AuthPlainLines("A002")
            + SelectAndFetchLines("A003", "A004", 18143),
            Encoding.ASCII.GetString(standardError.ToArray()));
        Assert.AreEqual(Message, Encoding.ASCII.GetString(standardOutput.ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_VerboseUidFetchWithLogin_WritesThePasswordUnmasked()
    {
        const string greeting = "* OK ready\r\n";
        const string capabilityReply = "* CAPABILITY IMAP4rev1\r\nA001 OK done\r\n";

        int exitCode = await RunAsync(
            ["-sv"], "INBOX;UID=1", 18144, 59449, [greeting, capabilityReply, "A002 OK LOGIN completed\r\n", Select("A003"), Fetch("A004"), Logout("A005")]);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(
            Opened(18144, 59449, greeting)
            + Headers("< ", capabilityReply)
            + "> A002 LOGIN user secret" + HeaderEnd
            + "< A002 OK LOGIN completed" + HeaderEnd
            + SelectAndFetchLines("A003", "A004", 18144),
            Encoding.ASCII.GetString(standardError.ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_VerboseAppend_WritesTheLiteralSentAndTheLineEndAfterIt()
    {
        files.ExistingContent["mail.txt"] = Encoding.ASCII.GetBytes(Upload);

        int exitCode = await RunAsync(
            ["-sv", "-T", "mail.txt"],
            "Sent",
            18145,
            59450,
            [Greeting, CapabilityReply, .. AuthPlain("A002"), "+ Ready for literal data\r\n", "A003 OK APPEND completed\r\n", Logout("A004")]);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(
            Opened(18145, 59450, Greeting)
            + Headers("< ", CapabilityReply)
            + AuthPlainLines("A002")
            + "> A003 APPEND Sent (\\Seen) {37}" + HeaderEnd
            + "< + Ready for literal data" + HeaderEnd
            + "} [37 bytes data]" + InfoEnd
            + "* upload completely sent off: 37 bytes" + InfoEnd
            + "> " + HeaderEnd
            + "< A003 OK APPEND completed" + HeaderEnd
            + "* Connection #0 to host 127.0.0.1:18145 left intact" + InfoEnd,
            Encoding.ASCII.GetString(standardError.ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_VerboseUidFetchWithStartTls_WritesTheSessionLinesAroundTheUpgrade()
    {
        // curl also writes two "schannel:" lines and a second "Established connection" line
        // between "< A002 OK Begin TLS negotiation now" and the second CAPABILITY; BL-806 adds them.
        const string secureCapabilityReply = "* CAPABILITY IMAP4rev1 AUTH=PLAIN AUTH=LOGIN\r\nA003 OK CAPABILITY completed\r\n";

        int exitCode = await RunAsync(
            ["-sv", "-k", "--ssl-reqd"],
            "INBOX;UID=1",
            18146,
            59452,
            [Greeting, CapabilityReply, "A002 OK Begin TLS negotiation now\r\n", secureCapabilityReply, .. AuthPlain("A004"), Select("A005"), Fetch("A006"), Logout("A007")]);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(
            Opened(18146, 59452, Greeting)
            + Headers("< ", CapabilityReply)
            + "> A002 STARTTLS" + HeaderEnd
            + "< A002 OK Begin TLS negotiation now" + HeaderEnd
            + "> A003 CAPABILITY" + HeaderEnd
            + Headers("< ", secureCapabilityReply)
            + AuthPlainLines("A004")
            + SelectAndFetchLines("A005", "A006", 18146),
            Encoding.ASCII.GetString(standardError.ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_TraceAsciiUidFetch_WritesTheMeasuredDumpAndTheLiteralOnStandardOutput()
    {
        int exitCode = await RunAsync(
            ["--trace-ascii", "-"], "INBOX;UID=1", 18147, 59453, [Greeting, CapabilityReply, .. AuthPlain("A002"), Select("A003"), Fetch("A004"), Logout("A005")]);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(
            "*   Trying 127.0.0.1:18147...\n"
            + "* Established connection to 127.0.0.1 (127.0.0.1 port 18147) from 127.0.0.1 port 59453 \n"
            + "<= Recv header, 66 bytes (0x42)\n0000: * OK [CAPABILITY IMAP4rev1 STARTTLS AUTH=PLAIN AUTH=LOGIN] ready\n"
            + "=> Send header, 17 bytes (0x11)\n0000: A001 CAPABILITY\n"
            + "<= Recv header, 55 bytes (0x37)\n0000: * CAPABILITY IMAP4rev1 STARTTLS AUTH=PLAIN AUTH=LOGIN\n"
            + "<= Recv header, 30 bytes (0x1e)\n0000: A001 OK CAPABILITY completed\n"
            + "=> Send header, 25 bytes (0x19)\n0000: A002 AUTHENTICATE PLAIN\n"
            + "<= Recv header, 4 bytes (0x4)\n0000: + \n"
            + "=> Send header, 18 bytes (0x12)\n0000: AHVzZXIAc2VjcmV0\n"
            + "<= Recv header, 23 bytes (0x17)\n0000: A002 OK Authenticated\n"
            + "=> Send header, 19 bytes (0x13)\n0000: A003 SELECT INBOX\n"
            + "<= Recv header, 52 bytes (0x34)\n0000: * FLAGS (\\Answered \\Flagged \\Deleted \\Seen \\Draft)\n"
            + "<= Recv header, 12 bytes (0xc)\n0000: * 2 EXISTS\n"
            + "<= Recv header, 12 bytes (0xc)\n0000: * 0 RECENT\n"
            + "<= Recv header, 33 bytes (0x21)\n0000: * OK [UIDVALIDITY 1] UIDs valid\n"
            + "<= Recv header, 37 bytes (0x25)\n0000: * OK [UIDNEXT 3] Predicted next UID\n"
            + "<= Recv header, 39 bytes (0x27)\n0000: A003 OK [READ-WRITE] SELECT completed\n"
            + "=> Send header, 25 bytes (0x19)\n0000: A004 UID FETCH 1 BODY[]\n"
            + "<= Recv header, 31 bytes (0x1f)\n0000: * 1 FETCH (UID 1 BODY[] {100}\n"
            + "* Found 100 bytes to download\n"
            + "<= Recv data, 100 bytes (0x64)\n"
            + "0000: From: sender@example.com\n"
            + "001a: To: recipient@example.com\n"
            + "0035: Subject: Recorded\n"
            + "0048: \n"
            + "004a: Hello from the recorder.\n"
            + Message
            + "* Written 100 bytes, 0 bytes are left for transfer\n"
            + "<= Recv header, 3 bytes (0x3)\n0000: )\n"
            + "<= Recv header, 25 bytes (0x19)\n0000: A004 OK FETCH completed\n"
            + "* Connection #0 to host 127.0.0.1:18147 left intact\n",
            Encoding.ASCII.GetString(standardOutput.ToArray()));
    }

    private static string[] AuthPlain(string tag) => ["+ \r\n", tag + " OK Authenticated\r\n"];

    private static string Select(string tag) =>
        "* FLAGS (\\Answered \\Flagged \\Deleted \\Seen \\Draft)\r\n* 2 EXISTS\r\n* 0 RECENT\r\n"
        + "* OK [UIDVALIDITY 1] UIDs valid\r\n* OK [UIDNEXT 3] Predicted next UID\r\n"
        + tag + " OK [READ-WRITE] SELECT completed\r\n";

    private static string Fetch(string tag) => "* 1 FETCH (UID 1 BODY[] {100}\r\n" + Message + ")\r\n" + tag + " OK FETCH completed\r\n";

    private static string Logout(string tag) => "* BYE Logging out\r\n" + tag + " OK LOGOUT completed\r\n";

    private static string Opened(int port, int localPort, string greeting) =>
        $"*   Trying 127.0.0.1:{port}..." + InfoEnd
        + $"* Established connection to 127.0.0.1 (127.0.0.1 port {port}) from 127.0.0.1 port {localPort} " + InfoEnd
        + "< " + greeting[..^2] + HeaderEnd
        + "> A001 CAPABILITY" + HeaderEnd;

    /// <summary>Writes each CRLF-ended line of <paramref name="lines" /> as curl's <c>-v</c> does.</summary>
    private static string Headers(string prefix, string lines) =>
        string.Concat(lines.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Select(line => prefix + line + HeaderEnd));

    private static string AuthPlainLines(string tag) =>
        $"> {tag} AUTHENTICATE PLAIN" + HeaderEnd
        + "< + " + HeaderEnd
        + "> AHVzZXIAc2VjcmV0" + HeaderEnd
        + $"< {tag} OK Authenticated" + HeaderEnd;

    private static string SelectAndFetchLines(string selectTag, string fetchTag, int port) =>
        $"> {selectTag} SELECT INBOX" + HeaderEnd
        + Headers("< ", Select(selectTag))
        + $"> {fetchTag} UID FETCH 1 BODY[]" + HeaderEnd
        + "< * 1 FETCH (UID 1 BODY[] {100}" + HeaderEnd
        + "* Found 100 bytes to download" + InfoEnd
        + "{ [100 bytes data]" + InfoEnd
        + "* Written 100 bytes, 0 bytes are left for transfer" + InfoEnd
        + "< )" + HeaderEnd
        + $"< {fetchTag} OK FETCH completed" + HeaderEnd
        + $"* Connection #0 to host 127.0.0.1:{port} left intact" + InfoEnd;

    private Task<int> RunAsync(IReadOnlyList<string> options, string path, int port, int localPort, IReadOnlyList<string> replies)
    {
        var connector = new ReportingConnector(new ScriptedConnector([.. replies.Select(Encoding.ASCII.GetBytes)]), localPort);

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
            .RunAsync([.. options, "-u", "user:secret", $"imap://127.0.0.1:{port}/{path}"]);
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
