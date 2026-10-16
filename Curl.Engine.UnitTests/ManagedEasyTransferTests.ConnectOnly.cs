using System.Text;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.ConnectOnly;
using Curl.Api.Options;
using Curl.Api.Options.Network;
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
    public async Task PerformAsync_ConnectOnlyHttps_ConnectsWithTlsToTheDefaultPort()
    {
        (IEasyTransfer easy, TargetRecordingConnector connector) = CreateConnectOnlyEasy(ConnectResult.Connected(new ControlledConnection()));
        await using IEasyTransfer handle = easy;
        easy.Options = ConnectOnlyTo("https://h/");

        await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(new ConnectTarget("h", 443, true), connector.Targets.Single());
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
