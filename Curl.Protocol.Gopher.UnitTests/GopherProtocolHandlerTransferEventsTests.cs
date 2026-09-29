using Curl.Protocol.Abstractions;
using Curl.Protocol.Gopher.Fakes;

namespace Curl.Protocol.Gopher;

/// <summary>
/// Pins what a <c>gopher://</c> or <c>gophers://</c> transfer reports to
/// <see cref="ITransferEvents" /> after connecting, against curl 8.21.0's <c>-v</c> and
/// <c>--trace-ascii</c> output measured on 2026-09-29 (BL-934 Notes): each read as data
/// received, the server's close as a zero-byte block, no block for the selector sent, and
/// the line that ends the connection.
/// </summary>
[TestClass]
public sealed class GopherProtocolHandlerTransferEventsTests
{
    /// <summary>The measured <c>gopher://host/1/</c> reply, which curl traced as one 51-byte block.</summary>
    private const string MeasuredMenu = "iHello\tfake\t(NULL)\t0\r\n0file\t/file\t127.0.0.1\t70\r\n.\r\n";

    private const string MissingCloseNotify = "schannel: server closed abruptly (missing close_notify)";

    [TestMethod]
    [DataRow("gopher://h/1/")]
    [DataRow("gopher://h/0/file")]
    public async Task ExecuteAsync_ReplyThenClose_ReportsTheReplyTheZeroByteCloseAndShuttingDown(string url)
    {
        // Measured: "<= Recv data, 51 bytes", "<= Recv data, 0 bytes (0x0)", "* shutting down connection #0",
        // and no "=> Send data" for the selector.
        ScriptedConnection connection = new(Latin1(MeasuredMenu));
        TranscriptTransferEvents events = new();

        await new GopherProtocolHandler(Connector(connection, 2)).ExecuteAsync(Context(url, events));

        CollectionAssert.AreEqual(
            new[] { "<= " + MeasuredMenu, "<= ", "* shutting down connection #2" },
            events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReplyInTwoReads_ReportsEachReadAsItsOwnBlock()
    {
        ScriptedConnection connection = new(Latin1("iHello"), Latin1("\r\n.\r\n"));
        TranscriptTransferEvents events = new();

        await new GopherProtocolHandler(Connector(connection, 0)).ExecuteAsync(Context("gopher://h/1/", events));

        CollectionAssert.AreEqual(
            new[] { "<= iHello", "<= \r\n.\r\n", "<= ", "* shutting down connection #0" },
            events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_SelectorDecodesToNul_ReportsOnlyShuttingDown()
    {
        // Measured: gopher://127.0.0.1:47939/1a%00b wrote "* shutting down connection #0" and exited 3.
        TranscriptTransferEvents events = new();

        await new GopherProtocolHandler(Connector(new ScriptedConnection(), 0)).ExecuteAsync(Context("gopher://h/1a%00b", events));

        CollectionAssert.AreEqual(new[] { "* shutting down connection #0" }, events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_GophersEndsWithoutCloseNotify_ReportsTheMessageThenClosingAndFailsWithIt()
    {
        // Measured: gophers://127.0.0.1:47936/1/ -k wrote "<= Recv data, 25 bytes", then
        // "* schannel: server closed abruptly (missing close_notify)", "* closing connection #0",
        // and "curl: (56) schannel: server closed abruptly (missing close_notify)".
        ScriptedConnection connection = new(Latin1("iHello\tfake\t(NULL)\t0\r\n.\r\n"), null)
        {
            ReadFailure = new MissingCloseNotifyException(MissingCloseNotify),
        };
        TranscriptTransferEvents events = new();

        TransferResult result = await new GopherProtocolHandler(Connector(connection, 0)).ExecuteAsync(Context("gophers://h/1/", events));

        Assert.AreEqual(new TransferResult(CurlExitCode.RecvError, 25, MissingCloseNotify), result);
        CollectionAssert.AreEqual(
            new[] { "<= iHello\tfake\t(NULL)\t0\r\n.\r\n", "* " + MissingCloseNotify, "* closing connection #0" },
            events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_OutputRefusesTheWrite_ReportsTheBlockTheMessageThenClosing()
    {
        // Measured: an output that refused the write traced "{ [3 bytes data]", then the
        // failure's message and "* closing connection #0", and exited 23.
        ScriptedConnection connection = new(Latin1("x\r\n"));
        TranscriptTransferEvents events = new();
        TransferContext context = new() { Url = CurlUrl.Parse("gopher://h/1"), Output = new WriteRefusingStream(), Events = events };

        TransferResult result = await new GopherProtocolHandler(Connector(connection, 0)).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.WriteError, result.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "<= x\r\n", "* " + result.ErrorMessage, "* closing connection #0" },
            events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReceiveFailsWithCurlsFallbackText_ReportsOnlyClosing()
    {
        ScriptedConnection connection = new(Latin1("iHe"), null);
        TranscriptTransferEvents events = new();

        TransferResult result = await new GopherProtocolHandler(Connector(connection, 1)).ExecuteAsync(Context("gopher://h/1/", events));

        Assert.AreEqual("Failure when receiving data from the peer", result.ErrorMessage);
        CollectionAssert.AreEqual(new[] { "<= iHe", "* closing connection #1" }, events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_SendFailsWithCurlsFallbackText_ReportsOnlyClosing()
    {
        ScriptedConnection connection = new() { FailWrites = true };
        TranscriptTransferEvents events = new();

        TransferResult result = await new GopherProtocolHandler(Connector(connection, 0)).ExecuteAsync(Context("gopher://h/1/", events));

        Assert.AreEqual(CurlExitCode.SendError, result.ExitCode);
        CollectionAssert.AreEqual(new[] { "* closing connection #0" }, events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnectFails_ReportsNothing()
    {
        TranscriptTransferEvents events = new();

        await new GopherProtocolHandler(new FakeConnector(ConnectResult.Failed(CurlExitCode.CouldntConnect, "refused")))
            .ExecuteAsync(Context("gopher://h/1/", events));

        Assert.IsEmpty(events.Transcript);
    }

    private static FakeConnector Connector(IConnection connection, long connectionNumber) =>
        new(ConnectResult.Connected(connection, null, connectionNumber: connectionNumber));

    private static TransferContext Context(string url, ITransferEvents events) =>
        new() { Url = CurlUrl.Parse(url), Output = new MemoryStream(), Events = events };

    private static byte[] Latin1(string text) => System.Text.Encoding.Latin1.GetBytes(text);
}
