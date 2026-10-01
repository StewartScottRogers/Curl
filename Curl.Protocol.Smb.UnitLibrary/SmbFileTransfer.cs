using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Smb;

/// <summary>
/// Downloads or uploads the URL's file over an SMBv1 session already set up, in curl
/// 8.21.0's order (<c>smb_request_state</c>): TREE_CONNECT_ANDX to the share, NT_CREATE_ANDX
/// to open the file, then for a download READ_ANDX until a read returns fewer than
/// <see cref="SmbReadRequest.MaxPayloadSize" /> bytes, writing each read's bytes to the
/// output, or for an upload (<see cref="ITransferContext.Upload" />) WRITE_ANDX until the
/// server has written the source's size; then CLOSE and TREE_DISCONNECT.
/// </summary>
/// <remarks>
/// The outcomes, as measured: a refused tree connect ends the transfer at once with exit 78
/// (exit 9 for the DOS error <c>ERRnoaccess</c>); a refused or short open sends only the
/// tree disconnect and ends the same way; a negative file size (exit 8), a refused read
/// (exit 56), a failed output write (exit 23) or a refused write (exit 25) closes the file
/// and disconnects first. The close and tree disconnect responses are not checked. A frame
/// the reader refuses (exit 56) ends the transfer at once, and a tree connect or open whose
/// bytes would pass 1024 is exit 63 with nothing sent, as curl closes the connection on
/// either. An upload counts the bytes each write response says were written, as curl's
/// <c>%{size_upload}</c> does, and writes the next piece from there.
/// </remarks>
/// <param name="connection">The connection the session was set up on.</param>
/// <param name="reader">Reads the server's messages from <paramref name="connection" />.</param>
/// <param name="userId">The UID the session setup response assigned.</param>
/// <param name="context">The transfer: its output or upload source, progress, <c>-R</c> and cancellation.</param>
internal sealed class SmbFileTransfer(IConnection connection, SmbMessageReader reader, ushort userId, ITransferContext context)
{
    // SMB_ERR_NOACCESS: ERRDOS class 0x01, ERRnoaccess 0x0005, as the status reads on the wire.
    private const uint DosNoAccess = 0x00050001;

    private readonly SmbTransferLog log = new(context.DiagnosticLog);

    /// <summary>Connects to the share, downloads or uploads the file and disconnects.</summary>
    /// <param name="hostName">The host name the transfer connected to, sent in the tree connect.</param>
    /// <param name="path">The share and file the URL names.</param>
    /// <returns>The transfer's outcome.</returns>
    public async ValueTask<TransferResult> TransferAsync(string hostName, SmbUrlPath path)
    {
        if (SmbTreeConnectRequest.Encode(hostName, path.Share, userId) is not { } treeConnect)
        {
            return MessageTooLarge();
        }

        SmbReceivedMessage reply = await ExchangeAsync(treeConnect).ConfigureAwait(false);
        if (reply.Bytes is not { } connected)
        {
            return ReceiveFailure(reply);
        }

        uint status = SmbMessageHeader.ReadStatus(connected);
        if (status != 0)
        {
            log.Refused("tree connect", status);
            return NotFoundOrDenied(status);
        }

        ushort treeId = SmbMessageHeader.ReadTreeId(connected);
        log.TreeConnected(path.Share, treeId);
        return await OpenAsync(treeId, path.FilePath).ConfigureAwait(false);
    }

    private static TransferResult MessageTooLarge() =>
        TransferResult.Failure(CurlExitCode.FilesizeExceeded, SmbMessages.MessageTooLarge);

    private static TransferResult ReceiveFailure(SmbReceivedMessage message) =>
        TransferResult.Failure(CurlExitCode.RecvError, message.ErrorMessage!);

    private static TransferResult NotFoundOrDenied(uint status) =>
        status == DosNoAccess
            ? TransferResult.Failure(CurlExitCode.RemoteAccessDenied, SmbMessages.RemoteAccessDenied)
            : TransferResult.Failure(CurlExitCode.RemoteFileNotFound, SmbMessages.RemoteFileNotFound);

    private static int BytesAcceptedBy(IOException exception) =>
        exception is OutputWriteFailedException failed ? failed.BytesAccepted : 0;

    // An upload's result carries the bytes written as %{size_upload}, and none downloaded.
    private static TransferResult Uploaded(TransferResult result, long written) =>
        result with { BytesTransferred = written, Report = new TransferReport { UploadSize = written } };

    // A failure after the file was open keeps what the transfer had moved and reported.
    private static TransferResult ReceiveFailureAfter(SmbReceivedMessage message, TransferResult outcome) =>
        ReceiveFailure(message) with { BytesTransferred = outcome.BytesTransferred, Report = outcome.Report };

    private async ValueTask<TransferResult> OpenAsync(ushort treeId, byte[] filePath)
    {
        if (SmbOpenRequest.Encode(filePath, userId, treeId, context.Upload is not null) is not { } open)
        {
            return MessageTooLarge();
        }

        SmbReceivedMessage reply = await ExchangeAsync(open).ConfigureAwait(false);
        if (reply.Bytes is not { } opened)
        {
            return ReceiveFailure(reply);
        }

        if (!SmbOpenResponse.TryRead(opened, out SmbOpenResponse? file))
        {
            uint status = SmbMessageHeader.ReadStatus(opened);
            log.Refused("open", status);
            return await DisconnectAsync(treeId, NotFoundOrDenied(status)).ConfigureAwait(false);
        }

        log.Opened(file!.EndOfFile);
        (TransferResult outcome, bool frameRefused) = await MoveFileAsync(treeId, file!).ConfigureAwait(false);
        return frameRefused ? outcome : await CloseAsync(treeId, file!.FileId, outcome).ConfigureAwait(false);
    }

    // The outcome, and whether it is a frame the reader refused, which ends the transfer
    // without closing the file.
    private async ValueTask<(TransferResult Outcome, bool FrameRefused)> MoveFileAsync(ushort treeId, SmbOpenResponse file)
    {
        if (context.Upload is { } upload)
        {
            return await WriteFileAsync(treeId, file.FileId, upload).ConfigureAwait(false);
        }

        return file.EndOfFile < 0
            ? (TransferResult.Failure(CurlExitCode.WeirdServerReply, SmbMessages.WeirdServerReply), false)
            : await ReadFileAsync(treeId, file).ConfigureAwait(false);
    }

    private async ValueTask<(TransferResult Outcome, bool FrameRefused)> ReadFileAsync(ushort treeId, SmbOpenResponse file)
    {
        context.Progress.ReportTransferStarted();
        long offset = 0;
        while (true)
        {
            SmbReceivedMessage reply = await ExchangeAsync(SmbReadRequest.Encode(userId, treeId, file.FileId, offset)).ConfigureAwait(false);
            if (reply.Bytes is not { } read)
            {
                return (ReceiveFailure(reply) with { BytesTransferred = offset }, true);
            }

            if (SmbReadResponse.TryGetData(read, out ReadOnlyMemory<byte> data) is { } refused)
            {
                return (TransferResult.Failure(CurlExitCode.RecvError, refused, offset), false);
            }

            ReportDataReceived(data.Span);
            if (await WriteAsync(data, offset).ConfigureAwait(false) is { } writeFailure)
            {
                return (writeFailure, false);
            }

            offset += data.Length;
            context.Progress.ReportDownloaded(offset, file.EndOfFile);
            if (data.Length < SmbReadRequest.MaxPayloadSize)
            {
                return (TransferResult.Success(offset, context.RemoteTime ? file.LastChangeTimeUtc : null), false);
            }
        }
    }

    // Writes the source from where it stands to its end, one piece of at most
    // SmbWriteRequest.MaxDataLength bytes per request, each from where the server's count
    // of bytes written so far ends; the handler has checked the source can seek.
    private async ValueTask<(TransferResult Outcome, bool FrameRefused)> WriteFileAsync(ushort treeId, ushort fileId, Stream upload)
    {
        long size = Math.Max(0, upload.Length - upload.Position);
        context.Progress.ReportTransferStarted();
        byte[] buffer = new byte[SmbWriteRequest.MaxDataLength];
        long written = 0;
        while (true)
        {
            int dataLength = (int)Math.Min(size - written, SmbWriteRequest.MaxDataLength);
            int read = await upload.ReadAtLeastAsync(buffer.AsMemory(0, dataLength), dataLength, throwOnEndOfStream: false, context.CancellationToken)
                .ConfigureAwait(false);
            SmbReceivedMessage reply = await ExchangeAsync(SmbWriteRequest.Encode(userId, treeId, fileId, written, dataLength, buffer.AsSpan(0, read)))
                .ConfigureAwait(false);
            if (reply.Bytes is not { } answered)
            {
                return (Uploaded(ReceiveFailure(reply), written), true);
            }

            if (!SmbWriteResponse.TryReadCount(answered, out int count))
            {
                return (Uploaded(TransferResult.Failure(CurlExitCode.UploadFailed, SmbMessages.UploadFailed), written), false);
            }

            written += count;
            context.Progress.ReportUploaded(written, size);
            if (written >= size)
            {
                return (Uploaded(TransferResult.Success(written), written), false);
            }
        }
    }

    // A read's bytes are -v's "{ [N bytes data]" line, as curl's client writer traces them;
    // an empty read, the end of the file, writes nothing to trace.
    private void ReportDataReceived(ReadOnlySpan<byte> data)
    {
        if (!data.IsEmpty)
        {
            context.Events.ReportDataReceived(data);
        }
    }

    private async ValueTask<TransferResult?> WriteAsync(ReadOnlyMemory<byte> data, long offset)
    {
        try
        {
            await context.Output.WriteAsync(data, context.CancellationToken).ConfigureAwait(false);
            return null;
        }
        catch (IOException exception)
        {
            return TransferResult.Failure(
                CurlExitCode.WriteError,
                SmbMessages.OutputWriteFailed(data.Length, BytesAcceptedBy(exception)),
                offset);
        }
    }

    private async ValueTask<TransferResult> CloseAsync(ushort treeId, ushort fileId, TransferResult outcome)
    {
        SmbReceivedMessage reply = await ExchangeAsync(SmbCloseRequest.Encode(userId, treeId, fileId)).ConfigureAwait(false);
        return reply.Bytes is null
            ? ReceiveFailureAfter(reply, outcome)
            : await DisconnectAsync(treeId, outcome).ConfigureAwait(false);
    }

    private async ValueTask<TransferResult> DisconnectAsync(ushort treeId, TransferResult outcome)
    {
        SmbReceivedMessage reply = await ExchangeAsync(SmbTreeDisconnectRequest.Encode(userId, treeId)).ConfigureAwait(false);
        return reply.Bytes is null ? ReceiveFailureAfter(reply, outcome) : outcome;
    }

    private async ValueTask<SmbReceivedMessage> ExchangeAsync(byte[] request)
    {
        await connection.WriteAsync(request, context.CancellationToken).ConfigureAwait(false);
        await connection.FlushAsync(context.CancellationToken).ConfigureAwait(false);
        return await reader.ReceiveAsync(context.CancellationToken).ConfigureAwait(false);
    }
}
