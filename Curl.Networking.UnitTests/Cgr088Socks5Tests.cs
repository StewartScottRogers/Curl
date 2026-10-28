using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// CGR-088 of the third-party gap report (BL-3950): curl 8.22.0's <c>failf</c> writes a failed SOCKS5
/// handshake's message as a <c>-v</c> line (measured against a loopback proxy,
/// <c>curl -v --proxy socks5h://127.0.0.1:P http://example.test/</c>: reply 5 prints
/// <c>* cannot complete SOCKS5 connection to example.test. (5)</c>, address type 9 prints
/// <c>* SOCKS5 reply has wrong address type.</c>, version 4 prints
/// <c>* SOCKS5 reply has wrong version, version should be 5.</c>).
/// </summary>
[TestClass]
public sealed class Cgr088Socks5Tests
{
    [TestMethod]
    [DataRow(new byte[] { 5, 5, 0, 1, 0, 0, 0, 0, 0, 0 }, "cannot complete SOCKS5 connection to example.test. (5)")]
    [DataRow(new byte[] { 5, 0, 0, 9, 0, 0, 0, 0, 0, 0 }, "SOCKS5 reply has wrong address type.")]
    [DataRow(new byte[] { 4, 0, 0, 1, 0, 0, 0, 0, 0, 0 }, "SOCKS5 reply has wrong version, version should be 5.")]
    public async Task ConnectAsync_Socks5ProxyFails_EchoesTheFailureAsAnInfoLine(byte[] connectReply, string message)
    {
        var events = new RecordingTransferEvents();
        byte[] reply = [5, 0, .. connectReply];
        var connector = new TcpConnector(
            new FakeDnsResolver(System.Net.IPAddress.Parse("192.0.2.10")),
            new FakeTcpDialer { DialOutcome = _ => new ScriptedConnection(reply) },
            new FakeTlsProvider(),
            new ManualTimeProvider());

        var result = await connector.ConnectAsync(
            new ConnectTarget("example.test", 80, UseTls: false) { Proxy = new ProxyEndpoint(ProxyKind.Socks5Hostname, "proxy.example", 1080, null), Events = events },
            CancellationToken.None);

        Assert.AreEqual(CurlExitCode.Proxy, result.ExitCode);
        Assert.AreEqual(message, result.ErrorMessage);
        Assert.AreEqual(message, events.Info[^1]);
    }

    [TestMethod]
    public async Task ConnectAsync_Socks5HostnameTooLong_EchoesTheFailureAsAnInfoLine()
    {
        var events = new RecordingTransferEvents();
        var connector = new TcpConnector(
            new FakeDnsResolver(System.Net.IPAddress.Parse("192.0.2.10")),
            new FakeTcpDialer { DialOutcome = _ => new ScriptedConnection([]) },
            new FakeTlsProvider(),
            new ManualTimeProvider());

        var result = await connector.ConnectAsync(
            new ConnectTarget(new string('a', 256), 80, UseTls: false) { Proxy = new ProxyEndpoint(ProxyKind.Socks5Hostname, "proxy.example", 1080, null), Events = events },
            CancellationToken.None);

        Assert.AreEqual(result.ErrorMessage, events.Info[^1]);
    }
}
