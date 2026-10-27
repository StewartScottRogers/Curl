using System.Net;
using System.Text;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// CGR-088 of the third-party gap report (BL-3617): curl 8.22.0's <c>failf</c> writes a refused
/// CONNECT's or SOCKS handshake's message as a <c>-v</c> line before <c>closing connection #0</c>
/// (measured: <c>curl -sv -p -x http://127.0.0.1:20600 http://example.test/</c> answered 403 prints
/// <c>* CONNECT tunnel failed, response 403</c>; a SOCKS4 proxy answering 91 prints
/// <c>* [SOCKS] cannot complete SOCKS4 connection to 0.0.0.0:0. (91), request rejected or failed.</c>).
/// </summary>
[TestClass]
public sealed class Cgr088Tests
{
    [TestMethod]
    public async Task ConnectAsync_ProxyRefusesTheConnect_EchoesTheFailureAsAnInfoLine()
    {
        var events = new RecordingTransferEvents();
        var connector = Connector(Encoding.Latin1.GetBytes("HTTP/1.1 403 Forbidden\r\n\r\n"));

        var result = await connector.ConnectAsync(
            new ConnectTarget("example.test", 80, UseTls: false) { Proxy = new ProxyEndpoint(ProxyKind.Http, "proxy.example", 3128, null), Events = events },
            CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("CONNECT tunnel failed, response 403", events.Info[^1]);
    }

    [TestMethod]
    public async Task ConnectAsync_Socks4ProxyRejects_EchoesTheFailureAsAnInfoLine()
    {
        var events = new RecordingTransferEvents();
        var connector = Connector([0x00, 0x5B, 0, 0, 0, 0, 0, 0]);

        var result = await connector.ConnectAsync(
            new ConnectTarget("127.0.0.1", 8080, UseTls: false) { Proxy = new ProxyEndpoint(ProxyKind.Socks4, "proxy.example", 1080, null), Events = events },
            CancellationToken.None);

        const string Message = "[SOCKS] cannot complete SOCKS4 connection to 0.0.0.0:0. (91), request rejected or failed.";
        Assert.AreEqual(CurlExitCode.Proxy, result.ExitCode);
        Assert.AreEqual(Message, result.ErrorMessage);
        Assert.AreEqual(Message, events.Info[^1]);
    }

    private static TcpConnector Connector(byte[] proxyReply)
    {
        var proxyConnection = new ScriptedConnection(proxyReply);
        return new TcpConnector(
            new FakeDnsResolver(IPAddress.Parse("192.0.2.10")),
            new FakeTcpDialer { DialOutcome = _ => proxyConnection },
            new FakeTlsProvider(),
            new ManualTimeProvider());
    }
}
