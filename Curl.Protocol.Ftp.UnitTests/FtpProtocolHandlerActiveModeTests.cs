using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ftp.Fakes;

namespace Curl.Protocol.Ftp;

/// <summary>
/// Pins active mode (<c>-P</c>/<c>--ftp-port</c> and <c>--disable-eprt</c>) over
/// <c>ftp://</c> against curl 8.21.0: the <c>EPRT</c> and <c>PORT</c> bytes, where each is
/// sent, the port bound for them, and the exit code and message of every failure. Every case
/// was recorded from real curl on 2026-09-27 with <c>Record-CurlExchange.ps1 -Ftp</c>, which
/// connects back to the announced port, serving the five bytes <c>hello</c> (BL-437,
/// ADR-0102's BL-437 addendum); the fake listener reports the ports curl bound.
/// </summary>
[TestClass]
public sealed class FtpProtocolHandlerActiveModeTests
{
    private const string Url = "ftp://127.0.0.1:18437";

    /// <summary>The recording server's replies from the greeting through <c>PWD</c>.</summary>
    private const string LoggedIn = "220 Recorder ready\r\n331 Password required\r\n230 Logged in\r\n257 \"/\" is current directory\r\n";

    private const string LogInSent = "USER anonymous\r\nPASS ftp@example.com\r\nPWD\r\n";

    private const string EprtOk = "200 EPRT command successful\r\n";

    private const string PortOk = "200 PORT command successful\r\n";

    private const string Refused = "500 no\r\n";

    private const string TypeSet = "200 Type set\r\n";

    private const string Sized = "213 5\r\n";

    private const string Opened = "150 Opening BINARY mode data connection\r\n";

    private const string Complete = "226 Transfer complete\r\n";

    private const string Bye = "221 Bye\r\n";

    /// <summary>The replies from <c>TYPE I</c> through <c>QUIT</c> for the five-byte file.</summary>
    private const string Retrieved = TypeSet + Sized + Opened + Complete + Bye;

    /// <summary>What curl sent from <c>TYPE I</c> through <c>QUIT</c> for <c>a.txt</c>.</summary>
    private const string RetrieveSent = "TYPE I\r\nSIZE a.txt\r\nRETR a.txt\r\nQUIT\r\n";

    /// <summary>The control connection's own address, as curl reported it: <c>from 127.0.0.1 port 55129</c>.</summary>
    private static readonly IPEndPoint ControlLocal = new(IPAddress.Loopback, 55129);

    /// <summary>
    /// curl 8.21.0's <c>-v</c> line for a bind on <c>-P 192.0.2.1</c>, as the Schannel build
    /// printed it (BL-464); the listener words it so, and the handler passes it on.
    /// </summary>
    private const string NotLocalLine = "bind(port=0) on non-local address failed: Address not available";

    private static readonly ListenResult NotLocal = ListenResult.Failed(CurlExitCode.FtpPortFailed, NotLocalLine);

    [TestMethod]
    public async Task ExecuteAsync_PortDash_SendsEprtWithTheControlAddressThenAcceptsTheData()
    {
        // curl -P - ftp://127.0.0.1:18437/d/a.txt
        var pending = Pending(56703);
        ActiveRun run = await RunAsync("/d/a.txt", "-", LoggedIn + "250 OK\r\n" + EprtOk + Retrieved, pending);

        Assert.AreEqual(LogInSent + "CWD d\r\nEPRT |1|127.0.0.1|56703|\r\n" + RetrieveSent, run.Sent);
        Assert.AreEqual(new ListenTarget(IPAddress.Loopback, 0, 0), run.Listener.Targets.Single());
        Assert.AreEqual(1, run.Connector.Targets.Count);
        Assert.IsTrue(pending.WasAccepted);
        Assert.IsTrue(pending.IsDisposed);
        Assert.AreEqual("hello", run.OutputText);
        Assert.AreEqual(TransferResult.Success(5), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_EprtRefused_BindsAFreshPortAndSendsPort()
    {
        // curl -P - ftp://127.0.0.1:18437/a.txt, EPRT answered 500 no
        var first = Pending(56717);
        var second = Pending(56718);
        ActiveRun run = await RunAsync("/a.txt", "-", LoggedIn + Refused + PortOk + Retrieved, first, second);

        Assert.AreEqual(LogInSent + "EPRT |1|127.0.0.1|56717|\r\nPORT 127,0,0,1,221,142\r\n" + RetrieveSent, run.Sent);
        Assert.AreEqual(2, run.Listener.Targets.Count);
        Assert.IsTrue(first.IsDisposed);
        Assert.IsFalse(first.WasAccepted);
        Assert.IsTrue(second.WasAccepted);
        Assert.AreEqual(TransferResult.Success(5), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_DisableEprt_SendsPortOnlyAndListsInActiveMode()
    {
        // curl -P - --disable-eprt ftp://127.0.0.1:18437/
        ActiveRun run = await RunAsync(
            "/",
            "-",
            LoggedIn + PortOk + TypeSet + Opened + Complete + Bye,
            context => context.FtpUseEprt = false,
            Pending(56722));

        Assert.AreEqual(LogInSent + "PORT 127,0,0,1,221,146\r\nTYPE A\r\nLIST\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Success(5), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_EprtAndPortRefused_QuitsWithExit30()
    {
        // curl -P - ftp://127.0.0.1:18437/a.txt, EPRT and PORT answered 500 no
        var first = Pending(56730);
        var second = Pending(56731);
        ActiveRun run = await RunAsync("/a.txt", "-", LoggedIn + Refused + Refused + Bye, first, second);

        Assert.AreEqual(LogInSent + "EPRT |1|127.0.0.1|56730|\r\nPORT 127,0,0,1,221,155\r\nQUIT\r\n", run.Sent);
        Assert.IsTrue(second.IsDisposed);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.FtpPortFailed, "Failed to do PORT"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_PortRange_BindsWithinTheRangeOnTheAddressGiven()
    {
        // curl -P 127.0.0.1:40000-40010 ftp://127.0.0.1:18437/a.txt
        ActiveRun run = await RunAsync("/a.txt", "127.0.0.1:40000-40010", LoggedIn + EprtOk + Retrieved, Pending(40000));

        Assert.AreEqual(LogInSent + "EPRT |1|127.0.0.1|40000|\r\n" + RetrieveSent, run.Sent);
        Assert.AreEqual(new ListenTarget(IPAddress.Loopback, 40000, 40010), run.Listener.Targets.Single());
        Assert.AreEqual(TransferResult.Success(5), run.Result);
    }

    [TestMethod]
    [DataRow("127.0.0.1", "127.0.0.1", 0, 0)]
    [DataRow("127.0.0.1:40000", "127.0.0.1", 40000, 40000)]
    [DataRow(":40000", "127.0.0.1", 40000, 40000)]
    [DataRow("-:40000-40002", "127.0.0.1", 40000, 40002)]
    [DataRow("127.0.0.1:40000-39000", "127.0.0.1", 0, 0)]
    [DataRow("127.0.0.1:40000-", "127.0.0.1", 0, 0)]
    [DataRow("127.0.0.1:99999", "127.0.0.1", 0, 0)]
    [DataRow("127.0.0.1:abc", "127.0.0.1", 0, 0)]
    [DataRow("127.0.0.1:4x-5y", "127.0.0.1", 4, 5)]
    [DataRow("[::1]:40000", "::1", 40000, 40000)]
    [DataRow("[::1]", "::1", 0, 0)]
    [DataRow("::1", "::1", 0, 0)]
    public async Task ExecuteAsync_PortValue_BindsTheAddressAndPortRangeItNames(string value, string address, int low, int high)
    {
        // curl reads the port as atoi does; a range that is empty or past 65535 means any port.
        ActiveRun run = await RunAsync("/a.txt", value, LoggedIn + EprtOk + Retrieved, Pending(40000, address));

        Assert.AreEqual(new ListenTarget(IPAddress.Parse(address), low, high), run.Listener.Targets.Single());
        Assert.AreEqual(TransferResult.Success(5), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_Ipv6Address_SendsEprtWithFamily2()
    {
        // curl -P [::1]:40000 ftp://127.0.0.1:18437/a.txt
        ActiveRun run = await RunAsync("/a.txt", "[::1]:40000", LoggedIn + EprtOk + Retrieved, Pending(40000, "::1"));

        Assert.AreEqual(LogInSent + "EPRT |2|::1|40000|\r\n" + RetrieveSent, run.Sent);
        Assert.AreEqual(TransferResult.Success(5), run.Result);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task ExecuteAsync_Ipv6EprtRefused_QuitsWithExit30WithoutPort(bool useEprt)
    {
        // curl -P [::1] ftp://127.0.0.1:18437/a.txt, EPRT answered 500 no: IPv6 has no PORT,
        // and --disable-eprt cannot skip EPRT for it. curl 8.21.0 sends nothing more and waits
        // until the server hangs up; this ends at once instead (ADR-0102's BL-437 addendum).
        var pending = Pending(62572, "::1");
        ActiveRun run = await RunAsync(
            "/a.txt",
            "[::1]",
            LoggedIn + Refused + Bye,
            context => context.FtpUseEprt = useEprt,
            pending);

        Assert.AreEqual(LogInSent + "EPRT |2|::1|62572|\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(1, run.Listener.Targets.Count);
        Assert.IsTrue(pending.IsDisposed);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.FtpPortFailed, "Failed to do PORT"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_ControlAddressMappedToIpv6_AnnouncesItAsIpv4()
    {
        // A dual-mode socket reports ::ffff:127.0.0.1; curl announces 127.0.0.1.
        ActiveRun run = await RunAsync(
            "/a.txt",
            "-",
            LoggedIn + EprtOk + Retrieved,
            controlLocal: new IPEndPoint(IPAddress.Parse("::ffff:127.0.0.1"), 55129),
            pending: [Pending(56703)]);

        Assert.AreEqual(new ListenTarget(IPAddress.Loopback, 0, 0), run.Listener.Targets.Single());
        Assert.AreEqual(TransferResult.Success(5), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_ControlAddressUnknown_QuitsWithExit30()
    {
        // -P - on a control connection that reports no local address has nothing to announce.
        ActiveRun run = await RunAsync("/a.txt", "-", LoggedIn + Bye, controlLocal: null, pending: []);

        Assert.AreEqual(LogInSent + "QUIT\r\n", run.Sent);
        Assert.AreEqual(0, run.Listener.Targets.Count);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.FtpPortFailed, "Failed to do PORT"), run.Result);
    }

    [TestMethod]
    [DataRow("nosuch.invalid")]
    [DataRow("[::1")]
    public async Task ExecuteAsync_PortNamesAHostWithNoResolver_EndsWithExit6WithoutQuit(string value)
    {
        // A handler built without a resolver resolves no name, so every name ends as curl
        // ends for one that does not resolve (ADR-0108).
        ActiveRun run = await RunAsync("/a.txt", value, LoggedIn, _ => { }, []);

        Assert.AreEqual(LogInSent, run.Sent);
        Assert.AreEqual(0, run.Listener.Targets.Count);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.CouldntResolveHost, "Could not resolve host: " + value), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_PortLocalhost_ListensOnAndAnnouncesTheFirstResolvedAddress()
    {
        // curl -v -P localhost ftp://127.0.0.1:47466/f.txt (measured 2026-09-27, BL-466): the
        // name resolves to ::1 first, and curl sends EPRT |2|::1|58064| on an IPv4 control
        // connection.
        var resolver = new NamedDnsResolver(new Dictionary<string, IPAddress[]>
        {
            ["localhost"] = [IPAddress.IPv6Loopback, IPAddress.Loopback],
        });
        ActiveRun run = await RunAsync("/a.txt", "localhost", LoggedIn + EprtOk + Retrieved, resolver, Pending(58064, "::1"));

        Assert.AreEqual(LogInSent + "EPRT |2|::1|58064|\r\n" + RetrieveSent, run.Sent);
        CollectionAssert.AreEqual(new[] { "localhost" }, resolver.Hosts);
        Assert.AreEqual(new ListenTarget(IPAddress.IPv6Loopback, 0, 0), run.Listener.Targets.Single());
        Assert.AreEqual("hello", run.OutputText);
        Assert.AreEqual(TransferResult.Success(5), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_PortNameResolvesToIpv4Mapped_AnnouncesItAsIpv4WithThePortRange()
    {
        // A name that resolves to ::ffff:192.0.2.7 is announced as 192.0.2.7, as a mapped
        // literal is; the port range still applies.
        var resolver = new NamedDnsResolver(new Dictionary<string, IPAddress[]>
        {
            ["host.example"] = [IPAddress.Parse("::ffff:192.0.2.7")],
        });
        ActiveRun run = await RunAsync("/a.txt", "host.example:40000-40010", LoggedIn + EprtOk + Retrieved, resolver, Pending(40000, "192.0.2.7"));

        Assert.AreEqual(LogInSent + "EPRT |1|192.0.2.7|40000|\r\n" + RetrieveSent, run.Sent);
        Assert.AreEqual(new ListenTarget(IPAddress.Parse("192.0.2.7"), 40000, 40010), run.Listener.Targets.Single());
        Assert.AreEqual(TransferResult.Success(5), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_PortLiteral_IsNotResolved()
    {
        // curl -P 127.0.0.1 ftp://127.0.0.1:18437/a.txt: a literal is used as it is.
        var resolver = new NamedDnsResolver(new Dictionary<string, IPAddress[]>());
        ActiveRun run = await RunAsync("/a.txt", "127.0.0.1", LoggedIn + EprtOk + Retrieved, resolver, Pending(56703));

        Assert.AreEqual(LogInSent + "EPRT |1|127.0.0.1|56703|\r\n" + RetrieveSent, run.Sent);
        Assert.IsEmpty(resolver.Hosts);
        Assert.AreEqual(TransferResult.Success(5), run.Result);
    }

    [TestMethod]
    [DataRow("nosuch.invalid")]
    [DataRow("Loopback Pseudo-Interface 1")]
    public async Task ExecuteAsync_PortNameDoesNotResolve_ReportsCurlsTwoLinesAndEndsWithExit6WithoutQuit(string value)
    {
        // curl -v -P nosuch.invalid ftp://127.0.0.1:47466/f.txt (measured 2026-09-27, BL-466):
        // two -v lines, exit 6 and no QUIT. The Schannel build does not look interface names
        // up, so "Loopback Pseudo-Interface 1" ended the same way (ADR-0108).
        var events = new RecordingTransferEvents();
        var resolver = new NamedDnsResolver(new Dictionary<string, IPAddress[]>());
        ActiveRun run = await RunAsync("/a.txt", value, LoggedIn, context => context.Events = events, new QueuedListener(), resolver: resolver);

        Assert.AreEqual(LogInSent, run.Sent);
        CollectionAssert.AreEqual(new[] { value }, resolver.Hosts);
        Assert.AreEqual(0, run.Listener.Targets.Count);
        CollectionAssert.AreEqual(
            new[] { "Could not resolve host: " + value, "failed to resolve the address provided to PORT: " + value },
            events.Info);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.CouldntResolveHost, "Could not resolve host: " + value), run.Result);
    }

    [TestMethod]
    public void Constructor_NullDnsResolver_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(
            () => new FtpProtocolHandler(new QueuedConnector(), new QueuedListener(), new QueuedTlsProvider(), null!));
    }

    [TestMethod]
    public async Task ExecuteAsync_BindFails_QuitsWithTheListenersExitCodeBeforeEprt()
    {
        // curl -P 127.0.0.1:18437 ftp://127.0.0.1:18437/a.txt, the only port already in use
        var listener = new QueuedListener(ListenResult.Failed(CurlExitCode.FtpPortFailed, "bind() failed, ran out of ports"));
        ActiveRun run = await RunAsync("/a.txt", "127.0.0.1:18437", LoggedIn + Bye, _ => { }, listener);

        Assert.AreEqual(LogInSent + "QUIT\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.FtpPortFailed, "bind() failed, ran out of ports"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_PortAddressNotLocal_ListensAgainOnTheControlAddressAndAnnouncesThePortAddress()
    {
        // curl -v -P 192.0.2.1 ftp://127.0.0.1:47464/f.txt (measured 2026-09-27, BL-464): the
        // bind on 192.0.2.1 fails, curl prints the non-local line with -v, binds again on the
        // control connection's address and still announces 192.0.2.1 in EPRT.
        var events = new RecordingTransferEvents();
        var listener = new QueuedListener(NotLocal, ListenResult.Listening(Pending(61200)));
        ActiveRun run = await RunAsync("/a.txt", "192.0.2.1", LoggedIn + EprtOk + Retrieved, context => context.Events = events, listener);

        Assert.AreEqual(LogInSent + "EPRT |1|192.0.2.1|61200|\r\n" + RetrieveSent, run.Sent);
        CollectionAssert.AreEqual(
            new[] { new ListenTarget(IPAddress.Parse("192.0.2.1"), 0, 0), new ListenTarget(IPAddress.Loopback, 0, 0) },
            listener.Targets);
        CollectionAssert.AreEqual(new[] { NotLocalLine }, events.Info);
        Assert.AreEqual(TransferResult.Success(5), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_PortAddressNotLocalAndEprtRefused_RetriesTheBindAgainForPort()
    {
        // curl -v -P 192.0.2.1 ftp://127.0.0.1:47464/f.txt, EPRT and PORT answered 500 no
        // (measured 2026-09-27, BL-464): each bind retries once, so the line is printed twice.
        var events = new RecordingTransferEvents();
        var listener = new QueuedListener(NotLocal, ListenResult.Listening(Pending(61200)), NotLocal, ListenResult.Listening(Pending(61201)));
        ActiveRun run = await RunAsync("/a.txt", "192.0.2.1", LoggedIn + Refused + Refused + Bye, context => context.Events = events, listener);

        Assert.AreEqual(LogInSent + "EPRT |1|192.0.2.1|61200|\r\nPORT 192,0,2,1,239,17\r\nQUIT\r\n", run.Sent);
        CollectionAssert.AreEqual(new[] { NotLocalLine, NotLocalLine }, events.Info);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.FtpPortFailed, "Failed to do PORT"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_RetryOnTheControlAddressFailsToo_QuitsWithExit30AndBindFailedWithoutAThirdAttempt()
    {
        // curl 8.21.0's ftp_port_bind_socket retries once; a second failure is
        // failf(data, "bind(port=%hu) failed: %s") -> exit 30.
        var events = new RecordingTransferEvents();
        var listener = new QueuedListener(NotLocal, NotLocal);
        ActiveRun run = await RunAsync("/a.txt", "192.0.2.1", LoggedIn + Bye, context => context.Events = events, listener);

        Assert.AreEqual(LogInSent + "QUIT\r\n", run.Sent);
        Assert.HasCount(2, listener.Targets);
        CollectionAssert.AreEqual(new[] { NotLocalLine }, events.Info);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.FtpPortFailed, "bind(port=0) failed: Address not available"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_PortDashAndAddressNotAvailable_DoesNotRetry()
    {
        // -P - already binds the control connection's address: curl's non_local is false, so
        // EADDRNOTAVAIL is failf(data, "bind(port=%hu) failed: %s") at once.
        var events = new RecordingTransferEvents();
        var listener = new QueuedListener(NotLocal);
        ActiveRun run = await RunAsync("/a.txt", "-", LoggedIn + Bye, context => context.Events = events, listener);

        Assert.HasCount(1, listener.Targets);
        Assert.IsEmpty(events.Info);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.FtpPortFailed, "bind(port=0) failed: Address not available"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_PortAddressNotLocalAndControlAddressUnknown_DoesNotRetry()
    {
        // With no control connection address there is nothing to bind again on.
        var listener = new QueuedListener(NotLocal);
        ActiveRun run = await RunAsync("/a.txt", "192.0.2.1", LoggedIn + Bye, _ => { }, listener, controlLocal: null, useDefaultControlLocal: false);

        Assert.HasCount(1, listener.Targets);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.FtpPortFailed, "bind(port=0) failed: Address not available"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_ServerNeverConnects_QuitsWithExit12After60Seconds()
    {
        // curl -P - ftp://127.0.0.1:18437/a.txt, RETR answered 150 with no connection:
        // 60 seconds later, whatever --connect-timeout says, exit 12 after QUIT.
        var clock = new ImmediateTimerTimeProvider();
        var pending = new ScriptedPendingConnection(new IPEndPoint(IPAddress.Loopback, 52272), null);
        ActiveRun run = await RunAsync(
            "/a.txt",
            "-",
            LoggedIn + EprtOk + TypeSet + Sized + "150 Opening\r\n" + Bye,
            context => context.TimeProvider = clock,
            pending);

        Assert.AreEqual(LogInSent + "EPRT |1|127.0.0.1|52272|\r\n" + RetrieveSent, run.Sent);
        CollectionAssert.AreEqual(new[] { TimeSpan.FromSeconds(60) }, clock.Waits);
        Assert.AreEqual(
            TransferResult.Failure(CurlExitCode.FtpAcceptTimeout, "Accept timeout occurred while waiting server connect"),
            run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_AcceptFails_QuitsWithTheAcceptsExitCode()
    {
        var pending = new ScriptedPendingConnection(
            new IPEndPoint(IPAddress.Loopback, 56703),
            ConnectResult.Failed(CurlExitCode.FtpAcceptFailed, "Error accept()ing server connect"));
        ActiveRun run = await RunAsync("/a.txt", "-", LoggedIn + EprtOk + Retrieved, pending);

        Assert.AreEqual(LogInSent + "EPRT |1|127.0.0.1|56703|\r\n" + RetrieveSent, run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.FtpAcceptFailed, "Error accept()ing server connect"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_CancelledWhileWaitingForTheServer_LeavesAsCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var pending = new CancellingPendingConnection(cancellation);

        await Assert.ThrowsExactlyAsync<TaskCanceledException>(() => RunAsync(
            "/a.txt",
            "-",
            LoggedIn + EprtOk + Retrieved,
            context => context.CancellationToken = cancellation.Token,
            new QueuedListener(ListenResult.Listening(pending))));
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadInActiveMode_WritesTheFileToTheAcceptedConnection()
    {
        // curl -P - -T global.json ftp://127.0.0.1:18437/u.txt
        var data = new ScriptedConnection();
        var pending = new ScriptedPendingConnection(new IPEndPoint(IPAddress.Loopback, 56726), ConnectResult.Connected(data));
        ActiveRun run = await RunAsync(
            "/u.txt",
            "-",
            LoggedIn + EprtOk + TypeSet + Opened + Complete + Bye,
            context => context.Upload = new MemoryStream(Encoding.Latin1.GetBytes("hello")),
            pending);

        Assert.AreEqual(LogInSent + "EPRT |1|127.0.0.1|56726|\r\nTYPE I\r\nSTOR u.txt\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual("hello", Encoding.Latin1.GetString(data.Sent));
        Assert.AreEqual(TransferResult.Success(5), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadAndServerNeverConnects_QuitsWithExit12()
    {
        var clock = new ImmediateTimerTimeProvider();
        var pending = new ScriptedPendingConnection(new IPEndPoint(IPAddress.Loopback, 56726), null);
        ActiveRun run = await RunAsync(
            "/u.txt",
            "-",
            LoggedIn + EprtOk + TypeSet + Opened + Bye,
            context =>
            {
                context.Upload = new MemoryStream([1]);
                context.TimeProvider = clock;
            },
            pending);

        Assert.AreEqual(LogInSent + "EPRT |1|127.0.0.1|56726|\r\nTYPE I\r\nSTOR u.txt\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(CurlExitCode.FtpAcceptTimeout, run.Result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_HandlerWithoutListener_QuitsWithExit30()
    {
        // A handler built with a connector only cannot bind a port.
        var control = new ScriptedConnection(Encoding.Latin1.GetBytes(LoggedIn + Bye)) { LocalEndPoint = ControlLocal };
        var context = new TransferContext { Url = CurlUrl.Parse(Url + "/a.txt"), Output = new MemoryStream(), FtpPort = "-" };

        TransferResult result = await new FtpProtocolHandler(new QueuedConnector(ConnectResult.Connected(control))).ExecuteAsync(context);

        Assert.AreEqual(LogInSent + "QUIT\r\n", Encoding.Latin1.GetString(control.Sent));
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.FtpPortFailed, "Failed to do PORT") with { Report = result.Report }, result);
    }

    private static ScriptedPendingConnection Pending(int port, string address = "127.0.0.1") =>
        new(new IPEndPoint(IPAddress.Parse(address), port), ConnectResult.Connected(new ScriptedConnection(Encoding.Latin1.GetBytes("hello"))));

    private static Task<ActiveRun> RunAsync(string path, string ftpPort, string replies, params ScriptedPendingConnection[] pending) =>
        RunAsync(path, ftpPort, replies, _ => { }, pending);

    private static Task<ActiveRun> RunAsync(
        string path,
        string ftpPort,
        string replies,
        IDnsResolver resolver,
        params ScriptedPendingConnection[] pending) =>
        RunAsync(path, ftpPort, replies, _ => { }, new QueuedListener([.. pending.Select(ListenResult.Listening)]), resolver: resolver);

    private static Task<ActiveRun> RunAsync(
        string path,
        string ftpPort,
        string replies,
        Action<MutableContext> adjust,
        params ScriptedPendingConnection[] pending) =>
        RunAsync(path, ftpPort, replies, adjust, new QueuedListener([.. pending.Select(ListenResult.Listening)]));

    private static Task<ActiveRun> RunAsync(
        string path,
        string ftpPort,
        string replies,
        IPEndPoint? controlLocal,
        ScriptedPendingConnection[] pending) =>
        RunAsync(path, ftpPort, replies, _ => { }, new QueuedListener([.. pending.Select(ListenResult.Listening)]), controlLocal, controlLocal is not null);

    private static async Task<ActiveRun> RunAsync(
        string path,
        string ftpPort,
        string replies,
        Action<MutableContext> adjust,
        QueuedListener listener,
        IPEndPoint? controlLocal = null,
        bool useDefaultControlLocal = true,
        IDnsResolver? resolver = null)
    {
        var control = new ScriptedConnection(Encoding.Latin1.GetBytes(replies))
        {
            LocalEndPoint = controlLocal ?? (useDefaultControlLocal ? ControlLocal : null),
        };
        var connector = new QueuedConnector(ConnectResult.Connected(control));
        var context = MutableContext.Build(
            new TransferContext { Url = CurlUrl.Parse(Url + path), Output = new MemoryStream() },
            mutable =>
            {
                mutable.FtpPort = ftpPort;
                adjust(mutable);
            });
        var handler = resolver is null
            ? new FtpProtocolHandler(connector, listener, new QueuedTlsProvider())
            : new FtpProtocolHandler(connector, listener, new QueuedTlsProvider(), resolver);

        TransferResult result = await handler.ExecuteAsync(context);

        return new ActiveRun(result with { Report = null }, control, connector, listener, context.Output);
    }

    /// <summary>One active-mode transfer and what it left behind.</summary>
    private sealed record ActiveRun(
        TransferResult Result,
        ScriptedConnection Control,
        QueuedConnector Connector,
        QueuedListener Listener,
        Stream Output)
    {
        public string Sent => Encoding.Latin1.GetString(Control.Sent);

        public string OutputText => Encoding.Latin1.GetString(((MemoryStream)Output).ToArray());
    }

    /// <summary>A pending connection that cancels the transfer while it waits for the server.</summary>
    private sealed class CancellingPendingConnection(CancellationTokenSource cancellation) : IPendingConnection
    {
        public EndPoint LocalEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, 56703);

        public async ValueTask<ConnectResult> AcceptAsync(CancellationToken cancellationToken)
        {
            await cancellation.CancelAsync();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new InvalidOperationException("The wait cannot end without cancellation.");
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
