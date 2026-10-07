using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Rtsp.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Rtsp;

/// <summary>
/// Pins how an <c>rtsp://</c> transfer treats reply header lines with no colon, a carriage
/// return inside them or continuation lines under them, and <c>Content-Length</c> lists,
/// against curl 8.21.0 (Git for Windows mingw64), measured on 2026-09-29 with
/// <c>-sS -i rtsp://127.0.0.1:&lt;port&gt;/media</c> (BL-840 Notes).
/// </summary>
[TestClass]
public sealed class RtspReplyHeaderLineTests
{
    /// <summary>Gets or sets the running test's context, which carries its diagnostics.</summary>
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("RTSP/1.0 200 OK\r\nCSeq: 1\r\nX-NoColon\r\n\r\n", "RTSP/1.0 200 OK\r\nCSeq: 1\r\n")]
    [DataRow("RTSP/1.0 200 OK\r\nX-NoColon\r\nCSeq: 1\r\n\r\n", "RTSP/1.0 200 OK\r\n")]
    [DataRow("RTSP/1.0 200 OK\r\nCSeq: 1\r\nX A\r\n\r\n", "RTSP/1.0 200 OK\r\nCSeq: 1\r\n")]
    [DataRow("RTSP/1.0 200 OK\r\n cont\r\nCSeq: 1\r\n\r\n", "RTSP/1.0 200 OK\r\n")]
    public async Task ExecuteAsync_HeaderWithoutColon_WritesTheLinesBeforeItAndFailsWith8(string reply, string written)
    {
        (TransferResult result, string headers, _) = await RunAsync(reply);

        Diagnostics.AssertExitCode(CurlExitCode.WeirdServerReply, result);
        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        Assert.AreEqual("Header without colon", result.ErrorMessage);
        Assert.AreEqual(written, headers);
    }

    [TestMethod]
    [DataRow("RTSP/1.0 200 OK\r\nCSeq: 1\r\nX-A: b\rc\r\n\r\n", "RTSP/1.0 200 OK\r\nCSeq: 1\r\n")]
    [DataRow("RTSP/1.0 200 OK\r\nX-A: b\rc\r\nCSeq: 1\r\n\r\n", "RTSP/1.0 200 OK\r\n")]
    [DataRow("RTSP/1.0 200 OK\r\nCSeq: 1\r\nX-A: b\r\r\n\r\n", "RTSP/1.0 200 OK\r\nCSeq: 1\r\n")]
    [DataRow("RTSP/1.0 200 OK\r\nCSeq: 1\r\nX-A b\rc\r\n\r\n", "RTSP/1.0 200 OK\r\nCSeq: 1\r\n")]
    [DataRow("RTSP/1.0 200 OK\r\nCSeq: 1\r\nX-A: 1\r\n c\rd\r\n\r\n", "RTSP/1.0 200 OK\r\nCSeq: 1\r\n")]
    [DataRow("RTSP/1.0 200 O\rK\r\nCSeq: 1\r\n\r\n", "")]
    public async Task ExecuteAsync_CarriageReturnInsideALine_WritesTheLinesBeforeItAndFailsWith8(string reply, string written)
    {
        (TransferResult result, string headers, _) = await RunAsync(reply);

        Diagnostics.AssertExitCode(CurlExitCode.WeirdServerReply, result);
        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        Assert.AreEqual("Carriage return found in header", result.ErrorMessage);
        Assert.AreEqual(written, headers);
    }

    [TestMethod]
    public async Task ExecuteAsync_CarriageReturnInAWeirdStatusLine_FailsAsWeird()
    {
        (TransferResult result, string headers, _) = await RunAsync("RTSP/1.0 abc\rx\r\nCSeq: 1\r\n\r\n");

        Diagnostics.Assert("error", "Weird server reply", result.ErrorMessage);
        Assert.AreEqual("Weird server reply", result.ErrorMessage);
        Assert.AreEqual(string.Empty, headers);
    }

    [TestMethod]
    public async Task ExecuteAsync_NulByteInAHeaderLine_ReportsItAfterTheLinesBeforeAndFailsWith8()
    {
        (TransferResult result, string headers, List<string> transcript) =
            await RunAsync("RTSP/1.0 200 OK\r\nCSeq: 1\r\nX-A: a\0b\r\nContent-Length: 0\r\n\r\n");

        Diagnostics.AssertExitCode(CurlExitCode.WeirdServerReply, result);
        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        Assert.AreEqual("Nul byte in header", result.ErrorMessage);
        Assert.AreEqual("RTSP/1.0 200 OK\r\nCSeq: 1\r\n", headers);
        CollectionAssert.AreEqual(
            new[] { "< RTSP/1.0 200 OK\r\n", "< CSeq: 1\r\n", "* Nul byte in header", "* closing connection #0" },
            transcript[^4..]);
    }

    [TestMethod]
    [DataRow("RTSP/1.0 200 OK\r\nCSeq: 1\r\nX-A: a\0\rb\r\n\r\n", "RTSP/1.0 200 OK\r\nCSeq: 1\r\n")]
    [DataRow("RTSP/1.0 200 OK\r\nCSeq: 1\r\nX-A a\0b\r\n\r\n", "RTSP/1.0 200 OK\r\nCSeq: 1\r\n")]
    [DataRow("RTSP/1.0 200 O\0K\r\nCSeq: 1\r\n\r\n", "")]
    [DataRow("RTSP/1.0 200 O\0\rK\r\nCSeq: 1\r\n\r\n", "")]
    [DataRow("RTSP/1.0 200 O\rK\0\r\nCSeq: 1\r\n\r\n", "")]
    public async Task ExecuteAsync_NulByteBesideAnotherFault_IsReportedFirst(string reply, string written)
    {
        (TransferResult result, string headers, _) = await RunAsync(reply);

        Diagnostics.AssertExitCode(CurlExitCode.WeirdServerReply, result);
        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        Assert.AreEqual("Nul byte in header", result.ErrorMessage);
        Assert.AreEqual(written, headers);
    }

    [TestMethod]
    public async Task ExecuteAsync_NulByteBeforeTheStatusLinesBlank_FailsAsWeird()
    {
        (TransferResult result, string headers, _) = await RunAsync("RTSP/1.0\0 200 OK\r\nCSeq: 1\r\n\r\n");

        Diagnostics.AssertExitCode(CurlExitCode.WeirdServerReply, result);
        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        Assert.AreEqual("Weird server reply", result.ErrorMessage);
        Assert.AreEqual(string.Empty, headers);
    }

    [TestMethod]
    [DataRow("Location: /x\r\nLocation: /y\r\n", "Location: /x\r\n")]
    [DataRow("Location:\r\nLocation: /y\r\nlocation: /z\r\n", "Location:\r\nLocation: /y\r\n")]
    public async Task ExecuteAsync_SecondDifferentLocation_ReportsItAfterTheLinesBeforeAndFailsWith8(string lines, string writtenAfterCSeq)
    {
        (TransferResult result, string headers, List<string> transcript) =
            await RunAsync("RTSP/1.0 302 Found\r\nCSeq: 1\r\n" + lines + "Content-Length: 0\r\n\r\n");

        Diagnostics.AssertExitCode(CurlExitCode.WeirdServerReply, result);
        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        Assert.AreEqual("Multiple Location headers", result.ErrorMessage);
        Assert.AreEqual("RTSP/1.0 302 Found\r\nCSeq: 1\r\n" + writtenAfterCSeq, headers);
        CollectionAssert.AreEqual(
            new[] { "< " + writtenAfterCSeq.Split("\r\n")[^2] + "\r\n", "* Multiple Location headers", "* closing connection #0" },
            transcript[^3..]);
    }

    [TestMethod]
    [DataRow("Location: /x\r\nLocation: /x\r\n")]
    [DataRow("Location: /x\r\nLocation:  /x \r\n")]
    [DataRow("Location: /x\r\nLocation:\r\n")]
    public async Task ExecuteAsync_RepeatedEqualLocation_IsAccepted(string lines)
    {
        (TransferResult result, string headers, _) = await RunAsync("RTSP/1.0 302 Found\r\nCSeq: 1\r\n" + lines + "Content-Length: 0\r\n\r\n");

        Diagnostics.AssertExitCode(CurlExitCode.Ok, result);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("RTSP/1.0 302 Found\r\nCSeq: 1\r\n" + lines + "Content-Length: 0\r\n\r\n", headers);
    }

    [TestMethod]
    [DataRow("X-A: 1\r\n  cont\r\n", "X-A: 1 cont\r\n")]
    [DataRow("X-A: 1  \r\n\t \tcont  \r\n", "X-A: 1 cont  \r\n")]
    [DataRow("X-A: 1\r\n a\r\n b\r\nX-B: 2\r\n", "X-A: 1 a b\r\nX-B: 2\r\n")]
    [DataRow("X-A: 1\r\n   \r\n", "X-A: 1 \r\n")]
    [DataRow("X-A: 1\n cont\r\n", "X-A: 1 cont\r\n")]
    [DataRow("Session:\r\n abc\r\n", "Session: abc\r\n")]
    [DataRow("Content-Length:\r\n 2\r\n", "Content-Length: 2\r\n")]
    [DataRow("Content-Length: 2\r\n , 2\r\n", "Content-Length: 2 , 2\r\n")]
    public async Task ExecuteAsync_ContinuationLines_AreJoinedToTheLineBefore(string lines, string joined)
    {
        (TransferResult result, string headers, _) = await RunAsync("RTSP/1.0 200 OK\r\nCSeq: 1\r\n" + lines + "\r\nab");

        Diagnostics.AssertExitCode(CurlExitCode.Ok, result);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("RTSP/1.0 200 OK\r\nCSeq: 1\r\n" + joined + "\r\n", headers);
    }

    [TestMethod]
    public async Task ExecuteAsync_FoldedCSeq_IsRead()
    {
        (TransferResult result, string headers, _) = await RunAsync("RTSP/1.0 200 OK\r\nCSeq:\r\n 1\r\n\r\n");

        Diagnostics.AssertExitCode(CurlExitCode.Ok, result);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("RTSP/1.0 200 OK\r\nCSeq: 1\r\n\r\n", headers);
    }

    [TestMethod]
    public async Task ExecuteAsync_LineFeedOnlyFoldedHead_KeepsItsLineEndings()
    {
        (TransferResult result, string headers, _) = await RunAsync("RTSP/1.0 200 OK\nCSeq: 1\nX-A: 1\n cont\n\n");

        Diagnostics.AssertExitCode(CurlExitCode.Ok, result);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("RTSP/1.0 200 OK\nCSeq: 1\nX-A: 1 cont\n\n", headers);
    }

    [TestMethod]
    public async Task ExecuteAsync_FoldedLine_IsReportedJoined()
    {
        (_, _, List<string> transcript) = await RunAsync("RTSP/1.0 200 OK\r\nCSeq: 1\r\nX-A: 1\r\n cont\r\n\r\n");

        Diagnostics.Assert("transcript holds the joined line", true, transcript.Contains("< X-A: 1 cont\r\n"));
        CollectionAssert.Contains(transcript, "< X-A: 1 cont\r\n");
    }

    [TestMethod]
    public async Task ExecuteAsync_ContinuationArrivingInPieces_IsJoinedOnceWhole()
    {
        var server = new ScriptedConnection(Bytes("RTSP/1.0 200 OK\r\nCSeq: 1\r\nX-A: 1\r\n"), Bytes(" co"), Bytes("nt\r\n"), Bytes("\r\n"));
        Diagnostics.ArrangeReads([Bytes("RTSP/1.0 200 OK\r\nCSeq: 1\r\nX-A: 1\r\n"), Bytes(" co"), Bytes("nt\r\n"), Bytes("\r\n")]);

        (TransferResult result, string headers, _) = await RunAsync(server);

        Diagnostics.AssertExitCode(CurlExitCode.Ok, result);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("RTSP/1.0 200 OK\r\nCSeq: 1\r\nX-A: 1 cont\r\n\r\n", headers);
    }

    [TestMethod]
    [DataRow("RTSP/1.0 200 OK\r\nCSeq: 1\r\nX-A: 1\r\n cont\r\n", "RTSP/1.0 200 OK\r\nCSeq: 1\r\nX-A: 1 cont\r\n")]
    [DataRow("RTSP/1.0 200 OK\r\nCSeq: 1\r\nX-A: 1\r\n cont\r\nPubl", "RTSP/1.0 200 OK\r\nCSeq: 1\r\nX-A: 1 cont\r\nPubl")]
    [DataRow("RTSP/1.0 200 OK\r\nCSeq: 1\r\nX-A: 1\r\n co", "RTSP/1.0 200 OK\r\nCSeq: 1\r\nX-A: 1\r\n co")]
    public async Task ExecuteAsync_HeadClosedAfterAFoldedLine_WritesItJoined(string reply, string written)
    {
        (TransferResult result, string headers, _) = await RunAsync(reply);

        Diagnostics.AssertExitCode(CurlExitCode.Ok, result);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(written, headers);
    }

    [TestMethod]
    [DataRow("Content-Length: 2, 2\r\n")]
    [DataRow("Content-Length: 2 ,2 , 2\r\n")]
    [DataRow("Content-Length: 02, 2\r\n")]
    [DataRow("Content-Length: 2\r\nContent-Length: 2\r\n")]
    [DataRow("Content-Length: 2\r\n , 2\r\n")]
    public async Task ExecuteAsync_ContentLengthListOfEqualNumbers_IsAcceptedAndReadsTheBody(string lines)
    {
        (TransferResult result, string headers, List<string> transcript) = await RunAsync("RTSP/1.0 200 OK\r\nCSeq: 1\r\n" + lines + "\r\nabc");

        Diagnostics.AssertExitCode(CurlExitCode.Ok, result);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.StartsWith("RTSP/1.0 200 OK\r\nCSeq: 1\r\nContent-Length: ", headers);
        CollectionAssert.Contains(transcript, "{ 2");
    }

    [TestMethod]
    [DataRow("Content-Length: 2, 3\r\n", "")]
    [DataRow("Content-Length: 2,,2\r\n", "")]
    [DataRow("Content-Length: 2,\r\n", "")]
    [DataRow("Content-Length: ,2\r\n", "")]
    [DataRow("Content-Length: 2\r\nContent-Length: 3\r\n", "Content-Length: 2\r\n")]
    [DataRow("Content-Length: 3\r\nContent-Length: 2\r\n", "Content-Length: 3\r\n")]
    [DataRow("Content-Length: 2, 2\r\nContent-Length: 3\r\n", "Content-Length: 2, 2\r\n")]
    public async Task ExecuteAsync_ContentLengthNumbersThatDisagree_FailWith8(string lines, string writtenAfterCSeq)
    {
        (TransferResult result, string headers, _) = await RunAsync("RTSP/1.0 200 OK\r\nCSeq: 1\r\n" + lines + "\r\nabc");

        Diagnostics.AssertExitCode(CurlExitCode.WeirdServerReply, result);
        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        Assert.AreEqual("Invalid Content-Length: value", result.ErrorMessage);
        Assert.AreEqual("RTSP/1.0 200 OK\r\nCSeq: 1\r\n" + writtenAfterCSeq, headers);
    }

    [TestMethod]
    [DataRow("Content-Length: 99999999999999999999\r\n")]
    [DataRow("Content-Length: 2\r\nContent-Length: 99999999999999999999\r\n")]
    [DataRow("Content-Length: 99999999999999999999\r\nContent-Length: 3\r\n")]
    [DataRow("Content-Length: 2, 99999999999999999999, 3\r\n")]
    public async Task ExecuteAsync_ContentLengthTooLarge_IsAcceptedWithNoBody(string lines)
    {
        (TransferResult result, string headers, List<string> transcript) = await RunAsync("RTSP/1.0 200 OK\r\nCSeq: 1\r\n" + lines + "\r\nab");

        Diagnostics.AssertExitCode(CurlExitCode.Ok, result);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("RTSP/1.0 200 OK\r\nCSeq: 1\r\n" + lines + "\r\n", headers);
        Assert.IsFalse(transcript.Exists(line => line.StartsWith('{')));
    }

    [TestMethod]
    public async Task ExecuteAsync_ContentLengthTooLarge_ReportsOverflowBeforeTheLineAndWritesNothing()
    {
        var output = new MemoryStream();
        Diagnostics.ArrangeReply("RTSP/1.0 200 OK\r\nCSeq: 1\r\nContent-Length: 99999999999999999999\r\n\r\nhello");
        (TransferResult result, _, List<string> transcript) = await RunAsync(
            new ScriptedConnection(Bytes("RTSP/1.0 200 OK\r\nCSeq: 1\r\nContent-Length: 99999999999999999999\r\n\r\nhello")),
            output);

        Diagnostics.AssertExitCode(CurlExitCode.Ok, result);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(0, output.Length);
        CollectionAssert.AreEqual(
            new[]
            {
                "< RTSP/1.0 200 OK\r\n",
                "< CSeq: 1\r\n",
                "* Overflow Content-Length: value",
                "< Content-Length: 99999999999999999999\r\n",
                "< \r\n",
                "* shutting down connection #0",
            },
            transcript[^6..]);
    }

    [TestMethod]
    public async Task ExecuteAsync_TwoEqualTooLargeContentLengthNumbersOnOneLine_ReportOneOverflowAndExit0()
    {
        (TransferResult result, _, List<string> transcript) = await RunAsync(
            "RTSP/1.0 200 OK\r\nCSeq: 1\r\nContent-Length: 99999999999999999999, 99999999999999999999\r\n\r\nhello");

        Diagnostics.AssertExitCode(CurlExitCode.Ok, result);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "< CSeq: 1\r\n", "* Overflow Content-Length: value", "< Content-Length: 99999999999999999999, 99999999999999999999\r\n", "< \r\n" },
            transcript[^5..^1]);
        Assert.AreEqual(1, transcript.Count(line => line.StartsWith("* Overflow", StringComparison.Ordinal)));
    }

    private Task<(TransferResult Result, string Headers, List<string> Transcript)> RunAsync(string reply)
    {
        Diagnostics.ArrangeReply(reply);
        return RunAsync(new ScriptedConnection(Bytes(reply)));
    }

    private async Task<(TransferResult Result, string Headers, List<string> Transcript)> RunAsync(ScriptedConnection server, Stream? output = null)
    {
        var headers = new MemoryStream();
        var events = new RecordingTransferEvents();
        var context = new TransferContext
        {
            Url = CurlUrl.Parse("rtsp://127.0.0.1:47950/media"),
            Output = output ?? new MemoryStream(),
            HeaderOutput = headers,
            Events = events,
        };
        Diagnostics.ArrangeContext(context);
        TransferResult result = await new RtspProtocolHandler(new RecordingConnector(ConnectResult.Connected(server)), new RecordingAuthenticator())
            .ExecuteAsync(context);
        Diagnostics.ActResult(result);
        Diagnostics.Bytes("header output", headers.ToArray());
        Diagnostics.ActTranscript(events.Transcript);
        return (result, Encoding.Latin1.GetString(headers.ToArray()), events.Transcript);
    }

    private static byte[] Bytes(string text) => Encoding.Latin1.GetBytes(text);
}
