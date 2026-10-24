using System.Text;
using Curl.Conformance.Differential.Protocols.Core;
using Curl.Conformance.Differential.Protocols.Proxy;
using Curl.Conformance.Differential.UnitTests.Http.Core;

namespace Curl.Conformance.Differential.UnitTests.Protocols.Proxy;

[TestClass]
public sealed class HttpProxyResponderTests
{
    private const string Forwarded = "HTTP/1.1 200 OK\r\nContent-Length: 11\r\nX-Via: proxy\r\n\r\nproxy reply";
    private const string Challenge = "HTTP/1.1 407 Proxy Authentication Required\r\nProxy-Authenticate: Basic realm=\"p\"\r\nContent-Length: 0\r\n\r\n";
    private const string Established = "HTTP/1.1 200 Connection established\r\nX-Proxy: yes\r\n\r\n";
    private const string Get = "GET http://example.test/ HTTP/1.1\r\nHost: example.test\r\n\r\n";

    [TestMethod]
    public async Task RespondAsync_ForwardedRequest_AnswersTheFixedReplyAndLogsTheRawHead()
    {
        var (output, log, connector) = await RunAsync(Get, false);

        Assert.AreEqual(Forwarded, output);
        Assert.AreEqual(Get, log);
        Assert.IsNull(connector.Host);
    }

    [TestMethod]
    public async Task RespondAsync_NeedAuthWithoutCredentials_ChallengesAndWaitsForTheNextRequest()
    {
        var authorised = "GET http://example.test/ HTTP/1.1\r\nproxy-Authorization : Basic eDp5\r\n\r\n";

        var (output, log, _) = await RunAsync(Get + authorised, true);

        Assert.AreEqual(Challenge + Forwarded, output);
        Assert.AreEqual(Get + "\n" + authorised, log);
    }

    [TestMethod]
    public async Task RespondAsync_NeedAuthAndTheClientHangsUp_LogsTheEmptyRequestAndEnds()
    {
        var (output, log, _) = await RunAsync(Get, true);

        Assert.AreEqual(Challenge, output);
        Assert.AreEqual(Get + "\n", log);
    }

    [TestMethod]
    public async Task RespondAsync_ConnectWithoutNeedAuth_EstablishesTheTunnelAndRelaysBothWays()
    {
        var request = "CONNECT host.test:8443 HTTP/1.1\r\nHost: host.test:8443\r\n\r\nhello";

        var (output, _, connector) = await RunAsync(request, false);

        Assert.AreEqual(Established + "from remote", output);
        Assert.AreEqual("host.test", connector.Host);
        Assert.AreEqual(8443, connector.Port);
    }

    [TestMethod]
    public async Task RespondAsync_ConnectToLocalhostTest_ConnectsToTheLoopbackAddress()
    {
        var (_, _, connector) = await RunAsync("CONNECT localhost.test:80 HTTP/1.1\r\n\r\n", false);

        Assert.AreEqual("127.0.0.1", connector.Host);
    }

    [TestMethod]
    public async Task RespondAsync_ConnectTargetUnreachable_AnswersEstablishedThenEnds()
    {
        var (output, _, _) = await RunAsync("CONNECT host.test:80 HTTP/1.1\r\n\r\n", false, reachable: false);

        Assert.AreEqual(Established, output);
    }

    [TestMethod]
    [DataRow("CONNECT host.test HTTP/1.1\r\n\r\n")]
    [DataRow("CONNECT host.test:x HTTP/1.1\r\n\r\n")]
    [DataRow("CONNECT :: HTTP/1.1\r\n\r\n")]
    public async Task RespondAsync_ConnectTargetWithoutANumericPort_EndsWithoutAnswering(string request)
    {
        var (output, log, connector) = await RunAsync(request, false);

        Assert.AreEqual(string.Empty, output);
        Assert.AreEqual(request, log);
        Assert.IsNull(connector.Host);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("GET /\r\n\r\n")]
    public async Task RespondAsync_RequestLineNotThreeWords_EndsWithoutAnswering(string request)
    {
        var (output, log, _) = await RunAsync(request, false);

        Assert.AreEqual(string.Empty, output);
        Assert.AreEqual(request, log);
    }

    private static async Task<(string Output, string Log, FakeConnector Connector)> RunAsync(string input, bool needAuth, bool reachable = true)
    {
        var log = new ProtocolWireLog();
        var connector = new FakeConnector(reachable);
        using var connection = new DuplexMemoryStream(Encoding.Latin1.GetBytes(input));

        await new HttpProxyResponder(log, connector, needAuth).RespondAsync(connection, CancellationToken.None);

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
