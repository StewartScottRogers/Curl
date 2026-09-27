using System.Text;

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

    [TestMethod]
    public async Task RunAsync_HttpUrlWithProxyOption_ForwardsTheRequestInAbsoluteFormToTheProxy()
    {
        ScriptedConnector server = new([Latin1(Hello)]);

        Run run = await RunAsync(server, "-sS", "-x", "127.0.0.1:18238", "http://example.com/a");

        Assert.AreEqual(0, run.ExitCode);
        Assert.AreEqual(ForwardedGet, Latin1(server.Written));
        Assert.AreEqual(Proxy, server.Targets.Single());
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
        Assert.AreEqual(Proxy, server.Targets.Single());
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
        Assert.AreEqual(Proxy, server.Targets.Single());
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
        Assert.AreEqual(new ConnectTarget("127.0.0.1", 18238, true), server.Targets.Single());
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

        Assert.AreEqual(new ConnectTarget("proxy", 3128, false), connector.Targets.Single());
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
        CurlComposition.CreateProxyTunnelOptions(CommandLineParser.Parse(arguments, _ => true).Options!);

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

    private static string Latin1(byte[] bytes) => Encoding.Latin1.GetString(bytes);

    private static byte[] Latin1(string text) => Encoding.Latin1.GetBytes(text);

    private sealed record Run(int ExitCode, string StandardOutput, string StandardError);
}
