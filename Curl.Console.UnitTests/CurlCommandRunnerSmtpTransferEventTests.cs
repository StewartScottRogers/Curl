using System.Net;
using System.Text;
using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins <c>-v</c> and <c>--trace-ascii</c> for an SMTP transfer end to end, through the
/// production handler set over a scripted connection, against curl 8.21.0 (mingw, Schannel)
/// recorded on 2026-09-28 with <c>Record-CurlExchange.ps1 -Smtp</c>, curl running
/// <c>-sv [-u user:secret] [-k --ssl-reqd] --mail-from a@b --mail-rcpt c@d -T mail.txt smtp://127.0.0.1:port/client</c>
/// with <c>mail.txt</c> holding <c>Subject: hi CRLF CRLF Hello CRLF</c> (BL-546 Notes). The
/// connector reports the <c>Trying</c> and <c>Established connection</c> lines with the ports
/// curl used, as <c>TcpConnector</c> does.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerSmtpTransferEventTests
{
    private const string InfoEnd = "\r\n";

    private const string HeaderEnd = "\r\r\n";

    private const string Message = "Subject: hi\r\n\r\nHello\r\n";

    private const string Greeting = "220 localhost ESMTP\r\n";

    private const string EhloReply =
        "250-localhost\r\n250-AUTH PLAIN LOGIN CRAM-MD5\r\n250-STARTTLS\r\n250-SIZE 1000000\r\n250-8BITMIME\r\n250 SMTPUTF8\r\n";

    private const string SecureEhloReply =
        "250-localhost\r\n250-AUTH PLAIN LOGIN CRAM-MD5\r\n250-SIZE 1000000\r\n250-8BITMIME\r\n250 SMTPUTF8\r\n";

    private const string Transaction =
        "250 OK\r\n250 OK\r\n354 End data with <CR><LF>.<CR><LF>\r\n250 OK message accepted\r\n221 Bye\r\n";

    private readonly MemoryStream standardOutput = new();

    private readonly MemoryStream standardError = new();

    private readonly InMemoryFileSystem files = new();

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private string StandardOutputText => Encoding.ASCII.GetString(standardOutput.ToArray());

    private string StandardErrorText => Encoding.ASCII.GetString(standardError.ToArray());

    public CurlCommandRunnerSmtpTransferEventTests()
    {
        files.ExistingContent["mail.txt"] = Encoding.ASCII.GetBytes(Message);
    }

    [TestMethod]
    public async Task RunAsync_VerboseMailUpload_WritesTheMeasuredLines()
    {
        int exitCode = await RunAsync(["-sv"], 18030, 53686, Greeting + EhloReply + Transaction);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        string expected = (
            Opened(18030, 53686)
            + Headers("< ", EhloReply)
            + "> MAIL FROM:<a@b> SIZE=22" + HeaderEnd
            + UploadTail(18030));
        Diagnostics.Diff("stderr", Lf(expected), Lf(StandardErrorText));
        Assert.AreEqual(expected, Encoding.ASCII.GetString(standardError.ToArray()));
    }

    [TestMethod]
    [DataRow("smtp")]
    [DataRow("protocol")]
    public async Task RunAsync_VerboseMailUploadWithTheSmtpTraceComponent_WritesTheSmtpLinesAmongTheVerboseLines(string component)
    {
        // Measured 2026-10-02 (BL-1163 Notes); "all" also turns the time and id prefixes on, so it is
        // pinned by CurlCompositionSmtpTraceTests.
        int exitCode = await RunAsync(["-sv", "--trace-config", component], 18030, 53686, Greeting + EhloReply + Transaction);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        string[] lines = Encoding.ASCII.GetString(standardError.ToArray()).Split(InfoEnd);
        Diagnostics.Assert("first 7 lines", "* [SMTP] smtp_setup_connection() -> 0|*   Trying 127.0.0.1:18030...|* Established connection to 127.0.0.1 (127.0.0.1 port 18030) from 127.0.0.1 port 53686 |* [SMTP] state change from STOP to SERVERGREET|< 220 localhost ESMTP|> EHLO client|* [SMTP] state change from SERVERGREET to EHLO", string.Join("|", lines.Take(7).Select(line => line.TrimEnd((char)13))));
        Diagnostics.Assert("last 4 lines", "* [SMTP] state change from POSTDATA to STOP|* [SMTP] smtp_done(status=0, premature=0) -> 0|* Connection #0 to host 127.0.0.1:18030 left intact|", string.Join("|", lines.TakeLast(4)));
        CollectionAssert.AreEqual(
            (string[])[
                "* [SMTP] smtp_setup_connection() -> 0",
                "*   Trying 127.0.0.1:18030...",
                "* Established connection to 127.0.0.1 (127.0.0.1 port 18030) from 127.0.0.1 port 53686 ",
                "* [SMTP] state change from STOP to SERVERGREET",
                "< 220 localhost ESMTP\r",
                "> EHLO client\r",
                "* [SMTP] state change from SERVERGREET to EHLO",
            ],
            lines.Take(7).ToArray());
        CollectionAssert.AreEqual(
            (string[])[
                "* [SMTP] state change from POSTDATA to STOP",
                "* [SMTP] smtp_done(status=0, premature=0) -> 0",
                "* Connection #0 to host 127.0.0.1:18030 left intact",
                string.Empty,
            ],
            lines.TakeLast(4).ToArray());
        Diagnostics.Assert("[SMTP] line count", 23, lines.Count(line => line.StartsWith("* [SMTP] ", StringComparison.Ordinal)));
        Assert.AreEqual(23, lines.Count(line => line.StartsWith("* [SMTP] ", StringComparison.Ordinal)));
    }

    [TestMethod]
    [DataRow("-sv", "--trace-config", "ftp")]
    [DataRow("-sv")]
    [DataRow("-s", "--trace-config", "smtp")]
    public async Task RunAsync_MailUploadWithoutTheSmtpTraceComponentOrVerbose_WritesNoSmtpLines(params string[] arguments)
    {
        int exitCode = await RunAsync(arguments, 18030, 53686, Greeting + EhloReply + Transaction);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Assert("stderr contains [SMTP]", false, StandardErrorText.Contains("[SMTP]", StringComparison.Ordinal));
        Assert.DoesNotContain("[SMTP]", Encoding.ASCII.GetString(standardError.ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_VerboseMailUploadWithAuthPlain_WritesTheCredentialsUnmasked()
    {
        const string ehloReply = "250-localhost\r\n250 AUTH PLAIN\r\n";

        int exitCode = await RunAsync(
            ["-sv", "-u", "user:secret"], 18029, 53685, Greeting + ehloReply + "334 \r\n235 Authentication successful\r\n" + Transaction);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        string expected = (
            Opened(18029, 53685)
            + Headers("< ", ehloReply)
            + "> AUTH PLAIN" + HeaderEnd
            + "< 334 " + HeaderEnd
            + "> AHVzZXIAc2VjcmV0" + HeaderEnd
            + "< 235 Authentication successful" + HeaderEnd
            + "> MAIL FROM:<a@b>" + HeaderEnd
            + UploadTail(18029));
        Diagnostics.Diff("stderr", Lf(expected), Lf(StandardErrorText));
        Assert.AreEqual(expected, Encoding.ASCII.GetString(standardError.ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_VerboseLoginWithNoMechanism_WritesTheSaslLineAndClosesWithExit67()
    {
        // Recorded on 2026-10-01 with -u user:secret and -SmtpReply 'EHLO=250-localhost\r\n250 AUTH FOO' (BL-1061 Notes).
        const string ehloReply = "250-localhost\r\n250 AUTH FOO\r\n";

        int exitCode = await RunAsync(["-sv", "-u", "user:secret"], 18031, 60797, Greeting + ehloReply);

        Diagnostics.Assert("exit code", 67, exitCode);
        Assert.AreEqual(67, exitCode);
        string expected = (
            Opened(18031, 60797)
            + Headers("< ", ehloReply)
            + "* SASL: no auth mechanism was offered or recognized" + InfoEnd
            + "* closing connection #0" + InfoEnd);
        Diagnostics.Diff("stderr", Lf(expected), Lf(StandardErrorText));
        Assert.AreEqual(expected, Encoding.ASCII.GetString(standardError.ToArray()));
        Diagnostics.Assert("stdout length", 0L, standardOutput.Length);
        Assert.AreEqual(0, standardOutput.Length);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task RunAsync_VerboseMailUploadWithStartTlsOnWindows_WritesTheSchannelLinesAndTheEstablishedConnectionLineAgain()
    {
        // Between "< 220 Ready to start TLS" and the second EHLO curl writes the connect's
        // "Established connection" line again (BL-1058), after the two "schannel:" lines the
        // Schannel build writes before every handshake, HTTPS's included (BL-1083).
        await AssertStartTlsUploadLinesAsync(
            "* schannel: disabled automatic use of client certificate" + InfoEnd
            + "* schannel: using IP address, SNI is not supported by OS." + InfoEnd);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task RunAsync_VerboseMailUploadWithStartTlsOffWindows_WritesTheOpenSslTrustLineAndTheEstablishedConnectionLineAgain()
    {
        // The OpenSSL build writes its "SSL Trust" line before the handshake instead (BL-1090).
        await AssertStartTlsUploadLinesAsync("* SSL Trust: peer verification disabled" + InfoEnd);
    }

    private async Task AssertStartTlsUploadLinesAsync(string tlsBackendLines)
    {
        int exitCode = await RunAsync(
            ["-sv", "-k", "--ssl-reqd"], 18027, 53681, Greeting + EhloReply + "220 Ready to start TLS\r\n" + NextRead + SecureEhloReply + Transaction);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        string expected = (
            Opened(18027, 53681)
            + Headers("< ", EhloReply)
            + "> STARTTLS" + HeaderEnd
            + "< 220 Ready to start TLS" + HeaderEnd
            + tlsBackendLines
            + "* Established connection to 127.0.0.1 (127.0.0.1 port 18027) from 127.0.0.1 port 53681 " + InfoEnd
            + "> EHLO client" + HeaderEnd
            + Headers("< ", SecureEhloReply)
            + "> MAIL FROM:<a@b> SIZE=22" + HeaderEnd
            + UploadTail(18027));
        Diagnostics.Diff("stderr", Lf(expected), Lf(StandardErrorText));
        Assert.AreEqual(expected, Encoding.ASCII.GetString(standardError.ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_TraceAsciiMailUpload_WritesTheMeasuredDumpToStandardOutput()
    {
        int exitCode = await RunAsync(["-s", "--trace-ascii", "-"], 18028, 53682, Greeting + EhloReply + Transaction);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        string expected = (
            "*   Trying 127.0.0.1:18028...\n"
            + "* Established connection to 127.0.0.1 (127.0.0.1 port 18028) from 127.0.0.1 port 53682 \n"
            + "<= Recv header, 21 bytes (0x15)\n0000: 220 localhost ESMTP\n"
            + "=> Send header, 13 bytes (0xd)\n0000: EHLO client\n"
            + "<= Recv header, 15 bytes (0xf)\n0000: 250-localhost\n"
            + "<= Recv header, 31 bytes (0x1f)\n0000: 250-AUTH PLAIN LOGIN CRAM-MD5\n"
            + "<= Recv header, 14 bytes (0xe)\n0000: 250-STARTTLS\n"
            + "<= Recv header, 18 bytes (0x12)\n0000: 250-SIZE 1000000\n"
            + "<= Recv header, 14 bytes (0xe)\n0000: 250-8BITMIME\n"
            + "<= Recv header, 14 bytes (0xe)\n0000: 250 SMTPUTF8\n"
            + "=> Send header, 25 bytes (0x19)\n0000: MAIL FROM:<a@b> SIZE=22\n"
            + "<= Recv header, 8 bytes (0x8)\n0000: 250 OK\n"
            + "=> Send header, 15 bytes (0xf)\n0000: RCPT TO:<c@d>\n"
            + "<= Recv header, 8 bytes (0x8)\n0000: 250 OK\n"
            + "=> Send header, 6 bytes (0x6)\n0000: DATA\n"
            + "<= Recv header, 37 bytes (0x25)\n0000: 354 End data with <CR><LF>.<CR><LF>\n"
            + "=> Send data, 25 bytes (0x19)\n0000: Subject: hi\n000d: \n000f: Hello\n0016: .\n"
            + "* upload completely sent off: 25 bytes\n"
            + "<= Recv header, 25 bytes (0x19)\n0000: 250 OK message accepted\n"
            + "* Connection #0 to host 127.0.0.1:18028 left intact\n");
        Diagnostics.Diff("stdout", Lf(expected), Lf(StandardOutputText));
        Assert.AreEqual(expected, Encoding.ASCII.GetString(standardOutput.ToArray()));
        Diagnostics.Diff("stderr", string.Empty, StandardErrorText);
        Assert.AreEqual(string.Empty, Encoding.ASCII.GetString(standardError.ToArray()));
    }

    private static string Opened(int port, int localPort) =>
        $"*   Trying 127.0.0.1:{port}..." + InfoEnd
        + $"* Established connection to 127.0.0.1 (127.0.0.1 port {port}) from 127.0.0.1 port {localPort} " + InfoEnd
        + "< 220 localhost ESMTP" + HeaderEnd
        + "> EHLO client" + HeaderEnd;

    /// <summary>Writes each CRLF-ended line of <paramref name="lines" /> as curl's <c>-v</c> does.</summary>
    private static string Headers(string prefix, string lines) =>
        string.Concat(lines.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Select(line => prefix + line + HeaderEnd));

    private static string UploadTail(int port) =>
        "< 250 OK" + HeaderEnd
        + "> RCPT TO:<c@d>" + HeaderEnd
        + "< 250 OK" + HeaderEnd
        + "> DATA" + HeaderEnd
        + "< 354 End data with <CR><LF>.<CR><LF>" + HeaderEnd
        + "} [25 bytes data]" + InfoEnd
        + "* upload completely sent off: 25 bytes" + InfoEnd
        + "< 250 OK message accepted" + HeaderEnd
        + $"* Connection #0 to host 127.0.0.1:{port} left intact" + InfoEnd;

    /// <summary>Splits a connector script into reads: the server answers the next command only once it is sent.</summary>
    private const string NextRead = "<next read>";

    private static string Lf(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);

    private async Task<int> RunAsync(IReadOnlyList<string> arguments, int port, int localPort, string replies)
    {
        var connector = new ReportingConnector(new ScriptedConnector(replies.Split(NextRead).Select(Encoding.ASCII.GetBytes)), localPort);
        Diagnostics.Arrange("arguments", string.Join(' ', arguments));
        Diagnostics.Arrange("target", "smtp://127.0.0.1:port/client with --mail-from a@b --mail-rcpt c@d -T mail.txt");
        Diagnostics.Arrange("connector script", Lf(replies.Replace(NextRead, string.Empty, StringComparison.Ordinal)));
        int exitCode;
        using (Diagnostics.Phase("run"))
        {
            exitCode = await new CurlCommandRunner(
                    options => new TransferDispatch(
                        new ProtocolDispatcher(CurlComposition.CreateProtocolHandlers(
                            connector, new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"), new TrustReportingTlsProvider(), new LoopbackDnsResolver(), tracesSmtp: CurlComposition.TracesSmtp(options)))),
                    files,
                    files,
                    standardOutput,
                    standardError,
                    new MemoryStream(),
                    runsOnWindows: true)
                .RunAsync([.. arguments, "--mail-from", "a@b", "--mail-rcpt", "c@d", "-T", "mail.txt", $"smtp://127.0.0.1:{port}/client"]);
        }

        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Act("stdout", Lf(StandardOutputText));
        Diagnostics.Act("stderr", Lf(StandardErrorText));
        return exitCode;
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

    /// <summary>
    /// A pass-through TLS provider that reports the trust before its handshake, as
    /// <c>SslStreamTlsProvider</c> does, so the Schannel build's <c>schannel:</c> lines appear.
    /// </summary>
    private sealed class TrustReportingTlsProvider : ITlsProvider
    {
        public ValueTask<ConnectResult> AuthenticateAsClientAsync(
            IConnection plaintext,
            string targetHost,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(ConnectResult.Connected(plaintext));

        public ValueTask<ConnectResult> AuthenticateAsClientAsync(
            IConnection plaintext,
            string targetHost,
            ITransferEvents events,
            CancellationToken cancellationToken)
        {
            events.ReportTlsTrust(new TlsTrustEvent { VerifiesPeer = false, TargetsIpAddress = IPAddress.TryParse(targetHost, out _) });
            return AuthenticateAsClientAsync(plaintext, targetHost, cancellationToken);
        }
    }
}
