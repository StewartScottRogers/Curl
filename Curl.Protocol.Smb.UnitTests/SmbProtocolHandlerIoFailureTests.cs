using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Smb.Fakes;

namespace Curl.Protocol.Smb;

/// <summary>
/// Pins an SMB transfer whose connection breaks (BL-1231), from <c>lib/smb.c</c> at
/// <c>curl-8_21_0</c>: a write that throws is exit 55, <c>Send failure: Connection was
/// reset</c> for a reset and <c>Failed sending data to the peer</c> otherwise; a read that
/// throws is exit 56, <c>Recv failure: Connection was reset</c> or <c>Failure when receiving
/// data from the peer</c>, keeping the bytes already written; the reset texts get a
/// <c>-v</c> line and the fallback texts do not.
/// </summary>
[TestClass]
public sealed class SmbProtocolHandlerIoFailureTests
{
    private static readonly NetworkCredential User = new("User", "Password");

    // The number of each request's write, and of each reply's read, in a download or upload.
    private const int NegotiateWrite = 1;
    private const int SessionSetupWrite = 2;
    private const int TreeConnectWrite = 3;
    private const int UploadWriteRequestWrite = 5;
    private const int NegotiateRead = 1;
    private const int SecondReadReplyRead = 6;

    [TestMethod]
    [DataRow(NegotiateWrite, true, "Send failure: Connection was reset")]
    [DataRow(NegotiateWrite, false, "Failed sending data to the peer")]
    [DataRow(SessionSetupWrite, true, "Send failure: Connection was reset")]
    [DataRow(SessionSetupWrite, false, "Failed sending data to the peer")]
    [DataRow(TreeConnectWrite, true, "Send failure: Connection was reset")]
    [DataRow(TreeConnectWrite, false, "Failed sending data to the peer")]
    public async Task ExecuteAsync_DownloadWriteThrows_Exits55(int failingWrite, bool reset, string message)
    {
        var connection = new ScriptedConnection(DownloadReplies()) { FailingWrite = failingWrite, Failure = Broken(reset) };

        TransferResult result = await Handler(connection).ExecuteAsync(Context(SmbRecordedExchange.DownloadUrl));

        Assert.AreEqual(CurlExitCode.SendError, result.ExitCode);
        Assert.AreEqual(message, result.ErrorMessage);
        Assert.AreEqual(0L, result.BytesTransferred);
        Assert.IsTrue(connection.IsDisposed);
    }

    [TestMethod]
    [DataRow(true, "Send failure: Connection was reset")]
    [DataRow(false, "Failed sending data to the peer")]
    public async Task ExecuteAsync_UploadWriteRequestThrows_Exits55WithNothingMoreSent(bool reset, string message)
    {
        var connection = new ScriptedConnection(
            SmbRecordedExchange.NegotiateResponse,
            SmbRecordedExchange.SessionSetupAccepted,
            SmbRecordedExchange.TreeConnectAccepted,
            SmbRecordedExchange.UploadOpenCreated,
            SmbRecordedExchange.WriteAccepted(11),
            SmbRecordedExchange.CloseAccepted,
            SmbRecordedExchange.TreeDisconnectAccepted)
        { FailingWrite = UploadWriteRequestWrite, Failure = Broken(reset) };
        var context = Context(SmbRecordedExchange.UploadUrl, upload: new MemoryStream(Encoding.ASCII.GetBytes(SmbRecordedExchange.FileContent)));

        TransferResult result = await Handler(connection).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.SendError, result.ExitCode);
        Assert.AreEqual(message, result.ErrorMessage);
        Assert.AreEqual(0L, result.Report!.UploadSize);
        Assert.IsTrue(connection.Sent.AsSpan().EndsWith(SmbRecordedExchange.UploadOpenRequest));
    }

    [TestMethod]
    [DataRow(true, "Recv failure: Connection was reset")]
    [DataRow(false, "Failure when receiving data from the peer")]
    public async Task ExecuteAsync_NegotiateReplyReadThrows_Exits56(bool reset, string message)
    {
        var connection = new ScriptedConnection(DownloadReplies()) { FailingRead = NegotiateRead, Failure = Broken(reset) };

        TransferResult result = await Handler(connection).ExecuteAsync(Context(SmbRecordedExchange.DownloadUrl));

        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual(message, result.ErrorMessage);
        CollectionAssert.AreEqual(SmbRecordedExchange.NegotiateRequest, connection.Sent);
    }

    [TestMethod]
    [DataRow(true, "Recv failure: Connection was reset")]
    [DataRow(false, "Failure when receiving data from the peer")]
    public async Task ExecuteAsync_ReadReplyThrowsAfterAFullRead_Exits56CountingTheBytesWritten(bool reset, string message)
    {
        byte[] first = new byte[SmbReadRequest.MaxPayloadSize];
        var output = new MemoryStream();
        var connection = new ScriptedConnection(
            SmbRecordedExchange.NegotiateResponse,
            SmbRecordedExchange.SessionSetupAccepted,
            SmbRecordedExchange.TreeConnectAccepted,
            SmbRecordedExchange.OpenAccepted,
            ReadResponse(first),
            SmbRecordedExchange.ReadAccepted)
        { FailingRead = SecondReadReplyRead, Failure = Broken(reset) };

        TransferResult result = await Handler(connection).ExecuteAsync(Context(SmbRecordedExchange.DownloadUrl, output: output));

        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual(message, result.ErrorMessage);
        Assert.AreEqual((long)SmbReadRequest.MaxPayloadSize, result.BytesTransferred);
        Assert.AreEqual((long)SmbReadRequest.MaxPayloadSize, output.Length);
    }

    [TestMethod]
    public async Task ExecuteAsync_ResetBeforeTheSession_ReportsTheTextThenClosing()
    {
        var events = new TranscriptTransferEvents();
        var connection = new ScriptedConnection(DownloadReplies()) { FailingWrite = SessionSetupWrite, Failure = Broken(reset: true) };

        await Handler(connection).ExecuteAsync(Context(SmbRecordedExchange.DownloadUrl, events));

        CollectionAssert.AreEqual(new[] { "* Send failure: Connection was reset", "* closing connection #0" }, events.Transcript.ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_ResetAfterTheSession_ReportsTheTextThenShuttingDown()
    {
        var events = new TranscriptTransferEvents();
        var connection = new ScriptedConnection(DownloadReplies()) { FailingWrite = TreeConnectWrite, Failure = Broken(reset: true) };

        await Handler(connection).ExecuteAsync(Context(SmbRecordedExchange.DownloadUrl, events));

        CollectionAssert.AreEqual(new[] { "* Send failure: Connection was reset", "* shutting down connection #0" }, events.Transcript.ToArray());
    }

    [TestMethod]
    [DataRow(NegotiateWrite, 0)]
    [DataRow(0, NegotiateRead)]
    public async Task ExecuteAsync_FallbackText_ReportsOnlyTheConnectionEnd(int failingWrite, int failingRead)
    {
        var events = new TranscriptTransferEvents();
        var connection = new ScriptedConnection(DownloadReplies())
        {
            FailingWrite = failingWrite,
            FailingRead = failingRead,
            Failure = Broken(reset: false),
        };

        await Handler(connection).ExecuteAsync(Context(SmbRecordedExchange.DownloadUrl, events));

        CollectionAssert.AreEqual(new[] { "* closing connection #0" }, events.Transcript.ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_CancelledToken_StillThrows()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var connection = new ScriptedConnection(DownloadReplies()) { FailingWrite = NegotiateWrite };
        var context = Context(SmbRecordedExchange.DownloadUrl, cancellationToken: cancellation.Token);

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await Handler(connection).ExecuteAsync(context));
    }

    private static IOException Broken(bool reset) =>
        reset
            ? new IOException("Unable to write data to the transport connection.", new SocketException((int)SocketError.ConnectionReset))
            : new IOException("The connection broke.");

    private static byte[][] DownloadReplies() =>
    [
        SmbRecordedExchange.NegotiateResponse,
        SmbRecordedExchange.SessionSetupAccepted,
        SmbRecordedExchange.TreeConnectAccepted,
        SmbRecordedExchange.OpenAccepted,
        SmbRecordedExchange.ReadAccepted,
        SmbRecordedExchange.CloseAccepted,
        SmbRecordedExchange.TreeDisconnectAccepted,
    ];

    private static SmbProtocolHandler Handler(ScriptedConnection connection) =>
        new(new RecordingConnector(ConnectResult.Connected(connection)), SmbCurlOperatingSystem.Linux);

    private static TransferContext Context(
        string url,
        ITransferEvents? events = null,
        Stream? output = null,
        Stream? upload = null,
        CancellationToken cancellationToken = default) => new()
        {
            Upload = upload,
            CancellationToken = cancellationToken,
            Url = CurlUrl.Parse(url),
            Output = output ?? new MemoryStream(),
            Credentials = User,
            Events = events ?? NoTransferEvents.Instance,
        };

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
