using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Imap.Fakes;
using Transport = Curl.Protocol.Abstractions.TransportSecurityLevel;

namespace Curl.Protocol.Imap;

/// <summary>
/// Pins how an IMAP session opens and closes against curl 8.21.0: the greeting,
/// <c>CAPABILITY</c>, <c>STARTTLS</c> under <c>--ssl</c> and <c>--ssl-reqd</c>,
/// <c>imaps://</c>, the command tags, <c>LOGOUT</c>, and the exit code and message of every
/// failure. Every case was recorded from real curl (the Schannel build) on 2026-09-28 with
/// <c>Record-CurlExchange.ps1 -Imap</c>, curl running <c>-sS imap://127.0.0.1:18143/</c>
/// (BL-553 Notes). Between the session opening and <c>LOGOUT</c> curl sends <c>LIST "" *</c>,
/// answered here with a bare <c>OK</c> (BL-556).
/// </summary>
[TestClass]
public sealed class ImapProtocolHandlerSessionTests
{
    private const string Url = "imap://127.0.0.1:18143/";

    /// <summary>The recorder's greeting; curl never reads the capabilities it carries.</summary>
    private const string Greeting = "* OK [CAPABILITY IMAP4rev1 STARTTLS AUTH=PLAIN AUTH=LOGIN] ready\r\n";

    private const string Capability = "A001 CAPABILITY\r\n";

    private const Transport Try = Transport.Try;

    private const Transport Required = Transport.Required;

    [TestMethod]
    public async Task ExecuteAsync_DefaultSession_SendsCapabilityThenLogout()
    {
        ImapRun run = await RunAsync(Url, Greeting + CapabilityReply("A001") + ListReply("A002") + LogoutReply("A003"));

        Assert.AreEqual(Capability + "A002 LIST \"\" *\r\nA003 LOGOUT\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Success(0), run.Result);
        Assert.AreEqual(new ConnectTarget("127.0.0.1", 18143, false), run.Connector.Targets.Single());
        Assert.IsTrue(run.Connection.IsDisposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_UrlWithoutPort_ConnectsToPort143()
    {
        ImapRun run = await RunAsync("imap://127.0.0.1/", Greeting + CapabilityReply("A001") + ListReply("A002") + LogoutReply("A003"));

        Assert.AreEqual(new ConnectTarget("127.0.0.1", 143, false), run.Connector.Targets.Single());
    }

    [TestMethod]
    public async Task ExecuteAsync_ProxyAndEvents_ArePassedToTheConnector()
    {
        var proxy = new ProxyEndpoint(ProxyKind.Socks5, "proxy", 1080, null);
        var events = new RecordingTransferEvents();
        var connection = new ScriptedConnection(Latin1(Greeting + CapabilityReply("A001") + ListReply("A002") + LogoutReply("A003")));
        var context = new TransferContext { Url = CurlUrl.Parse(Url), Output = Stream.Null, Proxy = proxy, Events = events };

        ImapRun run = await ImapRun.ExecuteAsync(context, connection);

        Assert.AreSame(proxy, run.Connector.Targets.Single().Proxy);
        Assert.AreSame(events, run.Connector.Targets.Single().Events);
        Assert.AreEqual(TransferResult.Success(0), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_ImapsUrl_ConnectsWithTlsToPort993AndNeverSendsStartTls()
    {
        // curl -k --ssl-reqd imaps://127.0.0.1:18143/ (recorder -Tls): CAPABILITY, LIST,
        // LOGOUT; a connection already TLS satisfies --ssl-reqd. Here without the port.
        ImapRun run = await RunAsync("imaps://127.0.0.1/", Greeting + CapabilityReply("A001") + ListReply("A002") + LogoutReply("A003"), Required);

        Assert.AreEqual(Capability + "A002 LIST \"\" *\r\nA003 LOGOUT\r\n", run.Sent);
        Assert.AreEqual(new ConnectTarget("127.0.0.1", 993, true), run.Connector.Targets.Single());
        Assert.IsEmpty(run.Tls.Handshakes);
        Assert.AreEqual(TransferResult.Success(0), run.Result);
    }

    [TestMethod]
    [DataRow("* BYE go away\r\n", DisplayName = "GREETING=* BYE go away")]
    [DataRow("* NO nope\r\n", DisplayName = "GREETING=* NO nope")]
    [DataRow("* BAD x\r\n", DisplayName = "GREETING=* BAD x")]
    [DataRow("* ok hi\r\n", DisplayName = "GREETING=* ok hi")]
    public async Task ExecuteAsync_GreetingNeitherOkNorPreauth_FailsWithExit8AndSendsNothing(string greeting)
    {
        ImapRun run = await RunAsync(Url, greeting);

        Assert.AreEqual(string.Empty, run.Sent);
        Assert.AreEqual(
            TransferResult.Failure(CurlExitCode.WeirdServerReply, "Got unexpected imap-server response"),
            run.Result);
    }

    [TestMethod]
    [DataRow("hello\r\n", DisplayName = "GREETING=hello")]
    [DataRow("A001 OK x\r\n", DisplayName = "GREETING=A001 OK x")]
    public async Task ExecuteAsync_GreetingNotUntagged_IsSkippedUntilTheServerCloses(string greeting)
    {
        // Measured: curl waits past the line, then exit 56 once the server hangs up.
        ImapRun run = await RunAsync(Url, greeting);

        Assert.AreEqual(string.Empty, run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.RecvError, "response reading failed (errno: 0)"), run.Result);
    }

    [TestMethod]
    [DataRow(Transport.None, DisplayName = "no --ssl")]
    [DataRow(Transport.Try, DisplayName = "--ssl")]
    public async Task ExecuteAsync_PreauthGreeting_OpensWithoutStartTls(Transport sslLevel)
    {
        // GREETING=* PREAUTH ready: CAPABILITY, LIST, LOGOUT, exit 0; with --ssl too, where
        // STARTTLS is advertised but PREAUTH rules it out.
        ImapRun run = await RunAsync(Url, "* PREAUTH ready\r\n" + CapabilityReply("A001") + ListReply("A002") + LogoutReply("A003"), sslLevel);

        Assert.AreEqual(Capability + "A002 LIST \"\" *\r\nA003 LOGOUT\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Success(0), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_PreauthGreetingUnderSslReqd_FailsWithExit64()
    {
        // GREETING=* PREAUTH ready, --ssl-reqd: exit 64, "STARTTLS not available.", no LOGOUT.
        ImapRun run = await RunAsync(Url, "* PREAUTH ready\r\n" + CapabilityReply("A001"), Required);

        Assert.AreEqual(Capability, run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.UseSslFailed, "STARTTLS not available."), run.Result);
    }

    [TestMethod]
    [DataRow("A001 BAD what\r\n", DisplayName = "CAPABILITY=BAD what")]
    [DataRow("A001 NO what\r\n", DisplayName = "CAPABILITY=NO what")]
    [DataRow("* CAPABILITY IMAP4rev1 STARTTLS\r\nA001 CLOSE\r\n", DisplayName = "an unknown completion")]
    [DataRow("* BYE going\r\nA001 OK done\r\n", DisplayName = "an untagged BYE")]
    public async Task ExecuteAsync_CapabilityAnsweredAnyway_CarriesOn(string capabilityReply)
    {
        ImapRun run = await RunAsync(Url, Greeting + capabilityReply + ListReply("A002") + LogoutReply("A003"));

        Assert.AreEqual(Capability + "A002 LIST \"\" *\r\nA003 LOGOUT\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Success(0), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_StartTlsRefusedUnderSsl_CarriesOnInPlaintext()
    {
        // --ssl, STARTTLS=NO refused: no second CAPABILITY, LIST and LOGOUT in plaintext, exit 0.
        ImapRun run = await RunAsync(
            Url,
            new ScriptedConnection(Latin1(Greeting + CapabilityReply("A001")), Latin1("A002 NO refused\r\n"), Latin1(ListReply("A003") + LogoutReply("A004"))),
            Try);

        Assert.AreEqual(Capability + "A002 STARTTLS\r\nA003 LIST \"\" *\r\nA004 LOGOUT\r\n", run.Sent);
        Assert.IsEmpty(run.Tls.Handshakes);
        Assert.AreEqual(TransferResult.Success(0), run.Result);
    }

    [TestMethod]
    [DataRow("A002 NO refused\r\n", DisplayName = "STARTTLS=NO refused")]
    [DataRow("A002 BAD nope\r\n", DisplayName = "STARTTLS=BAD nope")]
    public async Task ExecuteAsync_StartTlsRefusedUnderSslReqd_FailsWithExit64(string startTlsReply)
    {
        // --ssl-reqd: exit 64, "STARTTLS denied", no LOGOUT.
        ImapRun run = await RunAsync(Url, Greeting + CapabilityReply("A001") + startTlsReply, Required);

        Assert.AreEqual(Capability + "A002 STARTTLS\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.UseSslFailed, "STARTTLS denied"), run.Result);
    }

    [TestMethod]
    [DataRow("* CAPABILITY IMAP4rev1\r\nA001 OK done\r\n", DisplayName = "not advertised")]
    [DataRow("* CAPABILITY IMAP4rev1 STARTTLSX\r\nA001 OK done\r\n", DisplayName = "advertised as a prefix")]
    [DataRow("* OK STARTTLS\r\n* CAPABILITY IMAP4rev1\r\nA001 OK done\r\n", DisplayName = "in another untagged response")]
    [DataRow("* CAPABILITY IMAP4rev1 STARTTLS\r\nA001 ok done\r\n", DisplayName = "completed in lower case")]
    [DataRow("A001 BAD what\r\n", DisplayName = "CAPABILITY=BAD what")]
    public async Task ExecuteAsync_StartTlsNotAvailableUnderSslReqd_FailsWithExit64WithoutSendingIt(string capabilityReply)
    {
        // Each measured: exit 64, "STARTTLS not available.", no LOGOUT.
        ImapRun run = await RunAsync(Url, Greeting + capabilityReply, Required);

        Assert.AreEqual(Capability, run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.UseSslFailed, "STARTTLS not available."), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_StartTlsNotAdvertisedUnderSsl_CarriesOnInPlaintext()
    {
        ImapRun run = await RunAsync(Url, Greeting + "* CAPABILITY IMAP4rev1\r\nA001 OK done\r\n" + ListReply("A002") + LogoutReply("A003"), Try);

        Assert.AreEqual(Capability + "A002 LIST \"\" *\r\nA003 LOGOUT\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Success(0), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_StartTlsAcceptedUnderSslReqd_UpgradesAndAsksForCapabilitiesAgain()
    {
        // curl -k --ssl-reqd: STARTTLS, OK, the handshake, A003 CAPABILITY over TLS, then LIST
        // and LOGOUT; the tags carry on across the upgrade.
        var secured = new ScriptedConnection(Latin1("* CAPABILITY IMAP4rev1 AUTH=PLAIN AUTH=LOGIN\r\nA003 OK CAPABILITY completed\r\n" + ListReply("A004") + LogoutReply("A005")));
        ImapRun run = await RunAsync(
            Url,
            new ScriptedConnection(Latin1(Greeting + CapabilityReply("A001") + "A002 OK Begin TLS negotiation now\r\n")),
            Required,
            ConnectResult.Connected(secured));

        Assert.AreEqual(Capability + "A002 STARTTLS\r\n", run.Sent);
        Assert.AreEqual("A003 CAPABILITY\r\nA004 LIST \"\" *\r\nA005 LOGOUT\r\n", Encoding.Latin1.GetString(secured.Sent));
        Assert.AreSame(run.Connection, run.Tls.Handshakes.Single().Plaintext);
        Assert.AreEqual("127.0.0.1", run.Tls.Handshakes.Single().TargetHost);
        Assert.IsTrue(secured.IsDisposed);
        Assert.AreEqual(TransferResult.Success(0), run.Result);
    }

    [TestMethod]
    [DataRow("* CAPABILITY IMAP4rev1 starttls\r\nA001 OK done\r\n", DisplayName = "lower-case capability")]
    [DataRow("* capability IMAP4rev1 STARTTLS\r\nA001 OK done\r\n", DisplayName = "lower-case response name")]
    [DataRow("* CAPABILITY IMAP4rev1\tSTARTTLS\r\nA001 OK done\r\n", DisplayName = "tab between words")]
    [DataRow("* CAPABILITY IMAP4rev1 STARTTLS\r\nA001 OKAY done\r\n", DisplayName = "completed OKAY")]
    [DataRow("* 12 CAPABILITY STARTTLS\r\nA001 OK done\r\n", DisplayName = "numbered response")]
    [DataRow("* CAPABILITY\r\n* CAPABILITY STARTTLS\n* CAPABILITY IMAP4rev1\r\nA001 OK done\r\n", DisplayName = "spread over responses")]
    public async Task ExecuteAsync_StartTlsAdvertisedLoosely_IsStillSent(string capabilityReply)
    {
        // The first four measured; the rest follow curl's imap_matchresp and word scan.
        ImapRun run = await RunUpgradedAsync(capabilityReply);

        Assert.AreEqual(Capability + "A002 STARTTLS\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Success(0), run.Result);
    }

    [TestMethod]
    [DataRow("* 1X CAPABILITY STARTTLS\r\n", DisplayName = "number not followed by a space")]
    [DataRow("* 12\n", DisplayName = "number alone")]
    [DataRow("* CAPABILITIES STARTTLS\r\n", DisplayName = "another name")]
    [DataRow("* CAPABILITYX STARTTLS\r\n", DisplayName = "name as a prefix")]
    [DataRow("* CAPABILITY\n", DisplayName = "name alone, LF only")]
    [DataRow("* CAPA\r\n", DisplayName = "shorter than the name")]
    [DataRow("* \n", DisplayName = "star and space alone")]
    public async Task ExecuteAsync_UntaggedResponseNotCapability_IsNotRead(string untagged)
    {
        ImapRun run = await RunAsync(Url, Greeting + untagged + "A001 OK done\r\n", Required);

        Assert.AreEqual(Capability, run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.UseSslFailed, "STARTTLS not available."), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_CapabilityLiteral_IsReadAsPartOfTheResponse()
    {
        // CAPABILITY=* CAPABILITY IMAP4rev1 {8}\r\nSTARTTLS\r\nOK done, --ssl-reqd: curl sent
        // STARTTLS. Here in three-byte reads, so the literal and every line are split.
        byte[] replies = Latin1(Greeting + "* CAPABILITY IMAP4rev1 {8}\r\nSTARTTLS\r\nA001 OK done\r\n" + "A002 OK go\r\n");
        var secured = new ScriptedConnection(Latin1(CapabilityReply("A003") + ListReply("A004") + LogoutReply("A005")));

        ImapRun run = await RunAsync(Url, new ScriptedConnection([.. replies.Chunk(3)]), Required, ConnectResult.Connected(secured));

        Assert.AreEqual(Capability + "A002 STARTTLS\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Success(0), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_CapabilityLiteralHoldingATaggedLine_DoesNotCompleteTheResponse()
    {
        // The literal's bytes are data, not lines: "A001 NO fake" inside it neither completes
        // CAPABILITY nor is refused, and the second literal after the first carries STARTTLS.
        const string Reply = "* CAPABILITY {14}\r\nA001 NO fake\r\n {1}\r\nx STARTTLS {0}\r\n\r\nA001 OK done\r\n";
        byte[] replies = Latin1(Greeting + Reply + "A002 OK go\r\n");
        var secured = new ScriptedConnection(Latin1(CapabilityReply("A003") + ListReply("A004") + LogoutReply("A005")));

        ImapRun run = await RunAsync(Url, new ScriptedConnection([.. replies.Chunk(5)]), Required, ConnectResult.Connected(secured));

        Assert.AreEqual(Capability + "A002 STARTTLS\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Success(0), run.Result);
    }

    [TestMethod]
    [DataRow("* CAPABILITY {}\r\nSTARTTLS\r\n", DisplayName = "no digits")]
    [DataRow("* CAPABILITY {8a}\r\nSTARTTLS\r\n", DisplayName = "not only digits")]
    [DataRow("* CAPABILITY {1234567890}\r\nSTARTTLS\r\n", DisplayName = "ten digits")]
    [DataRow("* CAPABILITY {8} x\r\nSTARTTLS\r\n", DisplayName = "not at the end")]
    [DataRow("* CAPABILITY x}\r\nSTARTTLS\r\n", DisplayName = "no opening brace")]
    public async Task ExecuteAsync_CapabilityWithoutAWellFormedLiteral_ReadsTheNextLineAsALine(string untagged)
    {
        ImapRun run = await RunAsync(Url, Greeting + untagged + "A001 OK done\r\n", Required);

        Assert.AreEqual(Capability, run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.UseSslFailed, "STARTTLS not available."), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_UnwantedUntaggedLiteral_IsReadAsLines()
    {
        // CAPABILITY=* 1 X {3}\r\nabc\r\n* CAPABILITY IMAP4rev1 STARTTLS\r\nOK done: STARTTLS.
        ImapRun run = await RunUpgradedAsync("* 1 X {3}\r\nabc\r\n* CAPABILITY IMAP4rev1 STARTTLS\r\nA001 OK done\r\n");

        Assert.AreEqual(Capability + "A002 STARTTLS\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Success(0), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_StartTlsAnsweredWithAnUntaggedLine_StillUpgrades()
    {
        // STARTTLS=* 1 X\r\nOK go: measured under --ssl and --ssl-reqd, the handshake follows.
        var secured = new ScriptedConnection(Latin1(CapabilityReply("A003") + ListReply("A004") + LogoutReply("A005")));
        ImapRun run = await RunAsync(
            Url,
            new ScriptedConnection(Latin1(Greeting + CapabilityReply("A001")), Latin1("* 1 X\r\nA002 OK go\r\n")),
            Try,
            ConnectResult.Connected(secured));

        Assert.AreEqual("A003 CAPABILITY\r\nA004 LIST \"\" *\r\nA005 LOGOUT\r\n", Encoding.Latin1.GetString(secured.Sent));
        Assert.AreEqual(TransferResult.Success(0), run.Result);
    }

    [TestMethod]
    [DataRow("* 1 X {14}\r\nA002 NO fake\r\n)\r\nA002 OK go\r\n", DisplayName = "measured: a literal before the answer")]
    [DataRow("A002 OK go\r\n* 1 injected\r\n", DisplayName = "bytes after OK")]
    public async Task ExecuteAsync_StartTlsAnswerFollowedInTheSameRead_FailsWithExit8(string startTlsReply)
    {
        // curl refuses a response to STARTTLS with more bytes behind it ("pipelining"): exit 8
        // "Weird server reply", no handshake, no LOGOUT.
        ImapRun run = await RunAsync(
            Url,
            new ScriptedConnection(Latin1(Greeting + CapabilityReply("A001")), Latin1(startTlsReply)),
            Required);

        Assert.AreEqual(Capability + "A002 STARTTLS\r\n", run.Sent);
        Assert.IsEmpty(run.Tls.Handshakes);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.WeirdServerReply, "Weird server reply"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_StartTlsHandshakeFails_ReturnsTheProvidersFailureWithoutLogout()
    {
        ImapRun run = await RunAsync(
            Url,
            new ScriptedConnection(Latin1(Greeting + CapabilityReply("A001")), Latin1("A002 OK go\r\n")),
            Required,
            ConnectResult.Failed(CurlExitCode.PeerFailedVerification, "schannel: untrusted"));

        Assert.AreEqual(Capability + "A002 STARTTLS\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.PeerFailedVerification, "schannel: untrusted"), run.Result);
    }

    [TestMethod]
    [DataRow("* BYE Logging out\r\nA003 OK LOGOUT completed\r\n", DisplayName = "the recorder's answer")]
    [DataRow("", DisplayName = "LOGOUT=CLOSE")]
    [DataRow("+ go on\r\n", DisplayName = "a continuation")]
    [DataRow("A003 NO", DisplayName = "an unfinished line")]
    public async Task ExecuteAsync_LogoutAnsweredAnyway_StillSucceeds(string logoutReply)
    {
        // LOGOUT=CLOSE measured: exit 0.
        ImapRun run = await RunAsync(Url, Greeting + CapabilityReply("A001") + ListReply("A002") + logoutReply);

        Assert.AreEqual(Capability + "A002 LIST \"\" *\r\nA003 LOGOUT\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Success(0), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_LogoutAnsweredWithAnOverlongLine_StillSucceeds()
    {
        ImapRun run = await RunAsync(Url, Greeting + CapabilityReply("A001") + ListReply("A002") + "* BYE " + new string('x', 70000) + "\r\n");

        Assert.AreEqual(TransferResult.Success(0), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnectFails_ReturnsTheConnectorsFailure()
    {
        var connector = new QueuedConnector(ConnectResult.Refused("Failed to connect to 127.0.0.1 port 18143"));
        var handler = new ImapProtocolHandler(connector, new QueuedTlsProvider());

        TransferResult result = await handler.ExecuteAsync(new TransferContext { Url = CurlUrl.Parse(Url), Output = Stream.Null });

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("Failed to connect to 127.0.0.1 port 18143", result.ErrorMessage);
        Assert.IsTrue(result.IsConnectionRefused);
    }

    [TestMethod]
    public async Task ExecuteAsync_Cancelled_LeavesAsAnExceptionAndDisposesTheConnection()
    {
        var connection = new ScriptedConnection(Latin1(Greeting))
        {
            WritesBeforeFailure = 0,
            WriteFailure = new OperationCanceledException(),
        };

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () => await ImapRun.ExecuteAsync(Url, connection));

        Assert.IsTrue(connection.IsDisposed);
    }

    [TestMethod]
    public void SupportedSchemes_AreImapAndImaps()
    {
        var handler = new ImapProtocolHandler(new QueuedConnector(), new QueuedTlsProvider());

        CollectionAssert.AreEqual(new[] { "imap", "imaps" }, handler.SupportedSchemes.ToArray());
    }

    [TestMethod]
    public void Constructor_NullArgument_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new ImapProtocolHandler(null!, new QueuedTlsProvider()));
        Assert.ThrowsExactly<ArgumentNullException>(() => new ImapProtocolHandler(new QueuedConnector(), null!));
    }

    [TestMethod]
    public async Task ExecuteAsync_NullContext_Throws()
    {
        var handler = new ImapProtocolHandler(new QueuedConnector(), new QueuedTlsProvider());

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await handler.ExecuteAsync(null!));
    }

    private static string CapabilityReply(string tag) =>
        "* CAPABILITY IMAP4rev1 STARTTLS AUTH=PLAIN AUTH=LOGIN\r\n" + tag + " OK CAPABILITY completed\r\n";

    private static string ListReply(string tag) => tag + " OK LIST completed\r\n";

    private static string LogoutReply(string tag) => "* BYE Logging out\r\n" + tag + " OK LOGOUT completed\r\n";

    private static byte[] Latin1(string text) => Encoding.Latin1.GetBytes(text);

    /// <summary>
    /// Runs a session under <c>--ssl-reqd</c> whose <c>CAPABILITY</c> is answered
    /// <paramref name="capabilityReply" />, then <c>STARTTLS</c> <c>OK</c> and a secured
    /// connection that answers the rest.
    /// </summary>
    private static Task<ImapRun> RunUpgradedAsync(string capabilityReply) =>
        RunAsync(
            Url,
            new ScriptedConnection(Latin1(Greeting + capabilityReply), Latin1("A002 OK go\r\n")),
            Required,
            ConnectResult.Connected(new ScriptedConnection(Latin1(CapabilityReply("A003") + ListReply("A004") + LogoutReply("A005")))));

    private static Task<ImapRun> RunAsync(string url, string replies, Transport sslLevel = Transport.None) =>
        ImapRun.ExecuteAsync(url, new ScriptedConnection(Latin1(replies)), sslLevel);

    private static Task<ImapRun> RunAsync(string url, ScriptedConnection connection, Transport sslLevel) =>
        ImapRun.ExecuteAsync(url, connection, sslLevel);

    private static Task<ImapRun> RunAsync(string url, ScriptedConnection connection, Transport sslLevel, ConnectResult handshake) =>
        ImapRun.ExecuteAsync(url, connection, sslLevel, handshake);
}
