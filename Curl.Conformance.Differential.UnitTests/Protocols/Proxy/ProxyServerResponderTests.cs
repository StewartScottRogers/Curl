using System.Text;
using Curl.Conformance.Differential.Protocols.Core;
using Curl.Conformance.Differential.Protocols.Proxy;
using Curl.Conformance.Differential.UnitTests.Http.Core;

namespace Curl.Conformance.Differential.UnitTests.Protocols.Proxy;

[TestClass]
public sealed class ProxyServerResponderTests
{
    private const string Socks5Granted = "\x05\x00\x05\x00\x00\x01\x00\x00\x00\x00\x00\x00";

    [TestMethod]
    public async Task RespondAsync_Socks5ConnectByIPv4_LogsMethodsAndTargetGrantsAndTunnels()
    {
        var (output, log, connector) = await RunAsync("\x05\x02\x00\x02\x05\x01\x00\x01\x0a\x00\x00\x01\x1f\x90hello");

        Assert.AreEqual(Socks5Granted + "from remote", output);
        Assert.AreEqual("SOCKS5 methods=0002\nSOCKS5 connect atyp=1 host=10.0.0.1 port=8080", log);
        Assert.AreEqual("10.0.0.1", connector.Host);
        Assert.AreEqual(8080, connector.Port);
    }

    [TestMethod]
    public async Task RespondAsync_Socks5ConnectByNameLocalhostTest_LogsTheNameAndConnectsToTheLoopbackAddress()
    {
        var (_, log, connector) = await RunAsync("\x05\x01\x00\x05\x01\x00\x03\x0elocalhost.test\x00\x50");

        Assert.AreEqual("SOCKS5 methods=00\nSOCKS5 connect atyp=3 host=localhost.test port=80", log);
        Assert.AreEqual("127.0.0.1", connector.Host);
    }

    [TestMethod]
    public async Task RespondAsync_Socks5ConnectByIPv6_LogsTheAddressAsHex()
    {
        var (_, log, connector) = await RunAsync("\x05\x01\x00\x05\x01\x00\x04" + new string('\x00', 15) + "\x01\x01\xbb");

        Assert.AreEqual("SOCKS5 methods=00\nSOCKS5 connect atyp=4 host=00000000000000000000000000000001 port=443", log);
        Assert.AreEqual("00000000000000000000000000000001", connector.Host);
    }

    [TestMethod]
    public async Task RespondAsync_Socks5TargetUnreachable_GrantsThenEnds()
    {
        var (output, _, _) = await RunAsync("\x05\x01\x00\x05\x01\x00\x01\x0a\x00\x00\x01\x00\x50", reachable: false);

        Assert.AreEqual(Socks5Granted, output);
    }

    [TestMethod]
    [DataRow("\x05", "", "")]
    [DataRow("\x05\x01\x00\x05\x01", "\x05\x00", "SOCKS5 methods=00")]
    [DataRow("\x05\x01\x00\x05\x01\x00\x01\x0a\x00\x00\x01\x00", "\x05\x00", "SOCKS5 methods=00")]
    [DataRow("\x05\x01\x00\x05\x01\x00\x03", "\x05\x00", "SOCKS5 methods=00")]
    public async Task RespondAsync_Socks5RequestCutShort_EndsWithoutGranting(string input, string expectedOutput, string expectedLog)
    {
        var (output, log, connector) = await RunAsync(input);

        Assert.AreEqual(expectedOutput, output);
        Assert.AreEqual(expectedLog, log);
        Assert.IsNull(connector.Host);
    }

    [TestMethod]
    [DataRow("\x0a\x00\x00\x01", "10.0.0.1")]
    [DataRow("\x00\x01\x00\x01", "0.1.0.1")]
    [DataRow("\x00\x00\x01\x01", "0.0.1.1")]
    public async Task RespondAsync_Socks4Connect_LogsTheAddressAndUserIdGrantsAndTunnels(string address, string expectedHost)
    {
        var (output, log, connector) = await RunAsync("\x04\x01\x00\x50" + address + "us'r\x00hello");

        Assert.AreEqual("\x00\x5a\x00\x50" + address + "from remote", output);
        Assert.AreEqual($"SOCKS4 ip={expectedHost} port=80 uid=b\"us'r\"", log);
        Assert.AreEqual(expectedHost, connector.Host);
        Assert.AreEqual(80, connector.Port);
    }

    [TestMethod]
    public async Task RespondAsync_Socks4aConnectToLocalhostTest_LogsTheNameAndConnectsToTheLoopbackAddress()
    {
        var (output, log, connector) = await RunAsync("\x04\x01\x01\xbb\x00\x00\x00\x01\x00localhost.test\x00");

        Assert.AreEqual("\x00\x5a\x01\xbb\x00\x00\x00\x01" + "from remote", output);
        Assert.AreEqual("SOCKS4a host=localhost.test port=443 uid=b''", log);
        Assert.AreEqual("127.0.0.1", connector.Host);
        Assert.AreEqual(443, connector.Port);
    }

    [TestMethod]
    public async Task RespondAsync_Socks4aWhenTheStreamEndsInsideTheUserId_LogsAnEmptyHostName()
    {
        var (_, log, _) = await RunAsync("\x04\x01\x00\x50\x00\x00\x00\x01u");

        Assert.AreEqual("SOCKS4a host= port=80 uid=b'u'", log);
    }

    [TestMethod]
    public async Task RespondAsync_Socks4HeaderCutShort_EndsWithoutAnswering()
    {
        var (output, log, connector) = await RunAsync("\x04\x01\x00");

        Assert.AreEqual(string.Empty, output);
        Assert.AreEqual(string.Empty, log);
        Assert.IsNull(connector.Host);
    }

    [TestMethod]
    public async Task RespondAsync_HttpRequest_GoesToTheHttpProxyResponderWithItsFirstByte()
    {
        var request = "GET http://example.test/ HTTP/1.1\r\nHost: example.test\r\n\r\n";

        var (output, log, _) = await RunAsync(request);

        Assert.AreEqual("HTTP/1.1 200 OK\r\nContent-Length: 11\r\nX-Via: proxy\r\n\r\nproxy reply", output);
        Assert.AreEqual(request, log);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("\nGET / HTTP/1.1\r\n\r\n")]
    public async Task RespondAsync_HttpRequestLineNotThreeWords_EndsWithoutAnswering(string request)
    {
        var (output, log, _) = await RunAsync(request);

        Assert.AreEqual(string.Empty, output);
        Assert.AreEqual(request, log);
    }

    private static async Task<(string Output, string Log, FakeConnector Connector)> RunAsync(string input, bool reachable = true)
    {
        var log = new ProtocolWireLog();
        var connector = new FakeConnector(reachable);
        using var connection = new DuplexMemoryStream(Encoding.Latin1.GetBytes(input));
        var responder = new ProxyServerResponder(log, connector, new HttpProxyResponder(log, connector, false));

        await responder.RespondAsync(connection, CancellationToken.None);

        return (Encoding.Latin1.GetString(connection.Written), Encoding.UTF8.GetString(log.Take()), connector);
    }

    private sealed class FakeConnector(bool reachable) : IProxyTunnelConnector
    {
        public string? Host { get; private set; }

        public int Port { get; private set; }

        public Task<Stream?> ConnectAsync(string host, int port, CancellationToken cancellationToken)
        {
            Host = host;
            Port = port;
            return Task.FromResult<Stream?>(reachable ? new DuplexMemoryStream("from remote"u8.ToArray()) : null);
        }
    }
}
