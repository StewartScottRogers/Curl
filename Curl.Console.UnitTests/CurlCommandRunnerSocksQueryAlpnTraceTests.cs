using System.Text;
using Curl.Cli;
using Curl.Core;
using Curl.Networking;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins the <c>[SOCKS] query ALPN</c> line of a plain HTTP transfer through a SOCKS proxy, against
/// curl 8.21.0 (mingw, Schannel) measured on 2026-10-02 with <c>Record-CurlExchange.ps1 -Script</c>
/// playing a SOCKS5h proxy (BL-1246 Notes): after <c>Established connection</c> and before
/// <c>using HTTP/1.x</c> under <c>socks</c>, <c>proxy</c> and a named <c>all</c>, and no
/// <c>query ALPN</c> line at all under <c>network</c>. The transfer runs through the production
/// handler set over a real <see cref="TcpConnector" /> dialing a <see cref="ScriptedTcpDialer" />.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerSocksQueryAlpnTraceTests
{
    private static readonly byte[] Socks5NoAuthentication = [0x05, 0x00];

    private static readonly byte[] Socks5Granted = [0x05, 0x00, 0x00, 0x01, 0x7F, 0x00, 0x00, 0x01, 0x00, 0x50];

    private static readonly byte[] Response = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 3\r\n\r\nab\n");

    private readonly MemoryStream standardError = new();

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("socks")]
    [DataRow("proxy")]
    public async Task RunAsync_PlainGetThroughSocksUnderSocksOrProxy_QueriesAlpnBetweenEstablishedAndUsingHttp(string components)
    {
        // curl -s -v --trace-config socks -x socks5h://127.0.0.1:18611 http://example.test/a (BL-1246 Notes).
        int exitCode = await RunAsync("-v", "--trace-config", components, "-x", "socks5h://127.0.0.1:18611", "http://example.test/a");

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        List<string> lines = StandardErrorLines();
        int established = lines.FindIndex(line => line.StartsWith("* Established connection", StringComparison.Ordinal));
        Diagnostics.Assert("Established connection line found after the first line", true, established > 0);
        Assert.IsTrue(established > 0);
        Diagnostics.Diff("line after Established connection", "* [SOCKS] query ALPN", LineAt(lines, established + 1));
        Assert.AreEqual("* [SOCKS] query ALPN", lines[established + 1]);
        Diagnostics.Diff("second line after Established connection", "* using HTTP/1.x", LineAt(lines, established + 2));
        Assert.AreEqual("* using HTTP/1.x", lines[established + 2]);
    }

    [TestMethod]
    public async Task RunAsync_PlainGetThroughSocksUnderANamedAll_QueriesAlpnOnTheSocksFilter()
    {
        await RunAsync("-v", "--trace-config", "all", "-x", "socks5h://127.0.0.1:18611", "http://example.test/a");

        int socksQueries = StandardErrorLines().Count(line => line.EndsWith("[SOCKS] query ALPN", StringComparison.Ordinal));
        Diagnostics.Assert("[SOCKS] query ALPN lines", 1, socksQueries);
        Assert.AreEqual(1, StandardErrorLines().Count(line => line.EndsWith("[SOCKS] query ALPN", StringComparison.Ordinal)));
        Diagnostics.Assert("any [TCP] query ALPN line", false, StandardErrorLines().Any(line => line.EndsWith("[TCP] query ALPN", StringComparison.Ordinal)));
        Assert.IsFalse(StandardErrorLines().Any(line => line.EndsWith("[TCP] query ALPN", StringComparison.Ordinal)));
    }

    [TestMethod]
    [DataRow("-v")]
    [DataRow("-vvvv")]
    [DataRow("-v", "--trace-config", "network")]
    public async Task RunAsync_PlainGetThroughSocksWithoutTheSocksComponent_WritesNoQueryAlpnLine(params string[] arguments)
    {
        // curl -s -v --trace-config network -x socks5h://127.0.0.1:18613 http://example.test/a writes
        // [TCP] send lines but no query ALPN line (BL-1246 Notes).
        await RunAsync([.. arguments, "-x", "socks5h://127.0.0.1:18611", "http://example.test/a"]);

        Diagnostics.Assert("any query ALPN line", false, StandardErrorLines().Any(line => line.Contains("query ALPN", StringComparison.Ordinal)));
        Assert.IsFalse(StandardErrorLines().Any(line => line.Contains("query ALPN", StringComparison.Ordinal)));
    }

    private static string LineAt(List<string> lines, int index) =>
        index >= 0 && index < lines.Count ? lines[index] : "(no such line)";

    private List<string> StandardErrorLines() =>
        [.. Encoding.ASCII.GetString(standardError.ToArray())
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.TrimEnd('\r'))];

    /// <summary>
    /// Runs <c>-s</c> and <paramref name="arguments" /> through the production handler set over the
    /// connector the composition builds from them, whose connection grants the SOCKS5 request and
    /// replays a three-byte response.
    /// </summary>
    private async Task<int> RunAsync(params string[] arguments)
    {
        CommandLineParseResult parsed = CommandLineParser.Parse(["-s", .. arguments], _ => true);
        Diagnostics.Assert("command line accepted", true, parsed.IsAccepted);
        Assert.IsTrue(parsed.IsAccepted);
        TcpConnector connector = CurlComposition.CreateTcpConnector(
            parsed.Options,
            new LoopbackDnsResolver(),
            new ScriptedTcpDialer(new ScriptedConnector([Socks5NoAuthentication, Socks5Granted, Response])),
            new PassThroughTlsProvider(),
            TimeProvider.System,
            HttpProxyTunnelOptions.Default);
        InMemoryFileSystem files = new();
        Diagnostics.Arrange("arguments", "-s " + string.Join(' ', arguments));
        Diagnostics.Bytes("scripted SOCKS5 method reply", Socks5NoAuthentication);
        Diagnostics.Bytes("scripted SOCKS5 grant", Socks5Granted);
        Diagnostics.Bytes("scripted HTTP response", Response);

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
        Diagnostics.Act("stderr", string.Join("\n", StandardErrorLines()));
        return exitCode;
    }
}
