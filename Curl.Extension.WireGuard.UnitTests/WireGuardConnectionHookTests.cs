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
    public async Task DatagramConnectorOver_AnyUdpSocket_IsRefusedSoNothingLeavesOutsideTheTunnel()
    {
        WireGuardConnectionHook hook = new(TunnelTestPeer.Configuration(), new TunnelTestPeer(), TimeProvider.System, RandomNumberGenerator.Create());

        DatagramOpenResult result = await hook.DatagramConnectorOver(null!).OpenAsync("10.0.0.1", 443, CancellationToken.None);

        Assert.IsNull(result.Channel);
        Assert.AreEqual(CurlExitCode.NotBuiltIn, result.ExitCode);
        Assert.AreEqual("UDP to 10.0.0.1 port 443 through the WireGuard tunnel is not built in", result.ErrorMessage);
    }

    private sealed class RefusingTlsProvider : ITlsProvider
    {
        public ValueTask<ConnectResult> AuthenticateAsClientAsync(IConnection plaintext, string targetHost, CancellationToken cancellationToken) =>
            ValueTask.FromResult(ConnectResult.Failed(CurlExitCode.SslConnectError, "refused"));
    }
}
