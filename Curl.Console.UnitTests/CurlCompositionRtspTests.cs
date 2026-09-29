using System.Text;
using Curl.Cli;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins <c>rtsp://</c> end to end through the production composition over a fake connector:
/// the handler <see cref="CurlComposition.CreateProtocolHandlers" /> registers, the request bytes,
/// standard output, the <c>-v</c> lines and the exit code, and the <c>-V</c> protocol list.
/// Recorded from curl 8.21.0 (mingw, Schannel) with <c>Record-CurlExchange.ps1</c> (ADR-0169's
/// table, BL-593 Notes). The connector reports no <c>Trying</c> or <c>Established connection</c>
/// lines, which are <c>TcpConnector</c>'s, so those are left out of the expected output.
/// </summary>
[TestClass]
public sealed class CurlCompositionRtspTests
{
    private const string Request = "OPTIONS * RTSP/1.0\r\nCSeq: 1\r\nUser-Agent: curl/8.21.0\r\n\r\n";

    private const string Ok = "RTSP/1.0 200 OK\r\nCSeq: 1\r\nPublic: OPTIONS, DESCRIBE\r\n\r\n";

    private static readonly Encoding Latin1 = Encoding.Latin1;

    private static readonly string[] RequestLines =
    [
        "> OPTIONS * RTSP/1.0",
        "> CSeq: 1",
        "> User-Agent: curl/8.21.0",
        "> ",
        "* Request completely sent off",
    ];

    [TestMethod]
    public async Task CreateRunner_VerboseOptions_SendsOptionsStarAndWritesTheMeasuredLines()
    {
        ScriptedConnector connector = new([Latin1.GetBytes(Ok)]);

        (int exitCode, string standardOutput, string standardError) = await RunAsync(connector, ["-sv", "rtsp://127.0.0.1:47950/media"]);

        Assert.AreEqual(Request, Latin1.GetString(connector.Written));
        Assert.AreEqual(("127.0.0.1", 47950, false), (connector.Targets.Single().Host, connector.Targets.Single().Port, connector.Targets.Single().UseTls));
        Assert.AreEqual(string.Empty, standardOutput);
        Assert.AreEqual(
            Lines(
                [.. RequestLines,
                "< RTSP/1.0 200 OK",
                "< CSeq: 1",
                "< Public: OPTIONS, DESCRIBE",
                "< ",
                "* Connection #0 to host 127.0.0.1:47950 left intact"]),
            standardError);
        Assert.AreEqual(0, exitCode);
    }

    [TestMethod]
    public async Task CreateRunner_VerboseDescribeWithHeaderAndData_StillSendsOptionsStarWithTheHeader()
    {
        ScriptedConnector connector = new([Latin1.GetBytes("RTSP/1.0 200 OK\r\nCSeq: 1\r\n\r\n")]);

        (int exitCode, string standardOutput, string standardError) = await RunAsync(
            connector, ["-v", "-s", "-X", "DESCRIBE", "-H", "Accept: application/sdp", "-d", "abc", "rtsp://127.0.0.1:47960/media"]);

        Assert.AreEqual("OPTIONS * RTSP/1.0\r\nCSeq: 1\r\nUser-Agent: curl/8.21.0\r\nAccept: application/sdp\r\n\r\n", Latin1.GetString(connector.Written));
        Assert.AreEqual(string.Empty, standardOutput);
        Assert.AreEqual(
            Lines(
                "> OPTIONS * RTSP/1.0",
                "> CSeq: 1",
                "> User-Agent: curl/8.21.0",
                "> Accept: application/sdp",
                "> ",
                "* Request completely sent off",
                "< RTSP/1.0 200 OK",
                "< CSeq: 1",
                "< ",
                "* Connection #0 to host 127.0.0.1:47960 left intact"),
            standardError);
        Assert.AreEqual(0, exitCode);
    }

    [TestMethod]
    public async Task CreateRunner_VerboseIncludeWithBody_WritesTheHeadOnlyAndShutsTheConnectionDown()
    {
        ScriptedConnector connector = new([Latin1.GetBytes("RTSP/1.0 200 OK\r\nCSeq: 1\r\nContent-Length: 2\r\n\r\nok")]);

        (int exitCode, string standardOutput, string standardError) = await RunAsync(connector, ["-v", "-s", "-i", "rtsp://127.0.0.1:47973/media"]);

        Assert.AreEqual("RTSP/1.0 200 OK\r\nCSeq: 1\r\nContent-Length: 2\r\n\r\n", standardOutput);
        Assert.AreEqual(
            Lines(
                [.. RequestLines,
                "< RTSP/1.0 200 OK",
                "< CSeq: 1",
                "< Content-Length: 2",
                "< ",
                "{ [2 bytes data]",
                "* shutting down connection #0"]),
            standardError);
        Assert.AreEqual(0, exitCode);
    }

    [TestMethod]
    public async Task CreateRunner_VerboseFailOn404_WritesTheRefusalBeforeTheBlankLineAndExits22()
    {
        ScriptedConnector connector = new([Latin1.GetBytes("RTSP/1.0 404 Not Found\r\nCSeq: 1\r\n\r\n")]);

        (int exitCode, string standardOutput, string standardError) = await RunAsync(connector, ["-v", "-sSf", "rtsp://127.0.0.1:47976/media"]);

        Assert.AreEqual(string.Empty, standardOutput);
        Assert.AreEqual(
            Lines(
                [.. RequestLines,
                "< RTSP/1.0 404 Not Found",
                "< CSeq: 1",
                "* The requested URL returned error: 404",
                "< ",
                "* closing connection #0",
                "curl: (22) The requested URL returned error: 404"]),
            standardError);
        Assert.AreEqual(22, exitCode);
    }

    [TestMethod]
    public async Task CreateRunner_VerboseCSeqMismatch_WritesTheMessageLeavesTheConnectionIntactAndExits85()
    {
        ScriptedConnector connector = new([Latin1.GetBytes("RTSP/1.0 200 OK\r\nCSeq: 7\r\n\r\n")]);

        (int exitCode, string standardOutput, string standardError) = await RunAsync(connector, ["-v", "-s", "rtsp://127.0.0.1:47971/media"]);

        Assert.AreEqual(string.Empty, standardOutput);
        Assert.AreEqual(
            Lines(
                [.. RequestLines,
                "< RTSP/1.0 200 OK",
                "< CSeq: 7",
                "< ",
                "* The CSeq of this request 1 did not match the response 7",
                "* Connection #0 to host 127.0.0.1:47971 left intact"]),
            standardError);
        Assert.AreEqual(85, exitCode);
    }

    [TestMethod]
    public async Task CreateRunner_VerboseHttpReply_WritesEmptyReplyShutsDownAndExits52()
    {
        ScriptedConnector connector = new([Latin1.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n")]);

        (int exitCode, string standardOutput, string standardError) = await RunAsync(connector, ["-v", "-sS", "rtsp://127.0.0.1:47972/media"]);

        Assert.AreEqual(string.Empty, standardOutput);
        Assert.AreEqual(
            Lines(
                [.. RequestLines,
                "* Empty reply from server",
                "* shutting down connection #0",
                "curl: (52) Empty reply from server"]),
            standardError);
        Assert.AreEqual(52, exitCode);
    }

    [TestMethod]
    public async Task CreateRunner_TwoUrlsOnOnePooledConnection_SendsCSeq0WithoutTheSessionOnTheReusedOne()
    {
        ScriptedConnector connector = new(
        [
            Latin1.GetBytes("RTSP/1.0 200 OK\r\nCSeq: 1\r\nSession: 1234;timeout=60\r\n\r\n"),
            Latin1.GetBytes("RTSP/1.0 200 OK\r\nCSeq: 0\r\n\r\n"),
        ]);
        await using PoolingConnector pool = new(connector, TimeProvider.System);

        (int exitCode, _, string standardError) = await RunAsync(pool, ["-v", "-s", "rtsp://127.0.0.1:47979/a", "rtsp://127.0.0.1:47979/b"]);

        Assert.AreEqual(Request + "OPTIONS * RTSP/1.0\r\nCSeq: 0\r\nUser-Agent: curl/8.21.0\r\n\r\n", Latin1.GetString(connector.Written));
        Assert.HasCount(1, connector.Targets);
        StringAssert.Contains(
            standardError,
            Lines("* Connection #0 to host 127.0.0.1:47979 left intact", "* Reusing existing rtsp: connection with host 127.0.0.1", "> OPTIONS * RTSP/1.0", "> CSeq: 0"));
        Assert.AreEqual(0, exitCode);
    }

    [TestMethod]
    public async Task CreateRunner_Version_ListsRtspBetweenPop3sAndSmtp()
    {
        (int exitCode, string standardOutput, _) = await RunAsync(new ScriptedConnector([]), ["-V"]);

        string protocols = standardOutput.Split(Environment.NewLine).Single(line => line.StartsWith("Protocols:", StringComparison.Ordinal));
        Assert.AreEqual(CurlVersionText.ProtocolsLine, protocols);
        StringAssert.Contains(protocols, " pop3 pop3s rtsp smtp smtps ");
        Assert.AreEqual(0, exitCode);
    }

    /// <summary>
    /// The lines as <c>-v</c> writes them: a header line (<c>&gt; </c> or <c>&lt; </c>) keeps the
    /// CR of its CR LF before the line ending, which on Windows makes it <c>\r\r\n</c>, as
    /// measured.
    /// </summary>
    private static string Lines(params string[] lines) =>
        string.Concat(lines.Select(line => line + (line[0] is '>' or '<' ? "\r" : string.Empty) + Environment.NewLine));

    private static async Task<(int ExitCode, string StandardOutput, string StandardError)> RunAsync(IConnector connector, string[] arguments)
    {
        using MemoryStream standardOutput = new();
        using MemoryStream standardError = new();

        int exitCode = await CurlComposition
            .CreateRunner(standardOutput, standardError, new MemoryStream(), connector, new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"))
            .RunAsync(arguments);

        return (exitCode, Latin1.GetString(standardOutput.ToArray()), Encoding.UTF8.GetString(standardError.ToArray()));
    }
}
