using System.Text;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.ConnectOnly;
using Curl.Api.EngineContracts.Multi;
using Curl.Api.Options;
using Curl.Api.Options.Network;
using Curl.Api.Options.Proxy;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

public sealed partial class ManagedEasyTransferTests
{
    private static EasyOptions ConnectOnlyTo(string url) =>
        new() { Network = new EasyNetworkOptions { Url = url, ConnectOnly = ConnectOnly.Connect } };

    private static (IEasyTransfer Easy, TargetRecordingConnector Connector) CreateConnectOnlyEasy(ConnectResult result)
    {
        TargetRecordingConnector connector = new(result);
        ManagedEasyTransfer easy = (ManagedEasyTransfer)CreateEngine(new ScriptedConnector([])).CreateEasy();
        easy.CreateConnectOnlyConnector = (_, _) => connector;
        return (easy, connector);
    }

    [TestMethod]
    public async Task PerformAsync_ConnectOnly_ConnectsSendsNothingAndLeavesTheConnectionToSendAndRecv()
    {
        ControlledConnection peer = new();
        (IEasyTransfer easy, TargetRecordingConnector connector) = CreateConnectOnlyEasy(ConnectResult.Connected(peer));
        await using IEasyTransfer handle = easy;
        easy.Options = ConnectOnlyTo("http://h:8080/x");

        CurlCode code = await easy.PerformAsync(CancellationToken.None);
        byte[] nothingSent = peer.Written;
        CurlConnectOnlyResult sent = await easy.Connection.SendAsync("ping"u8.ToArray(), CancellationToken.None);
        byte[] buffer = new byte[16];
        CurlConnectOnlyResult notReady = await easy.Connection.ReceiveAsync(buffer, CancellationToken.None);
        peer.Deliver("pong"u8.ToArray());
        CurlConnectOnlyResult received = await easy.Connection.ReceiveAsync(buffer, CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual(string.Empty, easy.ErrorText);
        Assert.AreEqual(new ConnectTarget("h", 8080, false), connector.Targets.Single());
        Assert.IsEmpty(nothingSent);
        Assert.AreEqual(new CurlConnectOnlyResult(CurlCode.Ok, 4), sent);
        Assert.AreEqual("ping", Encoding.ASCII.GetString(peer.Written));
        Assert.AreEqual(new CurlConnectOnlyResult(CurlCode.Again, 0), notReady);
        Assert.AreEqual(new CurlConnectOnlyResult(CurlCode.Ok, 4), received);
        Assert.AreEqual("pong", Encoding.ASCII.GetString(buffer, 0, 4));
        Assert.AreEqual("http://h:8080/x", easy.Info.EffectiveUrl);
    }

    [TestMethod]
    public async Task Info_ConnectOnlyConnectionOpen_GivesItsSocketAsActiveAndLastSocketUntilNoLongerConnectOnly()
    {
        (IEasyTransfer easy, _) = CreateConnectOnlyEasy(ConnectResult.Connected(new ControlledConnection()));
        await using IEasyTransfer handle = easy;
        easy.Options = ConnectOnlyTo("http://h:8080/x");
        long beforePerform = easy.Info.ActiveSocket;

        await easy.PerformAsync(CancellationToken.None);
        long active = easy.Info.ActiveSocket;
        long last = easy.Info.LastSocket;
        easy.Options = easy.Options with { Network = easy.Options.Network with { ConnectOnly = ConnectOnly.Disabled } };

        Assert.AreEqual(-1, beforePerform);
        Assert.IsGreaterThan(ConnectOnlyConnection.FirstSocket, active);
        Assert.AreEqual(active, last);
        Assert.AreEqual(-1, easy.Info.ActiveSocket);
    }

    [TestMethod]
    public async Task MultiWaitAsync_ConnectOnlyActiveSocket_ReportsWritableAndReadableOnceBytesArrive()
    {
        ControlledConnection peer = new();
        (IEasyTransfer easy, _) = CreateConnectOnlyEasy(ConnectResult.Connected(peer));
        await using IEasyTransfer handle = easy;
        easy.Options = ConnectOnlyTo("http://h:8080/x");
        await using IMultiTransfer multi = CreateEngine(new ScriptedConnector([])).CreateMulti();
        multi.Add(easy);
        while ((await multi.PerformAsync(CancellationToken.None)).RunningTransfers > 0)
        {
            await multi.WaitAsync(Memory<CurlWaitSocket>.Empty, TimeSpan.FromMilliseconds(50), CancellationToken.None);
        }

        CurlSocket socket = new((nint)easy.Info.ActiveSocket);
        CurlWaitSocket[] writable = [new(socket, CurlWaitEvents.PollOut, CurlWaitEvents.None)];
        (CurlMultiCode Code, int ReadySockets) writableWait = await multi.WaitAsync(writable, TimeSpan.FromSeconds(5), CancellationToken.None);
        CurlWaitSocket[] readable = [new(socket, CurlWaitEvents.PollIn, CurlWaitEvents.None)];
        (CurlMultiCode Code, int ReadySockets) emptyWait = await multi.WaitAsync(readable, TimeSpan.Zero, CancellationToken.None);
        peer.Deliver("* OK\r\n"u8.ToArray());
        (CurlMultiCode Code, int ReadySockets) readableWait = await multi.WaitAsync(readable, TimeSpan.FromSeconds(5), CancellationToken.None);

        Assert.AreEqual((CurlMultiCode.Ok, 1), writableWait);
        Assert.AreEqual(CurlWaitEvents.PollOut, writable[0].ReturnedEvents);
        Assert.AreEqual((CurlMultiCode.Ok, 0), emptyWait);
        Assert.AreEqual((CurlMultiCode.Ok, 1), readableWait);
        Assert.AreEqual(CurlWaitEvents.PollIn, readable[0].ReturnedEvents);
    }

    [TestMethod]
    public async Task ConnectOnlyReadyEvents_AnotherSocket_IsNull()
    {
        (IEasyTransfer easy, _) = CreateConnectOnlyEasy(ConnectResult.Connected(new ControlledConnection()));
        await using IEasyTransfer handle = easy;
        easy.Options = ConnectOnlyTo("http://h:8080/x");
        CurlWaitEvents? beforePerform = ((ManagedEasyTransfer)easy).ConnectOnlyReadyEvents(new CurlSocket(7), CurlWaitEvents.PollOut);
        await easy.PerformAsync(CancellationToken.None);

        CurlWaitEvents? other = ((ManagedEasyTransfer)easy).ConnectOnlyReadyEvents(new CurlSocket(7), CurlWaitEvents.PollOut);
        CurlWaitEvents? bad = ((ManagedEasyTransfer)easy).ConnectOnlyReadyEvents(CurlSocket.Bad, CurlWaitEvents.PollOut);

        Assert.IsNull(beforePerform);
        Assert.IsNull(other);
        Assert.IsNull(bad);
    }

    [TestMethod]
    public async Task PerformAsync_ConnectOnlyHttps_ConnectsWithTlsToTheDefaultPort()
    {
        (IEasyTransfer easy, TargetRecordingConnector connector) = CreateConnectOnlyEasy(ConnectResult.Connected(new ControlledConnection()));
        await using IEasyTransfer handle = easy;
        easy.Options = ConnectOnlyTo("https://h/");

        await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(new ConnectTarget("h", 443, true), connector.Targets.Single());
    }

    [TestMethod]
    public async Task PerformAsync_ConnectOnlyWithAProxy_DialsTheTargetThroughThatProxy()
    {
        (IEasyTransfer easy, TargetRecordingConnector connector) = CreateConnectOnlyEasy(ConnectResult.Connected(new ControlledConnection()));
        await using IEasyTransfer handle = easy;
        easy.Options = ConnectOnlyTo("http://h:8080/x") with { Proxy = new EasyProxyOptions { Proxy = "http://proxy:3128" } };

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, code);
        ConnectTarget target = connector.Targets.Single();
        Assert.AreEqual(new ConnectTarget("h", 8080, false) { Proxy = new ProxyEndpoint(ProxyKind.Http, "proxy", 3128, null) }, target);
    }

    [TestMethod]
    public async Task PerformAsync_ConnectOnlyWithASocks5Proxy_DialsTheTargetThroughThatProxy()
    {
        (IEasyTransfer easy, TargetRecordingConnector connector) = CreateConnectOnlyEasy(ConnectResult.Connected(new ControlledConnection()));
        await using IEasyTransfer handle = easy;
        easy.Options = ConnectOnlyTo("https://h/") with { Proxy = new EasyProxyOptions { Proxy = "socks5://s:1080" } };

        await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(new ConnectTarget("h", 443, true) { Proxy = new ProxyEndpoint(ProxyKind.Socks5, "s", 1080, null) }, connector.Targets.Single());
    }

    [TestMethod]
    public async Task PerformAsync_ConnectOnlyToAHostInTheNoProxyList_DialsNoProxy()
    {
        (IEasyTransfer easy, TargetRecordingConnector connector) = CreateConnectOnlyEasy(ConnectResult.Connected(new ControlledConnection()));
        await using IEasyTransfer handle = easy;
        easy.Options = ConnectOnlyTo("http://h:8080/x") with { Proxy = new EasyProxyOptions { Proxy = "http://proxy:3128", NoProxy = "h" } };

        await easy.PerformAsync(CancellationToken.None);

        Assert.IsNull(connector.Targets.Single().Proxy);
    }

    [TestMethod]
    public async Task PerformAsync_ConnectOnlyWithUnusableProxyText_AnswersTheSelectorsFailureAndDialsNothing()
    {
        (IEasyTransfer easy, TargetRecordingConnector connector) = CreateConnectOnlyEasy(ConnectResult.Connected(new ControlledConnection()));
        await using IEasyTransfer handle = easy;
        easy.Options = ConnectOnlyTo("http://h/") with { Proxy = new EasyProxyOptions { Proxy = "foo://proxy:1" } };

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.CouldntConnect, code);
        Assert.AreEqual("Unsupported proxy scheme for 'foo://proxy:1'", easy.ErrorText);
        Assert.IsEmpty(connector.Targets);
    }

    [TestMethod]
    public async Task PerformAsync_ConnectOnlyConnectFails_AnswersTheConnectorsCodeAndTextAndLeavesNoConnection()
    {
        (IEasyTransfer easy, _) = CreateConnectOnlyEasy(ConnectResult.Failed(CurlExitCode.CouldntConnect, ConnectFailure));
        await using IEasyTransfer handle = easy;
        easy.Options = ConnectOnlyTo("http://h/");

        CurlCode code = await easy.PerformAsync(CancellationToken.None);
        CurlConnectOnlyResult receive = await easy.Connection.ReceiveAsync(new byte[1], CancellationToken.None);

        Assert.AreEqual(CurlCode.CouldntConnect, code);
        Assert.AreEqual(ConnectFailure, easy.ErrorText);
        Assert.AreEqual(CurlCode.UnsupportedProtocol, receive.Result);
    }

    [TestMethod]
    public async Task Connection_BeforeAnyPerform_AnswersUnsupportedProtocol()
    {
        await using IEasyTransfer easy = CreateEngine(new ScriptedConnector([])).CreateEasy();

        CurlConnectOnlyResult send = await easy.Connection.SendAsync("x"u8.ToArray(), CancellationToken.None);

        Assert.AreEqual(new CurlConnectOnlyResult(CurlCode.UnsupportedProtocol, 0), send);
    }

    [TestMethod]
    public async Task PerformAsync_AfterAConnectOnlyPerform_ClosesItsConnection()
    {
        ControlledConnection peer = new();
        (IEasyTransfer easy, _) = CreateConnectOnlyEasy(ConnectResult.Connected(peer));
        await using IEasyTransfer handle = easy;
        easy.Options = ConnectOnlyTo("http://h/");
        await easy.PerformAsync(CancellationToken.None);
        easy.Options = new EasyOptions();

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.UrlMalformat, code);
        Assert.IsTrue(peer.IsDisposed);
    }

    [TestMethod]
    public async Task DisposeAsync_AfterAConnectOnlyPerform_ClosesItsConnection()
    {
        ControlledConnection peer = new();
        (IEasyTransfer easy, _) = CreateConnectOnlyEasy(ConnectResult.Connected(peer));
        easy.Options = ConnectOnlyTo("http://h/");
        await easy.PerformAsync(CancellationToken.None);

        await easy.DisposeAsync();

        Assert.IsTrue(peer.IsDisposed);
    }

    [TestMethod]
    public async Task Duplicate_ConnectOnlyPerform_DialsThroughTheSameConnectorAndKeepsItsOwnConnection()
    {
        ControlledConnection peer = new();
        (IEasyTransfer easy, TargetRecordingConnector connector) = CreateConnectOnlyEasy(ConnectResult.Connected(peer));
        await using IEasyTransfer handle = easy;
        easy.Options = ConnectOnlyTo("http://h/");
        await easy.PerformAsync(CancellationToken.None);
        await using IEasyTransfer duplicate = easy.Duplicate();

        CurlConnectOnlyResult beforePerform = await duplicate.Connection.SendAsync("x"u8.ToArray(), CancellationToken.None);
        await duplicate.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.UnsupportedProtocol, beforePerform.Result);
        Assert.HasCount(2, connector.Targets);
    }

    private static (IEasyTransfer Easy, ScriptedConnector Connector) CreateConnectOnlyLoginEasy(params string[] replies)
    {
        ScriptedConnector connector = new(replies.Select(Encoding.ASCII.GetBytes));
        ManagedEasyTransfer easy = (ManagedEasyTransfer)CreateEngine(new ScriptedConnector([])).CreateEasy();
        easy.CreateConnectOnlyConnector = (_, _) => connector;
        return (easy, connector);
    }

    [TestMethod]
    public async Task PerformAsync_ConnectOnlyFtp_LogsInAndLeavesTheControlConnectionToSendAndRecv()
    {
        (IEasyTransfer easy, ScriptedConnector connector) = CreateConnectOnlyLoginEasy("220 hi\r\n", "331 pw\r\n", "230 in\r\n", "257 \"/\"\r\n", "200 noop\r\n");
        await using IEasyTransfer handle = easy;
        easy.Options = ConnectOnlyTo("ftp://h:2121/");

        CurlCode code = await easy.PerformAsync(CancellationToken.None);
        string loggedIn = Encoding.ASCII.GetString(connector.Written);
        CurlConnectOnlyResult sent = await easy.Connection.SendAsync("NOOP\r\n"u8.ToArray(), CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual("h", connector.Targets.Single().Host);
        Assert.AreEqual(2121, connector.Targets.Single().Port);
        Assert.AreEqual("USER anonymous\r\nPASS ftp@example.com\r\nPWD\r\n", loggedIn);
        Assert.AreEqual(new CurlConnectOnlyResult(CurlCode.Ok, 6), sent);
        Assert.EndsWith("NOOP\r\n", Encoding.ASCII.GetString(connector.Written));
    }

    [TestMethod]
    public async Task PerformAsync_ConnectOnlyFtpLoginRefused_AnswersLoginDeniedAndLeavesNoConnection()
    {
        (IEasyTransfer easy, _) = CreateConnectOnlyLoginEasy("220 hi\r\n", "530 no\r\n");
        await using IEasyTransfer handle = easy;
        easy.Options = ConnectOnlyTo("ftp://h/");

        CurlCode code = await easy.PerformAsync(CancellationToken.None);
        CurlConnectOnlyResult receive = await easy.Connection.ReceiveAsync(new byte[1], CancellationToken.None);

        Assert.AreEqual(CurlCode.LoginDenied, code);
        Assert.AreEqual(CurlCode.UnsupportedProtocol, receive.Result);
    }

    [TestMethod]
    public async Task PerformAsync_ConnectOnlyImap_SendsCapabilityAndLeavesTheConnectionToSendAndRecv()
    {
        (IEasyTransfer easy, ScriptedConnector connector) = CreateConnectOnlyLoginEasy("* OK hi\r\n", "* CAPABILITY IMAP4rev1\r\nA001 OK done\r\n");
        await using IEasyTransfer handle = easy;
        easy.Options = ConnectOnlyTo("imap://h/");

        CurlCode code = await easy.PerformAsync(CancellationToken.None);
        string sentBeforeEnd = Encoding.ASCII.GetString(connector.Written);
        CurlConnectOnlyResult sent = await easy.Connection.SendAsync("A002 NOOP\r\n"u8.ToArray(), CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual(143, connector.Targets.Single().Port);
        Assert.AreEqual("A001 CAPABILITY\r\n", sentBeforeEnd);
        Assert.AreEqual(new CurlConnectOnlyResult(CurlCode.Ok, 11), sent);
    }

    [TestMethod]
    public async Task CreateConnectOnlyConnector_DefaultOptions_BuildsTheEnginesTcpConnector()
    {
        await using ManagedCurlEngine engine = new();

        IConnector connector = ManagedCurlEngine.CreateConnectOnlyConnector(new EasyOptions(), engine.Shared);

        Assert.IsInstanceOfType<Curl.Networking.TcpConnector>(connector);
    }

    private sealed class TargetRecordingConnector(ConnectResult result) : IConnector
    {
        public List<ConnectTarget> Targets { get; } = [];

        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
        {
            Targets.Add(target);
            return ValueTask.FromResult(result);
        }
    }
}
