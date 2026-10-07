using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using Curl.Cli;
using Curl.Core;
using Curl.Networking;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins the <c>-v --trace-config dns</c> lines of a connect through a proxy, over a Unix socket, on a
/// negative DNS cache entry and to a looked-up name whose dial is refused, against curl 8.21.0
/// (mingw, Schannel) measured on 2026-10-02 (BL-1157 and BL-1181 Notes), leaving out the repeated
/// <c>done=0</c> lines that follow curl's poll timing and the system resolver's own lines
/// (ADR-0366). Elapsed milliseconds are written as <c>N</c>. The transfer runs through the
/// production handler set over the connector <see cref="CurlComposition.CreateTcpConnector" />
/// builds from the command line, whose resolver answers <c>127.0.0.1</c> and whose dials are refused.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerDnsFilterRouteTraceTests
{
    private readonly MemoryStream standardError = new();

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task RunAsync_ThroughAProxyUnderTraceConfigDns_WritesTheFilterLinesForTheProxy()
    {
        // curl -s -v --trace-config dns -x http://127.0.0.1:47115 http://example.test/
        int exitCode = await RunAsync("-s", "-v", "--trace-config", "dns", "-x", "http://127.0.0.1:47115", "http://example.test/");

        Diagnostics.Assert("exit code", (int)CurlExitCode.CouldntConnect, exitCode);
        DiagnoseLines(
            "stderr lines",
            [
                "* [DNS] created DNS filter for 127.0.0.1:47115, transport=3, queries=3",
                "* [DNS] added",
                "* [DNS] cf_dns_start host 127.0.0.1:47115",
                "*   Trying 127.0.0.1:47115...",
                "* [DNS] Curl_conn_connect(block=0) -> 0, done=0",
                "* connect to 127.0.0.1 port 47115 from 0.0.0.0 port 0 failed: Connection refused",
                "* Failed to connect to 127.0.0.1:47115 over proxy 127.0.0.1 after N ms: Could not connect to server",
                "* [DNS] Curl_conn_connect(block=0) -> 7, done=0",
                "* [DNS] Curl_conn_connect(), filter returned 7",
                "* closing connection #0",
            ],
            StandardErrorLines());
        Assert.AreEqual((int)CurlExitCode.CouldntConnect, exitCode);
        CollectionAssert.AreEqual(
            new[]
            {
                "* [DNS] created DNS filter for 127.0.0.1:47115, transport=3, queries=3",
                "* [DNS] added",
                "* [DNS] cf_dns_start host 127.0.0.1:47115",
                "*   Trying 127.0.0.1:47115...",
                "* [DNS] Curl_conn_connect(block=0) -> 0, done=0",
                "* connect to 127.0.0.1 port 47115 from 0.0.0.0 port 0 failed: Connection refused",
                "* Failed to connect to 127.0.0.1:47115 over proxy 127.0.0.1 after N ms: Could not connect to server",
                "* [DNS] Curl_conn_connect(block=0) -> 7, done=0",
                "* [DNS] Curl_conn_connect(), filter returned 7",
                "* closing connection #0",
            },
            StandardErrorLines());
    }

    [TestMethod]
    public async Task RunAsync_ThroughATunnellingProxyUnderTraceConfigDns_WritesTheFilterLinesForTheProxy()
    {
        // curl -s -v --trace-config dns -p -x http://127.0.0.1:47115 http://example.test/
        int exitCode = await RunAsync("-s", "-v", "--trace-config", "dns", "-p", "-x", "http://127.0.0.1:47115", "http://example.test/");

        List<string> lines = StandardErrorLines();
        Diagnostics.Assert("exit code", (int)CurlExitCode.CouldntConnect, exitCode);
        Diagnostics.Assert("first line", "* [DNS] created DNS filter for 127.0.0.1:47115, transport=3, queries=3", lines[0]);
        DiagnoseLines(
            "last four lines",
            [
                "* Failed to connect to example.test:80 over proxy 127.0.0.1 after N ms: Could not connect to server",
                "* [DNS] Curl_conn_connect(block=0) -> 7, done=0",
                "* [DNS] Curl_conn_connect(), filter returned 7",
                "* closing connection #0",
            ],
            lines.TakeLast(4).ToArray());
        Assert.AreEqual("* [DNS] created DNS filter for 127.0.0.1:47115, transport=3, queries=3", lines[0]);
        CollectionAssert.AreEqual(
            new[]
            {
                "* Failed to connect to example.test:80 over proxy 127.0.0.1 after N ms: Could not connect to server",
                "* [DNS] Curl_conn_connect(block=0) -> 7, done=0",
                "* [DNS] Curl_conn_connect(), filter returned 7",
                "* closing connection #0",
            },
            lines.TakeLast(4).ToArray());
    }

    [TestMethod]
    public async Task RunAsync_OverARefusedUnixSocketUnderTraceConfigDns_WritesTheUnixSocketFiltersLines()
    {
        // curl -s -v --trace-config dns --unix-socket /tmp/nonexistent.sock http://example.test/
        int exitCode = await RunAsync("-s", "-v", "--trace-config", "dns", "--unix-socket", "/tmp/nonexistent.sock", "http://example.test/");

        Diagnostics.Assert("exit code", (int)CurlExitCode.CouldntConnect, exitCode);
        DiagnoseLines(
            "stderr lines",
            [
                "* [DNS] created DNS filter for /tmp/nonexistent.sock:0, transport=6, queries=3",
                "* [DNS] added",
                "* [DNS] cf_dns_start unix-domain-socket /tmp/nonexistent.sock:0",
                "*   Trying /tmp/nonexistent.sock:0...",
                "* Immediate connect fail for /tmp/nonexistent.sock: Connection refused",
                "* connect to /tmp/nonexistent.sock port 0 from  port 0 failed: Connection refused",
                "* Failed to connect to example.test:80 over unix:///tmp/nonexistent.sock after N ms: Could not connect to server",
                "* [DNS] Curl_conn_connect(block=0) -> 7, done=0",
                "* [DNS] Curl_conn_connect(), filter returned 7",
                "* closing connection #0",
            ],
            StandardErrorLines());
        Assert.AreEqual((int)CurlExitCode.CouldntConnect, exitCode);
        CollectionAssert.AreEqual(
            new[]
            {
                "* [DNS] created DNS filter for /tmp/nonexistent.sock:0, transport=6, queries=3",
                "* [DNS] added",
                "* [DNS] cf_dns_start unix-domain-socket /tmp/nonexistent.sock:0",
                "*   Trying /tmp/nonexistent.sock:0...",
                "* Immediate connect fail for /tmp/nonexistent.sock: Connection refused",
                "* connect to /tmp/nonexistent.sock port 0 from  port 0 failed: Connection refused",
                "* Failed to connect to example.test:80 over unix:///tmp/nonexistent.sock after N ms: Could not connect to server",
                "* [DNS] Curl_conn_connect(block=0) -> 7, done=0",
                "* [DNS] Curl_conn_connect(), filter returned 7",
                "* closing connection #0",
            },
            StandardErrorLines());
    }

    [TestMethod]
    public async Task RunAsync_OnANegativeCacheEntryUnderTraceConfigDns_WritesTheEntrysLinesAndNoAsyncTeardown()
    {
        // curl -s -v --trace-config dns -6 --resolve foo:47500:127.0.0.1 http://foo:47500/
        int exitCode = await RunAsync("-s", "-v", "--trace-config", "dns", "-6", "--resolve", "foo:47500:127.0.0.1", "http://foo:47500/");

        Diagnostics.Assert("exit code", (int)CurlExitCode.CouldntResolveHost, exitCode);
        DiagnoseLines(
            "stderr lines",
            [
                "* Added foo:47500:127.0.0.1 to DNS cache",
                "* [DNS] created DNS filter for foo:47500, transport=3, queries=2",
                "* [DNS] added",
                "* [DNS] cf_dns_start host foo:47500",
                "* [DNS] cache entry does not have type=AAAA addresses",
                "* Negative DNS entry",
                "* Could not resolve host: foo",
                "* Could not resolve: foo:47500",
                "* Could not resolve: foo",
                "* [DNS] error resolving: 6",
                "* [DNS] Curl_conn_connect(block=0) -> 6, done=0",
                "* [DNS] Curl_conn_connect(), filter returned 6",
                "* closing connection #0",
            ],
            StandardErrorLines());
        Assert.AreEqual((int)CurlExitCode.CouldntResolveHost, exitCode);
        CollectionAssert.AreEqual(
            new[]
            {
                "* Added foo:47500:127.0.0.1 to DNS cache",
                "* [DNS] created DNS filter for foo:47500, transport=3, queries=2",
                "* [DNS] added",
                "* [DNS] cf_dns_start host foo:47500",
                "* [DNS] cache entry does not have type=AAAA addresses",
                "* Negative DNS entry",
                "* Could not resolve host: foo",
                "* Could not resolve: foo:47500",
                "* Could not resolve: foo",
                "* [DNS] error resolving: 6",
                "* [DNS] Curl_conn_connect(block=0) -> 6, done=0",
                "* [DNS] Curl_conn_connect(), filter returned 6",
                "* closing connection #0",
            },
            StandardErrorLines());
    }

    [TestMethod]
    public async Task RunAsync_OnANegativeCacheEntryWithVerboseAlone_WritesTheThreeCouldNotResolveLines()
    {
        // curl -s -v -6 --resolve foo:47500:127.0.0.1 http://foo:47500/
        int exitCode = await RunAsync("-s", "-v", "-6", "--resolve", "foo:47500:127.0.0.1", "http://foo:47500/");

        Diagnostics.Assert("exit code", (int)CurlExitCode.CouldntResolveHost, exitCode);
        DiagnoseLines(
            "stderr lines",
            [
                "* Added foo:47500:127.0.0.1 to DNS cache",
                "* Negative DNS entry",
                "* Could not resolve host: foo",
                "* Could not resolve: foo:47500",
                "* Could not resolve: foo",
                "* closing connection #0",
            ],
            StandardErrorLines());
        CollectionAssert.AreEqual(
            new[]
            {
                "* Added foo:47500:127.0.0.1 to DNS cache",
                "* Negative DNS entry",
                "* Could not resolve host: foo",
                "* Could not resolve: foo:47500",
                "* Could not resolve: foo",
                "* closing connection #0",
            },
            StandardErrorLines());
    }

    [TestMethod]
    public async Task RunAsync_WhenALookedUpNameIsRefusedUnderTraceConfigDns_CompletesTheResolveAndTearsItDown()
    {
        // curl -s -v --trace-config dns http://<a name the system resolver answers>:47199/
        int exitCode = await RunAsync("-s", "-v", "--trace-config", "dns", "http://example.test:47199/");

        List<string> lines = StandardErrorLines();
        Diagnostics.Assert("exit code", (int)CurlExitCode.CouldntConnect, exitCode);
        DiagnoseLines(
            "resolve lines",
            [
                "* [DNS] cf_dns_start host example.test:47199",
                "* [DNS] resolve complete for example.test:47199",
                "* Host example.test:47199 was resolved.",
            ],
            lines.Skip(2).Take(3).ToArray());
        DiagnoseLines(
            "teardown lines",
            [
                "* [DNS] Curl_conn_connect(block=0) -> 7, done=0",
                "* [DNS] Curl_conn_connect(), filter returned 7",
                "* [DNS] [1] shutdown async",
                "* closing connection #0",
                "* [DNS] [1] destroy async",
            ],
            lines.TakeLast(5).ToArray());
        CollectionAssert.AreEqual(
            new[]
            {
                "* [DNS] cf_dns_start host example.test:47199",
                "* [DNS] resolve complete for example.test:47199",
                "* Host example.test:47199 was resolved.",
            },
            lines.Skip(2).Take(3).ToArray());
        CollectionAssert.AreEqual(
            new[]
            {
                "* [DNS] Curl_conn_connect(block=0) -> 7, done=0",
                "* [DNS] Curl_conn_connect(), filter returned 7",
                "* [DNS] [1] shutdown async",
                "* closing connection #0",
                "* [DNS] [1] destroy async",
            },
            lines.TakeLast(5).ToArray());
    }

    private List<string> StandardErrorLines() =>
        [.. Encoding.ASCII.GetString(standardError.ToArray())
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => Regex.Replace(line.TrimEnd('\r'), @"after \d+ ms", "after N ms"))];

    private void DiagnoseLines(string label, string[] expected, IReadOnlyCollection<string> actual) =>
        Diagnostics.Diff(label, string.Join("\n", expected), string.Join("\n", actual));

    /// <summary>
    /// Runs <paramref name="arguments" /> through the production handler set over the connector the
    /// composition builds from them, whose resolver answers <c>127.0.0.1</c> and whose dials are refused.
    /// </summary>
    private async Task<int> RunAsync(params string[] arguments)
    {
        Diagnostics.Arrange("command line", string.Join(" ", arguments));
        CommandLineParseResult parsed = CommandLineParser.Parse(arguments, _ => true);
        Assert.IsTrue(parsed.IsAccepted);
        TcpConnector connector = CurlComposition.CreateTcpConnector(
            parsed.Options,
            new LoopbackDnsResolver(),
            new RefusingTcpDialer(),
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
                .RunAsync(arguments);
        }

        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Bytes("stderr", standardError.ToArray());
        return exitCode;
    }

    /// <summary>A resolver that answers <c>127.0.0.1</c> for any name.</summary>
    private sealed class LoopbackDnsResolver : IDnsResolver
    {
        public ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<IPAddress>>([IPAddress.Loopback]);
    }

    /// <summary>A dialer every dial of which, TCP or Unix socket, is refused.</summary>
    private sealed class RefusingTcpDialer : ITcpDialer
    {
        public ValueTask<DialedTcpConnection> DialAsync(IPEndPoint endPoint, CancellationToken cancellationToken) =>
            throw new SocketException((int)SocketError.ConnectionRefused);

        public ValueTask<DialedTcpConnection> DialFromAsync(IPEndPoint endPoint, IPEndPoint localEndPoint, int localPortCount, CancellationToken cancellationToken) =>
            throw new SocketException((int)SocketError.ConnectionRefused);

        public ValueTask<IConnection> DialUnixSocketAsync(UnixSocketAddress address, CancellationToken cancellationToken) =>
            throw new SocketException((int)SocketError.ConnectionRefused);
    }
}
