using System.Text;
using Curl.Cli;
using Curl.Core;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins the <c>[HTTPS-CONNECT]</c> lines of an <c>https://</c> transfer against curl 8.21.0 (mingw,
/// Schannel) measured on 2026-10-02 with <c>Record-CurlExchange.ps1 -Tls</c> (BL-1192 Notes), and
/// which <c>--trace-config</c> components turn them on. The pass-through TLS provider reports no
/// handshake, so the handshake's two poll rounds are not written here (ADR-0357's BL-1192
/// amendment). The transfer runs through the production handler set over a real
/// <see cref="TcpConnector" /> dialing a <see cref="ScriptedTcpDialer" />.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerHttpsConnectTraceTests
{
    private static readonly byte[] Response =
        Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello");

    private readonly MemoryStream standardError = new();

    [TestMethod]
    [DataRow("https-connect", "h2")]
    [DataRow("https-connect", "h1", "--http1.1")]
    public async Task RunAsync_HttpsGetUnderTraceConfigHttpsConnect_WritesTheFiltersLinesAroundTheConnect(string components, string version, params string[] options)
    {
        // curl -s -k -v --trace-config https-connect https://127.0.0.1:18443/ (BL-1192 Notes).
        int exitCode = await RunAsync([.. options, "-k", "-v", "--trace-config", components, "https://127.0.0.1:18443/"]);

        Assert.AreEqual(0, exitCode);
        List<string> lines = StandardErrorLines();
        CollectionAssert.AreEqual(
            new[]
            {
                "* [HTTPS-CONNECT] added",
                "* [HTTPS-CONNECT] connect, init",
                $"* [HTTPS-CONNECT] 1st attempt uses {version} from wanted versions",
                "*   Trying 127.0.0.1:18443...",
                "* [HTTPS-CONNECT] connect -> 0, done=0",
                "* [HTTPS-CONNECT] adjust_pollset -> 0, 1 socks",
                "* [HTTPS-CONNECT] connect -> 0, done=1",
                "* Established connection to 127.0.0.1 (127.0.0.1 port 18443) from 127.0.0.1 port 50000 ",
                "* [HTTPS-CONNECT] removing connected setup filter",
                "* [HTTPS-CONNECT] destroy",
                "* using HTTP/1.x",
            },
            lines.Take(lines.IndexOf("* using HTTP/1.x") + 1).ToArray());
    }

    [TestMethod]
    [DataRow("-v", "--trace-config", "all")]
    [DataRow("-vvvv")]
    public async Task RunAsync_HttpsGetUnderAllOrVvvv_WritesTheHttpsConnectLines(params string[] arguments)
    {
        // curl -s -vvvv and -v --trace-config all prefix every line with its time and transfer.
        await RunAsync([.. arguments, "-k", "https://127.0.0.1:18443/"]);

        string[] httpsConnectLines = [.. StandardErrorLines().Where(line => line.Contains("* [HTTPS-CONNECT] ", StringComparison.Ordinal))
            .Select(line => line[line.IndexOf("[HTTPS-CONNECT]", StringComparison.Ordinal)..])];
        CollectionAssert.AreEqual(
            new[]
            {
                "[HTTPS-CONNECT] added",
                "[HTTPS-CONNECT] connect, init",
                "[HTTPS-CONNECT] 1st attempt uses h2 from wanted versions",
                "[HTTPS-CONNECT] connect -> 0, done=0",
                "[HTTPS-CONNECT] adjust_pollset -> 0, 1 socks",
                "[HTTPS-CONNECT] connect -> 0, done=1",
                "[HTTPS-CONNECT] removing connected setup filter",
                "[HTTPS-CONNECT] destroy",
            },
            httpsConnectLines);
    }

    [TestMethod]
    [DataRow("-v", "https://127.0.0.1:18443/")]
    [DataRow("-vvv", "https://127.0.0.1:18443/")]
    [DataRow("-v", "--trace-config", "network", "https://127.0.0.1:18443/")]
    [DataRow("-v", "--trace-config", "proxy", "https://127.0.0.1:18443/")]
    [DataRow("-v", "--trace-config", "https-connect", "http://127.0.0.1:18443/")]
    public async Task RunAsync_WithoutTheHttpsConnectComponentOrAnHttpsUrl_WritesNoHttpsConnectLine(params string[] arguments)
    {
        await RunAsync(["-k", .. arguments]);

        Assert.IsFalse(StandardErrorLines().Any(line => line.Contains("[HTTPS-CONNECT]", StringComparison.Ordinal)));
    }

    [TestMethod]
    [DataRow(null, "h2")]
    [DataRow(RequestedHttpVersion.Http10, "h1")]
    [DataRow(RequestedHttpVersion.Http11, "h1")]
    [DataRow(RequestedHttpVersion.Http2, "h2")]
    [DataRow(RequestedHttpVersion.Http2PriorKnowledge, "h2")]
    [DataRow(RequestedHttpVersion.Http3, "h3")]
    [DataRow(RequestedHttpVersion.Http3Only, "h3")]
    public void HttpsConnectFirstAttemptVersionOf_EachVersionOption_NamesTheFirstAttemptsVersion(RequestedHttpVersion? version, string expected)
    {
        Assert.AreEqual(expected, CurlComposition.HttpsConnectFirstAttemptVersionOf(version));
    }

    [TestMethod]
    [DataRow("-x", "http://127.0.0.1:18454", "HTTP/1.1 200 Connection established\r\n\r\n", "[SETUP] happy eyeballing to proxy 127.0.0.1:18454", "[SETUP] added HTTP proxy tunnel filter", 1)]
    [DataRow("-x", "socks5h://127.0.0.1:18455", "\u0005\0\u0005\0\0\u0001\u007f\0\0\u0001\u0001\u00bb", "[SETUP] happy eyeballing to origin 127.0.0.1:18455", "[SETUP] added SOCKS filter to example.test:443", 2)]
    public async Task RunAsync_HttpsGetThroughAProxyUnderTraceConfigHttpsConnectAndSetup_WritesTheFiltersLinesAroundTheTunnel(
        string option, string proxy, string proxyReply, string eyeballing, string tunnelFilterAdded, int tunnelPollRounds)
    {
        // curl -s -k -v --trace-config https-connect,setup -x <proxy> https://example.test/ (BL-1254 Notes).
        int exitCode = await RunAsync([Encoding.Latin1.GetBytes(proxyReply), Response], "-k", "-v", "--trace-config", "https-connect,setup", option, proxy, "https://example.test/");

        Assert.AreEqual(0, exitCode);
        CollectionAssert.AreEqual(
            (string[])
            [
                "* [HTTPS-CONNECT] added",
                "* [HTTPS-CONNECT] connect, init",
                "* [HTTPS-CONNECT] 1st attempt uses h2 from wanted versions",
                $"* {eyeballing}",
                "* [HTTPS-CONNECT] connect -> 0, done=0",
                "* [HTTPS-CONNECT] adjust_pollset -> 0, 1 socks",
                $"* {tunnelFilterAdded}",
                .. Enumerable.Repeat(new[] { "* [HTTPS-CONNECT] connect -> 0, done=0", "* [HTTPS-CONNECT] adjust_pollset -> 0, 1 socks" }, tunnelPollRounds).SelectMany(pair => pair),
                "* [SETUP] added SSL filter for origin",
                "* [HTTPS-CONNECT] connect -> 0, done=1",
                "* [HTTPS-CONNECT] removing connected setup filter",
                "* [HTTPS-CONNECT] destroy",
                "* [SETUP] removing connected setup filter",
                "* [SETUP] destroy",
            ],
            FilterLines());
    }

    [TestMethod]
    [DataRow("setup")]
    [DataRow("all")]
    public async Task RunAsync_HttpsGetThroughAnHttpsProxyUnderTraceConfigSetup_WritesTheSetupLinesAroundTheTunnel(string components)
    {
        // curl -s -k --proxy-insecure -v --trace-config https-connect,setup -x https://127.0.0.1:18458
        // https://example.test/ (BL-1283 Context): the origin's ALPN connect filter adds the setup filter,
        // so no [SETUP] added line, and its SSL filter is added once the tunnel is open.
        int exitCode = await RunAsync([Encoding.ASCII.GetBytes("HTTP/1.1 200 Connection established\r\n\r\n"), Response], "-k", "--proxy-insecure", "-v", "--trace-config", components, "-x", "https://127.0.0.1:18458", "https://example.test/");

        Assert.AreEqual(0, exitCode);
        CollectionAssert.AreEqual(
            new[]
            {
                "[SETUP] happy eyeballing to proxy 127.0.0.1:18458",
                "[SETUP] added SSL filter for HTTP proxy",
                "[SETUP] added HTTP proxy tunnel filter",
                "[SETUP] added SSL filter for origin",
                "[SETUP] removing connected setup filter",
                "[SETUP] destroy",
            },
            SetupLines());
    }

    [TestMethod]
    [DataRow("setup")]
    [DataRow("all")]
    public async Task RunAsync_HttpGetThroughAnHttpsProxyUnderTraceConfigSetup_WritesTheSetupLinesAroundTheTunnel(string components)
    {
        // curl -s --proxy-insecure -v --trace-config proxy,setup -p -x https://127.0.0.1:18955
        // http://example.test/x (BL-1255 Notes): the setup filter is added first, and no origin SSL filter.
        int exitCode = await RunAsync([Encoding.ASCII.GetBytes("HTTP/1.1 200 Connection established\r\n\r\n"), Response], "--proxy-insecure", "-v", "--trace-config", components, "-p", "-x", "https://127.0.0.1:18955", "http://example.test/x");

        Assert.AreEqual(0, exitCode);
        CollectionAssert.AreEqual(
            new[]
            {
                "[SETUP] added",
                "[SETUP] happy eyeballing to proxy 127.0.0.1:18955",
                "[SETUP] added SSL filter for HTTP proxy",
                "[SETUP] added HTTP proxy tunnel filter",
                "[SETUP] removing connected setup filter",
                "[SETUP] destroy",
            },
            SetupLines());
    }

    [TestMethod]
    public async Task RunAsync_HttpsGetThroughAnHttpsProxyWithoutTheSetupComponent_WritesNoSetupLine()
    {
        int exitCode = await RunAsync([Encoding.ASCII.GetBytes("HTTP/1.1 200 Connection established\r\n\r\n"), Response], "-k", "--proxy-insecure", "-v", "--trace-config", "proxy", "-x", "https://127.0.0.1:18458", "https://example.test/");

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(0, SetupLines().Length);
    }

    [TestMethod]
    public async Task RunAsync_HttpsGetOverAUnixSocketUnderTraceConfigHttpsConnectAndSetup_EyeballsToThePathAtPort0()
    {
        // curl -s -k -v --trace-config https-connect,setup --unix-socket <path> https://example.test/ (BL-1254 Notes).
        int exitCode = await RunAsync([Response], "-k", "-v", "--trace-config", "https-connect,setup", "--unix-socket", "/tmp/bl1254.sock", "https://example.test/");

        Assert.AreEqual(0, exitCode);
        CollectionAssert.AreEqual(
            new[]
            {
                "* [HTTPS-CONNECT] added",
                "* [HTTPS-CONNECT] connect, init",
                "* [HTTPS-CONNECT] 1st attempt uses h2 from wanted versions",
                "* [SETUP] happy eyeballing to origin /tmp/bl1254.sock:0",
                "* [HTTPS-CONNECT] connect -> 0, done=0",
                "* [HTTPS-CONNECT] adjust_pollset -> 0, 1 socks",
                "* [SETUP] added SSL filter for origin",
                "* [HTTPS-CONNECT] connect -> 0, done=1",
                "* [HTTPS-CONNECT] removing connected setup filter",
                "* [HTTPS-CONNECT] destroy",
                "* [SETUP] removing connected setup filter",
                "* [SETUP] destroy",
            },
            FilterLines());
    }

    private string[] FilterLines() =>
        [.. StandardErrorLines().Where(line => line.StartsWith("* [HTTPS-CONNECT] ", StringComparison.Ordinal) || line.StartsWith("* [SETUP] ", StringComparison.Ordinal))];

    // Under all every line carries its time and transfer first, so each [SETUP] line is cut from its tag.
    private string[] SetupLines() =>
        [.. StandardErrorLines().Where(line => line.Contains("* [SETUP] ", StringComparison.Ordinal))
            .Select(line => line[line.IndexOf("[SETUP]", StringComparison.Ordinal)..])];

    private List<string> StandardErrorLines() =>
        [.. Encoding.ASCII.GetString(standardError.ToArray())
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.TrimEnd('\r'))];

    /// <summary>
    /// Runs <c>-s</c> and <paramref name="arguments" /> through the production handler set over the
    /// connector the composition builds from them, whose connection replays the response.
    /// </summary>
    private Task<int> RunAsync(params string[] arguments) => RunAsync([Response], arguments);

    /// <summary>Runs <paramref name="arguments" /> as above over connections that replay <paramref name="reads" />.</summary>
    private async Task<int> RunAsync(byte[][] reads, params string[] arguments)
    {
        CommandLineParseResult parsed = CommandLineParser.Parse(["-s", .. arguments], _ => true);
        Assert.IsTrue(parsed.IsAccepted);
        TcpConnector connector = CurlComposition.CreateTcpConnector(
            parsed.Options,
            new LoopbackDnsResolver(),
            new ScriptedTcpDialer(new ScriptedConnector(reads)),
            new PassThroughTlsProvider(),
            TimeProvider.System,
            HttpProxyTunnelOptions.Default);
        InMemoryFileSystem files = new();

        return await new CurlCommandRunner(
                _ => new TransferDispatch(
                    new ProtocolDispatcher(CurlComposition.CreateProtocolHandlers(connector, new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"), new PassThroughTlsProvider(), new LoopbackDnsResolver())),
                    [],
                    loadResolveEntries: connector.LoadResolveEntries),
                files,
                files,
                new MemoryStream(),
                standardError,
                new MemoryStream(),
                runsOnWindows: true)
            .RunAsync(["-s", .. arguments]);
    }
}
