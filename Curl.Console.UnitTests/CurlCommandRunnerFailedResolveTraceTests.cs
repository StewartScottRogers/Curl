using System.Net;
using System.Text;
using Curl.Cli;
using Curl.Core;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins the <c>-v</c> lines of a name that does not resolve, with and without
/// <c>--trace-config dns</c>, against curl 8.21.0 (mingw, Schannel) measured on 2026-10-02 with
/// <c>Record-CurlExchange.ps1</c> (BL-1157 Notes), leaving out the threaded resolver's
/// <c>queueing query</c>, <c>resolve incomplete</c>, repeated <c>done=0</c> and per-family
/// <c>resolved IPv4: (none)</c> lines, which follow curl's poll timing (ADR-0366). The transfer runs
/// through the production handler set over the connector <see cref="CurlComposition.CreateTcpConnector" />
/// builds from the command line, whose resolver answers nothing.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerFailedResolveTraceTests
{
    private readonly MemoryStream standardError = new();

    [TestMethod]
    public async Task RunAsync_UnderTraceConfigDns_WritesCurlsFailedResolveLines()
    {
        // curl -s -v --trace-config dns http://nonexistent.invalid:47114/
        int exitCode = await RunAsync("-s", "-v", "--trace-config", "dns", "http://nonexistent.invalid:47114/");

        Assert.AreEqual((int)CurlExitCode.CouldntResolveHost, exitCode);
        CollectionAssert.AreEqual(
            new[]
            {
                "* [DNS] created DNS filter for nonexistent.invalid:47114, transport=3, queries=3",
                "* [DNS] added",
                "* [DNS] cf_dns_start host nonexistent.invalid:47114",
                "* Could not resolve host: nonexistent.invalid",
                "* Could not resolve host: nonexistent.invalid",
                "* [DNS] cache negative name resolve for nonexistent.invalid:47114 type=A+AAAA",
                "* Could not resolve: nonexistent.invalid:47114",
                "* [DNS] error resolving: 6",
                "* [DNS] Curl_conn_connect(block=0) -> 6, done=0",
                "* [DNS] Curl_conn_connect(), filter returned 6",
                "* [DNS] [1] shutdown async",
                "* closing connection #0",
                "* [DNS] [1] destroy async",
            },
            StandardErrorLines());
    }

    [TestMethod]
    public async Task RunAsync_UnderTraceConfigDnsAndIPv4Only_AsksForTheARecordAlone()
    {
        // curl -s -v -4 --trace-config dns http://nonexistent.invalid:47114/
        await RunAsync("-s", "-v", "-4", "--trace-config", "dns", "http://nonexistent.invalid:47114/");

        List<string> lines = StandardErrorLines();
        Assert.AreEqual("* [DNS] created DNS filter for nonexistent.invalid:47114, transport=3, queries=1", lines[0]);
        Assert.Contains("* [DNS] cache negative name resolve for nonexistent.invalid:47114 type=A", lines);
    }

    [TestMethod]
    public async Task RunAsync_WithVerboseAlone_WritesCurlsCouldNotResolveLines()
    {
        // curl -s -v http://nonexistent.invalid:47114/
        await RunAsync("-s", "-v", "http://nonexistent.invalid:47114/");

        CollectionAssert.AreEqual(
            new[]
            {
                "* Could not resolve host: nonexistent.invalid",
                "* Could not resolve host: nonexistent.invalid",
                "* Could not resolve: nonexistent.invalid:47114",
                "* closing connection #0",
            },
            StandardErrorLines());
    }

    private List<string> StandardErrorLines() =>
        [.. Encoding.ASCII.GetString(standardError.ToArray()).Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => line.TrimEnd('\r'))];

    /// <summary>
    /// Runs <paramref name="arguments" /> through the production handler set over the connector the
    /// composition builds from them, whose resolver answers nothing.
    /// </summary>
    private async Task<int> RunAsync(params string[] arguments)
    {
        CommandLineParseResult parsed = CommandLineParser.Parse(arguments, _ => true);
        Assert.IsTrue(parsed.IsAccepted);
        TcpConnector connector = CurlComposition.CreateTcpConnector(
            parsed.Options,
            new UnresolvingDnsResolver(),
            new UnusedTcpDialer(),
            new PassThroughTlsProvider(),
            TimeProvider.System,
            HttpProxyTunnelOptions.Default);
        InMemoryFileSystem files = new();

        return await new CurlCommandRunner(
                _ => new TransferDispatch(
                    new ProtocolDispatcher(CurlComposition.CreateProtocolHandlers(connector, new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"), new PassThroughTlsProvider(), new UnresolvingDnsResolver())),
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

    /// <summary>A resolver that answers no address for any name.</summary>
    private sealed class UnresolvingDnsResolver : IDnsResolver
    {
        public ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<IPAddress>>([]);
    }

    /// <summary>A dialer no test reaches: a name that does not resolve is never dialled.</summary>
    private sealed class UnusedTcpDialer : ITcpDialer
    {
        public ValueTask<DialedTcpConnection> DialAsync(IPEndPoint endPoint, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("A name that did not resolve was dialled.");

        public ValueTask<DialedTcpConnection> DialFromAsync(IPEndPoint endPoint, IPEndPoint localEndPoint, int localPortCount, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("A name that did not resolve was dialled.");

        public ValueTask<IConnection> DialUnixSocketAsync(UnixSocketAddress address, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("A name that did not resolve was dialled.");
    }
}
