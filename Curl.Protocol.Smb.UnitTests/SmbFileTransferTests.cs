using System.Buffers.Binary;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Smb.Fakes;

namespace Curl.Protocol.Smb;

/// <summary>
/// Pins <see cref="SmbFileTransfer" />'s outcomes beyond the handler's, from
/// <c>lib/smb.c</c>'s <c>smb_request_state</c> at <c>curl-8_21_0</c>: a refused frame at
/// each step (exit 56, nothing more sent), a tree connect or open too large (exit 63), an
/// open refused with ERRnoaccess (exit 9) or too short (exit 78), a negative size (exit 8),
/// a read whose data runs past its frame (exit 56), a failed output write (exit 23), and a
/// file read in several pieces; and for an upload, the pieces of a large or empty file,
/// a server writing fewer bytes than sent, a short write response (exit 25) and a refused
/// frame during or after the writes (exit 56).
/// </summary>
[TestClass]
public sealed class SmbFileTransferTests
{
    private const ushort UserId = 0x0064;

    [TestMethod]
    public async Task TransferAsync_HostTooLongForTheTreeConnect_Exits63WithNothingSent()
    {
        var connection = new ScriptedConnection();

        TransferResult result = await Transfer(connection).TransferAsync(new string('h', 1015), Path("/share/x"));

        Assert.AreEqual(CurlExitCode.FilesizeExceeded, result.ExitCode);
        Assert.AreEqual("Maximum file size exceeded", result.ErrorMessage);
        Assert.IsEmpty(connection.Sent);
    }

    [TestMethod]
    public async Task TransferAsync_PathTooLongForTheOpen_Exits63AfterTheTreeConnect()
    {
        var connection = new ScriptedConnection(SmbRecordedExchange.TreeConnectAccepted);

        TransferResult result = await Transfer(connection).TransferAsync(SmbRecordedExchange.Host, Path("/share/" + new string('f', 1024)));

        Assert.AreEqual(CurlExitCode.FilesizeExceeded, result.ExitCode);
        CollectionAssert.AreEqual(SmbRecordedExchange.TreeConnectRequest, connection.Sent);
    }

    [TestMethod]
    [DataRow(0, 1)]
    [DataRow(1, 2)]
    [DataRow(2, 3)]
    [DataRow(3, 4)]
    [DataRow(4, 5)]
    public async Task TransferAsync_FrameRefusedAtAStep_Exits56WithNothingMoreSent(int step, int requestsSent)
    {
        byte[][] replies = Replies();
        replies[step] = SmbRecordedExchange.TooSmallFrame;
        var connection = new ScriptedConnection(replies[..(step + 1)]);

        TransferResult result = await Transfer(connection).TransferAsync(SmbRecordedExchange.Host, DownloadPath());

        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual("too small NetBIOS frame size 5", result.ErrorMessage);
        CollectionAssert.AreEqual(Requests()[..requestsSent].SelectMany(request => request).ToArray(), connection.Sent);
    }

    [TestMethod]
    public async Task TransferAsync_OpenRefusedWithNoAccess_Exits9AfterDisconnecting()
    {
        byte[] refused = SmbRecordedExchange.OpenMissingFile;
        BinaryPrimitives.WriteUInt32LittleEndian(refused.AsSpan(9), 0x00050001);
        var connection = new ScriptedConnection(SmbRecordedExchange.TreeConnectAccepted, refused, SmbRecordedExchange.TreeDisconnectAccepted);

        TransferResult result = await Transfer(connection).TransferAsync(SmbRecordedExchange.Host, DownloadPath());

        Assert.AreEqual(CurlExitCode.RemoteAccessDenied, result.ExitCode);
        Assert.IsTrue(connection.Sent.AsSpan().EndsWith(SmbRecordedExchange.TreeDisconnectRequest));
    }

    [TestMethod]
    public async Task TransferAsync_OpenResponseOneByteShortOfCurlsStruct_Exits78()
    {
        byte[] opened = SmbRecordedExchange.OpenAccepted[..(SmbOpenResponse.Length - 1)];
        opened[3] = SmbOpenResponse.Length - 1 - SmbMessageHeader.NetBiosHeaderLength;
        var connection = new ScriptedConnection(SmbRecordedExchange.TreeConnectAccepted, opened, SmbRecordedExchange.TreeDisconnectAccepted);

        TransferResult result = await Transfer(connection).TransferAsync(SmbRecordedExchange.Host, DownloadPath());

        Assert.AreEqual(CurlExitCode.RemoteFileNotFound, result.ExitCode);
    }

    [TestMethod]
    public async Task TransferAsync_NegativeFileSize_Exits8AfterClosingWithoutReading()
    {
        byte[] opened = SmbRecordedExchange.OpenAccepted;
        BinaryPrimitives.WriteInt64LittleEndian(opened.AsSpan(92), -1);
        var connection = new ScriptedConnection(
            SmbRecordedExchange.TreeConnectAccepted, opened, SmbRecordedExchange.CloseAccepted, SmbRecordedExchange.TreeDisconnectAccepted);

        TransferResult result = await Transfer(connection).TransferAsync(SmbRecordedExchange.Host, DownloadPath());

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        Assert.AreEqual("Weird server reply", result.ErrorMessage);
        Assert.IsTrue(connection.Sent.AsSpan().EndsWith(
            [.. SmbRecordedExchange.OpenRequest, .. SmbRecordedExchange.CloseRequest, .. SmbRecordedExchange.TreeDisconnectRequest]));
    }

    [TestMethod]
    public async Task TransferAsync_DataRunningPastTheFrame_Exits56InvalidInputPacket()
    {
        byte[] read = SmbRecordedExchange.ReadAccepted;
        read[47]++;
        var connection = new ScriptedConnection(Replies(read));

        TransferResult result = await Transfer(connection).TransferAsync(SmbRecordedExchange.Host, DownloadPath());

        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual("Invalid input packet", result.ErrorMessage);
        CollectionAssert.AreEqual(Requests().SelectMany(request => request).ToArray(), connection.Sent);
    }

    [TestMethod]
    public async Task TransferAsync_EmptyFile_SucceedsWithNothingWritten()
    {
        var output = new MemoryStream();
        var connection = new ScriptedConnection(Replies(ReadResponse([])));

        TransferResult result = await Transfer(connection, output).TransferAsync(SmbRecordedExchange.Host, DownloadPath());

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(0L, output.Length);
    }

    [TestMethod]
    public async Task TransferAsync_FullRead_ReadsAgainFromWhereItEnded()
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

        TransferResult result = await Transfer(connection, output).TransferAsync(SmbRecordedExchange.Host, DownloadPath());

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(0x8000L + 11, result.BytesTransferred);
        Assert.AreEqual(0x8000L + 11, output.Length);
        byte[] secondRead = SmbReadRequest.Encode(UserId, 7, 0x4001, 0x8000);
        Assert.IsTrue(connection.Sent.AsSpan().EndsWith(
            [.. SmbRecordedExchange.ReadRequest, .. secondRead, .. SmbRecordedExchange.CloseRequest, .. SmbRecordedExchange.TreeDisconnectRequest]));
    }

    [TestMethod]
    public async Task TransferAsync_OutputRefusesPartOfAWrite_Exits23NamingWhatItAccepted()
    {
        var connection = new ScriptedConnection(Replies());
        var output = new FailingOutputStream(new OutputWriteFailedException(3, "full"));

        TransferResult result = await Transfer(connection, output).TransferAsync(SmbRecordedExchange.Host, DownloadPath());

        Assert.AreEqual(CurlExitCode.WriteError, result.ExitCode);
        Assert.AreEqual("Failure writing output to destination, passed 11 returned 3", result.ErrorMessage);
        CollectionAssert.AreEqual(Requests().SelectMany(request => request).ToArray(), connection.Sent);
    }

    [TestMethod]
    public async Task TransferAsync_OutputFailsWithAnotherIOException_Exits23Returned0()
    {
        var connection = new ScriptedConnection(Replies());

        TransferResult result = await Transfer(connection, new FailingOutputStream(new IOException("gone")))
            .TransferAsync(SmbRecordedExchange.Host, DownloadPath());

        Assert.AreEqual("Failure writing output to destination, passed 11 returned 0", result.ErrorMessage);
    }

    [TestMethod]
    public async Task TransferAsync_CloseRefused_StillSucceeds()
    {
        byte[] closeRefused = SmbRecordedExchange.CloseAccepted;
        closeRefused[12] = 0xc0;
        var connection = new ScriptedConnection(
            SmbRecordedExchange.TreeConnectAccepted,
            SmbRecordedExchange.OpenAccepted,
            SmbRecordedExchange.ReadAccepted,
            closeRefused,
            SmbRecordedExchange.TreeDisconnectAccepted);

        TransferResult result = await Transfer(connection).TransferAsync(SmbRecordedExchange.Host, DownloadPath());

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    [TestMethod]
    public async Task TransferAsync_UploadLargerThanOneWrite_WritesCurlsPiecesInOrder()
    {
        byte[] source = new byte[40000];
        source.AsSpan().Fill((byte)'a');
        var progress = new RecordingProgress();
        var connection = new ScriptedConnection(UploadReplies(
            SmbRecordedExchange.WriteAccepted(0x7fff), SmbRecordedExchange.WriteAccepted(0x1c41)));

        TransferResult result = await Upload(connection, source, progress).TransferAsync(SmbRecordedExchange.Host, DownloadPath());

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(40000L, result.Report!.UploadSize);
        CollectionAssert.AreEqual(new (long, long?)[] { (0x7fff, 40000), (40000, 40000) }, progress.Uploads.ToArray());
        byte[] sent = connection.Sent;
        int first = SmbRecordedExchange.TreeConnectRequest.Length + SmbRecordedExchange.UploadOpenRequest.Length;
        int second = first + 68 + 0x7fff;
        CollectionAssert.AreEqual(SmbRecordedExchange.LargeWriteHeaders[0], sent[first..(first + 68)]);
        CollectionAssert.AreEqual(SmbRecordedExchange.LargeWriteHeaders[1], sent[second..(second + 68)]);
        Assert.IsTrue(sent.AsSpan((first + 68)..second).IndexOfAnyExcept((byte)'a') < 0);
        Assert.IsTrue(sent.AsSpan().EndsWith(
            [.. Enumerable.Repeat((byte)'a', 0x1c41), .. SmbRecordedExchange.CloseRequest, .. SmbRecordedExchange.TreeDisconnectRequest]));
    }

    [TestMethod]
    public async Task TransferAsync_UploadEmptyFile_SendsOneWriteOfNoData()
    {
        var connection = new ScriptedConnection(UploadReplies(SmbRecordedExchange.WriteAccepted(0)));

        TransferResult result = await Upload(connection, []).TransferAsync(SmbRecordedExchange.Host, DownloadPath());

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(0L, result.Report!.UploadSize);
        Assert.IsTrue(connection.Sent.AsSpan().EndsWith(
            [.. SmbRecordedExchange.EmptyWriteRequest, .. SmbRecordedExchange.CloseRequest, .. SmbRecordedExchange.TreeDisconnectRequest]));
    }

    [TestMethod]
    public async Task TransferAsync_ServerWritesFewerBytesThanSent_WritesTheRestFromItsCount()
    {
        var connection = new ScriptedConnection(UploadReplies(
            SmbRecordedExchange.WriteAccepted(5), SmbRecordedExchange.WriteAccepted(6)));

        TransferResult result = await Upload(connection, Encoding.ASCII.GetBytes(SmbRecordedExchange.FileContent))
            .TransferAsync(SmbRecordedExchange.Host, DownloadPath());

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsTrue(connection.Sent.AsSpan().EndsWith(
            [.. SmbRecordedExchange.WriteRequest, .. SmbRecordedExchange.ShortWriteRequest, .. SmbRecordedExchange.CloseRequest, .. SmbRecordedExchange.TreeDisconnectRequest]));
    }

    [TestMethod]
    public async Task TransferAsync_WriteResponseTooShortForItsCount_Exits25()
    {
        byte[] shortResponse = SmbRecordedExchange.WriteAccepted(11)[..41];
        shortResponse[3] = 41 - SmbMessageHeader.NetBiosHeaderLength;
        var connection = new ScriptedConnection(UploadReplies(shortResponse));

        TransferResult result = await Upload(connection, Encoding.ASCII.GetBytes(SmbRecordedExchange.FileContent))
            .TransferAsync(SmbRecordedExchange.Host, DownloadPath());

        Assert.AreEqual(CurlExitCode.UploadFailed, result.ExitCode);
    }

    [TestMethod]
    public async Task TransferAsync_WriteFrameRefused_Exits56WithNothingMoreSent()
    {
        var connection = new ScriptedConnection(
            SmbRecordedExchange.TreeConnectAccepted, SmbRecordedExchange.UploadOpenCreated, SmbRecordedExchange.TooSmallFrame);

        TransferResult result = await Upload(connection, Encoding.ASCII.GetBytes(SmbRecordedExchange.FileContent))
            .TransferAsync(SmbRecordedExchange.Host, DownloadPath());

        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual(0L, result.Report!.UploadSize);
        Assert.IsTrue(connection.Sent.AsSpan().EndsWith(SmbRecordedExchange.WriteRequest));
    }

    [TestMethod]
    public async Task TransferAsync_CloseFrameRefusedAfterAnUpload_Exits56KeepingTheBytesUploaded()
    {
        var connection = new ScriptedConnection(
            SmbRecordedExchange.TreeConnectAccepted,
            SmbRecordedExchange.UploadOpenCreated,
            SmbRecordedExchange.WriteAccepted(11),
            SmbRecordedExchange.TooSmallFrame);

        TransferResult result = await Upload(connection, Encoding.ASCII.GetBytes(SmbRecordedExchange.FileContent))
            .TransferAsync(SmbRecordedExchange.Host, DownloadPath());

        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual(11L, result.BytesTransferred);
        Assert.AreEqual(11L, result.Report!.UploadSize);
    }

    private static SmbFileTransfer Upload(ScriptedConnection connection, byte[] source, RecordingProgress? progress = null) =>
        new(
            connection,
            new SmbMessageReader(connection, TimeProvider.System),
            UserId,
            new TransferContext
            {
                Url = CurlUrl.Parse(SmbRecordedExchange.UploadUrl),
                Output = new MemoryStream(),
                Upload = new MemoryStream(source),
                Progress = progress ?? new RecordingProgress(),
            });

    private static byte[][] UploadReplies(params byte[][] writes) =>
    [
        SmbRecordedExchange.TreeConnectAccepted,
        SmbRecordedExchange.UploadOpenCreated,
        .. writes,
        SmbRecordedExchange.CloseAccepted,
        SmbRecordedExchange.TreeDisconnectAccepted,
    ];

    private static SmbFileTransfer Transfer(ScriptedConnection connection, Stream? output = null) =>
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
