using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Pop3.Fakes;

namespace Curl.Protocol.Pop3;

/// <summary>
/// Pins how a POP3 session opens and closes against curl 8.21.0: the greeting, <c>CAPA</c>,
/// <c>STLS</c> under <c>--ssl</c> and <c>--ssl-reqd</c>, <c>pop3s://</c>, <c>QUIT</c>, and the
/// exit code and message of every failure. Every case was recorded from real curl (the
/// Schannel build) on 2026-09-28 with <c>Record-CurlExchange.ps1 -Pop3</c>, curl running
/// <c>-sS pop3://127.0.0.1:18110/</c> (BL-547 Notes). Without credentials curl sends no
/// authentication, so <c>LIST</c> follows <c>CAPA</c>; <see cref="Pop3ProtocolHandlerTransferTests" />
/// pins what it writes.
/// </summary>
[TestClass]
public sealed class Pop3ProtocolHandlerSessionTests
{
    private const string Url = "pop3://127.0.0.1:18110/";

    private const string Greeting = "+OK POP3 ready <1896.697170952@localhost>\r\n";

    /// <summary>The recorder's <c>CAPA</c> answer on a plaintext connection.</summary>
    private const string CapaReply =
        "+OK Capability list follows\r\nUSER\r\nSASL PLAIN LOGIN\r\nSTLS\r\nTOP\r\nUIDL\r\n.\r\n";

    /// <summary>The recorder's <c>CAPA</c> answer once the connection is TLS: no <c>STLS</c>.</summary>
    private const string SecureCapaReply =
        "+OK Capability list follows\r\nUSER\r\nSASL PLAIN LOGIN\r\nTOP\r\nUIDL\r\n.\r\n";

    /// <summary>The recorder's <c>LIST</c> answer for its default maildrop of two 52-byte messages.</summary>
    private const string ListReply = "+OK 2 messages (104 octets)\r\n1 52\r\n2 52\r\n.\r\n";

    /// <summary>The bytes curl wrote for <see cref="ListReply" />: <c>1 52</c> and <c>2 52</c>, each with CRLF.</summary>
    private const long ListedBytes = 12;

    private const string List = "LIST\r\n";

    private const string Bye = "+OK Bye\r\n";

    private const string StlsAccepted = "+OK Begin TLS negotiation\r\n";

    private const string Capa = "CAPA\r\n";

    private const string Stls = "STLS\r\n";

    private const string Quit = "QUIT\r\n";

    private const string ResponseReadingFailed = "response reading failed (errno: 0)";

    private const TransportSecurityLevel Try = TransportSecurityLevel.Try;

    private const TransportSecurityLevel Required = TransportSecurityLevel.Required;

    [TestMethod]
    public async Task ExecuteAsync_DefaultSession_SendsCapaThenQuit()
    {
        Pop3Run run = await RunAsync(Url, Greeting + CapaReply + ListReply + Bye);

        Assert.AreEqual(Capa + List + Quit, run.Sent);
        Assert.AreEqual(TransferResult.Success(ListedBytes), run.Result);
        Assert.AreEqual(new ConnectTarget("127.0.0.1", 18110, false), run.Connector.Targets.Single() with { Events = NoTransferEvents.Instance });
        Assert.IsEmpty(run.Tls.Handshakes);
        Assert.IsTrue(run.Connection.IsDisposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_UrlWithoutPort_ConnectsToPort110()
    {
        Pop3Run run = await RunAsync("pop3://127.0.0.1/", Greeting + CapaReply + ListReply + Bye);

        Assert.AreEqual(new ConnectTarget("127.0.0.1", 110, false), run.Connector.Targets.Single() with { Events = NoTransferEvents.Instance });
    }

    [TestMethod]
    public async Task ExecuteAsync_ProxyAndEvents_ArePassedToTheConnector()
    {
        var proxy = new ProxyEndpoint(ProxyKind.Socks5, "proxy", 1080, null);
        var events = new RecordingTransferEvents();
        var connector = new QueuedConnector(ConnectResult.Connected(Script(Greeting + CapaReply + ListReply + Bye)));
        var context = new TransferContext { Url = CurlUrl.Parse(Url), Output = Stream.Null, Proxy = proxy, Events = events };

        TransferResult result = await new Pop3ProtocolHandler(connector, new QueuedTlsProvider()).ExecuteAsync(context);

        Assert.AreSame(proxy, connector.Targets.Single().Proxy);
        connector.Targets.Single().Events.ReportInfo("from the connector");
        Assert.AreEqual("from the connector", events.Info.Last());
        Assert.AreEqual(TransferResult.Success(ListedBytes), result);
    }

    [TestMethod]
    [DataRow(TransportSecurityLevel.None, DisplayName = "-k pop3s://")]
    [DataRow(TransportSecurityLevel.Required, DisplayName = "-k --ssl-reqd pop3s://")]
    public async Task ExecuteAsync_Pop3sUrl_ConnectsWithTlsToPort995AndNeverSendsStls(TransportSecurityLevel sslLevel)
    {
        // Recorder -Tls: CAPA (no STLS offered), LIST, QUIT, exit 0; here without the port.
        Pop3Run run = await RunAsync("pop3s://127.0.0.1/", Greeting + SecureCapaReply + ListReply + Bye, sslLevel);

        Assert.AreEqual(Capa + List + Quit, run.Sent);
        Assert.AreEqual(new ConnectTarget("127.0.0.1", 995, true), run.Connector.Targets.Single() with { Events = NoTransferEvents.Instance });
        Assert.IsEmpty(run.Tls.Handshakes);
        Assert.AreEqual(TransferResult.Success(ListedBytes), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_Pop3sUrlWhoseCapaOffersStls_StillNeverSendsIt()
    {
        Pop3Run run = await RunAsync("pop3s://127.0.0.1/", Greeting + CapaReply + ListReply + Bye, Required);

        Assert.AreEqual(Capa + List + Quit, run.Sent);
        Assert.AreEqual(TransferResult.Success(ListedBytes), run.Result);
    }

    [TestMethod]
    [DataRow("-ERR go away", DisplayName = "GREETING=-ERR go away")]
    [DataRow("-ERRx", DisplayName = "GREETING=-ERRx")]
    [DataRow("+junk", DisplayName = "GREETING=+junk")]
    [DataRow("+ok hi", DisplayName = "GREETING=+ok hi")]
    public async Task ExecuteAsync_GreetingNotOk_FailsWithExit8AndSendsNothing(string greeting)
    {
        // Each measured: exit 8, "Got unexpected pop3-server response", nothing sent.
        Pop3Run run = await RunAsync(Url, greeting + "\r\n");

        Assert.AreEqual(string.Empty, run.Sent);
        Assert.AreEqual(
            TransferResult.Failure(CurlExitCode.WeirdServerReply, "Got unexpected pop3-server response"),
            run.Result);
    }

    [TestMethod]
    [DataRow("junk\r\n+OK hi\r\n", DisplayName = "GREETING=junk then +OK hi")]
    [DataRow("-ER\r\n\r\n\n+OK hi\r\n", DisplayName = "-ER and empty lines skipped")]
    [DataRow("+OKjunk\r\n", DisplayName = "GREETING=+OKjunk")]
    [DataRow("+OK hi\n", DisplayName = "LF only")]
    public async Task ExecuteAsync_GreetingAfterSkippedLinesOrLoose_IsAccepted(string greeting)
    {
        Pop3Run run = await RunAsync(Url, greeting + CapaReply + ListReply + Bye);

        Assert.AreEqual(Capa + List + Quit, run.Sent);
        Assert.AreEqual(TransferResult.Success(ListedBytes), run.Result);
    }

    [TestMethod]
    [DataRow("", DisplayName = "before the greeting")]
    [DataRow("hello there\r\n", DisplayName = "GREETING=hello there, then the server hangs up")]
    [DataRow(Greeting, DisplayName = "CAPA=CLOSE")]
    [DataRow(Greeting + "+OK Capability list follows\r\nSTLS\r\n", DisplayName = "mid-CAPA")]
    [DataRow(Greeting + "+OK Capability list follows\r\nSTLS", DisplayName = "mid-line")]
    public async Task ExecuteAsync_ServerClosesBeforeAResponseIsComplete_FailsWithExit56(string replies)
    {
        Pop3Run run = await RunAsync(Url, replies);

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.RecvError, ResponseReadingFailed), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReadFails_FailsWithExit56()
    {
        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes(Greeting)) { FailReadsWhenExhausted = true };

        Pop3Run run = await Pop3Run.ExecuteAsync(Url, connection);

        Assert.AreEqual(Capa, run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.RecvError, ResponseReadingFailed), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_SendFails_FailsWithExit56WhenNoResponseFollows()
    {
        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes(Greeting)) { WritesBeforeFailure = 0 };

        Pop3Run run = await Pop3Run.ExecuteAsync(Url, connection);

        Assert.AreEqual(string.Empty, run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.RecvError, ResponseReadingFailed), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_CapaSplitAcrossReads_IsReadWhole()
    {
        // The default session's bytes in three-byte reads, so every line and CRLF is split.
        byte[] replies = Encoding.Latin1.GetBytes(Greeting + CapaReply + StlsAccepted);
        var secured = Script(SecureCapaReply + ListReply + Bye);

        Pop3Run run = await Pop3Run.ExecuteAsync(
            Url,
            new ScriptedConnection([.. replies.Chunk(3)]),
            Required,
            ConnectResult.Connected(secured));

        Assert.AreEqual(Capa + Stls, run.Sent);
        Assert.AreEqual(Capa + List + Quit, Encoding.Latin1.GetString(secured.Sent));
        Assert.AreEqual(TransferResult.Success(ListedBytes), run.Result);
    }

    [TestMethod]
    [DataRow(TransportSecurityLevel.None, DisplayName = "CAPA=-ERR no")]
    [DataRow(TransportSecurityLevel.Try, DisplayName = "--ssl, CAPA=-ERR no")]
    public async Task ExecuteAsync_CapaRefused_CarriesOnWithoutStls(TransportSecurityLevel sslLevel)
    {
        // Measured: CAPA, -ERR, then LIST and QUIT, exit 0.
        Pop3Run run = await RunAsync(Url, Greeting + "-ERR no\r\n" + ListReply + Bye, sslLevel);

        Assert.AreEqual(Capa + List + Quit, run.Sent);
        Assert.AreEqual(TransferResult.Success(ListedBytes), run.Result);
    }

    [TestMethod]
    [DataRow("-ERR no\r\n", DisplayName = "--ssl-reqd -k, CAPA=-ERR no")]
    [DataRow("+OK\r\nSTLS\r\n-ERR x\r\n.\r\n", DisplayName = "-ERR after STLS was listed")]
    [DataRow("+OK\r\nUSER\r\n.\r\n", DisplayName = "no STLS listed")]
    [DataRow("+OK\r\nXSTLS\r\n.\r\n", DisplayName = "XSTLS")]
    public async Task ExecuteAsync_StlsNotAvailableUnderSslReqd_FailsWithExit64WithoutSendingIt(string capaReply)
    {
        // Each measured: exit 64, "STLS not supported.", nothing sent after CAPA.
        Pop3Run run = await RunAsync(Url, Greeting + capaReply, Required);

        Assert.AreEqual(Capa, run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.UseSslFailed, "STLS not supported."), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_StlsNotListedUnderSsl_CarriesOnInPlaintext()
    {
        Pop3Run run = await RunAsync(Url, Greeting + "+OK\r\nUSER\r\n.\r\n" + ListReply + Bye, Try);

        Assert.AreEqual(Capa + List + Quit, run.Sent);
        Assert.AreEqual(TransferResult.Success(ListedBytes), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_StlsRefusedUnderSsl_CarriesOnInPlaintext()
    {
        // --ssl, STLS=-ERR not now: CAPA, STLS, then LIST and QUIT, exit 0.
        Pop3Run run = await RunAsync(Url, Greeting + CapaReply + "-ERR not now\r\n" + ListReply + Bye, Try);

        Assert.AreEqual(Capa + Stls + List + Quit, run.Sent);
        Assert.IsEmpty(run.Tls.Handshakes);
        Assert.AreEqual(TransferResult.Success(ListedBytes), run.Result);
    }

    [TestMethod]
    [DataRow("-ERR not now", DisplayName = "STLS=-ERR not now")]
    [DataRow("+", DisplayName = "STLS=+")]
    public async Task ExecuteAsync_StlsRefusedUnderSslReqd_FailsWithExit64(string stlsReply)
    {
        // Each measured: exit 64, "STARTTLS denied", no QUIT.
        Pop3Run run = await RunAsync(Url, Greeting + CapaReply + stlsReply + "\r\n", Required);

        Assert.AreEqual(Capa + Stls, run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.UseSslFailed, "STARTTLS denied"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_StlsAcceptedUnderSslReqd_UpgradesAndAsksForCapabilitiesAgain()
    {
        // curl --ssl-reqd -k: CAPA, STLS, +OK, the handshake, CAPA again over TLS, LIST, QUIT.
        var secured = Script(SecureCapaReply + ListReply + Bye);
        Pop3Run run = await Pop3Run.ExecuteAsync(
            Url,
            Script(Greeting + CapaReply + StlsAccepted),
            Required,
            ConnectResult.Connected(secured));

        Assert.AreEqual(Capa + Stls, run.Sent);
        Assert.AreEqual(Capa + List + Quit, Encoding.Latin1.GetString(secured.Sent));
        Assert.AreSame(run.Connection, run.Tls.Handshakes.Single().Plaintext);
        Assert.AreEqual("127.0.0.1", run.Tls.Handshakes.Single().TargetHost);
        Assert.IsTrue(secured.IsDisposed);
        Assert.AreEqual(TransferResult.Success(ListedBytes), run.Result);
    }

    [TestMethod]
    [DataRow("+OK\r\nstls\r\n.\r\n", DisplayName = "lower case")]
    [DataRow("+OK\r\nSTLSX\r\n.\r\n", DisplayName = "keyword as a prefix")]
    [DataRow("+OK\r\n.x\r\n. \r\nSTLS\r\n.\r\n", DisplayName = "lines starting with a dot do not end the list")]
    [DataRow("STLS\r\n.\r\n", DisplayName = "no status line")]
    [DataRow("+OK\nSTLS\n.\n", DisplayName = "LF only")]
    [DataRow("+OK\r\n.\r\r\nSTLS\r\n.\r\n", DisplayName = "only one CR is dropped")]
    public async Task ExecuteAsync_StlsListedLoosely_IsStillSent(string capaReply)
    {
        // Each measured but the last: curl sent STLS.
        var secured = Script(SecureCapaReply + ListReply + Bye);
        Pop3Run run = await Pop3Run.ExecuteAsync(
            Url,
            Script(Greeting + capaReply + StlsAccepted),
            Required,
            ConnectResult.Connected(secured));

        Assert.AreEqual(Capa + Stls, run.Sent);
        Assert.AreEqual(TransferResult.Success(ListedBytes), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_StlsAcceptedUnderSsl_Upgrades()
    {
        var secured = Script(SecureCapaReply + ListReply + Bye);
        Pop3Run run = await Pop3Run.ExecuteAsync(
            Url,
            Script(Greeting + CapaReply + StlsAccepted),
            Try,
            ConnectResult.Connected(secured));

        Assert.AreEqual(Capa + List + Quit, Encoding.Latin1.GetString(secured.Sent));
        Assert.AreEqual(TransferResult.Success(ListedBytes), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_StlsHandshakeFails_ReturnsTheProvidersFailureWithoutQuit()
    {
        // curl --ssl-reqd without -k: exit 60 from the handshake after STLS's +OK, no QUIT.
        Pop3Run run = await Pop3Run.ExecuteAsync(
            Url,
            Script(Greeting + CapaReply + StlsAccepted),
            Required,
            ConnectResult.Failed(CurlExitCode.PeerFailedVerification, "schannel: untrusted"));

        Assert.AreEqual(Capa + Stls, run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.PeerFailedVerification, "schannel: untrusted"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_ResponseLineOf65536Bytes_FailsWithExit100()
    {
        // GREETING=+OK and 65530 x: 65534 characters and CRLF, exit 100, "A value or data
        // field grew larger than allowed".
        Pop3Run run = await RunAsync(Url, "+OK " + new string('x', 65530) + "\r\n");

        Assert.AreEqual(string.Empty, run.Sent);
        Assert.AreEqual(
            TransferResult.Failure(CurlExitCode.TooLarge, "A value or data field grew larger than allowed"),
            run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_ResponseLineOf65535Bytes_IsRead()
    {
        // GREETING=+OK and 65529 x: 65533 characters and CRLF, exit 0.
        Pop3Run run = await RunAsync(Url, "+OK " + new string('x', 65529) + "\r\n" + CapaReply + ListReply + Bye);

        Assert.AreEqual(Capa + List + Quit, run.Sent);
        Assert.AreEqual(TransferResult.Success(ListedBytes), run.Result);
    }

    [TestMethod]
    [DataRow("-ERR no\r\n", DisplayName = "QUIT=-ERR no")]
    [DataRow("", DisplayName = "the server closes")]
    [DataRow("+OK " + "x", DisplayName = "an unfinished line")]
    public async Task ExecuteAsync_QuitAnsweredAnyway_StillSucceeds(string quitReply)
    {
        Pop3Run run = await RunAsync(Url, Greeting + CapaReply + ListReply + quitReply);

        Assert.AreEqual(Capa + List + Quit, run.Sent);
        Assert.AreEqual(TransferResult.Success(ListedBytes), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_QuitAnsweredWithAnOverlongLine_StillSucceeds()
    {
        Pop3Run run = await RunAsync(Url, Greeting + CapaReply + ListReply + "+OK " + new string('x', 70000) + "\r\n");

        Assert.AreEqual(TransferResult.Success(ListedBytes), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnectFails_ReturnsTheConnectorsFailure()
    {
        var connector = new QueuedConnector(ConnectResult.Refused("Failed to connect to 127.0.0.1 port 18110"));
        var handler = new Pop3ProtocolHandler(connector, new QueuedTlsProvider());

        TransferResult result = await handler.ExecuteAsync(new TransferContext { Url = CurlUrl.Parse(Url), Output = Stream.Null });

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("Failed to connect to 127.0.0.1 port 18110", result.ErrorMessage);
        Assert.IsTrue(result.IsConnectionRefused);
    }

    [TestMethod]
    public void SupportedSchemes_ArePop3AndPop3s()
    {
        var handler = new Pop3ProtocolHandler(new QueuedConnector(), new QueuedTlsProvider());

        CollectionAssert.AreEqual(new[] { "pop3", "pop3s" }, handler.SupportedSchemes.ToArray());
    }

    [TestMethod]
    public void Constructor_NullArgument_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new Pop3ProtocolHandler(null!, new QueuedTlsProvider()));
        Assert.ThrowsExactly<ArgumentNullException>(() => new Pop3ProtocolHandler(new QueuedConnector(), null!));
    }

    [TestMethod]
    public async Task ExecuteAsync_NullContext_Throws()
    {
        var handler = new Pop3ProtocolHandler(new QueuedConnector(), new QueuedTlsProvider());

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await handler.ExecuteAsync(null!));
    }

    /// <summary>
    /// A server sending <paramref name="replies" />, with a read ending after
    /// <see cref="ListReply" />: the terminator ends a listing only when it ends a read, and
    /// the recorder sends the <c>QUIT</c> reply only once <c>QUIT</c> arrives.
    /// </summary>
    private static ScriptedConnection Script(string replies)
    {
        int listEnd = replies.IndexOf(ListReply, StringComparison.Ordinal) + ListReply.Length;
        string[] reads = listEnd < ListReply.Length ? [replies] : [replies[..listEnd], replies[listEnd..]];
        return new([.. reads.Where(read => read.Length > 0).Select(Encoding.Latin1.GetBytes)]);
    }

    private static Task<Pop3Run> RunAsync(string url, string replies, TransportSecurityLevel sslLevel = TransportSecurityLevel.None) =>
        Pop3Run.ExecuteAsync(url, Script(replies), sslLevel);
}
