using System.Globalization;
using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Tftp.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Tftp;

/// <summary>
/// Pins <c>--tftp-blksize</c> (<see cref="ITransferContext.TftpBlockSize" />) and
/// <c>--tftp-no-options</c> (<see cref="ITransferContext.TftpNoOptions" />) against
/// curl 8.21.0, measured on 2026-09-26 against a loopback UDP server that answered from a
/// second port: the <c>blksize</c> sent, the request with no options, and the block size
/// in force with and without an option acknowledgement.
/// </summary>
[TestClass]
public sealed class TftpOptionsTests
{
    private static readonly IPEndPoint ServerEndPoint = new(IPAddress.Loopback, 69);

    /// <summary>The server's transfer identifier: the new port it answers from.</summary>
    private static readonly IPEndPoint TransferEndPoint = new(IPAddress.Loopback, 50123);

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(5, "8", DisplayName = "5 is raised to 8")]
    [DataRow(8, "8", DisplayName = "8 is sent as 8")]
    [DataRow(1024, "1024", DisplayName = "1024 is sent as 1024")]
    [DataRow(65464, "65464", DisplayName = "65464 is sent as 65464")]
    [DataRow(70000, "65464", DisplayName = "70000 is lowered to 65464")]
    [DataRow(0, "512", DisplayName = "0 sends the default 512")]
    public async Task ExecuteAsync_TftpBlockSize_SendsClampedBlksizeWithDefaultTsizeAndTimeout(
        int tftpBlockSize,
        string expectedBlksize)
    {
        var channel = Channel(Data(1, "hello"));

        await RunAsync(channel, Context(tftpBlockSize: tftpBlockSize));

        byte[] expected = ReadRequest($"file.txt\0octet\0tsize\00\0blksize\0{expectedBlksize}\0timeout\06\0");
        Diagnostics.Arrange("expected blksize", expectedBlksize);
        Diagnostics.Diff("read request", expected, channel.Sent[0].Datagram);
        CollectionAssert.AreEqual(expected, channel.Sent[0].Datagram);
    }

    [TestMethod]
    public async Task ExecuteAsync_TftpNoOptions_SendsFileNameAndOctetOnlyAndDownloadsPlainData()
    {
        var channel = Channel(Data(1, "hello"));
        var output = new MemoryStream();

        var result = await RunAsync(channel, Context(output: output, noOptions: true));

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Diff("read request", ReadRequest("file.txt\0octet\0"), channel.Sent[0].Datagram);
        CollectionAssert.AreEqual(ReadRequest("file.txt\0octet\0"), channel.Sent[0].Datagram);
        string written = Encoding.ASCII.GetString(output.ToArray());
        Diagnostics.Assert("output", "hello", written);
        Assert.AreEqual("hello", written);
        Diagnostics.Diff("ack", new byte[] { 0, 4, 0, 1 }, channel.Sent[1].Datagram);
        CollectionAssert.AreEqual(new byte[] { 0, 4, 0, 1 }, channel.Sent[1].Datagram);
    }

    [TestMethod]
    public async Task ExecuteAsync_TftpNoOptionsWithBlockSize_StillSendsNoOptions()
    {
        var channel = Channel(Data(1, "hello"));

        await RunAsync(channel, Context(tftpBlockSize: 1024, noOptions: true));

        Diagnostics.Diff("read request", ReadRequest("file.txt\0octet\0"), channel.Sent[0].Datagram);
        CollectionAssert.AreEqual(ReadRequest("file.txt\0octet\0"), channel.Sent[0].Datagram);
    }

    [TestMethod]
    public async Task ExecuteAsync_BlockSize1024AcknowledgedAs1024_A1024ByteBlockIsNotLastAndA1000ByteBlockIs()
    {
        var channel = Channel(
            OptionAcknowledgement("blksize\01024\0"),
            Data(1, new string('a', 1024)),
            Data(2, new string('b', 1000)));

        var result = await RunAsync(channel, Context(tftpBlockSize: 1024));

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("bytes transferred", 2024, result.BytesTransferred);
        Assert.AreEqual(2024, result.BytesTransferred);
        Diagnostics.Assert("datagrams sent", 4, channel.Sent.Count);
        Assert.HasCount(4, channel.Sent);
        Diagnostics.Diff("ack 0", new byte[] { 0, 4, 0, 0 }, channel.Sent[1].Datagram);
        CollectionAssert.AreEqual(new byte[] { 0, 4, 0, 0 }, channel.Sent[1].Datagram);
        Diagnostics.Diff("ack 1", new byte[] { 0, 4, 0, 1 }, channel.Sent[2].Datagram);
        CollectionAssert.AreEqual(new byte[] { 0, 4, 0, 1 }, channel.Sent[2].Datagram);
        Diagnostics.Diff("ack 2", new byte[] { 0, 4, 0, 2 }, channel.Sent[3].Datagram);
        CollectionAssert.AreEqual(new byte[] { 0, 4, 0, 2 }, channel.Sent[3].Datagram);
    }

    /// <summary>
    /// Measured: curl 8.21.0 with <c>--tftp-blksize 1024</c>, answered with a plain
    /// 512-byte DATA 1 and a 100-byte DATA 2 and no OACK, acknowledged both and wrote 612
    /// bytes, so the block size in force stayed 512.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_BlockSize1024AnsweredWithPlainData_UsesTheDefault512()
    {
        var channel = Channel(Data(1, new string('a', 512)), Data(2, new string('b', 100)));

        var result = await RunAsync(channel, Context(tftpBlockSize: 1024));

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("bytes transferred", 612, result.BytesTransferred);
        Assert.AreEqual(612, result.BytesTransferred);
        Diagnostics.Assert("datagrams sent", 3, channel.Sent.Count);
        Assert.HasCount(3, channel.Sent);
        Diagnostics.Diff("ack 2", new byte[] { 0, 4, 0, 2 }, channel.Sent[2].Datagram);
        CollectionAssert.AreEqual(new byte[] { 0, 4, 0, 2 }, channel.Sent[2].Datagram);
    }

    /// <summary>
    /// Measured: curl 8.21.0 with <c>--tftp-no-options -T</c> of 3 bytes sent the write
    /// request <c>00 02 "dest.txt" 00 "octet" 00</c>, then DATA 1 after ACK 0, and exited 0.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_TftpNoOptionsUpload_SendsFileNameAndOctetOnlyThenData()
    {
        var channel = Channel(Ack(0), Ack(1));
        Diagnostics.Arrange("url", "tftp://h/dest.txt");
        Diagnostics.Arrange("no options", true);
        Diagnostics.Arrange("upload bytes", 3);

        var result = await RunAsync(channel, new TransferContext
        {
            Url = CurlUrl.Parse("tftp://h/dest.txt"),
            Output = new MemoryStream(),
            Upload = new MemoryStream("abc"u8.ToArray()),
            TftpNoOptions = true,
        });

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("bytes transferred", 3, result.BytesTransferred);
        Assert.AreEqual(3, result.BytesTransferred);
        Diagnostics.Diff("write request", WriteRequest("dest.txt\0octet\0"), channel.Sent[0].Datagram);
        CollectionAssert.AreEqual(WriteRequest("dest.txt\0octet\0"), channel.Sent[0].Datagram);
        Diagnostics.Diff("data 1", new byte[] { 0, 3, 0, 1, 97, 98, 99 }, channel.Sent[1].Datagram);
        CollectionAssert.AreEqual(new byte[] { 0, 3, 0, 1, 97, 98, 99 }, channel.Sent[1].Datagram);
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadWithTftpBlockSize_SendsItAsBlksize()
    {
        var channel = Channel(Ack(0), Ack(1));
        Diagnostics.Arrange("url", "tftp://h/dest.txt");
        Diagnostics.Arrange("block size", 70000);
        Diagnostics.Arrange("upload bytes", 3);

        await RunAsync(channel, new TransferContext
        {
            Url = CurlUrl.Parse("tftp://h/dest.txt"),
            Output = new MemoryStream(),
            Upload = new MemoryStream("abc"u8.ToArray()),
            TftpBlockSize = 70000,
        });

        byte[] expected = WriteRequest("dest.txt\0octet\0tsize\03\0blksize\065464\0timeout\06\0");
        Diagnostics.Diff("write request", expected, channel.Sent[0].Datagram);
        CollectionAssert.AreEqual(expected, channel.Sent[0].Datagram);
    }

    private static TftpProtocolHandler Handler(ScriptedDatagramChannel channel) =>
        new(new RecordingDatagramConnector(DatagramOpenResult.Opened(channel)));

    private async Task<TransferResult> RunAsync(ScriptedDatagramChannel channel, TransferContext context)
    {
        TransferResult result;
        using (Diagnostics.Phase("transfer"))
        {
            result = await Handler(channel).ExecuteAsync(context);
        }

        TftpTestDiagnostics.Result(Diagnostics, result);
        TftpTestDiagnostics.Sent(Diagnostics, channel);
        return result;
    }

    private TransferContext Context(Stream? output = null, int? tftpBlockSize = null, bool noOptions = false)
    {
        Diagnostics.Arrange("url", "tftp://h/file.txt");
        Diagnostics.Arrange("block size", tftpBlockSize?.ToString(CultureInfo.InvariantCulture) ?? "(default)");
        Diagnostics.Arrange("no options", noOptions);
        return new()
        {
            Url = CurlUrl.Parse("tftp://h/file.txt"),
            Output = output ?? new MemoryStream(),
            TftpBlockSize = tftpBlockSize,
            TftpNoOptions = noOptions,
        };
    }

    private ScriptedDatagramChannel Channel(params (byte[] Datagram, EndPoint Source)[] script)
    {
        for (int index = 0; index < script.Length; index++)
        {
            TftpTestDiagnostics.Scripted(Diagnostics, index, script[index].Datagram, script[index].Source);
        }

        return new(ServerEndPoint, script);
    }

    private static byte[] ReadRequest(string fields) => [0, 1, .. Encoding.ASCII.GetBytes(fields)];

    private static byte[] WriteRequest(string fields) => [0, 2, .. Encoding.ASCII.GetBytes(fields)];

    private static (byte[] Datagram, EndPoint Source) Data(ushort block, string payload) =>
        ([0, 3, (byte)(block >> 8), (byte)block, .. Encoding.ASCII.GetBytes(payload)], TransferEndPoint);

    private static (byte[] Datagram, EndPoint Source) Ack(ushort block) =>
        ([0, 4, (byte)(block >> 8), (byte)block], TransferEndPoint);

    private static (byte[] Datagram, EndPoint Source) OptionAcknowledgement(string options) =>
        ([0, 6, .. Encoding.ASCII.GetBytes(options)], TransferEndPoint);
}
