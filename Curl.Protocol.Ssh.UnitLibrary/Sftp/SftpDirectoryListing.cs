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
    /// Lists the directory at <paramref name="urlPath" /> into <paramref name="output" />.
    /// </summary>
    /// <param name="urlPath">The URL's path, with its percent-escapes, ending with a slash.</param>
    /// <param name="listOnly">Whether to write only the names, curl's <c>-l</c>.</param>
    /// <param name="noBody">Whether to stop before <c>OPENDIR</c>, curl's <c>-I</c>: as measured, nothing is written.</param>
    /// <param name="output">Where the listing goes.</param>
    /// <param name="progress">Told the bytes written so far after each line.</param>
    /// <param name="cancellationToken">Cancels the listing.</param>
    /// <param name="quotes">The <c>-Q</c> commands, run after <c>REALPATH</c> and after the handle's close; none when not given.</param>
    /// <returns>
    /// Success with the bytes written; the <c>READDIR</c> status's exit code and <c>Could not
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
        SftpQuoteCommands? quotes = null)
    {
        quotes ??= SftpQuoteCommands.None;
        SftpSession session = await SftpSession.StartAsync(transport, cancellationToken).ConfigureAwait(false);
        return await session.CloseChannelOnFailureAsync(
            async () =>
            {
                byte[] homeDirectory = await SshConnectionFailure.ReportAsSshLayerErrorAsync(
                    () => session.RealPathAsync(HomeDirectory, cancellationToken)).ConfigureAwait(false);
                byte[] directory = SftpRemotePath.ResolveUrlPath(urlPath, homeDirectory);
                await quotes.RunBeforeTransferAsync(session, homeDirectory, directory, cancellationToken).ConfigureAwait(false);
                if (noBody)
                {
                    return await quotes.FinishAsync(session, null, homeDirectory, TransferResult.Success(0), cancellationToken).ConfigureAwait(false);
                }

                byte[] handle = await SshConnectionFailure.ReportAsSshLayerErrorAsync(
                    () => session.OpenDirectoryAsync(directory, cancellationToken)).ConfigureAwait(false);
                Listing listing = new(session, handle, directory, listOnly, output, progress);
                TransferResult result = await listing.RunAsync(cancellationToken).ConfigureAwait(false);
                return await quotes.FinishAsync(session, handle, homeDirectory, result, cancellationToken).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
    }

    // curl copies each name into a C string, so a NUL byte ends what it prints.
    private static ReadOnlySpan<byte> UpToNul(byte[] bytes)
    {
        int end = Array.IndexOf(bytes, (byte)0);
        return end < 0 ? bytes : bytes.AsSpan(0, end);
    }

    // One listing of the directory to the output, counting the bytes as they go.
    private sealed class Listing(SftpSession session, byte[] handle, byte[] directory, bool listOnly, Stream output, ITransferProgress progress)
    {
        private long written;

        internal async ValueTask<TransferResult> RunAsync(CancellationToken cancellationToken)
        {
            try
            {
                await ListUntilEndAsync(cancellationToken).ConfigureAwait(false);
                return TransferResult.Success(written);
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

        private async ValueTask ListUntilEndAsync(CancellationToken cancellationToken)
        {
            IReadOnlyList<SftpDirectoryEntry> entries;
            while ((entries = await session.ReadDirectoryAsync(handle, cancellationToken).ConfigureAwait(false)).Count > 0)
            {
                foreach (SftpDirectoryEntry entry in entries)
                {
                    byte[] line = await LineForAsync(entry, cancellationToken).ConfigureAwait(false);
                    await output.WriteAsync(line, cancellationToken).ConfigureAwait(false);
                    written += line.Length;
                    progress.ReportDownloaded(written, null);
                }
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

            byte[] fileName = [.. UpToNul(entry.FileName)];
            byte[]? target = await session.ReadLinkAsync([.. directory, .. fileName], cancellationToken).ConfigureAwait(false);
            return [.. UpToNul(entry.LongName), .. LinkArrow, .. UpToNul(target ?? fileName), (byte)'\n'];
        }
    }
}
