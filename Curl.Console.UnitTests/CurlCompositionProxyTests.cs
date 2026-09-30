using System.Text;

using Curl.Authentication;
using Curl.Cli;
using Curl.Core;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins <c>-x</c>, <c>-U</c>, <c>--noproxy</c>, <c>-p</c>, the <c>--socks</c> options and the
/// proxy environment variables end to end through the production composition: a plain
/// <c>http</c> request forwarded by the HTTP handler over a <see cref="ScriptedConnector" />, and
/// the CONNECT tunnel opened by a real <see cref="TcpConnector" /> over a
/// <see cref="ScriptedTcpDialer" />. Every expected byte was measured on 2026-09-27 with
/// curl 8.21.0 (mingw, Schannel) against <c>Record-CurlExchange.ps1</c> or a loopback proxy
/// answering CONNECT with <c>200 Connection established</c> (BL-238 Notes). The SOCKS5 bytes
/// were measured the same way against a loopback SOCKS5 proxy answering <c>05 00</c>, and the
/// HTTPS-proxy CONNECT bytes inside the proxy's TLS by BL-266 (BL-328 Notes).
/// </summary>
[TestClass]
public sealed class CurlCompositionProxyTests
{
    private const string Hello = "HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello";

    private const string ProxyAuthenticationRequired = "HTTP/1.1 407 Proxy Authentication Required\r\nContent-Length: 0\r\n\r\n";

    private const string ConnectionEstablished = "HTTP/1.1 200 Connection established\r\n\r\n";

    private const string ForwardedGet =
        "GET http://example.com/a HTTP/1.1\r\nHost: example.com\r\n"
        + "User-Agent: curl/8.21.0\r\nAccept: */*\r\nProxy-Connection: Keep-Alive\r\n\r\n";

    private const string TunnelledGet = "GET /a HTTP/1.1\r\nHost: example.com\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n";

    private static readonly ConnectTarget Proxy = new("127.0.0.1", 18238, false);

    private static readonly ConnectTarget ForwardProxy = Proxy with { PoolScheme = "http", IsForwardProxy = true };

    [TestMethod]
    public async Task RunAsync_HttpUrlWithProxyOption_ForwardsTheRequestInAbsoluteFormToTheProxy()
    {
        ScriptedConnector server = new([Latin1(Hello)]);

        Run run = await RunAsync(server, "-sS", "-x", "127.0.0.1:18238", "http://example.com/a");

        Assert.AreEqual(0, run.ExitCode);
        Assert.AreEqual(ForwardedGet, Latin1(server.Written));
        Assert.AreEqual(ForwardProxy, server.Targets.Single());
        Assert.AreEqual("hello", run.StandardOutput);
    }

    [TestMethod]
    public async Task RunAsync_ProxyUserOption_SendsProxyAuthorizationToTheForwardProxy()
    {
        ScriptedConnector server = new([Latin1(Hello)]);

        Run run = await RunAsync(server, "-sS", "-x", "http://127.0.0.1:18238", "-U", "u:p", "http://example.com/a");

        Assert.AreEqual(0, run.ExitCode);
        Assert.AreEqual(
            "GET http://example.com/a HTTP/1.1\r\nHost: example.com\r\nProxy-Authorization: Basic dTpw\r\n"
            + "User-Agent: curl/8.21.0\r\nAccept: */*\r\nProxy-Connection: Keep-Alive\r\n\r\n",
            Latin1(server.Written));
    }

    [TestMethod]
    public async Task RunAsync_ProxyUserOption_ReplacesTheProxyUrlsUserInformation()
    {
        ScriptedConnector server = new([Latin1(Hello)]);

        await RunAsync(server, "-sS", "-x", "http://a:b@127.0.0.1:18238", "-U", "u:p", "http://example.com/a");

        StringAssert.Contains(Latin1(server.Written), "Proxy-Authorization: Basic dTpw\r\n");
    }

    [TestMethod]
    public async Task RunAsync_HttpProxyEnvironmentVariable_ForwardsTheRequestToIt()
    {
        ScriptedConnector server = new([Latin1(Hello)]);
        Dictionary<string, string> environment = new() { ["http_proxy"] = "http://127.0.0.1:18238" };

        Run run = await RunAsync(server, environment, "-sS", "http://example.com/a");

        Assert.AreEqual(0, run.ExitCode);
        Assert.AreEqual(ForwardedGet, Latin1(server.Written));
        Assert.AreEqual(ForwardProxy, server.Targets.Single());
    }

    [TestMethod]
    public async Task RunAsync_NoProxyOptionNamingTheHost_ConnectsDirectly()
    {
        ScriptedConnector server = new([Latin1(Hello)]);

        Run run = await RunAsync(server, "-sS", "-x", "127.0.0.1:1", "--noproxy", "127.0.0.1", "http://127.0.0.1:18238/a");

        Assert.AreEqual(0, run.ExitCode);
        Assert.AreEqual(
            "GET /a HTTP/1.1\r\nHost: 127.0.0.1:18238\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n",
            Latin1(server.Written));
        Assert.AreEqual(Proxy with { PoolScheme = "http" }, server.Targets.Single());
    }

    [TestMethod]
    public async Task RunAsync_RedirectToANoProxyHost_SendsTheSecondRequestDirectly()
    {
        // Measured (BL-329 Notes): curl 8.21.0 sends "GET http://a.test/" to the proxy, then
        // "GET / HTTP/1.1" with "Host: b.test:18329" straight to b.test.
        ScriptedConnector server = new([Latin1(RedirectTo("http://b.test:18329/")), Latin1(Hello)]);

        Run run = await RunAsync(server, "-sS", "-L", "-x", "127.0.0.1:18238", "--noproxy", "b.test", "http://a.test/");

        Assert.AreEqual(0, run.ExitCode);
        Assert.AreEqual(
            "GET http://a.test/ HTTP/1.1\r\nHost: a.test\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\nProxy-Connection: Keep-Alive\r\n\r\n"
            + "GET / HTTP/1.1\r\nHost: b.test:18329\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n",
            Latin1(server.Written));
        Assert.AreEqual(ForwardProxy, server.Targets[0]);
        Assert.AreEqual(("b.test", 18329, false, (ProxyEndpoint?)null), RouteOf(server.Targets[1]));
    }

    [TestMethod]
    public async Task RunAsync_RedirectFromHttpToHttpsWithOnlyHttpProxySet_ConnectsToTheHttpsHostDirectly()
    {
        // Measured (BL-329 Notes): with only http_proxy set, curl 8.21.0 forwards the http
        // request to the proxy, then opens TLS straight to b.test with no CONNECT.
        ScriptedConnector server = new([Latin1(RedirectTo("https://b.test:18329/")), Latin1(Hello)]);
        Dictionary<string, string> environment = new() { ["http_proxy"] = "http://127.0.0.1:18238" };

        Run run = await RunAsync(server, environment, "-sS", "-L", "http://a.test/");

        Assert.AreEqual(0, run.ExitCode);
        Assert.AreEqual(ForwardProxy, server.Targets[0]);
        Assert.AreEqual(("b.test", 18329, true, (ProxyEndpoint?)null), RouteOf(server.Targets[1]));
        StringAssert.EndsWith(Latin1(server.Written), "GET / HTTP/1.1\r\nHost: b.test:18329\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n");
    }

    [TestMethod]
    public async Task RunAsync_ProxyTunnelOption_SendsConnectThenTheRequestThroughTheTunnel()
    {
        ScriptedConnector server = new([Latin1(ConnectionEstablished), Latin1(Hello)]);

        Run run = await RunThroughTcpConnectorAsync(server, "-sS", "-p", "-x", "127.0.0.1:18238", "-U", "u:p", "http://example.com/a");

        Assert.AreEqual(0, run.ExitCode);
        Assert.AreEqual(
            "CONNECT example.com:80 HTTP/1.1\r\nHost: example.com:80\r\nProxy-Authorization: Basic dTpw\r\n"
            + "User-Agent: curl/8.21.0\r\nProxy-Connection: Keep-Alive\r\n\r\n"
            + TunnelledGet,
            Latin1(server.Written));
        Assert.AreEqual(Proxy, server.Targets.Single());
        Assert.AreEqual("hello", run.StandardOutput);
    }

    [TestMethod]
    public async Task RunAsync_ProxyTunnelOptionAnswered407_ExitsSevenWithCurlsLine()
    {
        ScriptedConnector server = new([Latin1(ProxyAuthenticationRequired)]);

        Run run = await RunThroughTcpConnectorAsync(server, "-sS", "-p", "-x", "127.0.0.1:18238", "http://example.com/a");

        Assert.AreEqual(7, run.ExitCode);
        Assert.AreEqual(
            "CONNECT example.com:80 HTTP/1.1\r\nHost: example.com:80\r\nUser-Agent: curl/8.21.0\r\nProxy-Connection: Keep-Alive\r\n\r\n",
            Latin1(server.Written));
        Assert.AreEqual($"curl: (7) CONNECT tunnel failed, response 407{Environment.NewLine}", run.StandardError);
        Assert.AreEqual(string.Empty, run.StandardOutput);
    }

    [TestMethod]
    public async Task RunAsync_HttpsUrlWithProxyOption_TunnelsWithConnectAndSendsTheRequestOverTls()
    {
        ScriptedConnector server = new([Latin1(ConnectionEstablished), Latin1(Hello)]);

        Run run = await RunThroughTcpConnectorAsync(server, "-sS", "-x", "127.0.0.1:18238", "https://example.com/a");

        Assert.AreEqual(0, run.ExitCode);
        Assert.AreEqual(
            "CONNECT example.com:443 HTTP/1.1\r\nHost: example.com:443\r\nUser-Agent: curl/8.21.0\r\nProxy-Connection: Keep-Alive\r\n\r\n"
            + TunnelledGet,
            Latin1(server.Written));
        Assert.AreEqual("hello", run.StandardOutput);
    }

    [TestMethod]
    public async Task RunAsync_HttpsUrlWithProxyAnswered407_ExitsSevenWithCurlsLine()
    {
        ScriptedConnector server = new([Latin1(ProxyAuthenticationRequired)]);

        Run run = await RunThroughTcpConnectorAsync(server, "-sS", "-x", "127.0.0.1:18238", "-U", "u:p", "https://example.com/a");

        Assert.AreEqual(7, run.ExitCode);
        Assert.AreEqual(
            "CONNECT example.com:443 HTTP/1.1\r\nHost: example.com:443\r\nProxy-Authorization: Basic dTpw\r\n"
            + "User-Agent: curl/8.21.0\r\nProxy-Connection: Keep-Alive\r\n\r\n",
            Latin1(server.Written));
        Assert.AreEqual($"curl: (7) CONNECT tunnel failed, response 407{Environment.NewLine}", run.StandardError);
    }

    [TestMethod]
    public async Task RunAsync_UnsupportedProxyScheme_ExitsSevenWithoutConnecting()
    {
        ScriptedConnector server = new([Latin1(Hello)]);

        Run run = await RunAsync(server, "-sS", "-x", "foo://127.0.0.1:1111", "http://example.com/a");

        Assert.AreEqual(7, run.ExitCode);
        Assert.AreEqual($"curl: (7) Unsupported proxy scheme for 'foo://127.0.0.1:1111'{Environment.NewLine}", run.StandardError);
        Assert.AreEqual(0, server.Targets.Count);
    }

    [TestMethod]
    [DataRow("--socks5", new byte[] { 5, 1, 0, 1, 127, 0, 0, 1, 0, 80 }, DisplayName = "--socks5")]
    [DataRow("--socks5-hostname", new byte[] { 5, 1, 0, 3, 11, (byte)'e', (byte)'x', (byte)'a', (byte)'m', (byte)'p', (byte)'l', (byte)'e', (byte)'.', (byte)'c', (byte)'o', (byte)'m', 0, 80 }, DisplayName = "--socks5-hostname")]
    public async Task RunAsync_Socks5Option_SendsCurlsGreetingAndConnectRequestThenTheRequestThroughTheTunnel(string option, byte[] connectRequest)
    {
        ScriptedConnector server = new([[5, 0], [5, 0, 0, 1, 127, 0, 0, 1, 0, 80], Latin1(Hello)]);

        Run run = await RunThroughTcpConnectorAsync(server, "-sS", option, "127.0.0.1:18238", "http://example.com/a");

        Assert.AreEqual(0, run.ExitCode);
        CollectionAssert.AreEqual(
            (byte[])[5, 2, 0, 1, .. connectRequest, .. Latin1(TunnelledGet)],
            server.Written);
        Assert.AreEqual(Proxy, server.Targets.Single());
        Assert.AreEqual("hello", run.StandardOutput);
    }

    [TestMethod]
    [DataRow(new[] { "-x", "https://127.0.0.1:18238", "https://example.com/a" }, 443, DisplayName = "https URL")]
    [DataRow(new[] { "-p", "-x", "https://127.0.0.1:18238", "http://example.com/a" }, 80, DisplayName = "-p, http URL")]
    public async Task RunAsync_HttpsProxyTunnel_SendsConnectOverTlsThenTheRequestThroughTheTunnel(string[] arguments, int port)
    {
        ScriptedConnector server = new([Latin1(ConnectionEstablished), Latin1(Hello)]);

        Run run = await RunThroughTcpConnectorAsync(server, ["-sS", .. arguments]);

        Assert.AreEqual(0, run.ExitCode);
        Assert.AreEqual(
            $"CONNECT example.com:{port} HTTP/1.1\r\nHost: example.com:{port}\r\nUser-Agent: curl/8.21.0\r\nProxy-Connection: Keep-Alive\r\n\r\n"
            + TunnelledGet,
            Latin1(server.Written));
        Assert.AreEqual(Proxy, server.Targets.Single());
        Assert.AreEqual("hello", run.StandardOutput);
    }

    [TestMethod]
    [DataRow(new[] { "--socks5", "127.0.0.1:1", "dict://example.com/d:x" }, ProxyKind.Socks5, DisplayName = "--socks5, dict URL")]
    [DataRow(new[] { "-x", "https://127.0.0.1:1", "gopher://example.com/" }, ProxyKind.Https, DisplayName = "https proxy, gopher URL")]
    [DataRow(new[] { "--socks4a", "127.0.0.1:1", "https://example.com/a" }, ProxyKind.Socks4a, DisplayName = "--socks4a, https URL")]
    [DataRow(new[] { "-L", "--socks4", "127.0.0.1:1", "http://example.com/a" }, ProxyKind.Socks4, DisplayName = "--socks4, -L")]
    public async Task RunAsync_SocksOrHttpsProxyForATunnelledUrl_ConnectTargetCarriesTheProxy(string[] arguments, ProxyKind kind)
    {
        RecordingConnector connector = new(CurlExitCode.CouldntConnect, "refused");

        await RunAsync(connector, new Dictionary<string, string>(), ["-sS", .. arguments]);

        ProxyEndpoint proxy = connector.Targets.Single().Proxy!;
        Assert.AreEqual((kind, "127.0.0.1", 1), (proxy.Kind, proxy.Host, proxy.Port));
    }

    [TestMethod]
    public async Task RunAsync_HttpsProxyForAPlainHttpUrl_ForwardsTheRequestToTheProxyOverTls()
    {
        ScriptedConnector server = new([Latin1(Hello)]);

        Run run = await RunAsync(server, "-sS", "-x", "https://127.0.0.1:18238", "http://example.com/a");

        Assert.AreEqual(0, run.ExitCode);
        Assert.AreEqual(ForwardedGet, Latin1(server.Written));
        Assert.AreEqual(new ConnectTarget("127.0.0.1", 18238, true) { PoolScheme = "http", IsForwardProxy = true }, server.Targets.Single());
    }

    [TestMethod]
    public async Task RunAsync_SocksProxyEnvironmentVariableForAnotherScheme_ConnectTargetCarriesTheProxy()
    {
        RecordingConnector connector = new(CurlExitCode.CouldntConnect, "refused");
        Dictionary<string, string> environment = new() { ["all_proxy"] = "socks5://127.0.0.1:1" };

        await RunAsync(connector, environment, ["-sS", "telnet://127.0.0.1:18238"]);

        ProxyEndpoint proxy = connector.Targets.Single().Proxy!;
        Assert.AreEqual((ProxyKind.Socks5, "127.0.0.1", 1), (proxy.Kind, proxy.Host, proxy.Port));
    }

    [TestMethod]
    public async Task RunAsync_ProxyTunnelWithUserAgentOption_ConnectRequestCarriesIt()
    {
        ScriptedConnector server = new([Latin1(ConnectionEstablished), Latin1(Hello)]);
        string[] arguments = ["-sS", "-p", "-A", "agent/1.0", "-x", "127.0.0.1:18238", "http://example.com/a"];

        Run run = await RunThroughTcpConnectorAsync(server, TunnelOptionsFor(arguments), arguments);

        Assert.AreEqual(0, run.ExitCode);
        Assert.StartsWith(
            "CONNECT example.com:80 HTTP/1.1\r\nHost: example.com:80\r\nUser-Agent: agent/1.0\r\nProxy-Connection: Keep-Alive\r\n\r\n",
            Latin1(server.Written));
    }

    [TestMethod]
    public async Task RunAsync_ProxyTunnelWithProxyHeaderAndHeader_ConnectRequestCarriesOnlyTheProxyHeader()
    {
        // curl -s -x http://127.0.0.1:18336 -p --proxy-header "X-P: 1" -H "X-A: 1" http://example.com/ (BL-347 Notes)
        ScriptedConnector server = new([Latin1(ConnectionEstablished), Latin1(Hello)]);
        string[] arguments = ["-sS", "-p", "--proxy-header", "X-P: 1", "-H", "X-A: 1", "-x", "127.0.0.1:18238", "http://example.com/a"];

        Run run = await RunThroughTcpConnectorAsync(server, TunnelOptionsFor(arguments), arguments);

        Assert.AreEqual(0, run.ExitCode);
        Assert.StartsWith(
            "CONNECT example.com:80 HTTP/1.1\r\nHost: example.com:80\r\nUser-Agent: curl/8.21.0\r\nProxy-Connection: Keep-Alive\r\nX-P: 1\r\n\r\nGET /a HTTP/1.1\r\n",
            Latin1(server.Written));
    }

    [TestMethod]
    public void CreateProxyTunnelOptions_EncodesCommandLineTextInThePlatformEncoding()
    {
        HttpProxyTunnelOptions options = TunnelOptionsFor(["-p", "-x", "127.0.0.1:18238", "http://example.com/a"]);

        Assert.AreEqual(
            CredentialEncoding.ForPlatform(OperatingSystem.IsWindows()).CodePage,
            options.CommandLineTextEncoding.CodePage);
    }

    [TestMethod]
    [DataRow(new[] { "-p", "-x", "127.0.0.1:18602", "http://example.com/a" }, HttpAuthSchemes.Basic)]
    [DataRow(new[] { "--proxy-digest", "-p", "-x", "127.0.0.1:18602", "http://example.com/a" }, HttpAuthSchemes.Digest)]
    [DataRow(new[] { "--proxy-anyauth", "-p", "-x", "127.0.0.1:18602", "http://example.com/a" }, HttpAuthSchemes.Any)]
    public void CreateProxyTunnelOptions_AllowsTheSchemeTheProxyAuthSwitchesPickAndAnswersWithTheRankedAuthenticator(string[] arguments, HttpAuthSchemes expected)
    {
        HttpProxyTunnelOptions options = TunnelOptionsFor(arguments);

        Assert.AreEqual(expected, options.ProxyAuthSchemes);
        Assert.IsInstanceOfType<RankedHttpAuthenticator>(options.ProxyAuthenticator);
    }

    [TestMethod]
    public async Task CreateTransports_ProxyNtlm_AnswersTheTunnelWithTheRoutersType1()
    {
        // The tunnel's authenticator is bound to ADR-0142's router once the connectors exist
        // (BL-604): SSPI's Type 1 on Windows, curl's own NTLM's elsewhere.
        CurlTransports transports = CurlComposition.CreateTransports(
            CommandLineParser.Parse(["--proxy-ntlm", "-U", "u:p", "-p", "-x", "127.0.0.1:18604", "http://example.test/"], _ => true).Options!);
        HttpAuthRequest request = new("CONNECT", CurlUrl.Parse("http://127.0.0.1:18604/"), "example.test:80", new System.Net.NetworkCredential("u", "p"), null, HttpAuthSchemes.Ntlm, IsProxy: true);

        string? value = await transports.ProxyTunnelOptions.ProxyAuthenticator!.CreateAuthorizationAsync(request, [], CancellationToken.None);

        StringAssert.StartsWith(value, "NTLM TlRMTVNTUAAB", StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task RunAsync_ForwardProxyWithProxyAnyAuth_AnswersTheProxysBasicChallengeOnTheSameConnection()
    {
        // curl -x http://127.0.0.1:18603 -U u:p --proxy-anyauth http://example.invalid/ against a 407 offering Basic (BL-603 Notes).
        ScriptedConnector server = new(
        [
            Latin1("HTTP/1.1 407 Proxy Authentication Required\r\nProxy-Authenticate: Basic realm=\"r\"\r\nContent-Length: 0\r\n\r\n"),
            Latin1(Hello),
        ]);

        Run run = await RunAsync(server, "-sS", "-U", "u:p", "--proxy-anyauth", "-x", "127.0.0.1:18238", "http://example.com/a");

        Assert.AreEqual(0, run.ExitCode);
        Assert.AreEqual(
            ForwardedGet
            + "GET http://example.com/a HTTP/1.1\r\nHost: example.com\r\nProxy-Authorization: Basic dTpw\r\n"
            + "User-Agent: curl/8.21.0\r\nAccept: */*\r\nProxy-Connection: Keep-Alive\r\n\r\n",
            Latin1(server.Written));
        Assert.AreEqual("hello", run.StandardOutput);
    }

    [TestMethod]
    public async Task RunAsync_ProxyTunnelWithProxyAnyAuth_AnswersTheProxysBasicChallengeOnTheSameConnection()
    {
        // curl -p -x http://127.0.0.1:18602 -U u:p --proxy-anyauth http://example.test/ against a 407 offering Basic (BL-602 Notes).
        ScriptedConnector server = new(
        [
            Latin1("HTTP/1.1 407 Proxy Authentication Required\r\nProxy-Authenticate: Basic realm=\"r\"\r\nContent-Length: 0\r\n\r\n"),
            Latin1(ConnectionEstablished),
            Latin1(Hello),
        ]);
        string[] arguments = ["-sS", "-p", "-U", "u:p", "--proxy-anyauth", "-x", "127.0.0.1:18238", "http://example.com/a"];

        Run run = await RunThroughTcpConnectorAsync(server, TunnelOptionsFor(arguments), arguments);

        Assert.AreEqual(0, run.ExitCode);
        Assert.StartsWith(
            "CONNECT example.com:80 HTTP/1.1\r\nHost: example.com:80\r\nUser-Agent: curl/8.21.0\r\nProxy-Connection: Keep-Alive\r\n\r\n"
            + "CONNECT example.com:80 HTTP/1.1\r\nHost: example.com:80\r\nProxy-Authorization: Basic dTpw\r\nUser-Agent: curl/8.21.0\r\nProxy-Connection: Keep-Alive\r\n\r\n"
            + "GET /a HTTP/1.1\r\n",
            Latin1(server.Written));
    }

    [TestMethod]
    public async Task RunAsync_ProxyTunnelWithEmptyUserAgentOption_ConnectRequestHasNoUserAgent()
    {
        ScriptedConnector server = new([Latin1(ConnectionEstablished), Latin1(Hello)]);
        string[] arguments = ["-sS", "-p", "-A", "", "-x", "127.0.0.1:18238", "http://example.com/a"];

        Run run = await RunThroughTcpConnectorAsync(server, TunnelOptionsFor(arguments), arguments);

        Assert.AreEqual(0, run.ExitCode);
        Assert.StartsWith(
            "CONNECT example.com:80 HTTP/1.1\r\nHost: example.com:80\r\nProxy-Connection: Keep-Alive\r\n\r\n",
            Latin1(server.Written));
    }

    [TestMethod]
    public async Task RunAsync_HttpProxyAndProxyUserForADictUrl_ConnectTargetCarriesTheProxy()
    {
        RecordingConnector connector = new(CurlExitCode.CouldntConnect, "refused");

        await RunAsync(connector, new Dictionary<string, string>(), ["-sS", "-x", "http://proxy:3128", "-U", "user:pass", "dict://example.com/d:x"]);

        ConnectTarget target = connector.Targets.Single();
        Assert.AreEqual(new ConnectTarget("example.com", 2628, false), target with { Proxy = null });
        ProxyEndpoint proxy = target.Proxy!;
        Assert.AreEqual((ProxyKind.Http, "proxy", 3128), (proxy.Kind, proxy.Host, proxy.Port));
        Assert.AreEqual(("user", "pass"), (proxy.Credential!.UserName, proxy.Credential.Password));
    }

    [TestMethod]
    [DataRow("dict://example.com/d:x")]
    [DataRow("http://example.com/a")]
    public async Task RunAsync_NoProxyOption_ConnectTargetCarriesNoProxy(string url)
    {
        RecordingConnector connector = new(CurlExitCode.CouldntConnect, "refused");

        await RunAsync(connector, new Dictionary<string, string>(), ["-sS", url]);

        Assert.IsNull(connector.Targets.Single().Proxy);
    }

    [TestMethod]
    [DataRow("dict://example.com/d:x", false, 2628)]
    [DataRow("dict://example.com/d:x", true, 2628)]
    [DataRow("https://example.com/a", false, 443)]
    [DataRow("https://example.com/a", true, 443)]
    [DataRow("http://example.com/a", true, 80)]
    public async Task RunAsync_ProxyOptionForATunnelledUrl_ConnectTargetCarriesTheProxy(string url, bool proxyTunnel, int port)
    {
        RecordingConnector connector = new(CurlExitCode.CouldntConnect, "refused");
        string[] arguments = proxyTunnel ? ["-sS", "-p", "-x", "http://proxy:3128", url] : ["-sS", "-x", "http://proxy:3128", url];

        await RunAsync(connector, new Dictionary<string, string>(), arguments);

        ConnectTarget target = connector.Targets.Single();
        Assert.AreEqual(("example.com", port), (target.Host, target.Port));
        Assert.AreEqual(("proxy", 3128), (target.Proxy!.Host, target.Proxy.Port));
    }

    [TestMethod]
    public async Task RunAsync_ProxyOptionWithoutProxyTunnelForAnHttpUrl_ConnectsToTheProxyWithoutATunnel()
    {
        RecordingConnector connector = new(CurlExitCode.CouldntConnect, "refused");

        await RunAsync(connector, new Dictionary<string, string>(), ["-sS", "-x", "http://proxy:3128", "http://example.com/a"]);

        Assert.AreEqual(new ConnectTarget("proxy", 3128, false) { PoolScheme = "http", IsForwardProxy = true }, connector.Targets.Single());
    }

    [TestMethod]
    public async Task RunAsync_HttpProxyWithoutProxyTunnelForAnFtpUrl_ForwardsAnHttpGetToTheProxy()
    {
        ScriptedConnector server = new([Latin1(Hello)]);

        Run run = await RunAsync(server, "-sS", "-x", "http://127.0.0.1:18238", "ftp://example.com/f.txt");

        Assert.AreEqual(0, run.ExitCode);
        Assert.AreEqual(
            "GET ftp://example.com/f.txt HTTP/1.1\r\nHost: example.com:21\r\n"
            + "User-Agent: curl/8.21.0\r\nAccept: */*\r\nProxy-Connection: Keep-Alive\r\n\r\n",
            Latin1(server.Written));
        Assert.AreEqual(ForwardProxy, server.Targets.Single());
        Assert.AreEqual("hello", run.StandardOutput);
    }

    [TestMethod]
    public async Task RunAsync_TftpUrlThroughAnHttpProxy_SendsTheMasqueRequestToTheProxyAndExitsSeven()
    {
        // Measured by BL-330: curl -sS -x http://127.0.0.1:18331 tftp://example.com/f
        ScriptedConnector server = new([Latin1("HTTP/1.1 101 Switching Protocols\r\nConnection: Upgrade\r\nUpgrade: connect-udp\r\nCapsule-Protocol: ?1\r\n\r\n")]);

        Run run = await RunAsync(server, "-sS", "-x", "http://127.0.0.1:18331", "tftp://example.com/f");

        Assert.AreEqual(7, run.ExitCode);
        Assert.AreEqual(
            "GET http://127.0.0.1:18331/.well-known/masque/udp/example.com/69/ HTTP/1.1\r\n"
            + "Host: 127.0.0.1:18331\r\nUser-Agent: curl/8.21.0\r\nProxy-Connection: Keep-Alive\r\n"
            + "Connection: Upgrade\r\nUpgrade: connect-udp\r\nCapsule-Protocol: ?1\r\n\r\n",
            Latin1(server.Written));
        Assert.AreEqual(new ConnectTarget("127.0.0.1", 18331, false) { IsForwardProxy = true }, server.Targets.Single());
        Assert.AreEqual($"curl: (7) bind() failed; Invalid arguments{Environment.NewLine}", run.StandardError);
    }

    [TestMethod]
    public async Task RunAsync_TftpUrlThroughAnHttpProxyWithProxyUser_SendsProxyAuthorizationInTheMasqueRequest()
    {
        ScriptedConnector server = new([Latin1("HTTP/1.1 101 Switching Protocols\r\n\r\n")]);

        await RunAsync(server, "-sS", "-x", "http://127.0.0.1:18331", "-U", "u:p", "tftp://example.com/f");

        StringAssert.Contains(Latin1(server.Written), "Proxy-Authorization: Basic dTpw\r\n");
    }

    [TestMethod]
    [DataRow(new[] { "-sS", "-p", "-x", "http://127.0.0.1:18238", "ftp://example.com/f.txt" }, DisplayName = "-p")]
    [DataRow(new[] { "-sS", "ftp://example.com/f.txt" }, DisplayName = "no proxy")]
    public async Task RunAsync_FtpUrlNotForwardedThroughAnHttpProxy_ConnectsTheFtpHandlerToTheServer(string[] arguments)
    {
        RecordingConnector connector = new(CurlExitCode.CouldntConnect, "refused");

        Run run = await RunAsync(connector, new Dictionary<string, string>(), arguments);

        Assert.AreEqual((int)CurlExitCode.CouldntConnect, run.ExitCode);
        ConnectTarget target = connector.Targets.Single();
        Assert.AreEqual("example.com", target.Host);
        Assert.AreEqual(21, target.Port);
        Assert.IsFalse(target.IsForwardProxy);
    }

    private static Task<Run> RunAsync(ScriptedConnector server, params string[] arguments) =>
        RunAsync((IConnector)server, new Dictionary<string, string>(), arguments);

    private static Task<Run> RunAsync(ScriptedConnector server, Dictionary<string, string> environment, params string[] arguments) =>
        RunAsync((IConnector)server, environment, arguments);

    private static Task<Run> RunThroughTcpConnectorAsync(ScriptedConnector server, params string[] arguments) =>
        RunThroughTcpConnectorAsync(server, null, arguments);

    private static Task<Run> RunThroughTcpConnectorAsync(ScriptedConnector server, HttpProxyTunnelOptions? proxyTunnelOptions, string[] arguments) =>
        RunAsync(
            new TcpConnector(new LoopbackDnsResolver(), new ScriptedTcpDialer(server), new PassThroughTlsProvider(), TimeProvider.System, proxyTunnelOptions),
            new Dictionary<string, string>(),
            arguments);

    /// <summary>The tunnel options the production composition maps from <paramref name="arguments" />.</summary>
    private static HttpProxyTunnelOptions TunnelOptionsFor(string[] arguments) =>
        CurlComposition.CreateProxyTunnelOptions(CommandLineParser.Parse(arguments, _ => true).Options!, new SystemSecurityContextFactory());

    /// <summary>
    /// Runs <paramref name="arguments" /> through the production composition over
    /// <paramref name="connector" />, with <paramref name="environment" /> as the only
    /// environment variables the proxy selector reads.
    /// </summary>
    private static async Task<Run> RunAsync(
        IConnector connector,
        Dictionary<string, string> environment,
        string[] arguments)
    {
        using MemoryStream standardOutput = new();
        using MemoryStream standardError = new();
        using MemoryStream standardInput = new();

        int exitCode = await CurlComposition
            .CreateRunner(
                standardOutput,
                standardError,
                standardInput,
                connector,
                new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"),
                new ProxySelector(name => environment.GetValueOrDefault(name)))
            .RunAsync(arguments);

        return new Run(exitCode, Latin1(standardOutput.ToArray()), Latin1(standardError.ToArray()));
    }

    private static string RedirectTo(string location) =>
        $"HTTP/1.1 302 Found\r\nLocation: {location}\r\nContent-Length: 0\r\nConnection: close\r\n\r\n";

    private static (string Host, int Port, bool UseTls, ProxyEndpoint? Proxy) RouteOf(ConnectTarget target) =>
        (target.Host, target.Port, target.UseTls, target.Proxy);

    private static string Latin1(byte[] bytes) => Encoding.Latin1.GetString(bytes);

    private static byte[] Latin1(string text) => Encoding.Latin1.GetBytes(text);

    private sealed record Run(int ExitCode, string StandardOutput, string StandardError);
}
