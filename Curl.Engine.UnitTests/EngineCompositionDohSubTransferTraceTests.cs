using Curl.Console;
using System.Net.Sockets;
using System.Text;
using Curl.Cli;
using Curl.Networking;
using Curl.Output;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Engine;

/// <summary>
/// Pins the <c>-v</c> lines of each DoH sub-transfer under <c>--trace-config dns</c> against curl
/// 8.21.0 (mingw, Schannel) measured on 2026-10-02 with <c>Record-CurlExchange.ps1 -Tls -Connections 2</c>
/// (BL-1157 Notes), with curl 8.22.0's changes measured on 2026-10-21 (BL-2973, ADR-0738): every
/// line prefixed <c>[DoH] </c> but the head and data lines, AAAA's sub-transfer whole before A's
/// (ADR-0380), a succeeded query ending <c>DoH request complete, N to go</c>, and the poll-timing lines (<c>Connection #1 is not open enough</c>,
/// <c>Hostname 127.0.0.1 was found in DNS cache</c>) left out. The DoH URL is <c>http://</c>, so the
/// Schannel trust and ALPN lines, which <c>DohSubTransferEventsTests</c> pins, are not part of it. Both
/// connectors are the composition's, over scripted dialers, and the lines are written by
/// <see cref="VerboseTransferEventWriter" /> as standard error gets them.
/// </summary>
[TestClass]
public sealed class EngineCompositionDohSubTransferTraceTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private const string DohUrl = "http://127.0.0.1:47112/dns-query";

    private static readonly byte[] AnswerHead = Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: application/dns-message\r\nContent-Length: ");

    // The measured A answer for example.test: TTL 60, 127.0.0.1.
    private static readonly byte[] AAnswer =
    [
        .. AnswerHead, .. "46\r\n\r\n"u8,
        0x00, 0x00, 0x81, 0x80, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00,
        0x07, .. "example"u8, 0x04, .. "test"u8, 0x00, 0x00, 0x01, 0x00, 0x01,
        0xC0, 0x0C, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0x00, 0x3C, 0x00, 0x04, 0x7F, 0x00, 0x00, 0x01,
    ];

    // The failed resolve's 3-byte answers, too small to decode.
    private static readonly byte[] TooSmallAnswer = [.. AnswerHead, .. "3\r\n\r\n"u8, 0x00, 0x00, 0x81];

    private readonly MemoryStream standardError = new();

    [TestMethod]
    public async Task Connect_UnderTraceConfigDns_WhenTheDohAnswersDoNotDecode_WritesEachSubTransfersPrefixedLines()
    {
        ConnectResult result = await ConnectAsync([TooSmallAnswer, TooSmallAnswer]);

        string[] expected =
        [
            "* [DNS] created DNS filter for example.test:47113, transport=3, queries=3",
            "* [DNS] added",
            "* [DNS] cf_dns_start host example.test:47113",
            .. SubTransferLines(1, null, 3),
            "* [DoH] [DoH] [AAAA] error decoding response: Too small",
            .. SubTransferLines(2, null, 3),
            "* [DoH] [DoH] [A] error decoding response: Too small",
            "* Could not resolve host: example.test",
        ];
        Diagnostics.Assert("result.ExitCode", CurlExitCode.CouldntResolveHost, result.ExitCode);
        Assert.AreEqual(CurlExitCode.CouldntResolveHost, result.ExitCode);
        Assert.AreEqual(string.Join('\n', expected), string.Join('\n', StandardErrorLines().Take(expected.Length)));
    }

    [TestMethod]
    public async Task Connect_UnderTraceConfigDns_WhenTheDohAnswerResolves_WritesTheAaaaDecodeFailureThenTheASubTransfersCompletedLine()
    {
        ConnectResult result = await ConnectAsync([AAnswer, AAnswer]);

        string[] expected =
        [
            .. SubTransferLines(1, null, 46),
            "* [DoH] [DoH] [AAAA] error decoding response: Unexpected TYPE",
            .. SubTransferLines(2, "0 to go", 46),
            "* [DNS] resolve complete for example.test:47113",
            "* Host example.test:47113 was resolved.",
        ];
        Diagnostics.Assert("result.Connection is not null", true, result.Connection is not null);
        Assert.IsNotNull(result.Connection);
        Assert.AreEqual(string.Join('\n', expected), string.Join('\n', StandardErrorLines().Skip(3).Take(expected.Length)));
    }

    [TestMethod]
    public async Task Connect_WithVerboseAlone_WritesNoSubTransferLine()
    {
        await ConnectAsync([AAnswer, AAnswer], "-v");

        Diagnostics.Assert("sub-transfer line present", false, StandardErrorLines().Any(line => line.Contains("DoH request complete", StringComparison.Ordinal) || line.StartsWith("> POST", StringComparison.Ordinal)));
        Assert.IsFalse(StandardErrorLines().Any(line => line.Contains("DoH request complete", StringComparison.Ordinal) || line.StartsWith("> POST", StringComparison.Ordinal)));
    }

    // One sub-transfer's lines as curl writes them, the poll-timing ones left out; the POST's
    // head is the measured one. The second finds the DoH server's address in the DNS cache, as
    // curl's does.
    // toGo is null for a query that failed, which curl 8.22.0 ends with its error line in place of the completed line (BL-2970).
    private static string[] SubTransferLines(int connectionNumber, string? toGo, int answerLength)
    {
        return
        [
            "* [DNS] created DNS filter for 127.0.0.1:47112, transport=3, queries=3",
            "* [DoH] [DNS] added",
            "* [DoH] [DNS] cf_dns_start host 127.0.0.1:47112",
            .. connectionNumber > 1 ? ["* [DoH] Hostname 127.0.0.1 was found in DNS cache"] : Array.Empty<string>(),
            "* [DoH]   Trying 127.0.0.1:47112...",
            "* [DoH] [DNS] Curl_conn_connect(block=0) -> 0, done=0",
            "* [DoH] [DNS] connected filter chain below",
            "* [DoH] [DNS] Curl_conn_connect(block=0) -> 0, done=1",
            "* [DoH] Established connection to 127.0.0.1 (127.0.0.1 port 47112) from 127.0.0.1 port 50000 ",
            "* [DoH] [DNS] removing connected setup filter",
            "* [DoH] [DNS] destroy",
            "* [DoH] using HTTP/1.x",
            "> POST /dns-query HTTP/1.1",
            "> Host: 127.0.0.1:47112",
            "> Accept: */*",
            "> Content-Type: application/dns-message",
            "> Content-Length: 30",
            "> ",
            "} [30 bytes data]",
            "* [DoH] upload completely sent off: 30 bytes",
            "< HTTP/1.1 200 OK",
            "< Content-Type: application/dns-message",
            $"< Content-Length: {answerLength}",
            "< ",
            $"{{ [{answerLength} bytes data]",
            $"* [DoH] Connection #{connectionNumber} to host 127.0.0.1:47112 left intact",
            .. toGo is null ? Array.Empty<string>() : [$"* [DoH] DoH request complete, {toGo}"],
        ];
    }

    private List<string> StandardErrorLines() =>
        [.. Encoding.Latin1.GetString(standardError.ToArray()).Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => line.TrimEnd('\r'))];

    /// <summary>
    /// Connects to <c>example.test:47113</c> through the composition's TCP connector over the
    /// composition's DoH resolver, whose own connector is <see cref="EngineComposition.CreateDohConnector" />
    /// over a dialer that answers each DoH connection with the next of <paramref name="dohAnswers" />.
    /// </summary>
    private async Task<ConnectResult> ConnectAsync(byte[][] dohAnswers, params string[] arguments)
    {
        string[] given = arguments.Length == 0 ? ["-v", "--trace-config", "dns"] : arguments;
        Diagnostics.Arrange("arguments", string.Join(' ', given) + " --doh-url " + DohUrl + " http://example.test:47113/");
        Diagnostics.Arrange("DoH answer lengths", string.Join(", ", dohAnswers.Select(answer => answer.Length)));
        CommandLineParseResult parsed = CommandLineParser.Parse([.. given, "--doh-url", DohUrl, "http://example.test:47113/"], _ => true);
        Assert.IsTrue(parsed.IsAccepted);
        CommandLineOptions options = parsed.Options;
        FlowScopedTransferEvents? resolverEvents = EngineComposition.TracesDns(EasyOptionsFromCommandLine.TraceComponentsOf(options)) ? new() : null;
        TcpConnector dohConnector = EngineComposition.CreateDohConnector(EasyOptionsFromCommandLine.TraceComponentsOf(options), EasyOptionsFromCommandLine.TlsOf(options), new ScriptedTcpDialer(new ScriptedConnector(dohAnswers)), TimeProvider.System);
        TcpConnector connector = EngineComposition.CreateTcpConnector(
            EasyOptionsFromCommandLine.ToolOptionsOf(options), EasyOptionsFromCommandLine.TraceComponentsOf(options), EasyOptionsFromCommandLine.NetworkOf(options), EasyOptionsFromCommandLine.HttpOf(options), EasyOptionsFromCommandLine.ProxyOf(options), EasyOptionsFromCommandLine.AuthenticationOf(options),
            EngineComposition.CreateDohResolver(DohUrl, dohConnector, AddressFamily.Unspecified, resolverEvents),
            new ScriptedTcpDialer(new ScriptedConnector([])),
            new PassThroughTlsProvider(),
            TimeProvider.System,
            HttpProxyTunnelOptions.Default,
            resolverEvents: resolverEvents);
        VerboseTransferEventWriter events = new(standardError, writesDataLines: true, TlsBackend.Schannel);

        ConnectResult result;
        using (Diagnostics.Phase("connect"))
        {
            result = await connector.ConnectAsync(new ConnectTarget("example.test", 47113, false) { Events = events }, CancellationToken.None);
        }

        Diagnostics.Act("exit code", result.ExitCode);
        Diagnostics.Act("stderr lines", string.Join(" | ", StandardErrorLines()));
        return result;
    }
}
