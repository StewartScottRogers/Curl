using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Tftp.Fakes;

namespace Curl.Protocol.Tftp;

/// <summary>
/// Pins the <c>tftp://</c> upload (<c>-T</c>) against curl 8.21.0, measured on 2026-09-26
/// against a loopback UDP server that answered from a second port: the write request
/// bytes, where and when each DATA block goes, the zero-length last block, the
/// <c>tsize</c> of an upload that cannot seek, an empty upload, and ERROR packets.
/// </summary>
[TestClass]
public sealed class TftpUploadTests
{
    /// <summary>The write request curl 8.21.0 sent for 3 bytes to <c>tftp://h/dest.txt</c>.</summary>
    private static readonly byte[] ExpectedAbcWriteRequest =
    [
        0, 2,
        .. "dest.txt\0octet\0tsize\03\0blksize\0512\0timeout\06\0"u8,
    ];

    /// <summary>The write request curl 8.21.0 sent when the upload's length was unknown or 0.</summary>
    private static readonly byte[] ExpectedTsizeZeroWriteRequest =
    [
        0, 2,
        .. "dest.txt\0octet\0tsize\00\0blksize\0512\0timeout\06\0"u8,
    ];

    private static readonly IPEndPoint ServerEndPoint = new(IPAddress.Loopback, 69);

    /// <summary>The server's transfer identifier: the new port it answers from.</summary>
    private static readonly IPEndPoint TransferEndPoint = new(IPAddress.Loopback, 50123);

    [TestMethod]
    public async Task ExecuteAsync_UploadAbc_SendsCurlsWriteRequestThenData1ToTheEndPointAck0CameFrom()
    {
        var channel = Channel(Ack(0), Ack(1));
        var output = new MemoryStream();

        var result = await Run(channel, new MemoryStream("abc"u8.ToArray()), output);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(3, result.BytesTransferred);
        Assert.AreEqual(0, output.Length);
        Assert.HasCount(2, channel.Sent);
        CollectionAssert.AreEqual(ExpectedAbcWriteRequest, channel.Sent[0].Datagram);
        Assert.AreEqual(ServerEndPoint, channel.Sent[0].Destination);
        CollectionAssert.AreEqual(Data(1, "abc"), channel.Sent[1].Datagram);
        Assert.AreEqual(TransferEndPoint, channel.Sent[1].Destination);
        Assert.IsTrue(channel.IsDisposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_Upload1100Bytes_SendsBlocksOf512And512And76EachAfterThePreviousAck()
    {
        var content = Encoding.ASCII.GetBytes(new string('a', 512) + new string('b', 512) + new string('c', 76));
        var channel = new AcknowledgingDatagramChannel(ServerEndPoint, TransferEndPoint);

        var result = await Run(channel, new MemoryStream(content));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(1100, result.BytesTransferred);
        CollectionAssert.AreEqual(new[] { (1, 512, true), (2, 512, true), (3, 76, true) }, channel.DataBlocks);
        CollectionAssert.AreEqual(content, channel.Payload);
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadExactly512Bytes_SendsTsize512AndEndsWithZeroLengthData2()
    {
        var channel = Channel(Ack(0), Ack(1), Ack(2));

        var result = await Run(channel, new MemoryStream(new byte[512]));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(512, result.BytesTransferred);
        StringAssert.Contains(Encoding.ASCII.GetString(channel.Sent[0].Datagram), "\0tsize\0512\0");
        Assert.HasCount(3, channel.Sent);
        Assert.HasCount(516, channel.Sent[1].Datagram);
        CollectionAssert.AreEqual(Data(2, string.Empty), channel.Sent[2].Datagram);
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadCannotSeek_SendsTsizeZero()
    {
        var channel = Channel(Ack(0), Ack(1));

        var result = await Run(channel, new NonSeekableReadStream("abc"u8.ToArray()));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(ExpectedTsizeZeroWriteRequest, channel.Sent[0].Datagram);
        CollectionAssert.AreEqual(Data(1, "abc"), channel.Sent[1].Datagram);
    }

    [TestMethod]
    public async Task ExecuteAsync_EmptyUpload_SendsTsizeZeroAndOneEmptyData1()
    {
        var channel = Channel(Ack(0), Ack(1));

        var result = await Run(channel, new MemoryStream());

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(0, result.BytesTransferred);
        CollectionAssert.AreEqual(ExpectedTsizeZeroWriteRequest, channel.Sent[0].Datagram);
        Assert.HasCount(2, channel.Sent);
        CollectionAssert.AreEqual(Data(1, string.Empty), channel.Sent[1].Datagram);
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadArrivesInSmallReads_FillsEachBlockBeforeSendingIt()
    {
        var channel = Channel(Ack(0), Ack(1), Ack(2));

        var result = await Run(channel, new NonSeekableReadStream(new byte[600], chunkSize: 100));

        Assert.AreEqual(600, result.BytesTransferred);
        Assert.HasCount(516, channel.Sent[1].Datagram);
        Assert.HasCount(92, channel.Sent[2].Datagram);
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadPositionedPastStart_SendsRemainingLengthAsTsize()
    {
        var upload = new MemoryStream("xxabc"u8.ToArray()) { Position = 2 };
        var channel = Channel(Ack(0), Ack(1));

        await Run(channel, upload);

        CollectionAssert.AreEqual(ExpectedAbcWriteRequest, channel.Sent[0].Datagram);
        CollectionAssert.AreEqual(Data(1, "abc"), channel.Sent[1].Datagram);
    }

    [TestMethod]
    public async Task ExecuteAsync_OptionAcknowledgementOfWriteRequest_SendsData1InTheAcknowledgedBlockSize()
    {
        var channel = Channel(OptionAcknowledgement("blksize\08\0"), Ack(1), Ack(2));

        var result = await Run(channel, new NonSeekableReadStream("abcdefghijk"u8.ToArray()));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(11, result.BytesTransferred);
        CollectionAssert.AreEqual(Data(1, "abcdefgh"), channel.Sent[1].Datagram);
        Assert.AreEqual(TransferEndPoint, channel.Sent[1].Destination);
        CollectionAssert.AreEqual(Data(2, "ijk"), channel.Sent[2].Datagram);
    }

    [TestMethod]
    public async Task ExecuteAsync_ErrorCode6InReplyToWriteRequest_ReturnsExit73RemoteFileAlreadyExists()
    {
        var channel = Channel(Error(6, "File already exists"));

        var result = await Run(channel, new MemoryStream("abc"u8.ToArray()));

        Assert.AreEqual(CurlExitCode.RemoteFileExists, result.ExitCode);
        Assert.AreEqual(73, (int)result.ExitCode);
        Assert.AreEqual("Remote file already exists", result.ErrorMessage);
        Assert.HasCount(1, channel.Sent);
        Assert.IsTrue(channel.IsDisposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_ErrorAfterFirstBlock_ReturnsTheMappedExitCode()
    {
        var channel = Channel(Ack(0), Error(3, "disk full"));

        var result = await Run(channel, new MemoryStream(new byte[600]));

        Assert.AreEqual(CurlExitCode.RemoteDiskFull, result.ExitCode);
        Assert.AreEqual("Disk full or allocation exceeded", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_UnexpectedOpcode_IsIgnored()
    {
        var channel = Channel(Ack(0), ([0, 3, 0, 1], TransferEndPoint), Ack(1));

        var result = await Run(channel, new MemoryStream("abc"u8.ToArray()));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(3, result.BytesTransferred);
        Assert.HasCount(2, channel.Sent);
        CollectionAssert.AreEqual(Data(1, "abc"), channel.Sent[1].Datagram);
    }

    /// <summary>
    /// curl 8.21.0, 1000 bytes, server answering DATA 1 with <c>OACK blksize 8</c>: the next
    /// DATA was block 1 again, carrying bytes 512-519, then block 2 with bytes 520-527; the
    /// bytes already sent are not re-read.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_OptionAckAfterData1_RestartsAtBlock1WithTheNextBytesInTheAcknowledgedBlockSize()
    {
        var content = Encoding.ASCII.GetBytes(new string('a', 512) + "bcdefghijk");
        var channel = Channel(Ack(0), OptionAcknowledgement("blksize\08\0"), Ack(1), Ack(2));

        var result = await Run(channel, new MemoryStream(content));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(522, result.BytesTransferred);
        Assert.HasCount(4, channel.Sent);
        CollectionAssert.AreEqual(Data(1, new string('a', 512)), channel.Sent[1].Datagram);
        CollectionAssert.AreEqual(Data(1, "bcdefghi"), channel.Sent[2].Datagram);
        CollectionAssert.AreEqual(Data(2, "jk"), channel.Sent[3].Datagram);
        Assert.AreEqual(TransferEndPoint, channel.Sent[2].Destination);
    }

    /// <summary>
    /// curl 8.21.0, 3 bytes, server answering DATA 1 with <c>OACK blksize 8</c>: curl sent an
    /// empty DATA 1, and ACK 1 ended the upload with exit 0.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_OptionAckAfterTheLastBlock_SendsAnEmptyData1()
    {
        var channel = Channel(Ack(0), OptionAcknowledgement("blksize\08\0"), Ack(1));

        var result = await Run(channel, new MemoryStream("abc"u8.ToArray()));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(3, result.BytesTransferred);
        Assert.HasCount(3, channel.Sent);
        CollectionAssert.AreEqual(Data(1, "abc"), channel.Sent[1].Datagram);
        CollectionAssert.AreEqual(Data(1, string.Empty), channel.Sent[2].Datagram);
    }

    private static async Task<TransferResult> Run(IDatagramChannel channel, Stream upload, Stream? output = null) =>
        await new TftpProtocolHandler(new RecordingDatagramConnector(DatagramOpenResult.Opened(channel)))
            .ExecuteAsync(new TransferContext
            {
                Url = CurlUrl.Parse("tftp://h/dest.txt"),
                Output = output ?? new MemoryStream(),
                Upload = upload,
            });

    private static ScriptedDatagramChannel Channel(params (byte[] Datagram, EndPoint Source)[] script) =>
        new(ServerEndPoint, script);

    private static byte[] Data(ushort block, string payload) =>
        [0, 3, (byte)(block >> 8), (byte)block, .. Encoding.ASCII.GetBytes(payload)];

    private static (byte[] Datagram, EndPoint Source) Ack(ushort block) =>
        ([0, 4, (byte)(block >> 8), (byte)block], TransferEndPoint);

    private static (byte[] Datagram, EndPoint Source) Error(ushort code, string message) =>
        ([0, 5, (byte)(code >> 8), (byte)code, .. Encoding.ASCII.GetBytes(message), 0], TransferEndPoint);

    private static (byte[] Datagram, EndPoint Source) OptionAcknowledgement(string options) =>
        ([0, 6, .. Encoding.ASCII.GetBytes(options)], TransferEndPoint);
}
