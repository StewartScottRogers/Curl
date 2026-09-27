using System.Text;

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
/// answering CONNECT with <c>200 Connection established</c> (BL-238 Notes).
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
    [DataRow(new[] { "--socks5", "127.0.0.1:18238", "http://example.com/a" }, "127.0.0.1:18238", "Socks5", DisplayName = "--socks5")]
    [DataRow(new[] { "-x", "https://127.0.0.1:18238", "https://example.com/a" }, "127.0.0.1:18238", "Https", DisplayName = "https proxy, https URL")]
    [DataRow(new[] { "-p", "-x", "https://127.0.0.1:18238", "http://example.com/a" }, "127.0.0.1:18238", "Https", DisplayName = "https proxy, -p")]
    [DataRow(new[] { "-L", "-x", "https://127.0.0.1:18238", "http://example.com/a" }, "127.0.0.1:18238", "Https", DisplayName = "https proxy, -L")]
    [DataRow(new[] { "--socks5", "127.0.0.1:1", "dict://example.com/d:x" }, "127.0.0.1:1", "Socks5", DisplayName = "--socks5, dict URL")]
    [DataRow(new[] { "-x", "https://127.0.0.1:18238", "gopher://example.com/" }, "127.0.0.1:18238", "Https", DisplayName = "https proxy, gopher URL")]
    public async Task RunAsync_TunnelTheConnectorCannotOpenYet_ExitsFourWithoutConnecting(string[] arguments, string proxy, string kind)
    {
        ScriptedConnector server = new([Latin1(Hello)]);

        Run run = await RunAsync(server, ["-sS", .. arguments]);

        Assert.AreEqual(4, run.ExitCode);
        Assert.AreEqual(
            $"curl: (4) Unsupported proxy '{proxy}', Curl cannot tunnel through a {kind} proxy yet{Environment.NewLine}",
            run.StandardError);
        Assert.AreEqual(0, server.Targets.Count);
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
    public async Task RunAsync_SocksProxyEnvironmentVariableForAnotherScheme_ExitsFourWithoutConnecting()
    {
        ScriptedConnector server = new([Latin1("hello")]);
        Dictionary<string, string> environment = new() { ["all_proxy"] = "socks5://127.0.0.1:1" };

        Run run = await RunAsync(server, environment, "-sS", "telnet://127.0.0.1:18238");

        Assert.AreEqual(4, run.ExitCode);
        Assert.AreEqual(
            $"curl: (4) Unsupported proxy '127.0.0.1:1', Curl cannot tunnel through a Socks5 proxy yet{Environment.NewLine}",
            run.StandardError);
        Assert.AreEqual(0, server.Targets.Count);
    }

    private static Task<Run> RunAsync(ScriptedConnector server, params string[] arguments) =>
        RunAsync((IConnector)server, new Dictionary<string, string>(), arguments);

    private static Task<Run> RunAsync(ScriptedConnector server, Dictionary<string, string> environment, params string[] arguments) =>
        RunAsync((IConnector)server, environment, arguments);

    private static Task<Run> RunThroughTcpConnectorAsync(ScriptedConnector server, params string[] arguments) =>
        RunAsync(
            new TcpConnector(new LoopbackDnsResolver(), new ScriptedTcpDialer(server), new PassThroughTlsProvider(), TimeProvider.System),
            new Dictionary<string, string>(),
            arguments);

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
