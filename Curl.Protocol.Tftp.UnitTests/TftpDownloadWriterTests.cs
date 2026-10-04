using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Tftp.Fakes;

namespace Curl.Protocol.Tftp;

/// <summary>
/// Pins how a <c>tftp://</c> download honours <c>-I</c> and <c>--max-filesize</c> through
/// curl 8.21.0's download writer, measured on 2026-10-02 with
/// <c>Record-CurlExchange.ps1 -Tftp</c> (BL-1305): <c>-I</c> ends at the first DATA block
/// with exit 8 and writes nothing, a block past the limit is cut and ends with exit 63, and
/// either way the server is sent the bare ERROR packet <c>00 05</c> and the last block
/// acknowledged.
/// </summary>
[TestClass]
public sealed class TftpDownloadWriterTests
{
    private static readonly IPEndPoint ServerEndPoint = new(IPAddress.Loopback, 69);

    private static readonly IPEndPoint TransferEndPoint = new(IPAddress.Loopback, 50123);

    [TestMethod]
    public async Task ExecuteAsync_NoBody_EndsAtFirstBlockWithExit8WritingNothingAndSendsError0()
    {
        var channel = Channel(OptionAcknowledgement("tsize\06\0"), Data(1, "hello\n"));
        var output = new MemoryStream();
        var events = new RecordingTransferEvents();

        var result = await Run(channel, new TransferContext { Url = Url, Output = output, Events = events, NoBody = true });

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        Assert.AreEqual("Weird server reply", result.ErrorMessage);
        Assert.AreEqual(0, output.Length);
        CollectionAssert.Contains(events.Steps, "<= hello\n");
        Assert.IsFalse(events.Steps.Any(step => step.Contains("Weird", StringComparison.Ordinal)));
        CollectionAssert.AreEqual(new byte[] { 0, 5, 0, 0 }, channel.Sent[^1].Datagram);
        Assert.AreEqual(TransferEndPoint, channel.Sent[^1].Destination);
    }

    [TestMethod]
    public async Task ExecuteAsync_MaxFileSize3_WritesHelAndEndsWithExit63AndSendsError0()
    {
        var channel = Channel(Data(1, "hello\n"));
        var output = new MemoryStream();
        var events = new RecordingTransferEvents();

        var result = await Run(channel, new TransferContext { Url = Url, Output = output, Events = events, MaxFileSize = 3 });

        const string Message = "Exceeded the maximum allowed file size (3) with 3 bytes";
        Assert.AreEqual(CurlExitCode.FilesizeExceeded, result.ExitCode);
        Assert.AreEqual(Message, result.ErrorMessage);
        Assert.AreEqual(3, result.BytesTransferred);
        Assert.AreEqual("hel", Encoding.ASCII.GetString(output.ToArray()));
        var received = events.Steps.IndexOf("<= hello\n");
        Assert.IsGreaterThanOrEqualTo(0, received);
        Assert.AreEqual(Message, events.Steps[received + 1]);
        Assert.HasCount(2, channel.Sent);
        CollectionAssert.AreEqual(new byte[] { 0, 5, 0, 0 }, channel.Sent[^1].Datagram);
    }

    [TestMethod]
    public async Task ExecuteAsync_MaxFileSizeInsideSecondBlock_WritesFirstBlockWholeAnd88BytesAndSendsError1()
    {
        var first = new string('a', 512);
        var second = new string('b', 188);
        var channel = Channel(Data(1, first), Data(2, second));
        var output = new MemoryStream();

        var result = await Run(channel, new TransferContext { Url = Url, Output = output, MaxFileSize = 600 });

        Assert.AreEqual(CurlExitCode.FilesizeExceeded, result.ExitCode);
        Assert.AreEqual("Exceeded the maximum allowed file size (600) with 600 bytes", result.ErrorMessage);
        Assert.AreEqual(first + new string('b', 88), Encoding.ASCII.GetString(output.ToArray()));
        Assert.HasCount(3, channel.Sent);
        CollectionAssert.AreEqual(new byte[] { 0, 4, 0, 1 }, channel.Sent[1].Datagram);
        CollectionAssert.AreEqual(new byte[] { 0, 5, 0, 1 }, channel.Sent[2].Datagram);
    }

    [TestMethod]
    [DataRow(0L)]
    [DataRow(null)]
    [DataRow(6L)]
    public async Task ExecuteAsync_MaxFileSizeZeroUnsetOrExactlyTheFile_WritesTheWholeFile(long? maxFileSize)
    {
        var channel = Channel(Data(1, "hello\n"));
        var output = new MemoryStream();

        var result = await Run(channel, new TransferContext { Url = Url, Output = output, MaxFileSize = maxFileSize });

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("hello\n", Encoding.ASCII.GetString(output.ToArray()));
        CollectionAssert.AreEqual(new byte[] { 0, 4, 0, 1 }, channel.Sent[^1].Datagram);
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadWithNoBodyAndMaxFileSize1_CompletesAsWithout()
    {
        var channel = Channel(Ack(0), Ack(1));

        var result = await Run(channel, new TransferContext
        {
            Url = Url,
            Output = new MemoryStream(),
            Upload = new MemoryStream("abc"u8.ToArray()),
            NoBody = true,
            MaxFileSize = 1,
        });

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(3, result.BytesTransferred);
        CollectionAssert.AreEqual(new byte[] { 0, 3, 0, 1, (byte)'a', (byte)'b', (byte)'c' }, channel.Sent[1].Datagram);
    }

    private static CurlUrl Url => CurlUrl.Parse("tftp://h/file");

    private static async Task<TransferResult> Run(IDatagramChannel channel, TransferContext context) =>
        await new TftpProtocolHandler(new RecordingDatagramConnector(DatagramOpenResult.Opened(channel)))
            .ExecuteAsync(context);

    private static ScriptedDatagramChannel Channel(params (byte[] Datagram, EndPoint Source)[] script) =>
        new(ServerEndPoint, script);

    private static (byte[] Datagram, EndPoint Source) Data(ushort block, string payload) =>
        ([0, 3, (byte)(block >> 8), (byte)block, .. Encoding.ASCII.GetBytes(payload)], TransferEndPoint);

    private static (byte[] Datagram, EndPoint Source) Ack(ushort block) =>
        ([0, 4, (byte)(block >> 8), (byte)block], TransferEndPoint);

    private static (byte[] Datagram, EndPoint Source) OptionAcknowledgement(string options) =>
        ([0, 6, .. Encoding.ASCII.GetBytes(options)], TransferEndPoint);
}
