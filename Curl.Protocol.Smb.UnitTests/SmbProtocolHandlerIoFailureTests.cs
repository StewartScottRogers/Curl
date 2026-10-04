using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Smb.Fakes;

namespace Curl.Protocol.Smb;

/// <summary>
/// Pins an SMB transfer whose connection breaks (BL-1231, BL-1344), from <c>lib/smb.c</c> at
/// <c>curl-8_21_0</c>: a write that throws is exit 55, <c>Send failure: &lt;words&gt;</c> for
/// any socket error (Winsock words on Windows, the error's own message elsewhere) and
/// <c>Failed sending data to the peer</c> otherwise; a read that throws is exit 56,
/// <c>Recv failure: &lt;words&gt;</c> or <c>Failure when receiving data from the peer</c>,
/// keeping the bytes already written; the socket texts get a <c>-v</c> line and the fallback
/// texts do not.
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
    [OSCondition(OperatingSystems.Windows)]
    [DataRow(NegotiateWrite, SocketError.ConnectionReset, "Connection was reset")]
    [DataRow(SessionSetupWrite, SocketError.ConnectionReset, "Connection was reset")]
    [DataRow(TreeConnectWrite, SocketError.ConnectionReset, "Connection was reset")]
    [DataRow(SessionSetupWrite, SocketError.ConnectionAborted, "Connection was aborted")]
    public async Task ExecuteAsync_DownloadWriteSocketErrorOnWindows_Exits55WithWinsockWords(int failingWrite, SocketError error, string words)
    {
        TransferResult result = await DownloadWithFailingWrite(failingWrite, Broken(error));

        Assert.AreEqual("Send failure: " + words, result.ErrorMessage);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    [DataRow(NegotiateWrite, SocketError.ConnectionReset)]
    [DataRow(SessionSetupWrite, SocketError.ConnectionReset)]
    [DataRow(TreeConnectWrite, SocketError.ConnectionReset)]
    [DataRow(SessionSetupWrite, SocketError.ConnectionAborted)]
    public async Task ExecuteAsync_DownloadWriteSocketErrorOffWindows_Exits55WithTheErrorsOwnMessage(int failingWrite, SocketError error)
    {
        TransferResult result = await DownloadWithFailingWrite(failingWrite, Broken(error));

        Assert.AreEqual("Send failure: " + OwnWords(error), result.ErrorMessage);
    }

    [TestMethod]
    [DataRow(NegotiateWrite)]
    [DataRow(SessionSetupWrite)]
    [DataRow(TreeConnectWrite)]
    public async Task ExecuteAsync_DownloadWriteThrowsWithNoSocketError_Exits55FailedSendingData(int failingWrite)
    {
        TransferResult result = await DownloadWithFailingWrite(failingWrite, Broken());

        Assert.AreEqual("Failed sending data to the peer", result.ErrorMessage);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ExecuteAsync_UploadWriteRequestResetOnWindows_Exits55WithWinsockWords() =>
        await AssertUploadWriteRequestFails(Broken(SocketError.ConnectionReset), "Send failure: Connection was reset");

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task ExecuteAsync_UploadWriteRequestResetOffWindows_Exits55WithTheErrorsOwnMessage() =>
        await AssertUploadWriteRequestFails(Broken(SocketError.ConnectionReset), "Send failure: " + OwnWords(SocketError.ConnectionReset));

    [TestMethod]
    public async Task ExecuteAsync_UploadWriteRequestThrowsWithNoSocketError_Exits55FailedSendingData() =>
        await AssertUploadWriteRequestFails(Broken(), "Failed sending data to the peer");

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DataRow(SocketError.ConnectionReset, "Connection was reset")]
    [DataRow(SocketError.ConnectionAborted, "Connection was aborted")]
    public async Task ExecuteAsync_NegotiateReplyReadSocketErrorOnWindows_Exits56WithWinsockWords(SocketError error, string words) =>
        await AssertNegotiateReplyReadFails(Broken(error), "Recv failure: " + words);

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    [DataRow(SocketError.ConnectionReset)]
    [DataRow(SocketError.ConnectionAborted)]
    public async Task ExecuteAsync_NegotiateReplyReadSocketErrorOffWindows_Exits56WithTheErrorsOwnMessage(SocketError error) =>
        await AssertNegotiateReplyReadFails(Broken(error), "Recv failure: " + OwnWords(error));

    [TestMethod]
    public async Task ExecuteAsync_NegotiateReplyReadThrowsWithNoSocketError_Exits56FailureWhenReceiving() =>
        await AssertNegotiateReplyReadFails(Broken(), "Failure when receiving data from the peer");

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ExecuteAsync_ReadReplyResetAfterAFullReadOnWindows_Exits56CountingTheBytesWritten() =>
        await AssertReadReplyFailsAfterAFullRead(Broken(SocketError.ConnectionReset), "Recv failure: Connection was reset");

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task ExecuteAsync_ReadReplyResetAfterAFullReadOffWindows_Exits56CountingTheBytesWritten() =>
        await AssertReadReplyFailsAfterAFullRead(Broken(SocketError.ConnectionReset), "Recv failure: " + OwnWords(SocketError.ConnectionReset));

    [TestMethod]
    public async Task ExecuteAsync_ReadReplyThrowsWithNoSocketErrorAfterAFullRead_Exits56CountingTheBytesWritten() =>
        await AssertReadReplyFailsAfterAFullRead(Broken(), "Failure when receiving data from the peer");

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ExecuteAsync_ResetBeforeTheSessionOnWindows_ReportsTheTextThenClosing() =>
        CollectionAssert.AreEqual(
            new[] { "* Send failure: Connection was reset", "* closing connection #0" },
            await TranscriptOfResetWrite(SessionSetupWrite));

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task ExecuteAsync_ResetBeforeTheSessionOffWindows_ReportsTheTextThenClosing() =>
        CollectionAssert.AreEqual(
            new[] { "* Send failure: " + OwnWords(SocketError.ConnectionReset), "* closing connection #0" },
            await TranscriptOfResetWrite(SessionSetupWrite));

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ExecuteAsync_ResetAfterTheSessionOnWindows_ReportsTheTextThenShuttingDown() =>
        CollectionAssert.AreEqual(
            new[] { "* Send failure: Connection was reset", "* shutting down connection #0" },
            await TranscriptOfResetWrite(TreeConnectWrite));

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task ExecuteAsync_ResetAfterTheSessionOffWindows_ReportsTheTextThenShuttingDown() =>
        CollectionAssert.AreEqual(
            new[] { "* Send failure: " + OwnWords(SocketError.ConnectionReset), "* shutting down connection #0" },
            await TranscriptOfResetWrite(TreeConnectWrite));

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
            Failure = Broken(),
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

    private static IOException Broken(SocketError error) =>
        new("Unable to write data to the transport connection.", new SocketException((int)error));

    private static IOException Broken() => new("The connection broke.");

    // The words curl's OpenSSL build takes from strerror, which SocketException.Message is off Windows.
    private static string OwnWords(SocketError error) => new SocketException((int)error).Message;

    private static async Task<TransferResult> DownloadWithFailingWrite(int failingWrite, IOException failure)
    {
        var connection = new ScriptedConnection(DownloadReplies()) { FailingWrite = failingWrite, Failure = failure };

        TransferResult result = await Handler(connection).ExecuteAsync(Context(SmbRecordedExchange.DownloadUrl));

        Assert.AreEqual(CurlExitCode.SendError, result.ExitCode);
        Assert.AreEqual(0L, result.BytesTransferred);
        Assert.IsTrue(connection.IsDisposed);
        return result;
    }

    private static async Task AssertUploadWriteRequestFails(IOException failure, string message)
    {
        var connection = new ScriptedConnection(
            SmbRecordedExchange.NegotiateResponse,
            SmbRecordedExchange.SessionSetupAccepted,
            SmbRecordedExchange.TreeConnectAccepted,
            SmbRecordedExchange.UploadOpenCreated,
            SmbRecordedExchange.WriteAccepted(11),
            SmbRecordedExchange.CloseAccepted,
            SmbRecordedExchange.TreeDisconnectAccepted)
        { FailingWrite = UploadWriteRequestWrite, Failure = failure };
        var context = Context(SmbRecordedExchange.UploadUrl, upload: new MemoryStream(Encoding.ASCII.GetBytes(SmbRecordedExchange.FileContent)));

        TransferResult result = await Handler(connection).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.SendError, result.ExitCode);
        Assert.AreEqual(message, result.ErrorMessage);
        Assert.AreEqual(0L, result.Report!.UploadSize);
        Assert.IsTrue(connection.Sent.AsSpan().EndsWith(SmbRecordedExchange.UploadOpenRequest));
    }

    private static async Task AssertNegotiateReplyReadFails(IOException failure, string message)
    {
        var connection = new ScriptedConnection(DownloadReplies()) { FailingRead = NegotiateRead, Failure = failure };

        TransferResult result = await Handler(connection).ExecuteAsync(Context(SmbRecordedExchange.DownloadUrl));

        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual(message, result.ErrorMessage);
        CollectionAssert.AreEqual(SmbRecordedExchange.NegotiateRequest, connection.Sent);
    }

    private static async Task AssertReadReplyFailsAfterAFullRead(IOException failure, string message)
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
        { FailingRead = SecondReadReplyRead, Failure = failure };

        TransferResult result = await Handler(connection).ExecuteAsync(Context(SmbRecordedExchange.DownloadUrl, output: output));

        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual(message, result.ErrorMessage);
        Assert.AreEqual((long)SmbReadRequest.MaxPayloadSize, result.BytesTransferred);
        Assert.AreEqual((long)SmbReadRequest.MaxPayloadSize, output.Length);
    }

    private static async Task<string[]> TranscriptOfResetWrite(int failingWrite)
    {
        var events = new TranscriptTransferEvents();
        var connection = new ScriptedConnection(DownloadReplies()) { FailingWrite = failingWrite, Failure = Broken(SocketError.ConnectionReset) };

        await Handler(connection).ExecuteAsync(Context(SmbRecordedExchange.DownloadUrl, events));

        return events.Transcript.ToArray();
    }

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
