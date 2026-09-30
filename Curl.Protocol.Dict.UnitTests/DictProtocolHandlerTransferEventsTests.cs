using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Dict.Fakes;

namespace Curl.Protocol.Dict;

/// <summary>
/// Pins what a <c>dict://</c> transfer reports to <see cref="ITransferEvents" /> after
/// connecting, against curl 8.21.0's <c>-v</c> and <c>--trace-ascii</c> output measured on
/// 2026-09-29 (BL-934 Notes): the whole request as one block of data sent, each read as data
/// received, the server's close as a zero-byte block, and <c>shutting down connection #N</c>.
/// </summary>
[TestClass]
public sealed class DictProtocolHandlerTransferEventsTests
{
    /// <summary>The measured server's reply, which curl traced as one 68-byte block.</summary>
    private const string MeasuredReply = "220 ok\r\n150 1 definitions\r\n151 \"word\" db\r\ntext\r\n.\r\n250 ok\r\n221 bye\r\n";

    [TestMethod]
    [DataRow("dict://h/d:word", "CLIENT libcurl 8.21.0\r\nDEFINE ! word\r\nQUIT\r\n")]
    [DataRow("dict://h/m:word:db:strategy", "CLIENT libcurl 8.21.0\r\nMATCH db strategy word\r\nQUIT\r\n")]
    [DataRow("dict://h/", "CLIENT libcurl 8.21.0\r\n\r\nQUIT\r\n")]
    public async Task ExecuteAsync_ReplyThenClose_ReportsTheRequestTheReplyTheZeroByteCloseAndShuttingDown(string url, string request)
    {
        // Measured: "=> Send data, 44/53/31 bytes", "<= Recv data, 68 bytes", "<= Recv data, 0 bytes (0x0)",
        // "* shutting down connection #0".
        TranscriptTransferEvents events = new();

        await new DictProtocolHandler(Connector(new ScriptedConnection(Latin1(MeasuredReply)), 0)).ExecuteAsync(Context(url, events));

        CollectionAssert.AreEqual(
            new[] { "=> " + request, "<= " + MeasuredReply, "<= ", "* shutting down connection #0" },
            events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReplyInTwoReads_ReportsEachReadAsItsOwnBlockAndTheConnectionsNumber()
    {
        TranscriptTransferEvents events = new();
        ScriptedConnection connection = new(Latin1("220 ok\r\n"), Latin1("221 bye\r\n"));

        await new DictProtocolHandler(Connector(connection, 4)).ExecuteAsync(Context("dict://h/d:w", events));

        CollectionAssert.AreEqual(
            new[]
            {
                "=> CLIENT libcurl 8.21.0\r\nDEFINE ! w\r\nQUIT\r\n",
                "<= 220 ok\r\n",
                "<= 221 bye\r\n",
                "<= ",
                "* shutting down connection #4",
            },
            events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_PathWithAControlCharacter_ReportsOnlyShuttingDown()
    {
        // Measured: dict://127.0.0.1:47940/d:a%01b wrote "* shutting down connection #0" and exited 3.
        TranscriptTransferEvents events = new();

        TransferResult result = await new DictProtocolHandler(Connector(new ScriptedConnection(), 0)).ExecuteAsync(Context("dict://h/d:a%01b", events));

        Assert.AreEqual(CurlExitCode.UrlMalformat, result.ExitCode);
        CollectionAssert.AreEqual(new[] { "* shutting down connection #0" }, events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnectFails_ReportsNothing()
    {
        TranscriptTransferEvents events = new();

        await new DictProtocolHandler(new RecordingConnector(ConnectResult.Failed(CurlExitCode.CouldntConnect, "refused")))
            .ExecuteAsync(Context("dict://h/d:w", events));

        Assert.IsEmpty(events.Transcript);
    }

    private static RecordingConnector Connector(IConnection connection, long connectionNumber) =>
        new(ConnectResult.Connected(connection, null, connectionNumber: connectionNumber));

    private static TransferContext Context(string url, ITransferEvents events) =>
        new() { Url = CurlUrl.Parse(url), Output = new MemoryStream(), Events = events };

    private static byte[] Latin1(string text) => Encoding.Latin1.GetBytes(text);
}
