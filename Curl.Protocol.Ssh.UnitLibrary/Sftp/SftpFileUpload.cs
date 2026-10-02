using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh.Transport;

namespace Curl.Protocol.Ssh.Sftp;

/// <summary>
/// Uploads one file over SFTP as curl 8.21.0 does through libssh2 1.11.1 (ADR-0244):
/// starts the SFTP session, sends <c>REALPATH .</c> for the home directory, asks the
/// remote size with <c>STAT</c> under <c>-C -</c>, opens the URL's path for writing -
/// creating, truncating, appending or resuming as asked - and, when that open fails and
/// <c>--ftp-create-dirs</c> was given, makes every directory of the path with
/// <c>MKDIR</c> and opens it again. It then reads the source in curl's 64 KiB blocks,
/// sends each as <c>WRITE</c>s of at most 30000 bytes, waits for their statuses, reports
/// progress, closes the handle and closes the channel with <c>EOF</c> and <c>CLOSE</c>,
/// as measured. A failure before the copy closes the channel the same way before the
/// handler's <c>DISCONNECT</c> (BL-973). Each <c>WRITE</c>'s bytes are reported as sent
/// data, and a copy that reaches the source's end writes curl's <c>upload completely sent
/// off</c> line before the <c>-Q</c> commands that follow it, as measured (BL-988).
/// </summary>
/// <param name="transport">The transport, after the user is authenticated.</param>
/// <param name="events">Where the sent bytes, the line after them and each <c>SFTP: creating directory</c> line are reported.</param>
internal sealed class SftpFileUpload(SshTransport transport, ITransferEvents events)
{
    /// <summary>
    /// Gets where the upload's <c>--trace-config ssh</c> state changes go (BL-1204);
    /// <see cref="SshStateTrace.Off" /> when not given.
    /// </summary>
    internal SshStateTrace Trace { get; init; } = SshStateTrace.Off;

    /// <summary>How many bytes of the source curl reads at a time: its 64 KiB upload buffer, as measured.</summary>
    internal const int ReadBufferSize = 65536;

    /// <summary>The most bytes libssh2 puts in one <c>SSH_FXP_WRITE</c>, its <c>MAX_SFTP_OUTGOING_SIZE</c>, as measured.</summary>
    internal const int WriteChunkSize = 30000;

    private static readonly byte[] HomeDirectory = "."u8.ToArray();

    // The failed first-open statuses after which --ftp-create-dirs makes the directories,
    // and the MKDIR statuses it goes on past, as measured: no such file, failure and no
    // such path; then OK, permission denied, failure and file already exists.
    private static readonly uint[] StatusesThatCreateDirectories = [2, 4, 10];

    private static readonly uint[] MakeDirectoryStatusesPassed = [SftpStatusCode.Ok, 3, 4, 11];

    /// <summary>
    /// Uploads <paramref name="upload" /> to the file at <paramref name="urlPath" />.
    /// </summary>
    /// <param name="urlPath">The URL's path, with its percent-escapes.</param>
    /// <param name="options">The resume offset, append, directory creation and file mode asked for.</param>
    /// <param name="upload">The source; one that can seek is moved past a <c>-C</c> offset, standard input is sent whole.</param>
    /// <param name="progress">Told the bytes the server has acknowledged so far, and the size when known.</param>
    /// <param name="cancellationToken">Cancels the upload.</param>
    /// <param name="quotes">The <c>-Q</c> commands, run after <c>REALPATH</c> and after the handle's close; none when not given.</param>
    /// <returns>
    /// Success with the bytes uploaded, or exit 79, <c>Error in the SSH layer</c>, when a
    /// write is refused or the connection breaks during the copy, with the bytes
    /// acknowledged before it; either reports those bytes as <c>%{size_upload}</c>.
    /// </returns>
    /// <exception cref="SshTransferException">
    /// The session could not start (<see cref="SftpSession.StartAsync" />); <c>REALPATH</c>
    /// failed; the open failed (<see cref="SshTransferException.SftpUploadFailed" />, or
    /// <see cref="SshTransferException.SftpCreateFailed" /> after the directories were
    /// made); a <c>MKDIR</c> failed with a status curl does not go on past
    /// (<see cref="SshTransferException.SftpRequestFailed" />); or the connection broke
    /// before the copy (exit 79, <c>Error in the SSH layer</c>).
    /// </exception>
    internal async ValueTask<TransferResult> UploadAsync(
        string urlPath,
        SftpUploadOptions options,
        Stream upload,
        ITransferProgress progress,
        CancellationToken cancellationToken,
        SftpQuoteCommands? quotes = null)
    {
        quotes ??= SftpQuoteCommands.None;
        Trace.Enter("SSH_SFTP_INIT");
        SftpSession session = await SftpSession.StartAsync(transport, cancellationToken).ConfigureAwait(false);
        return await session.CloseChannelOnFailureAsync(
            async () =>
            {
                Trace.Enter("SSH_SFTP_REALPATH");
                byte[] homeDirectory = await SshConnectionFailure.ReportAsSshLayerErrorAsync(
                    () => session.RealPathAsync(HomeDirectory, cancellationToken)).ConfigureAwait(false);
                byte[] path = SftpRemotePath.ResolveUrlPath(urlPath, homeDirectory);
                Trace.EndSftpConnectPhase();
                Trace.Enter("SSH_SFTP_QUOTE_INIT");
                await quotes.RunBeforeTransferAsync(session, homeDirectory, path, cancellationToken).ConfigureAwait(false);
                Trace.Enter("SSH_SFTP_GETINFO");
                Trace.Enter("SSH_SFTP_TRANS_INIT");
                Trace.Enter("SSH_SFTP_UPLOAD_INIT");
                (byte[] handle, long offset) = await OpenTracedAsync(session, path, options, cancellationToken).ConfigureAwait(false);
                Trace.Rest();
                Trace.Write("DO phase is complete");

                // Measured: -a appends the whole source from offset 0 whatever -C says.
                long writeOffset = options.Append ? 0 : offset;
                SkipResumedPart(upload, writeOffset);
                long? expected = upload.CanSeek ? upload.Length - upload.Position : null;
                Copy copy = new(session, handle, upload, progress, expected, events);
                TransferResult result = await copy.RunAsync(writeOffset, cancellationToken).ConfigureAwait(false);
                if (result.IsSuccess)
                {
                    events.ReportInfo(SshInfoLines.UploadSent(result.BytesTransferred));
                }

                Trace.Enter("SSH_SFTP_CLOSE");
                result = await quotes.FinishAsync(session, handle, homeDirectory, result, cancellationToken).ConfigureAwait(false);
                Trace.EndSftpDonePhase();
                return result with { Report = new TransferReport { UploadSize = result.BytesTransferred } };
            },
            cancellationToken).ConfigureAwait(false);
    }

    // Measured (BL-1204): curl enters SSH_SFTP_CLOSE before it writes a failed open's line.
    private async ValueTask<(byte[] Handle, long Offset)> OpenTracedAsync(SftpSession session, byte[] path, SftpUploadOptions options, CancellationToken cancellationToken)
    {
        try
        {
            return await SshConnectionFailure.ReportAsSshLayerErrorAsync(
                () => OpenAsync(session, path, options, cancellationToken)).ConfigureAwait(false);
        }
        catch (SshTransferException)
        {
            Trace.Enter("SSH_SFTP_CLOSE");
            throw;
        }
    }

    // Measured: a file source skips the part the offset covers, up to its end; standard
    // input, which cannot seek, is sent whole from the offset.
    private static void SkipResumedPart(Stream upload, long offset)
    {
        if (upload.CanSeek)
        {
            upload.Position += Math.Min(offset, Math.Max(0, upload.Length - upload.Position));
        }
    }

    // Measured: -a opens WRITE|APPEND|CREAT, a positive offset WRITE alone, anything else
    // WRITE|CREAT|TRUNC.
    private static uint OpenFlagsFor(bool append, long offset) =>
        append ? SftpOpenFlags.Write | SftpOpenFlags.Append | SftpOpenFlags.Create
            : offset > 0 ? SftpOpenFlags.Write
            : SftpOpenFlags.Write | SftpOpenFlags.Create | SftpOpenFlags.Truncate;

    private async ValueTask<(byte[] Handle, long Offset)> OpenAsync(SftpSession session, byte[] path, SftpUploadOptions options, CancellationToken cancellationToken)
    {
        long offset = options.ResumeFromRemoteSize
            ? await session.StatSizeAsync(path, cancellationToken).ConfigureAwait(false) ?? 0
            : options.ResumeFrom;
        uint flags = OpenFlagsFor(options.Append, offset);

        // Measured (BL-984): curl sets CURLOPT_NEW_FILE_PERMS only for a non-zero
        // --create-file-mode, so 0 opens with its default, 0644.
        UnixFileMode createFileMode = options.CreateFileMode == 0 ? TransferContext.DefaultCreateFileMode : options.CreateFileMode;
        (byte[]? handle, uint status) = await session.OpenAsync(path, flags, createFileMode, cancellationToken).ConfigureAwait(false);
        handle ??= options.CreateDirectories && StatusesThatCreateDirectories.Contains(status)
            ? await CreateDirectoriesAndOpenAsync(session, path, flags, createFileMode, cancellationToken).ConfigureAwait(false)
            : throw SshTransferException.SftpUploadFailed(status);
        return (handle, offset);
    }

    // Measured: one MKDIR for every slash after the first, from the root down, each after
    // curl's "SFTP: creating directory" line, then the same open again.
    private async ValueTask<byte[]> CreateDirectoriesAndOpenAsync(SftpSession session, byte[] path, uint flags, UnixFileMode createFileMode, CancellationToken cancellationToken)
    {
        for (int index = 1; index < path.Length; index++)
        {
            if (path[index] == (byte)'/')
            {
                await MakeDirectoryAsync(session, path[..index], cancellationToken).ConfigureAwait(false);
            }
        }

        (byte[]? handle, uint status) = await session.OpenAsync(path, flags, createFileMode, cancellationToken).ConfigureAwait(false);
        return handle ?? throw SshTransferException.SftpCreateFailed(status);
    }

    private async ValueTask MakeDirectoryAsync(SftpSession session, byte[] directory, CancellationToken cancellationToken)
    {
        events.ReportInfo(SshInfoLines.CreatingDirectory(Encoding.UTF8.GetString(directory)));
        uint status = await session.MakeDirectoryAsync(directory, cancellationToken).ConfigureAwait(false);
        if (!MakeDirectoryStatusesPassed.Contains(status))
        {
            throw SshTransferException.SftpRequestFailed(status);
        }
    }

    // One copy of the source's bytes to the open file, counting those the server acknowledged.
    private sealed class Copy(SftpSession session, byte[] handle, Stream upload, ITransferProgress progress, long? expected, ITransferEvents events)
    {
        private long acknowledged;

        internal async ValueTask<TransferResult> RunAsync(long offset, CancellationToken cancellationToken)
        {
            try
            {
                return await CopyUntilEndAsync(offset, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is SshTransferException || SshConnectionFailure.Is(exception))
            {
                SshTransferException failure = SshTransferException.SshLayerError();
                return TransferResult.Failure(failure.ExitCode, failure.Message, acknowledged);
            }
        }

        private async ValueTask<TransferResult> CopyUntilEndAsync(long offset, CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[ReadBufferSize];
            int read;
            while ((read = await ReadSourceAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                await WriteBlockAsync(buffer.AsMemory(0, read), offset, cancellationToken).ConfigureAwait(false);
                offset += read;
            }

            return TransferResult.Success(acknowledged);
        }

        // A source that fails to read ends the upload as its end does, as the FTP upload
        // takes it (ADR-0244).
        private async ValueTask<int> ReadSourceAsync(byte[] buffer, CancellationToken cancellationToken)
        {
            try
            {
                return await upload.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            }
            catch (IOException)
            {
                return 0;
            }
        }

        // Measured: every WRITE of a block is sent before any status is read, and a failed
        // status ends the upload with the bytes acknowledged before it.
        private async ValueTask WriteBlockAsync(ReadOnlyMemory<byte> block, long offset, CancellationToken cancellationToken)
        {
            List<(uint Id, int Length)> writes = [];
            for (int start = 0; start < block.Length; start += WriteChunkSize)
            {
                ReadOnlyMemory<byte> chunk = block.Slice(start, Math.Min(WriteChunkSize, block.Length - start));
                writes.Add((await session.SendWriteAsync(handle, offset + start, chunk, cancellationToken).ConfigureAwait(false), chunk.Length));
                events.ReportDataSent(chunk.Span);
            }

            foreach ((uint id, int length) in writes)
            {
                uint status = await session.ReadStatusAsync(id, cancellationToken).ConfigureAwait(false);
                acknowledged += status == SftpStatusCode.Ok ? length : throw SshTransferException.SshLayerError();
                progress.ReportUploaded(acknowledged, expected);
            }
        }
    }
}
