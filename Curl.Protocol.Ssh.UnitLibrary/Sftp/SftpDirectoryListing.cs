using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh.Transport;

namespace Curl.Protocol.Ssh.Sftp;

/// <summary>
/// Lists one directory over SFTP as curl 8.21.0 does through libssh2 1.11.1 (ADR-0241):
/// starts the SFTP session, sends <c>REALPATH .</c> for the home directory, opens the URL's
/// path with <c>OPENDIR</c>, and sends <c>READDIR</c> until the server answers
/// <c>SSH_FX_EOF</c> or no names. Each name is written as the server's <c>ls -l</c>-style
/// long name and a line feed; a symbolic link's line gains <c> -&gt; </c> and the target
/// <c>READLINK</c> names; with <c>-l</c>, only the file name and a line feed. It then closes
/// the handle and the channel, as measured.
/// </summary>
/// <param name="transport">The transport, after the user is authenticated.</param>
internal sealed class SftpDirectoryListing(SshTransport transport)
{
    private static readonly byte[] HomeDirectory = "."u8.ToArray();

    private static readonly byte[] LinkArrow = " -> "u8.ToArray();

    /// <summary>
    /// Gets where the listing's <c>--trace-config ssh</c> state changes go (BL-1204): one
    /// <c>SSH_SFTP_READDIR_BOTTOM</c> and back for each long-name line, through
    /// <c>SSH_SFTP_READDIR_LINK</c> first for a symbolic link (BL-1207), none with <c>-l</c>;
    /// <see cref="SshStateTrace.Off" /> when not given.
    /// </summary>
    internal SshStateTrace Trace { get; init; } = SshStateTrace.Off;

    /// <summary>
    /// Lists the directory at <paramref name="urlPath" /> into <paramref name="output" />.
    /// </summary>
    /// <param name="urlPath">The URL's path, with its percent-escapes, ending with a slash.</param>
    /// <param name="listOnly">Whether to write only the names, curl's <c>-l</c>.</param>
    /// <param name="noBody">Whether to stop before <c>OPENDIR</c>, curl's <c>-I</c>: as measured, only a <c>STAT</c> of the directory is sent, for the file time, and nothing is written.</param>
    /// <param name="output">Where the listing goes.</param>
    /// <param name="progress">Told the bytes written so far after each line.</param>
    /// <param name="cancellationToken">Cancels the listing.</param>
    /// <param name="quotes">The <c>-Q</c> commands, run after <c>REALPATH</c> and after the handle's close; none when not given.</param>
    /// <param name="maxFileSize">The <c>--max-filesize</c> limit; no limit when not given or 0 (BL-1389).</param>
    /// <returns>
    /// Success with the bytes written; exit 63, <c>Exceeded the maximum allowed file size (N)
    /// with N bytes</c>, when a line would pass <paramref name="maxFileSize" />, after writing
    /// the bytes under it; the <c>READDIR</c> status's exit code and <c>Could not
    /// open remote file for reading: &lt;description&gt; :: -31</c> when a <c>READDIR</c>
    /// fails; exit 27, <c>Out of memory</c>, when a <c>READLINK</c> does; exit 79, <c>Error
    /// in the SSH layer</c>, when the connection breaks during the listing - each with the
    /// bytes written so far.
    /// </returns>
    /// <exception cref="SshTransferException">
    /// The session could not start (<see cref="SftpSession.StartAsync" />), <c>REALPATH</c>
    /// or <c>OPENDIR</c> was answered with a failed status, or the connection broke before
    /// the listing (exit 79, <c>Error in the SSH layer</c>).
    /// </exception>
    internal async ValueTask<TransferResult> ListAsync(
        string urlPath,
        bool listOnly,
        bool noBody,
        Stream output,
        ITransferProgress progress,
        CancellationToken cancellationToken,
        SftpQuoteCommands? quotes = null,
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
                byte[] directory = SftpRemotePath.ResolveUrlPath(urlPath, homeDirectory);
                Trace.EndSftpConnectPhase();
                Trace.Enter("SSH_SFTP_QUOTE_INIT");
                await quotes.RunBeforeTransferAsync(session, homeDirectory, directory, cancellationToken).ConfigureAwait(false);
                Trace.Enter("SSH_SFTP_GETINFO");
                if (noBody)
                {
                    // Measured (BL-1207): curl's -I asks for the file time, so libssh2 sends
                    // STAT for the directory and curl ignores the answer; then curl ends the
                    // DO phase from SSH_SFTP_READDIR_INIT without OPENDIR.
                    Trace.Enter("SSH_SFTP_FILETIME");
                    await SshConnectionFailure.ReportAsSshLayerErrorAsync(
                        () => session.StatAsync(directory, cancellationToken)).ConfigureAwait(false);
                    Trace.Enter("SSH_SFTP_TRANS_INIT");
                    Trace.Enter("SSH_SFTP_READDIR_INIT");
                    Trace.Rest();
                    Trace.Write("DO phase is complete");
                    Trace.Enter("SSH_SFTP_CLOSE");
                    return await FinishAsync(quotes, session, null, homeDirectory, TransferResult.Success(0), cancellationToken).ConfigureAwait(false);
                }

                Trace.Enter("SSH_SFTP_TRANS_INIT");
                Trace.Enter("SSH_SFTP_READDIR_INIT");
                byte[] handle = await SshConnectionFailure.ReportAsSshLayerErrorAsync(
                    () => session.OpenDirectoryAsync(directory, cancellationToken)).ConfigureAwait(false);
                Listing listing = new(session, handle, directory, listOnly, new DownloadSizeLimit(maxFileSize), output, progress, Trace);
                TransferResult result = await listing.RunAsync(cancellationToken).ConfigureAwait(false);
                if (result.IsSuccess)
                {
                    Trace.Enter("SSH_SFTP_READDIR_DONE");
                    Trace.Rest();
                    Trace.Write("DO phase is complete");
                    Trace.Enter("SSH_SFTP_CLOSE");
                }

                return await FinishAsync(quotes, session, handle, homeDirectory, result, cancellationToken).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
    }

    // Closes the handle and runs the -Q commands after the listing, then ends curl's DONE phase.
    private async ValueTask<TransferResult> FinishAsync(SftpQuoteCommands quotes, SftpSession session, byte[]? handle, byte[] homeDirectory, TransferResult result, CancellationToken cancellationToken)
    {
        TransferResult finished = await quotes.FinishAsync(session, handle, homeDirectory, result, cancellationToken).ConfigureAwait(false);
        Trace.EndSftpDonePhase();
        return finished;
    }

    // curl copies each name into a C string, so a NUL byte ends what it prints.
    private static ReadOnlySpan<byte> UpToNul(byte[] bytes)
    {
        int end = Array.IndexOf(bytes, (byte)0);
        return end < 0 ? bytes : bytes.AsSpan(0, end);
    }

    // One listing of the directory to the output, counting the bytes as they go; a line that
    // would pass the --max-filesize limit is cut at it, as curl 8.21.0's cw_download_write
    // cuts every body write, and the listing fails with exit 63 (BL-1389).
    private sealed class Listing(SftpSession session, byte[] handle, byte[] directory, bool listOnly, DownloadSizeLimit maxFileSize, Stream output, ITransferProgress progress, SshStateTrace trace)
    {
        private long written;

        internal async ValueTask<TransferResult> RunAsync(CancellationToken cancellationToken)
        {
            try
            {
                return await ListUntilEndAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (SshConnectionFailure.Is(exception))
            {
                return Failure(SshTransferException.SshLayerError());
            }
            catch (SshTransferException exception)
            {
                return Failure(exception);
            }
        }

        private TransferResult Failure(SshTransferException failure) => TransferResult.Failure(failure.ExitCode, failure.Message, written);

        private async ValueTask<TransferResult> ListUntilEndAsync(CancellationToken cancellationToken)
        {
            trace.Enter("SSH_SFTP_READDIR");
            IReadOnlyList<SftpDirectoryEntry> entries;
            while ((entries = await session.ReadDirectoryAsync(handle, cancellationToken).ConfigureAwait(false)).Count > 0)
            {
                foreach (SftpDirectoryEntry entry in entries)
                {
                    byte[] line = await LineForAsync(entry, cancellationToken).ConfigureAwait(false);
                    int allowed = maxFileSize.AllowedOf(written, line.Length);
                    await output.WriteAsync(line.AsMemory(0, allowed), cancellationToken).ConfigureAwait(false);
                    written += allowed;
                    progress.ReportDownloaded(written, null);
                    if (allowed < line.Length)
                    {
                        return maxFileSize.Exceeded(written);
                    }

                    TraceLineWritten();
                }
            }

            return TransferResult.Success(written);
        }

        // Measured (BL-1204): curl writes a -l name from SSH_SFTP_READDIR itself, and ends a
        // long-name line in SSH_SFTP_READDIR_BOTTOM before reading the next entry.
        private void TraceLineWritten()
        {
            if (!listOnly)
            {
                trace.Enter("SSH_SFTP_READDIR_BOTTOM");
                trace.Enter("SSH_SFTP_READDIR");
            }
        }

        // Measured: the name alone with -l; otherwise the long name, and for a symbolic link
        // " -> " and its target, which is the entry's own name when READLINK answers OK.
        private async ValueTask<byte[]> LineForAsync(SftpDirectoryEntry entry, CancellationToken cancellationToken)
        {
            if (listOnly)
            {
                return [.. entry.FileName, (byte)'\n'];
            }

            if (!entry.IsSymbolicLink)
            {
                return [.. UpToNul(entry.LongName), (byte)'\n'];
            }

            trace.Enter("SSH_SFTP_READDIR_LINK");
            byte[] fileName = [.. UpToNul(entry.FileName)];
            byte[]? target = await session.ReadLinkAsync([.. directory, .. fileName], cancellationToken).ConfigureAwait(false);
            return [.. UpToNul(entry.LongName), .. LinkArrow, .. UpToNul(target ?? fileName), (byte)'\n'];
        }
    }
}
