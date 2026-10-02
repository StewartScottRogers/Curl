using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Gopher.Fakes;

namespace Curl.Protocol.Gopher;

/// <summary>
/// Pins what a gopher transfer writes to <see cref="ITransferContext.DumpHeaderOutput" />
/// (<c>-D</c>) against curl 8.21.0 (measured, BL-1130): the selector sent and its CRLF,
/// before any of the reply, and nothing at all under <c>-i</c> alone.
/// </summary>
[TestClass]
public sealed class GopherProtocolHandlerDumpHeaderTests
{
    /// <summary>The reply the measured server sent: one info line and the terminator.</summary>
    private const string MeasuredReply = "iHello\tfake\t(NULL)\t0\r\n.\r\n";

    [TestMethod]
    public async Task ExecuteAsync_SelectorWithDumpHeaderOutput_WritesSelectorAndCrlfThereBeforeTheReply()
    {
        // Measured: curl -s -D <file> gopher://127.0.0.1:<port>/1sel wrote "sel\r\n" to the file.
        List<string> writes = [];
        ScriptedConnection connection = new(Encoding.ASCII.GetBytes(MeasuredReply));
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("gopher://h/1sel"),
            Output = new WriteRecordingStream("output", writes),
            DumpHeaderOutput = new WriteRecordingStream("dump", writes),
        };

        TransferResult result = await new GopherProtocolHandler(FakeConnector.For(connection)).ExecuteAsync(context);

        Assert.AreEqual(TransferResult.Success(MeasuredReply.Length), result);
        CollectionAssert.AreEqual(new[] { "dump:sel", "dump:\r\n", "output:" + MeasuredReply }, writes);
    }

    [TestMethod]
    [DataRow("gopher://h/", DisplayName = "root path")]
    [DataRow("gopher://h/1", DisplayName = "item type only")]
    public async Task ExecuteAsync_EmptySelectorWithDumpHeaderOutput_WritesOnlyCrlfThere(string url)
    {
        // Measured: curl -s -D <file> on gopher://127.0.0.1:<port>/ and .../1 wrote "\r\n".
        MemoryStream dump = new();
        TransferContext context = new()
        {
            Url = CurlUrl.Parse(url),
            Output = new MemoryStream(),
            DumpHeaderOutput = dump,
        };

        await new GopherProtocolHandler(FakeConnector.For(new ScriptedConnection())).ExecuteAsync(context);

        CollectionAssert.AreEqual("\r\n"u8.ToArray(), dump.ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_IncludeWithoutDumpHeader_WritesOnlyTheReplyToOutput()
    {
        // Measured: curl -s -i gopher://127.0.0.1:<port>/1sel wrote only the reply to stdout.
        MemoryStream output = new();
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("gopher://h/1sel"),
            Output = output,
            HeaderOutput = output,
            DumpHeaderOutput = null,
        };

        await new GopherProtocolHandler(FakeConnector.For(new ScriptedConnection(Encoding.ASCII.GetBytes(MeasuredReply))))
            .ExecuteAsync(context);

        Assert.AreEqual(MeasuredReply, Encoding.ASCII.GetString(output.ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_DumpHeaderOutputRefusesSelector_ReturnsWriteErrorAndSendsNoCrlf()
    {
        ScriptedConnection connection = new(Encoding.ASCII.GetBytes(MeasuredReply));
        MemoryStream output = new();
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("gopher://h/1sel"),
            Output = output,
            DumpHeaderOutput = new WriteRefusingStream(),
        };

        TransferResult result = await new GopherProtocolHandler(FakeConnector.For(connection)).ExecuteAsync(context);

        Assert.AreEqual(
            TransferResult.Failure(CurlExitCode.WriteError, "client returned ERROR on write of 3 bytes"),
            result);
        CollectionAssert.AreEqual("sel"u8.ToArray(), connection.Written);
        Assert.AreEqual(0, output.Length);
    }

    [TestMethod]
    public async Task ExecuteAsync_SendFailsWithDumpHeaderOutput_WritesNothingThere()
    {
        ScriptedConnection connection = new() { FailWrites = true };
        MemoryStream dump = new();
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("gopher://h/1sel"),
            Output = new MemoryStream(),
            DumpHeaderOutput = dump,
        };

        TransferResult result = await new GopherProtocolHandler(FakeConnector.For(connection)).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.SendError, result.ExitCode);
        Assert.AreEqual(0, dump.Length);
    }

    /// <summary>
    /// A stream that records each write, as <c>name:text</c>, in a list it shares with
    /// another, so the order of writes across both can be asserted.
    /// </summary>
    private sealed class WriteRecordingStream(string name, List<string> writes) : MemoryStream
    {
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            writes.Add(name + ":" + Encoding.ASCII.GetString(buffer.Span));
            return ValueTask.CompletedTask;
        }
    }
}
