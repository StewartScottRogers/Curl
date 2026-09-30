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
    /// <returns>
    /// Success with the bytes downloaded; exit 33 or 36, with nothing read, for a range or
    /// <c>-C</c> offset the file cannot serve (<see cref="SftpDownloadPart.Choose" />); exit 18, <c>end of response with N bytes
    /// missing</c>, when the file ends before the size <c>STAT</c> gave; exit 79,
    /// <c>Error in the SSH layer</c>, when a read fails or the connection breaks during
    /// the copy - each with the bytes written so far.
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
        long? resumeFrom = null)
    {
        quotes ??= SftpQuoteCommands.None;
        SftpSession session = await SftpSession.StartAsync(transport, cancellationToken).ConfigureAwait(false);
        return await session.CloseChannelOnFailureAsync(
            async () =>
            {
                byte[] homeDirectory = await SshConnectionFailure.ReportAsSshLayerErrorAsync(
                    () => session.RealPathAsync(HomeDirectory, cancellationToken)).ConfigureAwait(false);
                byte[] path = SftpRemotePath.Resolve(SftpRemotePath.Decode(urlPath), homeDirectory);
                await quotes.RunBeforeTransferAsync(session, homeDirectory, path, cancellationToken).ConfigureAwait(false);
                (byte[] handle, long? size) = await SshConnectionFailure.ReportAsSshLayerErrorAsync(async () =>
                {
                    byte[] opened = await session.OpenForReadingAsync(path, createFileMode, cancellationToken).ConfigureAwait(false);
                    return (opened, await session.StatSizeAsync(path, cancellationToken).ConfigureAwait(false));
                }).ConfigureAwait(false);
                TransferResult result = await CopyPartAsync(session, handle, size, range, resumeFrom, output, progress, cancellationToken).ConfigureAwait(false);
                return await quotes.FinishAsync(session, handle, homeDirectory, result, cancellationToken).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
    }

    // Measured: a range or -C offset the file cannot serve reads nothing, and the handle
    // is still closed.
    private static async ValueTask<TransferResult> CopyPartAsync(
        SftpSession session,
        byte[] handle,
        long? size,
        ByteRange? range,
        long? resumeFrom,
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

        Copy copy = new(new SftpReadAhead(session, handle, part), part.Length, output, progress);
        return await copy.RunAsync(cancellationToken).ConfigureAwait(false);
    }

    // One copy of the part's bytes to the output, counting them as they go; what a read
    // returns beyond the part is dropped, as curl stops at the size it expects.
    private sealed class Copy(SftpReadAhead reads, long? size, Stream output, ITransferProgress progress)
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
                await output.WriteAsync(data, cancellationToken).ConfigureAwait(false);
                received += data.Length;
                progress.ReportDownloaded(received, size);
            }

            return TransferResult.Success(received);
        }

        // Measured: a file that ends 5 bytes short of its STAT size is exit 18.
        private TransferResult EndOfFile() =>
            size is { } known && received < known
                ? TransferResult.Failure(CurlExitCode.PartialFile, $"end of response with {known - received} bytes missing", received)
                : TransferResult.Success(received);
    }
}
