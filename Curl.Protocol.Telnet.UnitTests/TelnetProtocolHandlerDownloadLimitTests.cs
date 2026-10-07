using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Telnet.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Telnet;

/// <summary>
/// Pins how a <c>telnet://</c> session meets <c>-I</c> and <c>--max-filesize</c>, as curl
/// 8.21.0's download writer (<c>cw_download_write</c>, <c>lib/sendf.c</c>) meets them,
/// measured on 2026-10-02 against a loopback server that sent <c>hello</c> and closed
/// (BL-1306): under <c>-I</c> the first data ends the session with exit 8 and writes
/// nothing; past the limit the allowed bytes are written and it ends with exit 63.
/// </summary>
[TestClass]
public sealed class TelnetProtocolHandlerDownloadLimitTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ExecuteAsync_NoBodyAndServerSendsData_EndsWithWeirdServerReplyAndWritesNothing()
    {
        // Measured: -sv -I gave exit 8, empty stdout, "{ [5 bytes data]" then "* shutting down connection #0".
        Session session = await RunAsync(noBody: true, maxFileSize: null, Latin1("hello"));

        Diagnostics.AssertExitCode(CurlExitCode.WeirdServerReply, session.Result.ExitCode);
        Diagnostics.Diff("output", string.Empty, session.Output);
        Diagnostics.AssertLines("transcript", ["<= hello", "* shutting down connection #0"], session.Transcript);
        Assert.AreEqual(CurlExitCode.WeirdServerReply, session.Result.ExitCode);
        Assert.AreEqual("Weird server reply", session.Result.ErrorMessage);
        Assert.AreEqual(string.Empty, session.Output);
        CollectionAssert.AreEqual(new[] { "<= hello", "* shutting down connection #0" }, session.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoBodyAndOnlyNegotiation_EndsWithOk()
    {
        Session session = await RunAsync(noBody: true, maxFileSize: null, Hex("FF FB 01"));

        Diagnostics.AssertExitCode(CurlExitCode.Ok, session.Result.ExitCode);
        Diagnostics.Diff("output", string.Empty, session.Output);
        Assert.AreEqual(CurlExitCode.Ok, session.Result.ExitCode);
        Assert.AreEqual(string.Empty, session.Output);
    }

    [TestMethod]
    public async Task ExecuteAsync_MaxFileSizeBelowTheData_WritesTheAllowedBytesAndEndsWithFilesizeExceeded()
    {
        // Measured: -sv --max-filesize 3 gave exit 63, stdout "hel", and the message before shutting down.
        Session session = await RunAsync(noBody: false, maxFileSize: 3, Latin1("hello"));

        string[] expectedTranscript =
        [
            "<= hello",
            "* Exceeded the maximum allowed file size (3) with 3 bytes",
            "* shutting down connection #0",
        ];
        Diagnostics.AssertExitCode(CurlExitCode.FilesizeExceeded, session.Result.ExitCode);
        Diagnostics.Diff("error", "Exceeded the maximum allowed file size (3) with 3 bytes", session.Result.ErrorMessage ?? string.Empty);
        Diagnostics.Diff("output", "hel", session.Output);
        Diagnostics.AssertLines("transcript", expectedTranscript, session.Transcript);
        Assert.AreEqual(CurlExitCode.FilesizeExceeded, session.Result.ExitCode);
        Assert.AreEqual("Exceeded the maximum allowed file size (3) with 3 bytes", session.Result.ErrorMessage);
        Assert.AreEqual(3, session.Result.BytesTransferred);
        Assert.AreEqual("hel", session.Output);
        CollectionAssert.AreEqual(
            new[]
            {
                "<= hello",
                "* Exceeded the maximum allowed file size (3) with 3 bytes",
                "* shutting down connection #0",
            },
            session.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_MaxFileSizeReachedOnTheSecondRead_CountsAcrossWrites()
    {
        Session session = await RunAsync(noBody: false, maxFileSize: 4, Latin1("he"), Latin1("llo"));

        Diagnostics.AssertExitCode(CurlExitCode.FilesizeExceeded, session.Result.ExitCode);
        Diagnostics.Diff("error", "Exceeded the maximum allowed file size (4) with 4 bytes", session.Result.ErrorMessage ?? string.Empty);
        Diagnostics.Diff("output", "hell", session.Output);
        Assert.AreEqual(CurlExitCode.FilesizeExceeded, session.Result.ExitCode);
        Assert.AreEqual("Exceeded the maximum allowed file size (4) with 4 bytes", session.Result.ErrorMessage);
        Assert.AreEqual("hell", session.Output);
    }

    [TestMethod]
    [DataRow(0L)]
    [DataRow(5L)]
    [DataRow(null)]
    public async Task ExecuteAsync_MaxFileSizeZeroUnsetOrExactlyTheData_WritesEverythingAndEndsWithOk(long? maxFileSize)
    {
        Session session = await RunAsync(noBody: false, maxFileSize, Latin1("hello"));

        Diagnostics.AssertExitCode(CurlExitCode.Ok, session.Result.ExitCode);
        Diagnostics.Diff("output", "hello", session.Output);
        Assert.AreEqual(CurlExitCode.Ok, session.Result.ExitCode);
        Assert.AreEqual("hello", session.Output);
    }

    [TestMethod]
    public async Task ExecuteAsync_NegotiationAmongTheData_IsNotCountedAgainstTheLimit()
    {
        Session session = await RunAsync(noBody: false, maxFileSize: 5, Hex("68 65 FF FD 01 FF FB 03 6C 6C 6F"));

        Diagnostics.AssertExitCode(CurlExitCode.Ok, session.Result.ExitCode);
        Diagnostics.Diff("output", "hello", session.Output);
        Assert.AreEqual(CurlExitCode.Ok, session.Result.ExitCode);
        Assert.AreEqual("hello", session.Output);
    }

    [TestMethod]
    public async Task ExecuteAsync_OutputRefusesTheAllowedBytes_EndsWithWriteError()
    {
        ScriptedConnection connection = new(new ScriptedRead(Latin1("hello")));
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("telnet://h/"),
            Output = new FaultingOutputStream(),
            Upload = new MemoryStream(),
            MaxFileSize = 3,
        };
        Diagnostics.ArrangeContext(context);
        Diagnostics.ArrangeReads([Latin1("hello")]);
        Diagnostics.Arrange("output", "refuses every write");

        TransferResult result = await new TelnetProtocolHandler(Connector(connection)).ExecuteAsync(context);

        Diagnostics.ActResult(result);
        Diagnostics.AssertExitCode(CurlExitCode.WriteError, result.ExitCode);
        Assert.AreEqual(CurlExitCode.WriteError, result.ExitCode);
    }

    private static RecordingConnector Connector(IConnection connection) =>
        new(ConnectResult.Connected(connection, null, connectionNumber: 0));

    private async Task<Session> RunAsync(bool noBody, long? maxFileSize, params byte[][] reads)
    {
        ScriptedConnection connection = new([.. reads.Select(read => new ScriptedRead(read))]);
        TranscriptTransferEvents events = new();
        using MemoryStream output = new();
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("telnet://h/"),
            Output = output,
            Upload = new MemoryStream(),
            Events = events,
            NoBody = noBody,
            MaxFileSize = maxFileSize,
        };
        Diagnostics.ArrangeContext(context);
        Diagnostics.ArrangeReads(reads);

        TransferResult result = await new TelnetProtocolHandler(Connector(connection)).ExecuteAsync(context);

        Diagnostics.ActResult(result);
        Diagnostics.ActSent(connection.Sent);
        Diagnostics.ActOutput(output.ToArray());
        Diagnostics.ActLines("transcript", events.Transcript);
        return new Session(result, Encoding.Latin1.GetString(output.ToArray()), events.Transcript);
    }

    private static byte[] Hex(string hex) => Convert.FromHexString(hex.Replace(" ", string.Empty, StringComparison.Ordinal));

    private static byte[] Latin1(string text) => Encoding.Latin1.GetBytes(text);

    private sealed record Session(TransferResult Result, string Output, List<string> Transcript);
}
