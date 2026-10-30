using System.Security.Cryptography;
using Curl.Extension.WireGuard.Tunnel;
using Curl.Extension.WireGuard.UnitTests.Tunnel;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.WireGuard.UnitTests;

/// <summary>Checks the <c>--wireguard</c> connection hook.</summary>
[TestClass]
public sealed class WireGuardConnectionHookTests
{
    private static readonly IConnector CurlConnector = new WireGuardConnector(TunnelTestPeer.Configuration(), new TunnelTestPeer(), TimeProvider.System, RandomNumberGenerator.Create(), httpOverTlsApplicationProtocols: ["h2", "http/1.1"]);

    [TestMethod]
    public async Task ConnectorOver_WithoutTlsProvider_GivesTheTunnelConnectorThatRefusesTls()
    {
        WireGuardConnectionHook hook = new(TunnelTestPeer.Configuration(), new TunnelTestPeer(), TimeProvider.System, RandomNumberGenerator.Create());

        IConnector connector = hook.ConnectorOver(CurlConnector);
        ConnectResult result = await connector.ConnectAsync(new ConnectTarget("10.0.0.1", 443, true), CancellationToken.None);

        Assert.AreSame(WireGuardExtension.Option, hook.Option);
        Assert.IsInstanceOfType<WireGuardConnector>(connector);
        Assert.AreEqual(CurlExitCode.NotBuiltIn, result.ExitCode);
    }

    [TestMethod]
    public void ConnectorOver_WithTlsProvider_OffersCurlsAlpnList()
    {
        WireGuardConnectionHook hook = new(TunnelTestPeer.Configuration(), new TunnelTestPeer(), TimeProvider.System, RandomNumberGenerator.Create());

        IConnector connector = hook.ConnectorOver(CurlConnector, new RefusingTlsProvider());

        CollectionAssert.AreEqual(new[] { "h2", "http/1.1" }, connector.HttpOverTlsApplicationProtocols.ToArray());
    }

    [TestMethod]
    public void ConnectorOver_NullCurlConnectorWithTlsProvider_Throws()
    {
        WireGuardConnectionHook hook = new(TunnelTestPeer.Configuration(), new TunnelTestPeer(), TimeProvider.System, RandomNumberGenerator.Create());

        Assert.ThrowsExactly<ArgumentNullException>(() => hook.ConnectorOver(null!, new RefusingTlsProvider()));
    }

    [TestMethod]
    public async Task DatagramConnectorOver_AddressOutsideTheTunnel_GivesTheTunnelDatagramConnectorThatOpensNothing()
    {
        TunnelTestPeer peer = new();
        WireGuardConnectionHook hook = new(TunnelTestPeer.Configuration(), peer, TimeProvider.System, RandomNumberGenerator.Create());

        IDatagramConnector connector = hook.DatagramConnectorOver(null!);
        DatagramOpenResult result = await connector.OpenAsync("192.168.1.1", 443, CancellationToken.None);

        Assert.IsInstanceOfType<WireGuardDatagramConnector>(connector);
        Assert.IsNull(result.Channel);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.IsNull(peer.OpenedEndpoint);
    }

    [TestMethod]
    public async Task ConnectorOver_QuicConnect_GoesThroughCurlsConnectorWhoseDatagramsTheTunnelCarries()
    {
        WireGuardConnectionHook hook = new(TunnelTestPeer.Configuration(), new TunnelTestPeer(), TimeProvider.System, RandomNumberGenerator.Create());
        QuicRecordingConnector curl = new();
        ConnectTarget target = new("10.0.0.1", 443, true);

        IConnector connector = hook.ConnectorOver(curl, new RefusingTlsProvider());
        MultiplexedConnectResult connection = await connector.ConnectMultiplexedAsync(target, CancellationToken.None);
        ConnectResult session = await connector.ConnectMultiplexedSessionAsync(target, _ => null!, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "connection", "session" }, curl.Calls);
        Assert.AreEqual("curl's QUIC", connection.ErrorMessage);
        Assert.AreEqual("curl's QUIC session", session.ErrorMessage);
    }

    [TestMethod]
    public async Task ConnectMultiplexed_WithoutCurlsConnector_IsRefused()
    {
        IConnector connector = new WireGuardConnector(TunnelTestPeer.Configuration(), new TunnelTestPeer(), TimeProvider.System, RandomNumberGenerator.Create());
        ConnectTarget target = new("10.0.0.1", 443, true);

        MultiplexedConnectResult connection = await connector.ConnectMultiplexedAsync(target, CancellationToken.None);
        ConnectResult session = await connector.ConnectMultiplexedSessionAsync(target, _ => null!, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntConnect, connection.ExitCode);
        Assert.AreEqual(CurlExitCode.CouldntConnect, session.ExitCode);
    }

    private sealed class QuicRecordingConnector : IConnector
    {
        public List<string> Calls { get; } = [];

        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("TCP never goes through curl's connector under --wireguard.");

        public ValueTask<MultiplexedConnectResult> ConnectMultiplexedAsync(ConnectTarget target, CancellationToken cancellationToken)
        {
            Calls.Add("connection");
            return ValueTask.FromResult(MultiplexedConnectResult.Failed(CurlExitCode.CouldntConnect, "curl's QUIC"));
        }

        public ValueTask<ConnectResult> ConnectMultiplexedSessionAsync(ConnectTarget target, Func<IMultiplexedConnection, IConnection> openSession, CancellationToken cancellationToken)
        {
            Calls.Add("session");
            return ValueTask.FromResult(ConnectResult.Failed(CurlExitCode.CouldntConnect, "curl's QUIC session"));
        }
    }

    private sealed class RefusingTlsProvider : ITlsProvider
    {
        public ValueTask<ConnectResult> AuthenticateAsClientAsync(IConnection plaintext, string targetHost, CancellationToken cancellationToken) =>
            ValueTask.FromResult(ConnectResult.Failed(CurlExitCode.SslConnectError, "refused"));
    }
}
