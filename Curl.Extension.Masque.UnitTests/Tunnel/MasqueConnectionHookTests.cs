using Curl.Extension.Masque.Wire;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Masque.Tunnel;

/// <summary>
/// Checks the <c>--masque</c> hook against RFC 9298 section 3 and RFC 9484 section 4: curl's stream connector is kept
/// and reaches the proxy, and every datagram channel QUIC opens goes through the proxy's upgrade.
/// </summary>
[TestClass]
public sealed class MasqueConnectionHookTests
{
    [TestMethod]
    public void ConnectorOver_KeepsCurlsConnector()
    {
        var curlConnector = new RecordingConnector(ConnectResult.Failed(CurlExitCode.CouldntConnect, "unused"));

        Assert.AreSame(curlConnector, new MasqueConnectionHook(MasqueProxyBytes.UdpTemplate, MasqueTunnelKind.ConnectUdp).ConnectorOver(curlConnector));
    }

    [TestMethod]
    public void ConnectorOver_Null_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => new MasqueConnectionHook(MasqueProxyBytes.UdpTemplate, MasqueTunnelKind.ConnectUdp).ConnectorOver(null!));

    [TestMethod]
    public async Task DatagramConnectorOver_ConnectUdp_SendsTheRfc9298Section3UpgradeThroughCurlsConnectorBoundAfterIt()
    {
        var connection = new ScriptedProxyConnection();
        connection.Enqueue(MasqueProxyBytes.SwitchingProtocols("connect-udp"));
        var curlConnector = new RecordingConnector(ConnectResult.Connected(connection));
        var hook = new MasqueConnectionHook(MasqueProxyBytes.UdpTemplate, MasqueTunnelKind.ConnectUdp);

        IDatagramConnector datagrams = hook.DatagramConnectorOver(new LateBoundConnectorTests.UnusedDatagramConnector());
        hook.ConnectorOver(curlConnector);
        DatagramOpenResult result = await datagrams.OpenAsync("quic.example", 443, CancellationToken.None);

        Assert.IsNotNull(result.Channel, result.ErrorMessage);
        Assert.AreEqual(new ConnectTarget("proxy.example", 4443, true), curlConnector.Target);
        var request = MasqueUpgrade.BuildRequest(new Uri("https://proxy.example:4443/masque?target_host=quic.example&target_port=443"), MasqueTunnelKind.ConnectUdp);
        CollectionAssert.AreEqual(request, connection.Written);
    }

    [TestMethod]
    public async Task DatagramConnectorOver_ConnectIp_SendsTheRfc9484Section4UpgradeThroughCurlsConnector()
    {
        var connection = new ScriptedProxyConnection();
        connection.Enqueue(MasqueProxyBytes.SwitchingProtocols("connect-ip"));
        var hook = new MasqueConnectionHook(MasqueProxyBytes.IpTemplate, MasqueTunnelKind.ConnectIp);
        hook.ConnectorOver(new RecordingConnector(ConnectResult.Connected(connection)));

        await hook.DatagramConnectorOver(new LateBoundConnectorTests.UnusedDatagramConnector()).OpenAsync("192.0.2.1", 443, CancellationToken.None);

        var request = MasqueUpgrade.BuildRequest(new Uri("https://proxy.example/ip?target=192.0.2.1&ipproto=17"), MasqueTunnelKind.ConnectIp);
        CollectionAssert.AreEqual(request, connection.Written.Take(request.Length).ToArray());
    }

    [TestMethod]
    public async Task DatagramConnectorOver_BeforeCurlsConnectorIsBound_FailsTheOpenWithExitSeven()
    {
        var hook = new MasqueConnectionHook(MasqueProxyBytes.UdpTemplate, MasqueTunnelKind.ConnectUdp);

        DatagramOpenResult result = await hook.DatagramConnectorOver(new LateBoundConnectorTests.UnusedDatagramConnector()).OpenAsync("quic.example", 443, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
    }
}
