using System.Text;
using Curl.Cli;
using Curl.Core;
using Curl.Networking;
using Curl.Protocol.Abstractions;
using Curl.Testing;

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

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("https-connect", "h2")]
    [DataRow("https-connect", "h1", "--http1.1")]
    public async Task RunAsync_HttpsGetUnderTraceConfigHttpsConnect_WritesTheFiltersLinesAroundTheConnect(string components, string version, params string[] options)
    {
        // curl -s -k -v --trace-config https-connect https://127.0.0.1:18443/ (BL-1192 Notes).
        int exitCode = await RunAsync([.. options, "-k", "-v", "--trace-config", components, "https://127.0.0.1:18443/"]);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        List<string> lines = StandardErrorLines();
        string[] expectedLines =
        [
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
        ];
        string[] actualLines = [.. lines.Take(lines.IndexOf("* using HTTP/1.x") + 1)];
        Diagnostics.Diff("stderr lines up to the HTTP/1.x line", Lf(expectedLines), Lf(actualLines));
        CollectionAssert.AreEqual(expectedLines, actualLines);
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
        string[] expectedLines =
        [
            "[HTTPS-CONNECT] added",
            "[HTTPS-CONNECT] connect, init",
            "[HTTPS-CONNECT] 1st attempt uses h2 from wanted versions",
            "[HTTPS-CONNECT] connect -> 0, done=0",
            "[HTTPS-CONNECT] adjust_pollset -> 0, 1 socks",
            "[HTTPS-CONNECT] connect -> 0, done=1",
            "[HTTPS-CONNECT] removing connected setup filter",
            "[HTTPS-CONNECT] destroy",
        ];
        Diagnostics.Diff("HTTPS-CONNECT lines", Lf(expectedLines), Lf(httpsConnectLines));
        CollectionAssert.AreEqual(expectedLines, httpsConnectLines);
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

        bool anyHttpsConnectLine = StandardErrorLines().Any(line => line.Contains("[HTTPS-CONNECT]", StringComparison.Ordinal));
        Diagnostics.Assert("any [HTTPS-CONNECT] line", false, anyHttpsConnectLine);
        Assert.IsFalse(anyHttpsConnectLine);
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
        Diagnostics.Arrange("requested version", version?.ToString() ?? "(none)");
        string actual;
        using (Diagnostics.Phase("run"))
        {
            actual = CurlComposition.HttpsConnectFirstAttemptVersionOf(version);
        }

        Diagnostics.Act("first attempt version", actual);
        Diagnostics.Assert("first attempt version", expected, actual);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    [DataRow("--http3", "h3", "h2")]
    [DataRow("--http3-only", "h3", null)]
    [DataRow("--http2", "h2", null)]
    public void CreateTcpConnector_UnderTraceConfigHttpsConnect_NamesTheAttemptsCurlsNgtcp2BuildNames(string option, string firstAttempt, string? secondAttempt)
    {
        // curl.se's curl 8.22.0 ngtcp2 build: --http3 names "2nd attempt uses h2", --http3-only none (BL-1284 Notes).
        Diagnostics.Arrange("option", option);
        CommandLineParseResult parsed = OpenSslBuildParser.Parse(["-k", "-v", "--trace-config", "https-connect", option, "https://127.0.0.1:18713/"], _ => true);
        Diagnostics.Assert("parse accepted", true, parsed.IsAccepted);
        Assert.IsTrue(parsed.IsAccepted);

        TcpConnector connector;
        using (Diagnostics.Phase("run"))
        {
            connector = CurlComposition.CreateTcpConnector(
                parsed.Options!,
                new LoopbackDnsResolver(),
                new ScriptedTcpDialer(new ScriptedConnector([Response])),
                new PassThroughTlsProvider(),
                TimeProvider.System,
                HttpProxyTunnelOptions.Default);
        }

        Diagnostics.Act("traces https-connect filter", connector.TracesHttpsConnectFilter);
        Diagnostics.Act("first attempt version", connector.HttpsConnectFirstAttemptVersion);
        Diagnostics.Act("second attempt version", connector.HttpsConnectSecondAttemptVersion);
        Diagnostics.Assert("traces https-connect filter", true, connector.TracesHttpsConnectFilter);
        Diagnostics.Assert("first attempt version", firstAttempt, connector.HttpsConnectFirstAttemptVersion);
        Diagnostics.Assert("second attempt version", secondAttempt, connector.HttpsConnectSecondAttemptVersion);
        Assert.IsTrue(connector.TracesHttpsConnectFilter);
        Assert.AreEqual(firstAttempt, connector.HttpsConnectFirstAttemptVersion);
        Assert.AreEqual(secondAttempt, connector.HttpsConnectSecondAttemptVersion);
    }

    [TestMethod]
    [DataRow("-x", "http://127.0.0.1:18454", "HTTP/1.1 200 Connection established\r\n\r\n", "[SETUP] happy eyeballing to proxy 127.0.0.1:18454", "[SETUP] added HTTP proxy tunnel filter", 1)]
    [DataRow("-x", "socks5h://127.0.0.1:18455", "\u0005\0\u0005\0\0\u0001\u007f\0\0\u0001\u0001\u00bb", "[SETUP] happy eyeballing to origin 127.0.0.1:18455", "[SETUP] added SOCKS filter to example.test:443", 2)]
    public async Task RunAsync_HttpsGetThroughAProxyUnderTraceConfigHttpsConnectAndSetup_WritesTheFiltersLinesAroundTheTunnel(
        string option, string proxy, string proxyReply, string eyeballing, string tunnelFilterAdded, int tunnelPollRounds)
    {
        // curl -s -k -v --trace-config https-connect,setup -x <proxy> https://example.test/ (BL-1254 Notes).
        int exitCode = await RunAsync([Encoding.Latin1.GetBytes(proxyReply), Response], "-k", "-v", "--trace-config", "https-connect,setup", option, proxy, "https://example.test/");

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        string[] expectedLines =
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
        ];
        string[] actualLines = FilterLines();
        Diagnostics.Diff("filter lines", Lf(expectedLines), Lf(actualLines));
        CollectionAssert.AreEqual(expectedLines, actualLines);
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

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        string[] expectedLines =
        [
            "[SETUP] happy eyeballing to proxy 127.0.0.1:18458",
            "[SETUP] added SSL filter for HTTP proxy",
            "[SETUP] added HTTP proxy tunnel filter",
            "[SETUP] added SSL filter for origin",
            "[SETUP] removing connected setup filter",
            "[SETUP] destroy",
        ];
        string[] actualLines = SetupLines();
        Diagnostics.Diff("setup lines", Lf(expectedLines), Lf(actualLines));
        CollectionAssert.AreEqual(expectedLines, actualLines);
    }

    [TestMethod]
    [DataRow("setup")]
    [DataRow("all")]
    public async Task RunAsync_HttpGetThroughAnHttpsProxyUnderTraceConfigSetup_WritesTheSetupLinesAroundTheTunnel(string components)
    {
        // curl -s --proxy-insecure -v --trace-config proxy,setup -p -x https://127.0.0.1:18955
        // http://example.test/x (BL-1255 Notes): the setup filter is added first, and no origin SSL filter.
        int exitCode = await RunAsync([Encoding.ASCII.GetBytes("HTTP/1.1 200 Connection established\r\n\r\n"), Response], "--proxy-insecure", "-v", "--trace-config", components, "-p", "-x", "https://127.0.0.1:18955", "http://example.test/x");

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        string[] expectedLines =
        [
            "[SETUP] added",
            "[SETUP] happy eyeballing to proxy 127.0.0.1:18955",
            "[SETUP] added SSL filter for HTTP proxy",
            "[SETUP] added HTTP proxy tunnel filter",
            "[SETUP] removing connected setup filter",
            "[SETUP] destroy",
        ];
        string[] actualLines = SetupLines();
        Diagnostics.Diff("setup lines", Lf(expectedLines), Lf(actualLines));
        CollectionAssert.AreEqual(expectedLines, actualLines);
    }

    [TestMethod]
    public async Task RunAsync_HttpsGetThroughAnHttpsProxyWithoutTheSetupComponent_WritesNoSetupLine()
    {
        int exitCode = await RunAsync([Encoding.ASCII.GetBytes("HTTP/1.1 200 Connection established\r\n\r\n"), Response], "-k", "--proxy-insecure", "-v", "--trace-config", "proxy", "-x", "https://127.0.0.1:18458", "https://example.test/");

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("setup line count", 0, SetupLines().Length);
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(0, SetupLines().Length);
    }

    [TestMethod]
    public async Task RunAsync_HttpsGetOverAUnixSocketUnderTraceConfigHttpsConnectAndSetup_EyeballsToThePathAtPort0()
    {
        // curl -s -k -v --trace-config https-connect,setup --unix-socket <path> https://example.test/ (BL-1254 Notes).
        int exitCode = await RunAsync([Response], "-k", "-v", "--trace-config", "https-connect,setup", "--unix-socket", "/tmp/bl1254.sock", "https://example.test/");

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        string[] expectedLines =
        [
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
        ];
        string[] actualLines = FilterLines();
        Diagnostics.Diff("filter lines", Lf(expectedLines), Lf(actualLines));
        CollectionAssert.AreEqual(expectedLines, actualLines);
    }

    [TestMethod]
    public async Task RunAsync_HttpsGetUnderTraceConfigSsl_WritesTheSslFiltersLinesAroundTheHandshakeAndTheAlpnQuery()
    {
        // curl -s -v -k --trace-config ssl https://127.0.0.1:18961/x (BL-1287 Notes); the pass-through
        // provider reports no handshake, so its schannel and ALPN lines are not among them.
        int exitCode = await RunAsync("-k", "-v", "--trace-config", "ssl", "--http1.1", "https://127.0.0.1:18443/");

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        List<string> lines = StandardErrorLines();
        string[] expectedLines =
        [
            "*   Trying 127.0.0.1:18443...",
            "* [SSL] cf_connect()",
            "* [SSL] cf_connect() -> 0, done=0",
            "* [SSL] adjust_pollset, POLLIN fd=3",
            "* [SSL] cf_connect()",
            "* [SSL] cf_connect() -> 0, done=0",
            "* [SSL] adjust_pollset, POLLIN fd=3",
            "* [SSL] cf_connect()",
            "* [SSL] cf_connect() -> 0, done=1",
            "* Established connection to 127.0.0.1 (127.0.0.1 port 18443) from 127.0.0.1 port 50000 ",
            "* [SSL] query ALPN",
            "* [SSL] query ALPN: returning '(nil)'",
            "* using HTTP/1.x",
        ];
        string[] actualLines = [.. lines.Take(lines.IndexOf("* using HTTP/1.x") + 1)];
        Diagnostics.Diff("stderr lines up to the HTTP/1.x line", Lf(expectedLines), Lf(actualLines));
        CollectionAssert.AreEqual(expectedLines, actualLines);
    }

    [TestMethod]
    [DataRow("-v")]
    [DataRow("-v", "--trace-config", "proxy")]
    [DataRow("-v", "--trace-config", "tls")]
    public async Task RunAsync_HttpsGetWithoutTheSslComponent_WritesNoSslLine(params string[] arguments)
    {
        await RunAsync(["-k", .. arguments, "https://127.0.0.1:18443/"]);

        bool anySslLine = StandardErrorLines().Any(line => line.Contains("[SSL", StringComparison.Ordinal));
        Diagnostics.Assert("any [SSL line", false, anySslLine);
        Assert.IsFalse(anySslLine);
    }

    private string[] FilterLines() =>
        [.. StandardErrorLines().Where(line => line.StartsWith("* [HTTPS-CONNECT] ", StringComparison.Ordinal) || line.StartsWith("* [SETUP] ", StringComparison.Ordinal))];

    // Under all every line carries its time and transfer first, so each [SETUP] line is cut from its tag.
    private string[] SetupLines() =>
        [.. StandardErrorLines().Where(line => line.Contains("* [SETUP] ", StringComparison.Ordinal))
            .Select(line => line[line.IndexOf("[SETUP]", StringComparison.Ordinal)..])];

    private static string Lf(IEnumerable<string> lines) => string.Join('\n', lines);

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
        string[] fullArguments = ["-s", .. arguments];
        Diagnostics.Arrange("command line", string.Join(' ', fullArguments));
        Diagnostics.Arrange("scripted reads", string.Join(" | ", reads.Select(read => Encoding.Latin1.GetString(read).Replace("\r\n", "\\r\\n", StringComparison.Ordinal))));
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

        int exitCode;
        using (Diagnostics.Phase("run"))
        {
            exitCode = await new CurlCommandRunner(
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

        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Bytes("stderr", standardError.ToArray());
        return exitCode;
    }
}
