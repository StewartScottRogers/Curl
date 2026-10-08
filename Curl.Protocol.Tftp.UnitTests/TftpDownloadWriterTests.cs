using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Tftp.Fakes;
using Curl.Testing;

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

    public TestContext TestContext { get; set; } = null!;

    private static CurlUrl Url => CurlUrl.Parse("tftp://h/file");

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ExecuteAsync_NoBody_EndsAtFirstBlockWithExit8WritingNothingAndSendsError0()
    {
        var channel = Channel(OptionAcknowledgement("tsize\06\0"), Data(1, "hello\n"));
        var output = new MemoryStream();
        var events = new RecordingTransferEvents();

        var result = await Run(channel, new TransferContext { Url = Url, Output = output, Events = events, NoBody = true });

        Diagnostics.Assert("exit code", CurlExitCode.WeirdServerReply, result.ExitCode);
        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        Diagnostics.Assert("error message", "Weird server reply", result.ErrorMessage);
        Assert.AreEqual("Weird server reply", result.ErrorMessage);
        Diagnostics.Assert("output length", 0, output.Length);
        Assert.AreEqual(0, output.Length);
        Diagnostics.Act("events", string.Join(" | ", events.Steps));
        CollectionAssert.Contains(events.Steps, "<= hello\n");
        Diagnostics.Assert("an event mentions Weird", false, events.Steps.Any(step => step.Contains("Weird", StringComparison.Ordinal)));
        Assert.IsFalse(events.Steps.Any(step => step.Contains("Weird", StringComparison.Ordinal)));
        Diagnostics.Diff("last datagram sent", new byte[] { 0, 5, 0, 0 }, channel.Sent[^1].Datagram);
        CollectionAssert.AreEqual(new byte[] { 0, 5, 0, 0 }, channel.Sent[^1].Datagram);
        Diagnostics.Assert("last datagram destination", TransferEndPoint, channel.Sent[^1].Destination);
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
        Diagnostics.Assert("exit code", CurlExitCode.FilesizeExceeded, result.ExitCode);
        Assert.AreEqual(CurlExitCode.FilesizeExceeded, result.ExitCode);
        Diagnostics.Assert("error message", Message, result.ErrorMessage);
        Assert.AreEqual(Message, result.ErrorMessage);
        Diagnostics.Assert("bytes transferred", 3L, result.BytesTransferred);
        Assert.AreEqual(3, result.BytesTransferred);
        Diagnostics.Bytes("output written", output.ToArray());
        Diagnostics.Diff("output written", "hel", Encoding.ASCII.GetString(output.ToArray()));
        Assert.AreEqual("hel", Encoding.ASCII.GetString(output.ToArray()));
        Diagnostics.Act("events", string.Join(" | ", events.Steps));
        var received = events.Steps.IndexOf("<= hello\n");
        Assert.IsGreaterThanOrEqualTo(0, received);
        Diagnostics.Assert("event after the received data", Message, events.Steps[received + 1]);
        Assert.AreEqual(Message, events.Steps[received + 1]);
        Diagnostics.Assert("datagrams sent", 2, channel.Sent.Count);
        Assert.HasCount(2, channel.Sent);
        Diagnostics.Diff("last datagram sent", new byte[] { 0, 5, 0, 0 }, channel.Sent[^1].Datagram);
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

        Diagnostics.Assert("exit code", CurlExitCode.FilesizeExceeded, result.ExitCode);
        Assert.AreEqual(CurlExitCode.FilesizeExceeded, result.ExitCode);
        Diagnostics.Assert("error message", "Exceeded the maximum allowed file size (600) with 600 bytes", result.ErrorMessage);
        Assert.AreEqual("Exceeded the maximum allowed file size (600) with 600 bytes", result.ErrorMessage);
        Diagnostics.Bytes("output written", output.ToArray());
        Diagnostics.Diff("output written", first + new string('b', 88), Encoding.ASCII.GetString(output.ToArray()));
        Assert.AreEqual(first + new string('b', 88), Encoding.ASCII.GetString(output.ToArray()));
        Diagnostics.Assert("datagrams sent", 3, channel.Sent.Count);
        Assert.HasCount(3, channel.Sent);
        Diagnostics.Diff("second datagram sent", new byte[] { 0, 4, 0, 1 }, channel.Sent[1].Datagram);
        CollectionAssert.AreEqual(new byte[] { 0, 4, 0, 1 }, channel.Sent[1].Datagram);
        Diagnostics.Diff("third datagram sent", new byte[] { 0, 5, 0, 1 }, channel.Sent[2].Datagram);
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

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Bytes("output written", output.ToArray());
        Diagnostics.Diff("output written", "hello\n", Encoding.ASCII.GetString(output.ToArray()));
        Assert.AreEqual("hello\n", Encoding.ASCII.GetString(output.ToArray()));
        Diagnostics.Diff("last datagram sent", new byte[] { 0, 4, 0, 1 }, channel.Sent[^1].Datagram);
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

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("bytes transferred", 3L, result.BytesTransferred);
        Assert.AreEqual(3, result.BytesTransferred);
        Diagnostics.Diff("second datagram sent", new byte[] { 0, 3, 0, 1, (byte)'a', (byte)'b', (byte)'c' }, channel.Sent[1].Datagram);
        CollectionAssert.AreEqual(new byte[] { 0, 3, 0, 1, (byte)'a', (byte)'b', (byte)'c' }, channel.Sent[1].Datagram);
    }

    private async Task<TransferResult> Run(ScriptedDatagramChannel channel, TransferContext context)
    {
        Diagnostics.Arrange("url", "tftp://h/file");
        Diagnostics.Arrange("no body", context.NoBody);
        Diagnostics.Arrange("max file size", context.MaxFileSize?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "(unset)");
        Diagnostics.Arrange(
            "upload length",
            context.Upload is { CanSeek: true } upload ? upload.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) : "(none)");
        try
        {
            TransferResult result;
            using (Diagnostics.Phase("transfer"))
            {
                result = await new TftpProtocolHandler(new RecordingDatagramConnector(DatagramOpenResult.Opened(channel)))
                    .ExecuteAsync(context);
            }

            TftpTestDiagnostics.Result(Diagnostics, result);
            return result;
        }
        finally
        {
            TftpTestDiagnostics.Sent(Diagnostics, channel);
        }
    }

    private ScriptedDatagramChannel Channel(params (byte[] Datagram, EndPoint Source)[] script)
    {
        for (var index = 0; index < script.Length; index++)
        {
            TftpTestDiagnostics.Scripted(Diagnostics, index, script[index].Datagram, script[index].Source);
        }

        return new(ServerEndPoint, script);
    }

    private static (byte[] Datagram, EndPoint Source) Data(ushort block, string payload) =>
        ([0, 3, (byte)(block >> 8), (byte)block, .. Encoding.ASCII.GetBytes(payload)], TransferEndPoint);

    private static (byte[] Datagram, EndPoint Source) Ack(ushort block) =>
        ([0, 4, (byte)(block >> 8), (byte)block], TransferEndPoint);

    private static (byte[] Datagram, EndPoint Source) OptionAcknowledgement(string options) =>
        ([0, 6, .. Encoding.ASCII.GetBytes(options)], TransferEndPoint);
}
