using System.Net.Sockets;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Dict.Fakes;

namespace Curl.Protocol.Dict;

/// <summary>
/// Pins what a <c>dict://</c> transfer whose send, receive or output write breaks returns:
/// curl 8.21.0's exit 55, 56 or 23 and its message (<c>lib/dict.c</c> <c>dict_do</c> and
/// <c>lib/cf-socket.c</c>, the texts the gopher and RTSP handlers measured), and the
/// <c>-v</c> lines that end the connection (BL-1125).
/// </summary>
[TestClass]
public sealed class DictProtocolHandlerIoFailureTests
{
    private const string Request = "CLIENT libcurl 8.21.0\r\nDEFINE ! w\r\nQUIT\r\n";

    [TestMethod]
    public async Task ExecuteAsync_WriteReset_ReturnsSendErrorConnectionWasResetAndReportsBothLinesThenClosing()
    {
        TranscriptTransferEvents events = new();
        FailingConnection connection = new(Reset(), Reset());

        TransferResult result = await new DictProtocolHandler(Connector(connection, 2)).ExecuteAsync(Context(new MemoryStream(), events));

        Assert.AreEqual(CurlExitCode.SendError, result.ExitCode);
        Assert.AreEqual("Send failure: Connection was reset", result.ErrorMessage);
        Assert.AreEqual(0, result.BytesTransferred);
        CollectionAssert.AreEqual(
            new[] { "* Send failure: Connection was reset", "* Failed sending DICT request", "* closing connection #2" },
            events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_WriteFailsOtherwise_ReturnsSendErrorFailedSendingDataAndReportsTheDictLineThenClosing()
    {
        TranscriptTransferEvents events = new();
        FailingConnection connection = new(new IOException("broken"), new IOException("broken"));

        TransferResult result = await new DictProtocolHandler(Connector(connection, 0)).ExecuteAsync(Context(new MemoryStream(), events));

        Assert.AreEqual(CurlExitCode.SendError, result.ExitCode);
        Assert.AreEqual("Failed sending data to the peer", result.ErrorMessage);
        CollectionAssert.AreEqual(new[] { "* Failed sending DICT request", "* closing connection #0" }, events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReadResetAfterAReply_ReturnsRecvErrorConnectionWasResetWithTheBytesWrittenCounted()
    {
        TranscriptTransferEvents events = new();
        MemoryStream output = new();
        FailingConnection connection = new(null, Reset(), Latin1("220 ok\r\n"));

        TransferResult result = await new DictProtocolHandler(Connector(connection, 1)).ExecuteAsync(Context(output, events));

        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual("Recv failure: Connection was reset", result.ErrorMessage);
        Assert.AreEqual(8, result.BytesTransferred);
        Assert.AreEqual("220 ok\r\n", Encoding.Latin1.GetString(output.ToArray()));
        CollectionAssert.AreEqual(
            new[] { "=> " + Request, "<= 220 ok\r\n", "* Recv failure: Connection was reset", "* closing connection #1" },
            events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReadFailsOtherwise_ReturnsRecvErrorFailureWhenReceivingWithoutItsOwnLine()
    {
        TranscriptTransferEvents events = new();
        FailingConnection connection = new(null, new IOException("broken"), Latin1("220 ok\r\n"), Latin1("221 bye\r\n"));

        TransferResult result = await new DictProtocolHandler(Connector(connection, 0)).ExecuteAsync(Context(new MemoryStream(), events));

        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual("Failure when receiving data from the peer", result.ErrorMessage);
        Assert.AreEqual(17, result.BytesTransferred);
        Assert.AreEqual("* closing connection #0", events.Transcript[^1]);
        Assert.AreEqual("<= 221 bye\r\n", events.Transcript[^2]);
    }

    [TestMethod]
    public async Task ExecuteAsync_OutputRefusesPartOfAWrite_ReturnsWriteErrorNamingPassedAndReturned()
    {
        TranscriptTransferEvents events = new();
        FailingConnection connection = new(null, new IOException("unused"), Latin1("220 ok\r\n"), Latin1("221 bye\r\n"));
        RefusingStream output = new(1, new OutputWriteFailedException(4, "disk full"));

        TransferResult result = await new DictProtocolHandler(Connector(connection, 3)).ExecuteAsync(Context(output, events));

        Assert.AreEqual(CurlExitCode.WriteError, result.ExitCode);
        Assert.AreEqual("Failure writing output to destination, passed 9 returned 4", result.ErrorMessage);
        Assert.AreEqual(8, result.BytesTransferred);
        CollectionAssert.AreEqual(
            new[]
            {
                "=> " + Request,
                "<= 220 ok\r\n",
                "<= 221 bye\r\n",
                "* Failure writing output to destination, passed 9 returned 4",
                "* closing connection #3",
            },
            events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_OutputThrowsAPlainIOException_ReturnsWriteErrorReturnedZero()
    {
        FailingConnection connection = new(null, new IOException("unused"), Latin1("220 ok\r\n"));
        RefusingStream output = new(0, new IOException("closed"));

        TransferResult result = await new DictProtocolHandler(Connector(connection, 0))
            .ExecuteAsync(Context(output, new TranscriptTransferEvents()));

        Assert.AreEqual(CurlExitCode.WriteError, result.ExitCode);
        Assert.AreEqual("Failure writing output to destination, passed 8 returned 0", result.ErrorMessage);
        Assert.AreEqual(0, result.BytesTransferred);
    }

    private static IOException Reset() =>
        new("reset", new SocketException((int)SocketError.ConnectionReset));

    private static RecordingConnector Connector(IConnection connection, long connectionNumber) =>
        new(ConnectResult.Connected(connection, null, connectionNumber: connectionNumber));

    private static TransferContext Context(Stream output, ITransferEvents events) =>
        new() { Url = CurlUrl.Parse("dict://h/d:w"), Output = output, Events = events };

    private static byte[] Latin1(string text) => Encoding.Latin1.GetBytes(text);

    /// <summary>A write-only stream that accepts <c>accepted</c> writes and then throws <c>failure</c>.</summary>
    private sealed class RefusingStream(int accepted, IOException failure) : MemoryStream
    {
        private int writes;

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (writes++ == accepted)
            {
                throw failure;
            }

            return base.WriteAsync(buffer, cancellationToken);
        }
    }
}
