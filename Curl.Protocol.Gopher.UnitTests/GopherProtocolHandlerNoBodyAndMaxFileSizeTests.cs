using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Gopher.Fakes;

namespace Curl.Protocol.Gopher;

/// <summary>
/// Pins how a <c>gopher://</c> transfer honours <c>-I</c> and <c>--max-filesize</c>,
/// against curl 8.21.0 measured on 2026-10-02 with the reply <c>hello\r\n.\r\n</c>
/// (BL-1308): <c>-I</c> sends the selector and exits 0 without reading, and
/// <c>--max-filesize 3</c> writes <c>hel</c> and exits 63.
/// </summary>
[TestClass]
public sealed class GopherProtocolHandlerNoBodyAndMaxFileSizeTests
{
    private const string Reply = "hello\r\n.\r\n";

    [TestMethod]
    public async Task ExecuteAsync_NoBody_SendsTheSelectorAndEndsWithoutReading()
    {
        // Measured: -sv -I wrote request "/x\r\n", nothing to stdout, no data line, then
        // "* shutting down connection #0", exit 0.
        ScriptedConnection connection = new(Encoding.Latin1.GetBytes(Reply));
        TranscriptTransferEvents events = new();
        MemoryStream output = new();
        TransferContext context = new() { Url = CurlUrl.Parse("gopher://h/0/x"), Output = output, Events = events, NoBody = true };

        TransferResult result = await new GopherProtocolHandler(Connector(connection)).ExecuteAsync(context);

        Assert.AreEqual(TransferResult.Success(0), result);
        CollectionAssert.AreEqual("/x\r\n"u8.ToArray(), connection.Written);
        Assert.AreEqual(0, connection.ReadCount);
        Assert.AreEqual(0, output.Length);
        CollectionAssert.AreEqual(new[] { "* shutting down connection #0" }, events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReplyPastMaxFileSize_WritesTheAllowedBytesAndFailsWith63()
    {
        // Measured: -sv --max-filesize 3 wrote "hel", traced "{ [10 bytes data]", then
        // "* Exceeded the maximum allowed file size (3) with 3 bytes", "* closing connection #0", exit 63.
        ScriptedConnection connection = new(Encoding.Latin1.GetBytes(Reply));
        TranscriptTransferEvents events = new();
        MemoryStream output = new();

        TransferResult result = await new GopherProtocolHandler(Connector(connection)).ExecuteAsync(Context(output, events, 3));

        const string Message = "Exceeded the maximum allowed file size (3) with 3 bytes";
        Assert.AreEqual(new TransferResult(CurlExitCode.FilesizeExceeded, 3, Message), result);
        Assert.AreEqual("hel", Encoding.Latin1.GetString(output.ToArray()));
        CollectionAssert.AreEqual(new[] { "<= " + Reply, "* " + Message, "* closing connection #0" }, events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_MaxFileSizeCrossedOnTheSecondRead_CountsAcrossReads()
    {
        ScriptedConnection connection = new("he"u8.ToArray(), "llo"u8.ToArray());
        MemoryStream output = new();

        TransferResult result = await new GopherProtocolHandler(Connector(connection))
            .ExecuteAsync(Context(output, new TranscriptTransferEvents(), 4));

        Assert.AreEqual(
            new TransferResult(CurlExitCode.FilesizeExceeded, 4, "Exceeded the maximum allowed file size (4) with 4 bytes"),
            result);
        Assert.AreEqual("hell", Encoding.Latin1.GetString(output.ToArray()));
    }

    [TestMethod]
    [DataRow(0L, DisplayName = "0 is no limit")]
    [DataRow(null, DisplayName = "no limit")]
    [DataRow(10L, DisplayName = "exactly the reply's length")]
    public async Task ExecuteAsync_ReplyWithinMaxFileSize_WritesTheWholeReplyAndSucceeds(long? maxFileSize)
    {
        ScriptedConnection connection = new(Encoding.Latin1.GetBytes(Reply));
        MemoryStream output = new();

        TransferResult result = await new GopherProtocolHandler(Connector(connection))
            .ExecuteAsync(Context(output, new TranscriptTransferEvents(), maxFileSize));

        Assert.AreEqual(TransferResult.Success(10), result);
        Assert.AreEqual(Reply, Encoding.Latin1.GetString(output.ToArray()));
    }

    private static FakeConnector Connector(IConnection connection) =>
        new(ConnectResult.Connected(connection, null, connectionNumber: 0));

    private static TransferContext Context(MemoryStream output, ITransferEvents events, long? maxFileSize) =>
        new() { Url = CurlUrl.Parse("gopher://h/0/x"), Output = output, Events = events, MaxFileSize = maxFileSize };
}
