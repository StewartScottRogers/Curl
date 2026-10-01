using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ftp.Fakes;

namespace Curl.Protocol.Ftp;

/// <summary>
/// Pins FTP over TLS against curl 8.21.0: <c>ftps://</c> (implicit TLS), and <c>AUTH</c>,
/// <c>PBSZ</c> and <c>PROT</c> on <c>ftp://</c> under <c>--ssl</c>, <c>--ssl-reqd</c> and
/// <c>--ftp-ssl-control</c>, with the exit code of every refusal. Every case was recorded
/// from real curl (the Schannel build) on 2026-09-27 with <c>Record-CurlExchange.ps1 -Ftp</c>
/// (with <c>-Tls</c> for <c>ftps</c>), which answers <c>AUTH</c> and <c>PROT P</c> by
/// wrapping its streams in TLS, serving the five bytes <c>hello</c> (BL-437, ADR-0102's
/// BL-437 addendum). The fake TLS provider stands a scripted connection in for each
/// secured one, so no test touches the network.
/// </summary>
[TestClass]
public sealed class FtpProtocolHandlerTlsTests
{
    private const string Greeting = "220 Recorder ready\r\n";

    private const string LoggedIn = "331 Password required\r\n230 Logged in\r\n";

    private const string LogInSent = "USER anonymous\r\nPASS ftp@example.com\r\n";

    private const string AuthAccepted = "234 AUTH accepted\r\n";

    private const string Refused = "500 no\r\n";

    private const string Protected = "200 PBSZ=0\r\n200 Protection level set to P\r\n";

    private const string Pwd = "257 \"/\" is current directory\r\n";

    private const string Epsv = "229 Entering Extended Passive Mode (|||64396|)\r\n";

    /// <summary>The replies from <c>EPSV</c> through <c>QUIT</c> for the five-byte file.</summary>
    private const string Retrieved = Epsv + "200 Type set\r\n213 5\r\n150 Opening BINARY mode data connection\r\n226 Transfer complete\r\n221 Bye\r\n";

    /// <summary>What curl sent from <c>PWD</c> through <c>QUIT</c> for <c>a.txt</c>.</summary>
    private const string RetrieveSent = "PWD\r\nEPSV\r\nTYPE I\r\nSIZE a.txt\r\nRETR a.txt\r\nQUIT\r\n";

    [TestMethod]
    public async Task ExecuteAsync_FtpsUrl_ConnectsWithTlsToPort990AndProtectsTheData()
    {
        // curl -k ftps://127.0.0.1:18437/a.txt, here without the port
        // curl -v writes the data handshake's schannel: lines, so it reports to the transfer's events (BL-1084).
        var events = new RecordingTransferEvents();
        var securedData = Scripted("hello");
        TlsRun run = await RunAsync(
            "ftps://127.0.0.1/a.txt",
            Greeting + LoggedIn + Protected + Pwd + Retrieved,
            m => m.Events = events,
            ConnectResult.Connected(securedData));

        Assert.AreEqual(LogInSent + "PBSZ 0\r\nPROT P\r\n" + RetrieveSent, run.Sent);
        Assert.AreEqual(new ConnectTarget("127.0.0.1", 990, true), run.Connector.Targets[0]);
        Assert.AreEqual(new ConnectTarget("127.0.0.1", 64396, false), run.Connector.Targets[1]);
        Assert.AreSame(run.Data, run.Tls.Handshakes.Single().Plaintext);
        Assert.AreSame(events, run.Tls.HandshakeEvents.Single());
        Assert.AreEqual("127.0.0.1", run.Tls.Handshakes.Single().TargetHost);
        Assert.IsTrue(securedData.IsDisposed);
        Assert.AreEqual("hello", run.OutputText);
        Assert.AreEqual(TransferResult.Success(5), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_FtpsUrlWithPort_ConnectsToThatPort()
    {
        TlsRun run = await RunAsync(
            "ftps://127.0.0.1:18437/a.txt",
            Greeting + LoggedIn + Protected + Pwd + Retrieved,
            _ => { },
            ConnectResult.Connected(Scripted("hello")));

        Assert.AreEqual(new ConnectTarget("127.0.0.1", 18437, true), run.Connector.Targets[0]);
        Assert.AreEqual(TransferResult.Success(5), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_FtpsUrlWithProtRefused_TransfersTheDataInPlaintext()
    {
        // curl -k ftps://127.0.0.1:18437/a.txt, PROT answered 500 no: exit 0.
        TlsRun run = await RunAsync(
            "ftps://127.0.0.1:18437/a.txt",
            Greeting + LoggedIn + "200 PBSZ=0\r\n" + Refused + Pwd + Retrieved,
            _ => { },
            data: "hello");

        Assert.AreEqual(LogInSent + "PBSZ 0\r\nPROT P\r\n" + RetrieveSent, run.Sent);
        Assert.IsEmpty(run.Tls.Handshakes);
        Assert.AreEqual(TransferResult.Success(5), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_SslReqd_SendsAuthSslThenCarriesOnOverTheSecuredControl()
    {
        // curl -k --ssl-reqd ftp://127.0.0.1:18437/a.txt
        var securedControl = Scripted(LoggedIn + Protected + Pwd + Retrieved);
        var securedData = Scripted("hello");
        TlsRun run = await RunAsync(
            "ftp://127.0.0.1:18437/a.txt",
            Greeting + AuthAccepted,
            context => context.SslLevel = TransportSecurityLevel.Required,
            ConnectResult.Connected(securedControl),
            ConnectResult.Connected(securedData));

        Assert.AreEqual("AUTH SSL\r\n", run.Sent);
        Assert.AreEqual(LogInSent + "PBSZ 0\r\nPROT P\r\n" + RetrieveSent, Encoding.Latin1.GetString(securedControl.Sent));
        Assert.AreEqual(new ConnectTarget("127.0.0.1", 18437, false), run.Connector.Targets[0]);
        Assert.AreSame(run.Control, run.Tls.Handshakes[0].Plaintext);
        Assert.AreSame(run.Data, run.Tls.Handshakes[1].Plaintext);
        Assert.IsTrue(securedControl.IsDisposed);
        Assert.AreEqual("hello", run.OutputText);
        Assert.AreEqual(TransferResult.Success(5), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_AuthSslRefusedAndAuthTls334_UpgradesAfterAuthTls()
    {
        // curl -k --ssl ftp://127.0.0.1:18437/a.txt, AUTH SSL answered 500 no and AUTH TLS 334:
        // curl takes 334 as acceptance and starts the handshake.
        var securedControl = Scripted(LoggedIn + Protected + Pwd + Retrieved);
        TlsRun run = await RunAsync(
            "ftp://127.0.0.1:18437/a.txt",
            Greeting + Refused + "334 ok\r\n",
            context => context.SslLevel = TransportSecurityLevel.Try,
            ConnectResult.Connected(securedControl),
            ConnectResult.Connected(Scripted("hello")));

        Assert.AreEqual("AUTH SSL\r\nAUTH TLS\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Success(5), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_SslReqdAndAuthRefused_EndsWithExit64WithoutQuit()
    {
        // curl -k --ssl-reqd ftp://127.0.0.1:18437/a.txt, AUTH answered 500 no
        TlsRun run = await RunAsync(
            "ftp://127.0.0.1:18437/a.txt",
            Greeting + Refused + Refused,
            context => context.SslLevel = TransportSecurityLevel.Required);

        Assert.AreEqual("AUTH SSL\r\nAUTH TLS\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.UseSslFailed, "Requested SSL level failed"), run.Result);
        Assert.AreEqual(500, run.Report?.ResponseCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_FtpSslControlAndAuthRefused_EndsWithExit64()
    {
        // curl -k --ssl --ftp-ssl-control ftp://127.0.0.1:18437/a.txt, AUTH answered 500 no:
        // --ftp-ssl-control outranks --ssl.
        TlsRun run = await RunAsync(
            "ftp://127.0.0.1:18437/a.txt",
            Greeting + Refused + Refused,
            context =>
            {
                context.SslLevel = TransportSecurityLevel.Try;
                context.FtpSslControlOnly = true;
            });

        Assert.AreEqual("AUTH SSL\r\nAUTH TLS\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.UseSslFailed, "Requested SSL level failed"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_SslAndAuthRefused_CarriesOnInPlaintext()
    {
        // curl -k --ssl ftp://127.0.0.1:18437/a.txt, AUTH answered 500 no: exit 0, no PBSZ.
        TlsRun run = await RunAsync(
            "ftp://127.0.0.1:18437/a.txt",
            Greeting + Refused + Refused + LoggedIn + Pwd + Retrieved,
            context => context.SslLevel = TransportSecurityLevel.Try,
            data: "hello");

        Assert.AreEqual("AUTH SSL\r\nAUTH TLS\r\n" + LogInSent + RetrieveSent, run.Sent);
        Assert.IsEmpty(run.Tls.Handshakes);
        Assert.AreEqual(TransferResult.Success(5), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_FtpSslControl_SendsProtCAndLeavesTheDataInPlaintext()
    {
        // curl -k --ftp-ssl-control ftp://127.0.0.1:18437/a.txt
        var securedControl = Scripted(LoggedIn + "200 PBSZ=0\r\n200 Protection level set to C\r\n" + Pwd + Retrieved);
        TlsRun run = await RunAsync(
            "ftp://127.0.0.1:18437/a.txt",
            Greeting + AuthAccepted,
            context => context.FtpSslControlOnly = true,
            [ConnectResult.Connected(securedControl)],
            "hello");

        Assert.AreEqual(LogInSent + "PBSZ 0\r\nPROT C\r\n" + RetrieveSent, Encoding.Latin1.GetString(securedControl.Sent));
        Assert.AreEqual(1, run.Tls.Handshakes.Count);
        Assert.AreEqual(TransferResult.Success(5), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_FtpSslControlAndProtCRefused_CarriesOn()
    {
        // curl -k --ftp-ssl-control ftp://127.0.0.1:18437/a.txt, PROT answered 500 no: exit 0.
        var securedControl = Scripted(LoggedIn + "200 PBSZ=0\r\n" + Refused + Pwd + Retrieved);
        TlsRun run = await RunAsync(
            "ftp://127.0.0.1:18437/a.txt",
            Greeting + AuthAccepted,
            context => context.FtpSslControlOnly = true,
            [ConnectResult.Connected(securedControl)],
            "hello");

        Assert.AreEqual(TransferResult.Success(5), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_SslReqdAndFtpSslControl_SendsProtP()
    {
        // curl -k --ssl-reqd --ftp-ssl-control ftp://127.0.0.1:18437/a.txt: --ssl-reqd outranks it.
        var securedControl = Scripted(LoggedIn + Protected + Pwd + Retrieved);
        TlsRun run = await RunAsync(
            "ftp://127.0.0.1:18437/a.txt",
            Greeting + AuthAccepted,
            context =>
            {
                context.SslLevel = TransportSecurityLevel.Required;
                context.FtpSslControlOnly = true;
            },
            ConnectResult.Connected(securedControl),
            ConnectResult.Connected(Scripted("hello")));

        StringAssert.Contains(Encoding.Latin1.GetString(securedControl.Sent), "PROT P\r\n");
        Assert.AreEqual(TransferResult.Success(5), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_SslReqdAndProtRefused_EndsWithExit64WithoutQuit()
    {
        // curl -k --ssl-reqd ftp://127.0.0.1:18437/a.txt, PROT answered 500 no
        var securedControl = Scripted(LoggedIn + "200 PBSZ=0\r\n" + Refused);
        TlsRun run = await RunAsync(
            "ftp://127.0.0.1:18437/a.txt",
            Greeting + AuthAccepted,
            context => context.SslLevel = TransportSecurityLevel.Required,
            ConnectResult.Connected(securedControl));

        Assert.AreEqual(LogInSent + "PBSZ 0\r\nPROT P\r\n", Encoding.Latin1.GetString(securedControl.Sent));
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.UseSslFailed, "Requested SSL level failed"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_PbszRefused_CarriesOn()
    {
        // curl -k --ssl-reqd ftp://127.0.0.1:18437/a.txt, PBSZ answered 500 no: exit 0.
        var securedControl = Scripted(LoggedIn + Refused + "200 Protection level set to P\r\n" + Pwd + Retrieved);
        TlsRun run = await RunAsync(
            "ftp://127.0.0.1:18437/a.txt",
            Greeting + AuthAccepted,
            context => context.SslLevel = TransportSecurityLevel.Required,
            ConnectResult.Connected(securedControl),
            ConnectResult.Connected(Scripted("hello")));

        Assert.AreEqual(TransferResult.Success(5), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_ControlHandshakeFails_EndsWithItsExitCodeAndSendsNothingMore()
    {
        // curl --ssl-reqd ftp://127.0.0.1:18437/a.txt without -k: the certificate is refused.
        TlsRun run = await RunAsync(
            "ftp://127.0.0.1:18437/a.txt",
            Greeting + AuthAccepted,
            context => context.SslLevel = TransportSecurityLevel.Required,
            ConnectResult.Failed(CurlExitCode.PeerFailedVerification, "schannel: SEC_E_UNTRUSTED_ROOT (0x80090325)"));

        Assert.AreEqual("AUTH SSL\r\n", run.Sent);
        Assert.IsTrue(run.Control.IsDisposed);
        Assert.AreEqual(
            TransferResult.Failure(CurlExitCode.PeerFailedVerification, "schannel: SEC_E_UNTRUSTED_ROOT (0x80090325)"),
            run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_DataHandshakeFails_EndsWithItsExitCodeWithoutQuit()
    {
        TlsRun run = await RunAsync(
            "ftps://127.0.0.1:18437/a.txt",
            Greeting + LoggedIn + Protected + Pwd + Retrieved,
            _ => { },
            ConnectResult.Failed(CurlExitCode.SslConnectError, "schannel: failed to receive handshake"));

        Assert.AreEqual(LogInSent + "PBSZ 0\r\nPROT P\r\nPWD\r\nEPSV\r\n", run.Sent);
        Assert.IsTrue(run.Data.IsDisposed);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.SslConnectError, "schannel: failed to receive handshake"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_PassiveProtP_RunsTheDataHandshakeBeforeTypeI()
    {
        // curl 8.21.0 -v -k --ssl-reqd ftp://127.0.0.1:18021/a.txt writes the data connection's
        // schannel: lines right after "Trying 127.0.0.1:<port>...", before "> TYPE I" (BL-1084).
        var events = new RecordingTransferEvents();
        var securedControl = Scripted(LoggedIn + Protected + Pwd + Retrieved);
        var tls = new QueuedTlsProvider(ConnectResult.Connected(securedControl), ConnectResult.Connected(Scripted("hello")));
        var headersAtHandshake = new List<string[]>();
        tls.BeforeEachHandshake = () => headersAtHandshake.Add([.. events.Headers]);
        var connector = new QueuedConnector(
            ConnectResult.Connected(Scripted(Greeting + AuthAccepted)),
            ConnectResult.Connected(new ScriptedConnection()));
        TransferContext context = MutableContext.Build(
            new TransferContext { Url = CurlUrl.Parse("ftp://127.0.0.1:18021/a.txt"), Output = new MemoryStream() },
            m =>
            {
                m.SslLevel = TransportSecurityLevel.Required;
                m.Events = events;
            });

        TransferResult result = await new FtpProtocolHandler(connector, new QueuedListener(), tls).ExecuteAsync(context);

        Assert.AreEqual("< " + Epsv, headersAtHandshake[1][^1]);
        CollectionAssert.Contains(events.Headers, "> TYPE I\r\n");
        Assert.AreEqual(TransferResult.Success(5), result with { Report = null });
    }

    [TestMethod]
    public async Task ExecuteAsync_ActiveProtP_RunsTheDataHandshakeAfterTheAccept()
    {
        // curl -k --ssl-reqd -P - ftp://127.0.0.1:18437/a.txt: the server connects back only
        // after RETR, so the handshake runs over the accepted connection.
        var accepted = new ScriptedConnection();
        var pending = new ScriptedPendingConnection(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 56703), ConnectResult.Connected(accepted));
        var controlLocal = new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 55129);
        var securedControl = new ScriptedConnection(Encoding.Latin1.GetBytes(
            LoggedIn + Protected + Pwd + "200 EPRT command successful\r\n200 Type set\r\n213 5\r\n150 Opening BINARY mode data connection\r\n226 Transfer complete\r\n221 Bye\r\n"))
        {
            LocalEndPoint = controlLocal,
        };
        var tls = new QueuedTlsProvider(ConnectResult.Connected(securedControl), ConnectResult.Connected(Scripted("hello")));
        var control = new ScriptedConnection(Encoding.Latin1.GetBytes(Greeting + AuthAccepted))
        {
            LocalEndPoint = controlLocal,
        };
        var connector = new QueuedConnector(ConnectResult.Connected(control));
        TransferContext context = MutableContext.Build(
            new TransferContext { Url = CurlUrl.Parse("ftp://127.0.0.1:18437/a.txt"), Output = new MemoryStream() },
            m =>
            {
                m.SslLevel = TransportSecurityLevel.Required;
                m.FtpPort = "-";
            });

        TransferResult result = await new FtpProtocolHandler(connector, new QueuedListener(ListenResult.Listening(pending)), tls).ExecuteAsync(context);

        Assert.AreSame(accepted, tls.Handshakes[1].Plaintext);
        StringAssert.Contains(Encoding.Latin1.GetString(securedControl.Sent), "RETR a.txt\r\n");
        Assert.AreEqual(TransferResult.Success(5), result with { Report = null });
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadAndDataHandshakeFails_EndsWithItsExitCode()
    {
        TlsRun run = await RunAsync(
            "ftps://127.0.0.1:18437/u.txt",
            Greeting + LoggedIn + Protected + Pwd + Epsv + "200 Type set\r\n150 Opening BINARY mode data connection\r\n",
            context => context.Upload = new MemoryStream([1]),
            ConnectResult.Failed(CurlExitCode.SslConnectError, "schannel: failed to receive handshake"));

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.SslConnectError, "schannel: failed to receive handshake"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadOverFtps_WritesTheFileToTheSecuredDataConnection()
    {
        var securedData = new ScriptedConnection();
        TlsRun run = await RunAsync(
            "ftps://127.0.0.1:18437/u.txt",
            Greeting + LoggedIn + Protected + Pwd + Epsv + "200 Type set\r\n150 Opening BINARY mode data connection\r\n226 Transfer complete\r\n221 Bye\r\n",
            context => context.Upload = new MemoryStream(Encoding.Latin1.GetBytes("hello")),
            ConnectResult.Connected(securedData));

        Assert.AreEqual("hello", Encoding.Latin1.GetString(securedData.Sent));
        Assert.IsEmpty(run.Data.Sent);
        Assert.AreEqual(TransferResult.Success(5), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_LoggedInByTheGreeting_SkipsAuth()
    {
        // A 230 greeting logs in at once; curl 8.21.0 sends AUTH only after a 220.
        TlsRun run = await RunAsync(
            "ftp://127.0.0.1:18437/a.txt",
            "230 Logged in\r\n" + Pwd + Retrieved,
            context => context.SslLevel = TransportSecurityLevel.Try,
            data: "hello");

        Assert.AreEqual(RetrieveSent, run.Sent);
        Assert.AreEqual(TransferResult.Success(5), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_HandlerWithoutTls_EndsAnAcceptedAuthWithExit64()
    {
        // A handler built with a connector only cannot secure the control connection.
        var control = Scripted(Greeting + AuthAccepted);
        var context = new TransferContext
        {
            Url = CurlUrl.Parse("ftp://127.0.0.1:18437/a.txt"),
            Output = new MemoryStream(),
            SslLevel = TransportSecurityLevel.Try,
        };

        TransferResult result = await new FtpProtocolHandler(new QueuedConnector(ConnectResult.Connected(control))).ExecuteAsync(context);

        Assert.AreEqual("AUTH SSL\r\n", Encoding.Latin1.GetString(control.Sent));
        Assert.IsTrue(control.IsDisposed);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.UseSslFailed, "Requested SSL level failed"), result with { Report = null });
    }

    [TestMethod]
    public void SupportedSchemes_HandlerWithListenerAndTls_ServesFtpAndFtps()
    {
        var handler = new FtpProtocolHandler(new QueuedConnector(), new QueuedListener(), new QueuedTlsProvider());

        CollectionAssert.AreEqual(new[] { "ftp", "ftps" }, handler.SupportedSchemes.ToArray());
    }

    [TestMethod]
    public void SupportedSchemes_HandlerWithConnectorOnly_ServesFtpOnly()
    {
        var handler = new FtpProtocolHandler(new QueuedConnector());

        CollectionAssert.AreEqual(new[] { "ftp" }, handler.SupportedSchemes.ToArray());
    }

    [TestMethod]
    public void Constructor_NullListener_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new FtpProtocolHandler(new QueuedConnector(), null!, new QueuedTlsProvider()));
    }

    [TestMethod]
    public void Constructor_NullTlsProvider_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new FtpProtocolHandler(new QueuedConnector(), new QueuedListener(), null!));
    }

    private static ScriptedConnection Scripted(string text) => new(Encoding.Latin1.GetBytes(text));

    private static Task<TlsRun> RunAsync(string url, string replies, Action<MutableContext> adjust, params ConnectResult[] handshakes) =>
        RunAsync(url, replies, adjust, handshakes, string.Empty);

    private static Task<TlsRun> RunAsync(string url, string replies, Action<MutableContext> adjust, string data) =>
        RunAsync(url, replies, adjust, [], data);

    private static async Task<TlsRun> RunAsync(
        string url,
        string replies,
        Action<MutableContext> adjust,
        ConnectResult[] handshakes,
        string data)
    {
        var control = Scripted(replies);
        var dataConnection = data.Length == 0 ? new ScriptedConnection() : Scripted(data);
        var connector = new QueuedConnector(ConnectResult.Connected(control), ConnectResult.Connected(dataConnection));
        var tls = new QueuedTlsProvider(handshakes);
        var context = MutableContext.Build(new TransferContext { Url = CurlUrl.Parse(url), Output = new MemoryStream() }, adjust);

        TransferResult result = await new FtpProtocolHandler(connector, new QueuedListener(), tls).ExecuteAsync(context);

        return new TlsRun(result with { Report = null }, result.Report, control, dataConnection, connector, tls, context.Output);
    }

    /// <summary>One transfer over TLS and what it left behind.</summary>
    private sealed record TlsRun(
        TransferResult Result,
        TransferReport? Report,
        ScriptedConnection Control,
        ScriptedConnection Data,
        QueuedConnector Connector,
        QueuedTlsProvider Tls,
        Stream Output)
    {
        public string Sent => Encoding.Latin1.GetString(Control.Sent);

        public string OutputText => Encoding.Latin1.GetString(((MemoryStream)Output).ToArray());
    }
}
