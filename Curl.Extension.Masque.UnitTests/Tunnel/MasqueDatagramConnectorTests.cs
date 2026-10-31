using System.Net.Sockets;
using System.Text;
using Curl.Extension.Masque.Wire;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Masque.Tunnel;

/// <summary>Checks how a MASQUE tunnel is opened through curl's connector and how each failure maps to curl's exit codes.</summary>
[TestClass]
public sealed class MasqueDatagramConnectorTests
{
    [TestMethod]
    public async Task OpenAsync_HttpsTemplate_ConnectsToTheProxyOverTlsAndSendsTheUpgrade()
    {
        var connection = new ScriptedProxyConnection();
        connection.Enqueue(MasqueProxyBytes.SwitchingProtocols("connect-udp"));
        var connector = new RecordingConnector(ConnectResult.Connected(connection));

        var result = await new MasqueDatagramConnector(connector, MasqueProxyBytes.UdpTemplate, MasqueTunnelKind.ConnectUdp)
            .OpenAsync("tftp.example", 69, CancellationToken.None);

        Assert.IsNotNull(result.Channel);
        Assert.AreEqual(new ConnectTarget("proxy.example", 4443, true), connector.Target);
        var request = MasqueUpgrade.BuildRequest(new Uri("https://proxy.example:4443/masque?target_host=tftp.example&target_port=69"), MasqueTunnelKind.ConnectUdp);
        CollectionAssert.AreEqual(request, connection.Written);
    }

    [TestMethod]
    public async Task OpenAsync_HttpTemplate_ConnectsToTheProxyWithoutTls()
    {
        var connection = new ScriptedProxyConnection();
        connection.Enqueue(MasqueProxyBytes.SwitchingProtocols("connect-udp"));
        var connector = new RecordingConnector(ConnectResult.Connected(connection));

        await new MasqueDatagramConnector(connector, "http://proxy.example/{target_host}/{target_port}", MasqueTunnelKind.ConnectUdp)
            .OpenAsync("192.0.2.1", 69, CancellationToken.None);

        Assert.AreEqual(new ConnectTarget("proxy.example", 80, false), connector.Target);
    }

    [TestMethod]
    public async Task OpenAsync_UnreachableProxy_KeepsTheConnectorsExitCodeAndMessage()
    {
        var connector = new RecordingConnector(ConnectResult.Failed(CurlExitCode.CouldntResolveHost, "Could not resolve host: proxy.example"));

        var result = await new MasqueDatagramConnector(connector, MasqueProxyBytes.UdpTemplate, MasqueTunnelKind.ConnectUdp)
            .OpenAsync("192.0.2.1", 69, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntResolveHost, result.ExitCode);
        Assert.AreEqual("Could not resolve host: proxy.example", result.ErrorMessage);
    }

    [TestMethod]
    public async Task OpenAsync_RefusedUpgrade_FailsWithExitSevenAndTheStatus()
    {
        var connection = new ScriptedProxyConnection();
        connection.Enqueue(Encoding.ASCII.GetBytes("HTTP/1.1 403 Forbidden\r\nContent-Length: 0\r\n\r\n"));

        var result = await OpenAsync(connection, MasqueTunnelKind.ConnectUdp, "192.0.2.1");

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("MASQUE proxy refused the connect-udp upgrade with status 403", result.ErrorMessage);
        Assert.IsTrue(connection.IsDisposed);
    }

    [TestMethod]
    public async Task OpenAsync_ProxyClosesBeforeAnswering_FailsWithExitSeven()
    {
        var connection = new ScriptedProxyConnection();
        connection.Enqueue("HTTP/1.1 101"u8.ToArray());

        var result = await OpenAsync(connection, MasqueTunnelKind.ConnectIp, "192.0.2.1");

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("MASQUE proxy closed the connection before answering the connect-ip upgrade", result.ErrorMessage);
    }

    [TestMethod]
    public async Task OpenAsync_ProxyClosesBeforeAssigningAnAddress_FailsWithExitSeven()
    {
        var connection = new ScriptedProxyConnection();
        connection.Enqueue(MasqueProxyBytes.SwitchingProtocols("connect-ip"));

        var result = await OpenAsync(connection, MasqueTunnelKind.ConnectIp, "192.0.2.1");

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("MASQUE proxy closed the connect-ip tunnel before assigning an address", result.ErrorMessage);
        Assert.IsTrue(connection.IsDisposed);
    }

    [TestMethod]
    public async Task OpenAsync_ConnectIpToAHostName_FailsWithExitSixBeforeConnecting()
    {
        var connector = new RecordingConnector(ConnectResult.Failed(CurlExitCode.CouldntConnect, "unused"));

        var result = await new MasqueDatagramConnector(connector, MasqueProxyBytes.IpTemplate, MasqueTunnelKind.ConnectIp)
            .OpenAsync("tftp.example", 69, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntResolveHost, result.ExitCode);
        Assert.AreEqual("MASQUE CONNECT-IP needs an IP address target, not the host name tftp.example", result.ErrorMessage);
        Assert.IsNull(connector.Target);
    }

    [TestMethod]
    public async Task OpenAsync_TemplateThatDoesNotExpand_FailsWithExitSeven()
    {
        var connector = new RecordingConnector(ConnectResult.Failed(CurlExitCode.CouldntConnect, "unused"));

        var result = await new MasqueDatagramConnector(connector, "https://proxy.example/{target_host}", MasqueTunnelKind.ConnectUdp)
            .OpenAsync("192.0.2.1", 69, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.StartsWith("MASQUE URI template must name", result.ErrorMessage!);
    }

    [TestMethod]
    public async Task OpenAsync_WriteFails_FailsWithExitSevenAndTheWritesMessage()
    {
        var connection = new ScriptedProxyConnection { WriteFailure = new IOException("broken pipe") };

        var result = await OpenAsync(connection, MasqueTunnelKind.ConnectUdp, "192.0.2.1");

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("broken pipe", result.ErrorMessage);
        Assert.IsTrue(connection.IsDisposed);
    }

    [TestMethod]
    public async Task OpenAsync_ReadFails_FailsWithExitSeven()
    {
        var connection = new ScriptedProxyConnection { ReadFailure = new SocketException((int)SocketError.ConnectionReset) };

        var result = await OpenAsync(connection, MasqueTunnelKind.ConnectUdp, "192.0.2.1");

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
    }

    [TestMethod]
    public async Task OpenAsync_ReplyHeadPastTheBufferLimit_FailsWithExitSeven()
    {
        var connection = new ScriptedProxyConnection();
        connection.Enqueue(new byte[MasqueTunnelStream.MaximumBufferedLength + 1]);

        var result = await OpenAsync(connection, MasqueTunnelKind.ConnectUdp, "192.0.2.1");

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual($"MASQUE proxy sent more than {MasqueTunnelStream.MaximumBufferedLength} bytes without ending a reply head or capsule", result.ErrorMessage);
    }

    [TestMethod]
    public async Task OpenAsync_Cancelled_LetsTheCancellationEscape()
    {
        var connection = new ScriptedProxyConnection { WriteFailure = new OperationCanceledException() };

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () => await OpenAsync(connection, MasqueTunnelKind.ConnectUdp, "192.0.2.1"));

        Assert.IsTrue(connection.IsDisposed);
    }

    private static async Task<DatagramOpenResult> OpenAsync(ScriptedProxyConnection connection, MasqueTunnelKind kind, string host)
    {
        var template = kind == MasqueTunnelKind.ConnectUdp ? MasqueProxyBytes.UdpTemplate : MasqueProxyBytes.IpTemplate;
        return await new MasqueDatagramConnector(new RecordingConnector(ConnectResult.Connected(connection)), template, kind)
            .OpenAsync(host, 69, CancellationToken.None);
    }
}
