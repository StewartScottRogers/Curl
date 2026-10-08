using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Curl.Cli;
using Curl.Core;
using Curl.Networking;
using Curl.Protocol.Abstractions;
using Curl.Testing;

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

    private byte[][] serverReads = [FortyByteResponse];

    private ITlsProvider tlsProvider = new PassThroughTlsProvider();

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("tcp")]
    [DataRow("network")]
    public async Task RunAsync_PlainGetUnderTraceConfigTcp_WritesQueryAlpnSendAndRecvBesideTheHeaders(string components)
    {
        // curl -s -v --trace-config tcp http://127.0.0.1:47195/ (BL-1195 Notes).
        int exitCode = await RunAsync("-v", "--trace-config", components, "http://127.0.0.1:47195/");

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        string[] expectedLines =
        [
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
        ];
        string[] actualLines = TransferLines();

        Diagnostics.Diff("trace lines", string.Join('\n', expectedLines), string.Join('\n', actualLines));
        CollectionAssert.AreEqual(expectedLines, actualLines);
    }

    [TestMethod]
    public async Task RunAsync_ThroughAForwardProxyUnderTraceConfigTcp_WritesQueryAlpnSendAndRecvBesideTheHeaders()
    {
        // curl -s -v --trace-config tcp -x http://127.0.0.1:18533 http://example.invalid/ (BL-1253 Notes).
        int exitCode = await RunAsync("-v", "--trace-config", "tcp", "-x", "http://127.0.0.1:47195", "http://example.invalid/");

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        string[] expectedLines =
        [
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
        ];
        string[] actualLines = TransferLines();

        Diagnostics.Diff("trace lines", string.Join('\n', expectedLines), string.Join('\n', actualLines));
        CollectionAssert.AreEqual(expectedLines, actualLines);
    }

    [TestMethod]
    public async Task RunAsync_WithHaproxyProtocolUnderTraceConfigTcp_WritesTheProxyLinesSendBeforeEstablishedConnection()
    {
        // curl -s -v --trace-config tcp --haproxy-protocol http://127.0.0.1:18531/ (BL-1253 Notes).
        int exitCode = await RunAsync("-v", "--trace-config", "tcp", "--haproxy-protocol", "http://127.0.0.1:47195/");

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        List<string> lines = StandardErrorLines();
        int established = lines.FindIndex(line => line.StartsWith("* Established connection", StringComparison.Ordinal));
        Diagnostics.Assert("established connection line index", true, established >= 1);
        string[] expectedLines = ["* [TCP] send(len=44) -> 0, 44", lines[established], "* [TCP] query ALPN", "* using HTTP/1.x", "* [TCP] send(len=79) -> 0, 79"];
        string[] actualLines = lines.Skip(established - 1).Take(5).ToArray();
        Diagnostics.Diff("trace lines around the established connection", string.Join('\n', expectedLines), string.Join('\n', actualLines));
        CollectionAssert.AreEqual(expectedLines, actualLines);
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
        string[] expectedLines = ["[TCP] query ALPN", "[TCP] send(len=79) -> 0, 79", "[TCP] recv(len=102400) -> 0, 40"];
        Diagnostics.Diff("tcp lines", string.Join('\n', expectedLines), string.Join('\n', tcpLines));
        CollectionAssert.AreEqual(expectedLines, tcpLines);
    }

    [TestMethod]
    [DataRow("-v")]
    [DataRow("-vvv")]
    [DataRow("-v", "--trace-config", "happy-eyeballs,dns,setup,timer")]
    public async Task RunAsync_PlainGetWithoutTheTcpComponent_WritesNoTcpLine(params string[] arguments)
    {
        await RunAsync([.. arguments, "http://127.0.0.1:47195/"]);

        Diagnostics.Assert("any [TCP] line", false, StandardErrorLines().Any(line => line.Contains("[TCP]", StringComparison.Ordinal)));
        Assert.IsFalse(StandardErrorLines().Any(line => line.Contains("[TCP]", StringComparison.Ordinal)));
    }

    [TestMethod]
    [DataRow("tcp")]
    [DataRow("network")]
    public async Task RunAsync_FtpRetrUnderTraceConfigTcp_WritesSendAndRecvBesideEachCommandAndReplyAndTcp1ForTheData(string components)
    {
        // curl -s -v --trace-config tcp ftp://127.0.0.1:18601/a.txt, a 5-byte file (BL-1259 Notes); the
        // scripted server answers at once, so no would-block recv line is written, and QUIT writes none.
        serverReads =
        [
            .. new[]
            {
                "220 Recorder ready\r\n", "331 Password required\r\n", "230 Logged in\r\n", "257 \"/\" is current directory\r\n",
                "229 Entering Extended Passive Mode (|||59271|)\r\n", "200 Type set\r\n", "213 5\r\n",
                "150 Opening BINARY mode data connection\r\n", "hello", "226 Transfer complete\r\n", "221 Bye\r\n",
            }.Select(Encoding.ASCII.GetBytes),
        ];

        int exitCode = await RunAsync("-v", "--trace-config", components, "ftp://127.0.0.1:18601/a.txt");

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        string[] expectedLines =
        [
            "* [TCP] recv(len=900) -> 0, 20", "< 220 Recorder ready",
            "* [TCP] send(len=16) -> 0, 16", "> USER anonymous",
            "* [TCP] recv(len=900) -> 0, 23", "< 331 Password required",
            "* [TCP] send(len=22) -> 0, 22", "> PASS ftp@example.com",
            "* [TCP] recv(len=900) -> 0, 15", "< 230 Logged in",
            "* [TCP] send(len=5) -> 0, 5", "> PWD",
            "* [TCP] recv(len=900) -> 0, 30", "< 257 \"/\" is current directory",
            "* [TCP] send(len=6) -> 0, 6", "> EPSV",
            "* [TCP] recv(len=900) -> 0, 48", "< 229 Entering Extended Passive Mode (|||59271|)",
            "* [TCP] send(len=8) -> 0, 8", "> TYPE I",
            "* [TCP] recv(len=900) -> 0, 14", "< 200 Type set",
            "* [TCP] send(len=12) -> 0, 12", "> SIZE a.txt",
            "* [TCP] recv(len=900) -> 0, 7", "< 213 5",
            "* [TCP] send(len=12) -> 0, 12", "> RETR a.txt",
            "* [TCP] recv(len=900) -> 0, 41", "< 150 Opening BINARY mode data connection",
            "* [TCP-1] recv(len=5) -> 0, 5",
            "* [TCP] recv(len=900) -> 0, 23", "< 226 Transfer complete",
        ];
        string[] actualLines = StandardErrorLines().Where(line => line.StartsWith("> ", StringComparison.Ordinal) || line.StartsWith("< ", StringComparison.Ordinal)
            || line.Contains("] send(len=", StringComparison.Ordinal) || line.Contains("] recv(len=", StringComparison.Ordinal)).ToArray();

        Diagnostics.Diff("trace lines", string.Join('\n', expectedLines), string.Join('\n', actualLines));
        CollectionAssert.AreEqual(expectedLines, actualLines);
    }

    [TestMethod]
    [DataRow("-v", "--trace-config", "tcp")]
    [DataRow("-v", "--trace-config", "network")]
    [DataRow("-v", "--trace-config", "all")]
    [DataRow("-vvvv")]
    public async Task RunAsync_HttpsGetUnderTraceConfigTcp_WritesTheHandshakeRecordsThenTheApplicationDataRecordsBesideTheHeaders(params string[] arguments)
    {
        // curl -s -v -k --trace-config tcp https://127.0.0.1:P/ (BL-1253 Notes): the handshake's records
        // with Schannel's 4096-byte reads, the request's and response's with its 103424-byte reads, and
        // no [TCP] query ALPN, the [SSL] filter answering it (BL-1287). The scripted server answers at once,
        // so no would-block recv line is written.
        serverReads = [new byte[81], new byte[1175], new byte[51], FortyByteResponse];
        tlsProvider = new RecordExchangingTlsProvider();

        int exitCode = await RunAsync([.. arguments, "https://127.0.0.1:47195/"]);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        string[] expectedLines =
        [
            "[TCP] send(len=429) -> 0, 429",
            "[TCP] recv(len=4096) -> 0, 81",
            "[TCP] recv(len=4096) -> 0, 1175",
            "[TCP] send(len=158) -> 0, 158",
            "[TCP] recv(len=4096) -> 0, 51",
            "[TCP] send(len=79) -> 0, 79",
            "> GET / HTTP/1.1",
            "> Accept: */*",
            "[TCP] recv(len=103424) -> 0, 40",
            "< HTTP/1.1 200 OK",
            "< ",
        ];
        string[] actualLines = StandardErrorLines()
            .Select(line => line.Contains("[TCP] ", StringComparison.Ordinal) ? line[line.IndexOf("[TCP] ", StringComparison.Ordinal)..] : Regex.Replace(line, @"^.*] (?=[<>] )", ""))
            .Where(line => (line.StartsWith("[TCP] ", StringComparison.Ordinal) && line.Contains("(len=", StringComparison.Ordinal)) || (line.Contains("ALPN", StringComparison.Ordinal) && !line.Contains("[SSL] ", StringComparison.Ordinal))
                || line is "> GET / HTTP/1.1" or "> Accept: */*" or "< HTTP/1.1 200 OK" or "< ")
            .ToArray();

        Diagnostics.Diff("trace lines", string.Join('\n', expectedLines), string.Join('\n', actualLines));
        CollectionAssert.AreEqual(expectedLines, actualLines);
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
        Diagnostics.Arrange("arguments", "-s " + string.Join(' ', arguments));
        Diagnostics.Arrange("scripted server read lengths", string.Join(", ", serverReads.Select(read => read.Length.ToString(CultureInfo.InvariantCulture))));
        Diagnostics.Arrange("tls provider", tlsProvider.GetType().Name);
        Diagnostics.Assert("command line accepted", true, parsed.IsAccepted);
        Assert.IsTrue(parsed.IsAccepted);
        TcpConnector connector = CurlComposition.CreateTcpConnector(
            parsed.Options,
            new LoopbackDnsResolver(),
            new ScriptedTcpDialer(new ScriptedConnector(serverReads)),
            tlsProvider,
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
        Diagnostics.Act("stderr", string.Join('\n', StandardErrorLines()));
        return exitCode;
    }

    /// <summary>
    /// A TLS provider whose handshake writes and reads records of curl's measured sizes over the
    /// plaintext connection - a 429-byte hello, two reads, a 158-byte finish and a third read - and
    /// returns that connection as secured, so the request and response pass through it as written.
    /// </summary>
    private sealed class RecordExchangingTlsProvider : ITlsProvider
    {
        public async ValueTask<ConnectResult> AuthenticateAsClientAsync(IConnection plaintext, string targetHost, CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[4096];
            await plaintext.WriteAsync(new byte[429], cancellationToken);
            await plaintext.ReadAsync(buffer, cancellationToken);
            await plaintext.ReadAsync(buffer, cancellationToken);
            await plaintext.WriteAsync(new byte[158], cancellationToken);
            await plaintext.ReadAsync(buffer, cancellationToken);
            return ConnectResult.Connected(plaintext);
        }
    }
}
