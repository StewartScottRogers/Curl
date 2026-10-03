using System.Text;
using Curl.Cli;
using Curl.Core;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins the <c>[TCP]</c> lines of a plain HTTP transfer's I/O - <c>query ALPN</c>, <c>send</c> and
/// <c>recv</c> - beside the <c>&gt;</c> and <c>&lt;</c> lines, against curl 8.21.0 (mingw, Schannel)
/// measured on 2026-10-02 with <c>Record-CurlExchange.ps1</c> (BL-1195 Notes). The scripted
/// connection answers at once, so no would-block <c>recv</c> line is written (ADR-0357's BL-1195
/// amendment). The transfer runs through the production handler set over a real
/// <see cref="TcpConnector" /> dialing a <see cref="ScriptedTcpDialer" />.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerTcpIoTraceTests
{
    private static readonly byte[] FortyByteResponse =
        Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nhi");

    private readonly MemoryStream standardError = new();

    [TestMethod]
    [DataRow("tcp")]
    [DataRow("network")]
    public async Task RunAsync_PlainGetUnderTraceConfigTcp_WritesQueryAlpnSendAndRecvBesideTheHeaders(string components)
    {
        // curl -s -v --trace-config tcp http://127.0.0.1:47195/ (BL-1195 Notes).
        int exitCode = await RunAsync("-v", "--trace-config", components, "http://127.0.0.1:47195/");

        Assert.AreEqual(0, exitCode);
        CollectionAssert.AreEqual(
            new[]
            {
                "* Established connection to 127.0.0.1 (127.0.0.1 port 47195) from 127.0.0.1 port 50000 ",
                "* [TCP] query ALPN",
                "* using HTTP/1.x",
                "* [TCP] send(len=79) -> 0, 79",
                "> GET / HTTP/1.1",
                "> Host: 127.0.0.1:47195",
                "> User-Agent: curl/8.21.0",
                "> Accept: */*",
                "> ",
                "* Request completely sent off",
                "* [TCP] recv(len=102400) -> 0, 40",
                "< HTTP/1.1 200 OK",
                "< Content-Length: 2",
                "< ",
            },
            TransferLines());
    }

    [TestMethod]
    public async Task RunAsync_ThroughAForwardProxyUnderTraceConfigTcp_WritesQueryAlpnSendAndRecvBesideTheHeaders()
    {
        // curl -s -v --trace-config tcp -x http://127.0.0.1:18533 http://example.invalid/ (BL-1253 Notes).
        int exitCode = await RunAsync("-v", "--trace-config", "tcp", "-x", "http://127.0.0.1:47195", "http://example.invalid/");

        Assert.AreEqual(0, exitCode);
        CollectionAssert.AreEqual(
            new[]
            {
                "* Established connection to 127.0.0.1 (127.0.0.1 port 47195) from 127.0.0.1 port 50000 ",
                "* [TCP] query ALPN",
                "* using HTTP/1.x",
                "* [TCP] send(len=131) -> 0, 131",
                "> GET http://example.invalid/ HTTP/1.1",
                "> Host: example.invalid",
                "> User-Agent: curl/8.21.0",
                "> Accept: */*",
                "> Proxy-Connection: Keep-Alive",
                "> ",
                "* Request completely sent off",
                "* [TCP] recv(len=102400) -> 0, 40",
                "< HTTP/1.1 200 OK",
                "< Content-Length: 2",
                "< ",
            },
            TransferLines());
    }

    [TestMethod]
    public async Task RunAsync_WithHaproxyProtocolUnderTraceConfigTcp_WritesTheProxyLinesSendBeforeEstablishedConnection()
    {
        // curl -s -v --trace-config tcp --haproxy-protocol http://127.0.0.1:18531/ (BL-1253 Notes).
        int exitCode = await RunAsync("-v", "--trace-config", "tcp", "--haproxy-protocol", "http://127.0.0.1:47195/");

        Assert.AreEqual(0, exitCode);
        List<string> lines = StandardErrorLines();
        int established = lines.FindIndex(line => line.StartsWith("* Established connection", StringComparison.Ordinal));
        CollectionAssert.AreEqual(
            new[] { "* [TCP] send(len=44) -> 0, 44", lines[established], "* [TCP] query ALPN", "* using HTTP/1.x", "* [TCP] send(len=79) -> 0, 79" },
            lines.Skip(established - 1).Take(5).ToArray());
    }

    [TestMethod]
    [DataRow("-v", "--trace-config", "all")]
    [DataRow("-vvvv")]
    public async Task RunAsync_PlainGetUnderAllOrVvvv_WritesQueryAlpnSendAndRecv(params string[] arguments)
    {
        // curl -s -vvvv and -v --trace-config all prefix every line with its time and transfer.
        await RunAsync([.. arguments, "http://127.0.0.1:47195/"]);

        List<string> lines = StandardErrorLines();
        string[] tcpLines = [.. lines.Where(line => line.Contains("* [TCP] ", StringComparison.Ordinal) && !line.Contains("fd=", StringComparison.Ordinal) && !line.Contains("local address", StringComparison.Ordinal))
            .Select(line => line[line.IndexOf("[TCP]", StringComparison.Ordinal)..])];
        CollectionAssert.AreEqual(
            new[] { "[TCP] query ALPN", "[TCP] send(len=79) -> 0, 79", "[TCP] recv(len=102400) -> 0, 40" },
            tcpLines);
    }

    [TestMethod]
    [DataRow("-v")]
    [DataRow("-vvv")]
    [DataRow("-v", "--trace-config", "happy-eyeballs,dns,setup,timer")]
    public async Task RunAsync_PlainGetWithoutTheTcpComponent_WritesNoTcpLine(params string[] arguments)
    {
        await RunAsync([.. arguments, "http://127.0.0.1:47195/"]);

        Assert.IsFalse(StandardErrorLines().Any(line => line.Contains("[TCP]", StringComparison.Ordinal)));
    }

    // The lines from Established connection to the response's blank line, every other component's
    // trace lines set aside.
    private string[] TransferLines()
    {
        List<string> lines = [.. StandardErrorLines().Where(line => !line.StartsWith("* [", StringComparison.Ordinal)
            || line.StartsWith("* [TCP]", StringComparison.Ordinal))];
        int start = lines.FindIndex(line => line.StartsWith("* Established connection", StringComparison.Ordinal));
        return [.. lines.Skip(start).Take(lines.IndexOf("< ") - start + 1)];
    }

    private List<string> StandardErrorLines() =>
        [.. Encoding.ASCII.GetString(standardError.ToArray())
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.TrimEnd('\r'))];

    /// <summary>
    /// Runs <c>-s</c> and <paramref name="arguments" /> through the production handler set over the
    /// connector the composition builds from them, whose connection replays the 40-byte response.
    /// </summary>
    private async Task<int> RunAsync(params string[] arguments)
    {
        CommandLineParseResult parsed = CommandLineParser.Parse(["-s", .. arguments], _ => true);
        Assert.IsTrue(parsed.IsAccepted);
        TcpConnector connector = CurlComposition.CreateTcpConnector(
            parsed.Options,
            new LoopbackDnsResolver(),
            new ScriptedTcpDialer(new ScriptedConnector([FortyByteResponse])),
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
