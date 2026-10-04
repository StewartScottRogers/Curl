using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Dict.Fakes;

namespace Curl.Protocol.Dict;

/// <summary>
/// Pins how a <c>dict://</c> transfer honours <c>-I</c> (<see cref="ITransferContext.NoBody" />)
/// and <c>--max-filesize</c> (<see cref="ITransferContext.MaxFileSize" />), against curl 8.21.0
/// measured on 2026-10-02 (BL-1309): under <c>-I</c> the request is sent and nothing is read;
/// past the limit the allowed bytes are written and the transfer ends with exit 63.
/// </summary>
[TestClass]
public sealed class DictProtocolHandlerBodyLimitTests
{
    /// <summary>The reply the measuring server sent.</summary>
    private const string MeasuredReply = "220 hi\r\n150 1 found\r\n151 \"hello\" wn\r\nhi\r\n.\r\n250 ok\r\n";

    private const string MeasuredRequest = "CLIENT libcurl 8.21.0\r\nDEFINE ! hello\r\nQUIT\r\n";

    [TestMethod]
    public async Task ExecuteAsync_NoBody_SendsTheRequestAndEndsWithoutReading()
    {
        // Measured: -sv -I dict://127.0.0.1:PORT/d:hello exited 0 with stdout empty and
        // "} [45 bytes data]" then "* shutting down connection #0".
        TranscriptTransferEvents events = new();
        ScriptedConnection connection = new(Latin1(MeasuredReply));
        MemoryStream output = new();

        TransferResult result = await new DictProtocolHandler(Connector(connection)).ExecuteAsync(
            new TransferContext { Url = CurlUrl.Parse("dict://h/d:hello"), Output = output, Events = events, NoBody = true });

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(MeasuredRequest, Encoding.Latin1.GetString(connection.Sent));
        Assert.AreEqual(0, connection.ReadCount);
        Assert.AreEqual(0, output.Length);
        CollectionAssert.AreEqual(new[] { "=> " + MeasuredRequest, "* shutting down connection #0" }, events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReplyPastMaxFileSize_WritesTheAllowedBytesAndExits63()
    {
        // Measured: -sv --max-filesize 3 dict://127.0.0.1:PORT/d:hello exited 63 with stdout "220" and
        // "* Exceeded the maximum allowed file size (3) with 3 bytes" then "* closing connection #0".
        TranscriptTransferEvents events = new();
        MemoryStream output = new();

        TransferResult result = await new DictProtocolHandler(Connector(new ScriptedConnection(Latin1(MeasuredReply)))).ExecuteAsync(
            Context(output, events, 3));

        Assert.AreEqual(CurlExitCode.FilesizeExceeded, result.ExitCode);
        Assert.AreEqual("Exceeded the maximum allowed file size (3) with 3 bytes", result.ErrorMessage);
        Assert.AreEqual(3, result.BytesTransferred);
        Assert.AreEqual("220", Encoding.Latin1.GetString(output.ToArray()));
        CollectionAssert.AreEqual(
            new[] { "* Exceeded the maximum allowed file size (3) with 3 bytes", "* closing connection #0" },
            events.Transcript.TakeLast(2).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_LimitInsideTheSecondRead_WritesTheFirstReadWholeAndCutsTheSecond()
    {
        MemoryStream output = new();
        ScriptedConnection connection = new(Latin1("220 hi\r\n"), Latin1("250 ok\r\n"));

        TransferResult result = await new DictProtocolHandler(Connector(connection)).ExecuteAsync(
            Context(output, new TranscriptTransferEvents(), 11));

        Assert.AreEqual(CurlExitCode.FilesizeExceeded, result.ExitCode);
        Assert.AreEqual("Exceeded the maximum allowed file size (11) with 11 bytes", result.ErrorMessage);
        Assert.AreEqual("220 hi\r\n250", Encoding.Latin1.GetString(output.ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_LimitReachedExactlyByTheFirstRead_FailsOnTheNextReadWritingNothingMore()
    {
        MemoryStream output = new();
        ScriptedConnection connection = new(Latin1("220 hi\r\n"), Latin1("250 ok\r\n"));

        TransferResult result = await new DictProtocolHandler(Connector(connection)).ExecuteAsync(
            Context(output, new TranscriptTransferEvents(), 8));

        Assert.AreEqual(CurlExitCode.FilesizeExceeded, result.ExitCode);
        Assert.AreEqual("Exceeded the maximum allowed file size (8) with 8 bytes", result.ErrorMessage);
        Assert.AreEqual("220 hi\r\n", Encoding.Latin1.GetString(output.ToArray()));
    }

    [TestMethod]
    [DataRow(0L)]
    [DataRow(null)]
    [DataRow(52L)]
    public async Task ExecuteAsync_NoLimitOrLimitAtTheReplysLength_WritesTheWholeReplyWithExit0(long? maxFileSize)
    {
        Assert.AreEqual(52, MeasuredReply.Length);
        MemoryStream output = new();

        TransferResult result = await new DictProtocolHandler(Connector(new ScriptedConnection(Latin1(MeasuredReply)))).ExecuteAsync(
            Context(output, new TranscriptTransferEvents(), maxFileSize));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(MeasuredReply, Encoding.Latin1.GetString(output.ToArray()));
    }

    private static RecordingConnector Connector(IConnection connection) =>
        new(ConnectResult.Connected(connection, null, connectionNumber: 0));

    private static TransferContext Context(MemoryStream output, ITransferEvents events, long? maxFileSize) =>
        new() { Url = CurlUrl.Parse("dict://h/d:hello"), Output = output, Events = events, MaxFileSize = maxFileSize };

    private static byte[] Latin1(string text) => Encoding.Latin1.GetBytes(text);
}
