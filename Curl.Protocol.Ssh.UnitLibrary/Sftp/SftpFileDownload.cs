using System.Globalization;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh.Transport;

namespace Curl.Protocol.Ssh.Sftp;

/// <summary>
/// Downloads one file over SFTP as curl 8.21.0 does through libssh2 1.11.1 (ADR-0220):
/// starts the SFTP session, sends <c>REALPATH .</c> for the home directory, opens the
/// URL's path for reading, asks its size with <c>STAT</c>, reads it - or the part of it
/// <c>-r</c> or <c>-C</c> asks for (ADR-0253) - with reads kept in
/// flight, writes each answer's bytes to the output as it arrives and reports progress,
/// closes the handle, and closes the channel with <c>EOF</c> and <c>CLOSE</c>, as measured.
/// A failure before the copy closes the channel the same way before the handler's
/// <c>DISCONNECT</c> (BL-973).
/// </summary>
/// <param name="transport">The transport, after the user is authenticated.</param>
internal sealed class SftpFileDownload(SshTransport transport)
{
    private static readonly byte[] HomeDirectory = "."u8.ToArray();

    /// <summary>
    /// Gets where the download's <c>--trace-config ssh</c> state changes go (BL-1166);
    /// <see cref="SshStateTrace.Off" /> when not given.
    /// </summary>
    internal SshStateTrace Trace { get; init; } = SshStateTrace.Off;

    /// <summary>
    /// Downloads the file at <paramref name="urlPath" /> into <paramref name="output" />.
    /// </summary>
    /// <param name="urlPath">The URL's path, with its percent-escapes.</param>
    /// <param name="createFileMode">The permission bits the open carries, curl's <c>--create-file-mode</c>.</param>
    /// <param name="output">Where the file's bytes go.</param>
    /// <param name="progress">Told the bytes downloaded so far, and the size when known.</param>
    /// <param name="cancellationToken">Cancels the download.</param>
    /// <param name="quotes">The <c>-Q</c> commands, run after <c>REALPATH</c> and after the handle's close; none when not given.</param>
    /// <param name="range">The <c>-r</c> range, read as <see cref="SftpDownloadPart.Choose" /> reads it; the whole file when not given.</param>
    /// <param name="resumeFrom">The <c>-C</c> offset; no resume when not given or 0.</param>
    /// <param name="maxFileSize">The <c>--max-filesize</c> limit; no limit when not given or 0 (BL-1327).</param>
    /// <returns>
    /// Success with the bytes downloaded; exit 33 or 36, with nothing read, for a range or
    /// <c>-C</c> offset the file cannot serve (<see cref="SftpDownloadPart.Choose" />); exit 18, <c>end of response with N bytes
    /// missing</c>, when the file ends before the size <c>STAT</c> gave; exit 79,
    /// <c>Error in the SSH layer</c>, when a read fails or the connection breaks during
    /// the copy; exit 63, <c>Exceeded the maximum allowed file size (N) with N bytes</c>, when
    /// a read would pass <paramref name="maxFileSize" />, after writing the bytes under it -
    /// each with the bytes written so far; success with nothing written when
    /// the connection is closed or reset at <c>OPEN</c>, as measured (BL-1046).
    /// </returns>
    /// <exception cref="SshTransferException">
    /// The session could not start (<see cref="SftpSession.StartAsync" />), <c>REALPATH</c>
    /// or <c>OPEN</c> was answered with a failed status, or the connection broke before
    /// the copy (exit 79, <c>Error in the SSH layer</c>).
    /// </exception>
    internal async ValueTask<TransferResult> DownloadAsync(
        string urlPath,
        UnixFileMode createFileMode,
        Stream output,
        ITransferProgress progress,
        CancellationToken cancellationToken,
        SftpQuoteCommands? quotes = null,
        ByteRange? range = null,
        long? resumeFrom = null,
        long? maxFileSize = null)
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
                Trace.Enter("SSH_SFTP_DOWNLOAD_INIT");
                if (await OpenUnlessTheConnectionEndsAsync(session, path, createFileMode, cancellationToken).ConfigureAwait(false) is not { } handle)
                {
                    return await quotes.FinishAsync(session, null, homeDirectory, TransferResult.Success(0), cancellationToken).ConfigureAwait(false);
                }

                Trace.Enter("SSH_SFTP_DOWNLOAD_STAT");
                long? size = await SshConnectionFailure.ReportAsSshLayerErrorAsync(
                    () => session.StatSizeAsync(path, cancellationToken)).ConfigureAwait(false);
                TransferResult result = await CopyPartOfKnownSizeAsync(session, handle, size, range, resumeFrom, maxFileSize, output, progress, cancellationToken).ConfigureAwait(false);
                Trace.Enter("SSH_SFTP_CLOSE");
                result = await quotes.FinishAsync(session, handle, homeDirectory, result, cancellationToken).ConfigureAwait(false);
                Trace.EndSftpDonePhase();
                return result;
            },
            cancellationToken).ConfigureAwait(false);
    }

    // Measured (BL-1046): curl takes a connection closed or reset while it waits for the
    // OPEN answer as libssh2's SSH_FX_OK, so the download succeeds with nothing written.
    private static async ValueTask<byte[]?> OpenUnlessTheConnectionEndsAsync(SftpSession session, byte[] path, UnixFileMode createFileMode, CancellationToken cancellationToken)
    {
        try
        {
            return await session.OpenForReadingAsync(path, createFileMode, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is EndOfStreamException or SshConnectionLostException)
        {
            return null;
        }
    }

    // From curl 8.21.0's sftp_download_stat (BL-1241): a size with its top bit set reads
    // as negative and fails with exit 36 before the DO phase completes; the handle is
    // still closed.
    private async ValueTask<TransferResult> CopyPartOfKnownSizeAsync(
        SftpSession session,
        byte[] handle,
        long? size,
        ByteRange? range,
        long? resumeFrom,
        long? maxFileSize,
        Stream output,
        ITransferProgress progress,
        CancellationToken cancellationToken)
    {
        if (size < 0)
        {
            SshTransferException failure = SshTransferException.SftpBadFileSize(size.Value);
            return TransferResult.Failure(failure.ExitCode, failure.Message);
        }

        Trace.Rest();
        Trace.Write("DO phase is complete");
        return await CopyPartAsync(session, handle, size, range, resumeFrom, maxFileSize, output, progress, cancellationToken).ConfigureAwait(false);
    }

    // Measured: a range or -C offset the file cannot serve reads nothing, and the handle
    // is still closed.
    private static async ValueTask<TransferResult> CopyPartAsync(
        SftpSession session,
        byte[] handle,
        long? size,
        ByteRange? range,
        long? resumeFrom,
        long? maxFileSize,
        Stream output,
        ITransferProgress progress,
        CancellationToken cancellationToken)
    {
        SftpDownloadPart part;
        try
        {
            part = SftpDownloadPart.Choose(range, resumeFrom, size);
        }
        catch (SshTransferException failure)
        {
            return TransferResult.Failure(failure.ExitCode, failure.Message);
        }

        Copy copy = new(new SftpReadAhead(session, handle, part), part.Length, maxFileSize is > 0 and long limit ? limit : long.MaxValue, output, progress);
        return await copy.RunAsync(cancellationToken).ConfigureAwait(false);
    }

    // One copy of the part's bytes to the output, counting them as they go; what a read
    // returns beyond the part is dropped, as curl stops at the size it expects; a read
    // that would pass the --max-filesize limit is cut at it, as curl 8.21.0's
    // cw_download_write cuts it, and the copy fails with exit 63 (BL-1327).
    private sealed class Copy(SftpReadAhead reads, long? size, long maxFileSize, Stream output, ITransferProgress progress)
    {
        private long received;

        internal async ValueTask<TransferResult> RunAsync(CancellationToken cancellationToken)
        {
            try
            {
                return await CopyUntilEndAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is SshTransferException || SshConnectionFailure.Is(exception))
            {
                SshTransferException failure = SshTransferException.SshLayerError();
                return TransferResult.Failure(failure.ExitCode, failure.Message, received);
            }
        }

        private async ValueTask<TransferResult> CopyUntilEndAsync(CancellationToken cancellationToken)
        {
            while (received < (size ?? long.MaxValue))
            {
                ReadOnlyMemory<byte> data = await reads.ReadNextAsync(received, cancellationToken).ConfigureAwait(false);
                if (data.IsEmpty)
                {
                    return EndOfFile();
                }

                data = data[..(int)Math.Min(data.Length, (size ?? long.MaxValue) - received)];
                int allowed = (int)Math.Min(data.Length, maxFileSize - received);
                await output.WriteAsync(data[..allowed], cancellationToken).ConfigureAwait(false);
                received += allowed;
                progress.ReportDownloaded(received, size);
                if (allowed < data.Length)
                {
                    return MaxFileSizeExceeded();
                }
            }

            return TransferResult.Success(received);
        }

        // From curl 8.21.0's cw_download_write (lib/sendf.c): a body exactly at the limit
        // succeeds; one cut at it fails after the bytes under it are written.
        private TransferResult MaxFileSizeExceeded() =>
            TransferResult.Failure(
                CurlExitCode.FilesizeExceeded,
                string.Create(CultureInfo.InvariantCulture, $"Exceeded the maximum allowed file size ({maxFileSize}) with {received} bytes"),
                received);

        // Measured: a file that ends 5 bytes short of its STAT size is exit 18.
        private TransferResult EndOfFile() =>
            size is { } known && received < known
                ? TransferResult.Failure(CurlExitCode.PartialFile, $"end of response with {known - received} bytes missing", received)
                : TransferResult.Success(received);
    }
}
