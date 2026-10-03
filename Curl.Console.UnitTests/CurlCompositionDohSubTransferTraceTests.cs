using System.Net.Sockets;
using System.Text;
using Curl.Cli;
using Curl.Networking;
using Curl.Output;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins the <c>-v</c> lines of each DoH sub-transfer under <c>--trace-config dns</c> against curl
/// 8.21.0 (mingw, Schannel) measured on 2026-10-02 with <c>Record-CurlExchange.ps1 -Tls -Connections 2</c>
/// (BL-1157 Notes): every line prefixed <c>[DNS] </c> but the head and data lines, A's sub-transfer
/// whole before AAAA's (ADR-0380), and the poll-timing lines (<c>Connection #1 is not open enough</c>,
/// <c>Hostname 127.0.0.1 was found in DNS cache</c>) left out. The DoH URL is <c>http://</c>, so the
/// Schannel trust and ALPN lines, which <c>DohSubTransferEventsTests</c> pins, are not part of it. Both
/// connectors are the composition's, over scripted dialers, and the lines are written by
/// <see cref="VerboseTransferEventWriter" /> as standard error gets them.
/// </summary>
[TestClass]
public sealed class CurlCompositionDohSubTransferTraceTests
{
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
            .. SubTransferLines(1, "1 to go", 3),
            .. SubTransferLines(2, "0 to go", 3),
            "* [DNS] DoH: Too small type A for example.test",
            "* [DNS] DoH: Too small type AAAA for example.test",
            "* Could not resolve host: example.test",
        ];
        Assert.AreEqual(CurlExitCode.CouldntResolveHost, result.ExitCode);
        Assert.AreEqual(string.Join('\n', expected), string.Join('\n', StandardErrorLines().Take(expected.Length)));
    }

    [TestMethod]
    public async Task Connect_UnderTraceConfigDns_WhenTheDohAnswerResolves_WritesEachSubTransfersPrefixedLinesBeforeTheEntry()
    {
        ConnectResult result = await ConnectAsync([AAnswer, AAnswer]);

        string[] expected =
        [
            .. SubTransferLines(1, "1 to go", 46),
            .. SubTransferLines(2, "0 to go", 46),
            "* [DNS] DoH: Unexpected TYPE type AAAA for example.test",
            "* [DNS] hostname: example.test",
            "* [DoH] TTL: 60 seconds",
            "* [DoH] A: 127.0.0.1",
            "* [DNS] resolve complete for example.test:47113",
            "* Host example.test:47113 was resolved.",
        ];
        Assert.IsNotNull(result.Connection);
        Assert.AreEqual(string.Join('\n', expected), string.Join('\n', StandardErrorLines().Skip(3).Take(expected.Length)));
    }

    [TestMethod]
    public async Task Connect_WithVerboseAlone_WritesNoSubTransferLine()
    {
        await ConnectAsync([AAnswer, AAnswer], "-v");

        Assert.IsFalse(StandardErrorLines().Any(line => line.Contains("DoH request is completed", StringComparison.Ordinal) || line.StartsWith("> POST", StringComparison.Ordinal)));
    }

    // One sub-transfer's lines as curl writes them, the poll-timing ones left out; the POST's
    // head is the measured one. The second finds the DoH server's address in the DNS cache, as
    // curl's does.
    private static string[] SubTransferLines(int connectionNumber, string toGo, int answerLength)
    {
        return
        [
            "* [DNS] created DNS filter for 127.0.0.1:47112, transport=3, queries=3",
            "* [DNS] [DNS] added",
            "* [DNS] [DNS] cf_dns_start host 127.0.0.1:47112",
            .. connectionNumber > 1 ? ["* [DNS] Hostname 127.0.0.1 was found in DNS cache"] : Array.Empty<string>(),
            "* [DNS]   Trying 127.0.0.1:47112...",
            "* [DNS] [DNS] Curl_conn_connect(block=0) -> 0, done=0",
            "* [DNS] [DNS] connected filter chain below",
            "* [DNS] [DNS] Curl_conn_connect(block=0) -> 0, done=1",
            "* [DNS] Established connection to 127.0.0.1 (127.0.0.1 port 47112) from 127.0.0.1 port 50000 ",
            "* [DNS] [DNS] removing connected setup filter",
            "* [DNS] [DNS] destroy",
            "* [DNS] using HTTP/1.x",
            "> POST /dns-query HTTP/1.1",
            "> Host: 127.0.0.1:47112",
            "> Accept: */*",
            "> Content-Type: application/dns-message",
            "> Content-Length: 30",
            "> ",
            "} [30 bytes data]",
            "* [DNS] upload completely sent off: 30 bytes",
            "< HTTP/1.1 200 OK",
            "< Content-Type: application/dns-message",
            $"< Content-Length: {answerLength}",
            "< ",
            $"{{ [{answerLength} bytes data]",
            $"* [DNS] Connection #{connectionNumber} to host 127.0.0.1:47112 left intact",
            $"* [DNS] a DoH request is completed, {toGo}",
        ];
    }

    private List<string> StandardErrorLines() =>
        [.. Encoding.Latin1.GetString(standardError.ToArray()).Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => line.TrimEnd('\r'))];

    /// <summary>
    /// Connects to <c>example.test:47113</c> through the composition's TCP connector over the
    /// composition's DoH resolver, whose own connector is <see cref="CurlComposition.CreateDohConnector" />
    /// over a dialer that answers each DoH connection with the next of <paramref name="dohAnswers" />.
    /// </summary>
    private async Task<ConnectResult> ConnectAsync(byte[][] dohAnswers, params string[] arguments)
    {
        string[] given = arguments.Length == 0 ? ["-v", "--trace-config", "dns"] : arguments;
        CommandLineParseResult parsed = CommandLineParser.Parse([.. given, "--doh-url", DohUrl, "http://example.test:47113/"], _ => true);
        Assert.IsTrue(parsed.IsAccepted);
        CommandLineOptions options = parsed.Options;
        FlowScopedTransferEvents? resolverEvents = CurlComposition.TracesDns(options) ? new() : null;
        TcpConnector dohConnector = CurlComposition.CreateDohConnector(options, new ScriptedTcpDialer(new ScriptedConnector(dohAnswers)), TimeProvider.System);
        TcpConnector connector = CurlComposition.CreateTcpConnector(
            options,
            CurlComposition.CreateDohResolver(DohUrl, dohConnector, AddressFamily.Unspecified, resolverEvents),
            new ScriptedTcpDialer(new ScriptedConnector([])),
            new PassThroughTlsProvider(),
            TimeProvider.System,
            HttpProxyTunnelOptions.Default,
            resolverEvents: resolverEvents);
        VerboseTransferEventWriter events = new(standardError, writesDataLines: true, TlsBackend.Schannel);

        return await connector.ConnectAsync(new ConnectTarget("example.test", 47113, false) { Events = events }, CancellationToken.None);
    }
}
