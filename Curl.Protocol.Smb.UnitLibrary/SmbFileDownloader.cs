using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Smb;

/// <summary>
/// Downloads the URL's file over an SMBv1 session already set up, in curl 8.21.0's order
/// (<c>smb_request_state</c>): TREE_CONNECT_ANDX to the share, NT_CREATE_ANDX to open the
/// file, READ_ANDX until a read returns fewer than <see cref="SmbReadRequest.MaxPayloadSize" />
/// bytes, then CLOSE and TREE_DISCONNECT, writing each read's bytes to the output.
/// </summary>
/// <remarks>
/// The outcomes, as measured: a refused tree connect ends the transfer at once with exit 78
/// (exit 9 for the DOS error <c>ERRnoaccess</c>); a refused or short open sends only the
/// tree disconnect and ends the same way; a negative file size (exit 8), a refused read
/// (exit 56) or a failed output write (exit 23) closes the file and disconnects first.
/// The close and tree disconnect responses are not checked. A frame the reader refuses
/// (exit 56) ends the transfer at once, and a tree connect or open whose bytes would pass
/// 1024 is exit 63 with nothing sent, as curl closes the connection on either.
/// </remarks>
/// <param name="connection">The connection the session was set up on.</param>
/// <param name="reader">Reads the server's messages from <paramref name="connection" />.</param>
/// <param name="userId">The UID the session setup response assigned.</param>
/// <param name="context">The transfer: its output, progress, <c>-R</c> and cancellation.</param>
internal sealed class SmbFileDownloader(IConnection connection, SmbMessageReader reader, ushort userId, ITransferContext context)
{
    // SMB_ERR_NOACCESS: ERRDOS class 0x01, ERRnoaccess 0x0005, as the status reads on the wire.
    private const uint DosNoAccess = 0x00050001;

    /// <summary>Connects to the share, downloads the file and disconnects.</summary>
    /// <param name="hostName">The host name the transfer connected to, sent in the tree connect.</param>
    /// <param name="path">The share and file the URL names.</param>
    /// <returns>The transfer's outcome.</returns>
    public async ValueTask<TransferResult> DownloadAsync(string hostName, SmbUrlPath path)
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
        return status != 0
            ? NotFoundOrDenied(status)
            : await OpenAsync(SmbMessageHeader.ReadTreeId(connected), path.FilePath).ConfigureAwait(false);
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

    private async ValueTask<TransferResult> OpenAsync(ushort treeId, byte[] filePath)
    {
        if (SmbOpenRequest.Encode(filePath, userId, treeId) is not { } open)
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
            return await DisconnectAsync(treeId, NotFoundOrDenied(SmbMessageHeader.ReadStatus(opened))).ConfigureAwait(false);
        }

        (TransferResult outcome, bool frameRefused) = file!.EndOfFile < 0
            ? (TransferResult.Failure(CurlExitCode.WeirdServerReply, SmbMessages.WeirdServerReply), false)
            : await ReadFileAsync(treeId, file).ConfigureAwait(false);
        return frameRefused ? outcome : await CloseAsync(treeId, file.FileId, outcome).ConfigureAwait(false);
    }

    // The outcome, and whether it is a frame the reader refused, which ends the transfer
    // without closing the file.
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
            ? ReceiveFailure(reply) with { BytesTransferred = outcome.BytesTransferred }
            : await DisconnectAsync(treeId, outcome).ConfigureAwait(false);
    }

    private async ValueTask<TransferResult> DisconnectAsync(ushort treeId, TransferResult outcome)
    {
        SmbReceivedMessage reply = await ExchangeAsync(SmbTreeDisconnectRequest.Encode(userId, treeId)).ConfigureAwait(false);
        return reply.Bytes is null ? ReceiveFailure(reply) with { BytesTransferred = outcome.BytesTransferred } : outcome;
    }

    private async ValueTask<SmbReceivedMessage> ExchangeAsync(byte[] request)
    {
        await connection.WriteAsync(request, context.CancellationToken).ConfigureAwait(false);
        await connection.FlushAsync(context.CancellationToken).ConfigureAwait(false);
        return await reader.ReceiveAsync(context.CancellationToken).ConfigureAwait(false);
    }
}
