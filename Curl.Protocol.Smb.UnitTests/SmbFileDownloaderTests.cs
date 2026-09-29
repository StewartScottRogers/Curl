using System.Buffers.Binary;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Smb.Fakes;

namespace Curl.Protocol.Smb;

/// <summary>
/// Pins <see cref="SmbFileDownloader" />'s outcomes beyond the handler's, from
/// <c>lib/smb.c</c>'s <c>smb_request_state</c> at <c>curl-8_21_0</c>: a refused frame at
/// each step (exit 56, nothing more sent), a tree connect or open too large (exit 63), an
/// open refused with ERRnoaccess (exit 9) or too short (exit 78), a negative size (exit 8),
/// a read whose data runs past its frame (exit 56), a failed output write (exit 23), and a
/// file read in several pieces.
/// </summary>
[TestClass]
public sealed class SmbFileDownloaderTests
{
    private const ushort UserId = 0x0064;

    [TestMethod]
    public async Task DownloadAsync_HostTooLongForTheTreeConnect_Exits63WithNothingSent()
    {
        var connection = new ScriptedConnection();

        TransferResult result = await Downloader(connection).DownloadAsync(new string('h', 1015), Path("/share/x"));

        Assert.AreEqual(CurlExitCode.FilesizeExceeded, result.ExitCode);
        Assert.AreEqual("Maximum file size exceeded", result.ErrorMessage);
        Assert.IsEmpty(connection.Sent);
    }

    [TestMethod]
    public async Task DownloadAsync_PathTooLongForTheOpen_Exits63AfterTheTreeConnect()
    {
        var connection = new ScriptedConnection(SmbRecordedExchange.TreeConnectAccepted);

        TransferResult result = await Downloader(connection).DownloadAsync(SmbRecordedExchange.Host, Path("/share/" + new string('f', 1024)));

        Assert.AreEqual(CurlExitCode.FilesizeExceeded, result.ExitCode);
        CollectionAssert.AreEqual(SmbRecordedExchange.TreeConnectRequest, connection.Sent);
    }

    [TestMethod]
    [DataRow(0, 1)]
    [DataRow(1, 2)]
    [DataRow(2, 3)]
    [DataRow(3, 4)]
    [DataRow(4, 5)]
    public async Task DownloadAsync_FrameRefusedAtAStep_Exits56WithNothingMoreSent(int step, int requestsSent)
    {
        byte[][] replies = Replies();
        replies[step] = SmbRecordedExchange.TooSmallFrame;
        var connection = new ScriptedConnection(replies[..(step + 1)]);

        TransferResult result = await Downloader(connection).DownloadAsync(SmbRecordedExchange.Host, DownloadPath());

        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual("too small NetBIOS frame size 5", result.ErrorMessage);
        CollectionAssert.AreEqual(Requests()[..requestsSent].SelectMany(request => request).ToArray(), connection.Sent);
    }

    [TestMethod]
    public async Task DownloadAsync_OpenRefusedWithNoAccess_Exits9AfterDisconnecting()
    {
        byte[] refused = SmbRecordedExchange.OpenMissingFile;
        BinaryPrimitives.WriteUInt32LittleEndian(refused.AsSpan(9), 0x00050001);
        var connection = new ScriptedConnection(SmbRecordedExchange.TreeConnectAccepted, refused, SmbRecordedExchange.TreeDisconnectAccepted);

        TransferResult result = await Downloader(connection).DownloadAsync(SmbRecordedExchange.Host, DownloadPath());

        Assert.AreEqual(CurlExitCode.RemoteAccessDenied, result.ExitCode);
        Assert.IsTrue(connection.Sent.AsSpan().EndsWith(SmbRecordedExchange.TreeDisconnectRequest));
    }

    [TestMethod]
    public async Task DownloadAsync_OpenResponseOneByteShortOfCurlsStruct_Exits78()
    {
        byte[] opened = SmbRecordedExchange.OpenAccepted[..(SmbOpenResponse.Length - 1)];
        opened[3] = SmbOpenResponse.Length - 1 - SmbMessageHeader.NetBiosHeaderLength;
        var connection = new ScriptedConnection(SmbRecordedExchange.TreeConnectAccepted, opened, SmbRecordedExchange.TreeDisconnectAccepted);

        TransferResult result = await Downloader(connection).DownloadAsync(SmbRecordedExchange.Host, DownloadPath());

        Assert.AreEqual(CurlExitCode.RemoteFileNotFound, result.ExitCode);
    }

    [TestMethod]
    public async Task DownloadAsync_NegativeFileSize_Exits8AfterClosingWithoutReading()
    {
        byte[] opened = SmbRecordedExchange.OpenAccepted;
        BinaryPrimitives.WriteInt64LittleEndian(opened.AsSpan(92), -1);
        var connection = new ScriptedConnection(
            SmbRecordedExchange.TreeConnectAccepted, opened, SmbRecordedExchange.CloseAccepted, SmbRecordedExchange.TreeDisconnectAccepted);

        TransferResult result = await Downloader(connection).DownloadAsync(SmbRecordedExchange.Host, DownloadPath());

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        Assert.AreEqual("Weird server reply", result.ErrorMessage);
        Assert.IsTrue(connection.Sent.AsSpan().EndsWith(
            [.. SmbRecordedExchange.OpenRequest, .. SmbRecordedExchange.CloseRequest, .. SmbRecordedExchange.TreeDisconnectRequest]));
    }

    [TestMethod]
    public async Task DownloadAsync_DataRunningPastTheFrame_Exits56InvalidInputPacket()
    {
        byte[] read = SmbRecordedExchange.ReadAccepted;
        read[47]++;
        var connection = new ScriptedConnection(Replies(read));

        TransferResult result = await Downloader(connection).DownloadAsync(SmbRecordedExchange.Host, DownloadPath());

        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual("Invalid input packet", result.ErrorMessage);
        CollectionAssert.AreEqual(Requests().SelectMany(request => request).ToArray(), connection.Sent);
    }

    [TestMethod]
    public async Task DownloadAsync_EmptyFile_SucceedsWithNothingWritten()
    {
        var output = new MemoryStream();
        var connection = new ScriptedConnection(Replies(ReadResponse([])));

        TransferResult result = await Downloader(connection, output).DownloadAsync(SmbRecordedExchange.Host, DownloadPath());

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(0L, output.Length);
    }

    [TestMethod]
    public async Task DownloadAsync_FullRead_ReadsAgainFromWhereItEnded()
    {
        byte[] first = new byte[SmbReadRequest.MaxPayloadSize];
        first.AsSpan().Fill((byte)'a');
        var output = new MemoryStream();
        var connection = new ScriptedConnection(
            SmbRecordedExchange.TreeConnectAccepted,
            SmbRecordedExchange.OpenAccepted,
            ReadResponse(first),
            SmbRecordedExchange.ReadAccepted,
            SmbRecordedExchange.CloseAccepted,
            SmbRecordedExchange.TreeDisconnectAccepted);

        TransferResult result = await Downloader(connection, output).DownloadAsync(SmbRecordedExchange.Host, DownloadPath());

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(0x8000L + 11, result.BytesTransferred);
        Assert.AreEqual(0x8000L + 11, output.Length);
        byte[] secondRead = SmbReadRequest.Encode(UserId, 7, 0x4001, 0x8000);
        Assert.IsTrue(connection.Sent.AsSpan().EndsWith(
            [.. SmbRecordedExchange.ReadRequest, .. secondRead, .. SmbRecordedExchange.CloseRequest, .. SmbRecordedExchange.TreeDisconnectRequest]));
    }

    [TestMethod]
    public async Task DownloadAsync_OutputRefusesPartOfAWrite_Exits23NamingWhatItAccepted()
    {
        var connection = new ScriptedConnection(Replies());
        var output = new FailingOutputStream(new OutputWriteFailedException(3, "full"));

        TransferResult result = await Downloader(connection, output).DownloadAsync(SmbRecordedExchange.Host, DownloadPath());

        Assert.AreEqual(CurlExitCode.WriteError, result.ExitCode);
        Assert.AreEqual("Failure writing output to destination, passed 11 returned 3", result.ErrorMessage);
        CollectionAssert.AreEqual(Requests().SelectMany(request => request).ToArray(), connection.Sent);
    }

    [TestMethod]
    public async Task DownloadAsync_OutputFailsWithAnotherIOException_Exits23Returned0()
    {
        var connection = new ScriptedConnection(Replies());

        TransferResult result = await Downloader(connection, new FailingOutputStream(new IOException("gone")))
            .DownloadAsync(SmbRecordedExchange.Host, DownloadPath());

        Assert.AreEqual("Failure writing output to destination, passed 11 returned 0", result.ErrorMessage);
    }

    [TestMethod]
    public async Task DownloadAsync_CloseRefused_StillSucceeds()
    {
        byte[] closeRefused = SmbRecordedExchange.CloseAccepted;
        closeRefused[12] = 0xc0;
        var connection = new ScriptedConnection(
            SmbRecordedExchange.TreeConnectAccepted,
            SmbRecordedExchange.OpenAccepted,
            SmbRecordedExchange.ReadAccepted,
            closeRefused,
            SmbRecordedExchange.TreeDisconnectAccepted);

        TransferResult result = await Downloader(connection).DownloadAsync(SmbRecordedExchange.Host, DownloadPath());

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    private static SmbFileDownloader Downloader(ScriptedConnection connection, Stream? output = null) =>
        new(
            connection,
            new SmbMessageReader(connection, TimeProvider.System),
            UserId,
            new TransferContext { Url = CurlUrl.Parse(SmbRecordedExchange.DownloadUrl), Output = output ?? new MemoryStream() });

    private static SmbUrlPath Path(string absolutePath)
    {
        SmbUrlPath.TryParse(absolutePath, out SmbUrlPath? path);
        return path!;
    }

    private static SmbUrlPath DownloadPath() => Path("/share/dir/x.txt");

    private static byte[][] Replies(byte[]? read = null) =>
    [
        SmbRecordedExchange.TreeConnectAccepted,
        SmbRecordedExchange.OpenAccepted,
        read ?? SmbRecordedExchange.ReadAccepted,
        SmbRecordedExchange.CloseAccepted,
        SmbRecordedExchange.TreeDisconnectAccepted,
    ];

    private static byte[][] Requests() =>
    [
        SmbRecordedExchange.TreeConnectRequest,
        SmbRecordedExchange.OpenRequest,
        SmbRecordedExchange.ReadRequest,
        SmbRecordedExchange.CloseRequest,
        SmbRecordedExchange.TreeDisconnectRequest,
    ];

    // The measured read response, carrying data in place of its 11 bytes.
    private static byte[] ReadResponse(byte[] data)
    {
        const int DataStart = SmbMessageHeader.Length + 27;
        byte[] reply = new byte[DataStart + data.Length];
        SmbRecordedExchange.ReadAccepted.AsSpan(0, DataStart).CopyTo(reply);
        BinaryPrimitives.WriteUInt16BigEndian(reply.AsSpan(2), (ushort)(reply.Length - SmbMessageHeader.NetBiosHeaderLength));
        BinaryPrimitives.WriteUInt16LittleEndian(reply.AsSpan(SmbMessageHeader.Length + 11), (ushort)data.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(reply.AsSpan(SmbMessageHeader.Length + 25), (ushort)data.Length);
        data.CopyTo(reply.AsSpan(DataStart));
        return reply;
    }
}
