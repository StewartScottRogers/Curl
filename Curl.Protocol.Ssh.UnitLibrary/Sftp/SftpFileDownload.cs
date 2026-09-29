using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh.Transport;

namespace Curl.Protocol.Ssh.Sftp;

/// <summary>
/// Downloads one file over SFTP as curl 8.21.0 does through libssh2 1.11.1 (ADR-0220):
/// starts the SFTP session, sends <c>REALPATH .</c> for the home directory, opens the
/// URL's path for reading, asks its size with <c>STAT</c>, reads it with reads kept in
/// flight, writes each answer's bytes to the output as it arrives and reports progress,
/// closes the handle, and closes the channel with <c>EOF</c> and <c>CLOSE</c>, as measured.
/// A failure before the copy leaves the channel open for the handler's <c>DISCONNECT</c>.
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
    /// <returns>
    /// Success with the bytes downloaded; exit 18, <c>end of response with N bytes
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
        CancellationToken cancellationToken)
    {
        SftpSession session = await SftpSession.StartAsync(transport, cancellationToken).ConfigureAwait(false);
        (byte[] handle, long? size) = await InSshLayerAsync(async () =>
        {
            byte[] homeDirectory = await session.RealPathAsync(HomeDirectory, cancellationToken).ConfigureAwait(false);
            byte[] path = SftpRemotePath.Resolve(SftpRemotePath.Decode(urlPath), homeDirectory);
            byte[] opened = await session.OpenForReadingAsync(path, createFileMode, cancellationToken).ConfigureAwait(false);
            return (opened, await session.StatSizeAsync(path, cancellationToken).ConfigureAwait(false));
        }).ConfigureAwait(false);
        Copy copy = new(new SftpReadAhead(session, handle, size), size, output, progress);
        TransferResult result = await copy.RunAsync(cancellationToken).ConfigureAwait(false);
        await CloseIgnoringFailureAsync(session, handle, cancellationToken).ConfigureAwait(false);
        await ShutdownIgnoringFailureAsync(session, cancellationToken).ConfigureAwait(false);
        return result;
    }

    private static async ValueTask<T> InSshLayerAsync<T>(Func<ValueTask<T>> step)
    {
        try
        {
            return await step().ConfigureAwait(false);
        }
        catch (Exception exception) when (SshConnectionFailure.Is(exception))
        {
            throw SshTransferException.SshLayerError();
        }
    }

    // curl only logs a close that fails, and a broken connection has already failed the
    // transfer.
    private static async ValueTask CloseIgnoringFailureAsync(SftpSession session, byte[] handle, CancellationToken cancellationToken)
    {
        try
        {
            await session.CloseHandleAsync(handle, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (SshConnectionFailure.Is(exception))
        {
        }
    }

    // Measured (ADR-0220): curl's CHANNEL_EOF, then its CHANNEL_CLOSE after the server's.
    // A broken connection has already decided the transfer's outcome.
    private static async ValueTask ShutdownIgnoringFailureAsync(SftpSession session, CancellationToken cancellationToken)
    {
        try
        {
            await session.ShutdownAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (SshConnectionFailure.Is(exception))
        {
        }
    }

    // One copy of the file's bytes to the output, counting them as they go.
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
