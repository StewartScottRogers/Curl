using System.Globalization;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh.Connection;
using Curl.Protocol.Ssh.Transport;

namespace Curl.Protocol.Ssh.Scp;

/// <summary>
/// Uploads one file over SCP as curl 8.21.0 does through libssh2 1.11.1 (ADR-0258):
/// refuses a source of unknown size before opening anything, opens a <c>session</c>
/// channel, runs <c>scp -t &lt;path&gt;</c> with <c>exec</c>, waits for <c>scp</c>'s
/// acknowledgement, sends the <c>C</c> line with the file's mode, size and name, waits for
/// the second acknowledgement, sends the source's bytes in curl's 64 KiB blocks and closes
/// the channel. No zero byte follows the bytes and no <c>T</c> line precedes the
/// <c>C</c> line, as measured.
/// </summary>
/// <param name="transport">The transport, after the user is authenticated.</param>
internal sealed class ScpFileUpload(SshTransport transport)
{
    /// <summary>How many bytes of the source curl reads at a time: its 64 KiB upload buffer.</summary>
    internal const int ReadBufferSize = 65536;

    /// <summary>
    /// The mode the <c>C</c> line carries when <c>--create-file-mode</c> gives 0: curl then
    /// leaves the option unset and sends its default, measured 2026-09-29 as <c>C0644</c>.
    /// </summary>
    internal const UnixFileMode DefaultFileMode = TransferContext.DefaultCreateFileMode;

    private const string UnexpectedChannelClose = "Unexpected channel close";

    private const string InvalidAcknowledgement = "Invalid ACK response from remote";

    private const int PermissionBits = 0b111_111_111;

    /// <summary>
    /// Uploads <paramref name="upload" /> to the file at <paramref name="urlPath" />.
    /// </summary>
    /// <param name="urlPath">The URL's path, with its percent-escapes.</param>
    /// <param name="createFileMode">The permission bits the <c>C</c> line carries, curl's <c>--create-file-mode</c>.</param>
    /// <param name="upload">The source; its size is what remains of it, so it must be able to seek.</param>
    /// <param name="progress">Told the bytes sent so far and the size.</param>
    /// <param name="cancellationToken">Cancels the upload.</param>
    /// <returns>
    /// Success with the bytes sent, or exit 79, <c>Error in the SSH layer</c>, when the
    /// connection breaks during the bytes, with the bytes sent before it; either reports
    /// those bytes as <c>%{size_upload}</c>.
    /// </returns>
    /// <exception cref="SshTransferException">
    /// The source cannot seek (<see cref="SshTransferException.ScpUploadSizeUnknown" />), or
    /// the server refused the channel, the <c>exec</c> request or the file, or the
    /// connection broke before the bytes: exit 25 and libssh2's message.
    /// </exception>
    internal async ValueTask<TransferResult> UploadAsync(
        string urlPath,
        UnixFileMode createFileMode,
        Stream upload,
        ITransferProgress progress,
        CancellationToken cancellationToken)
    {
        long size = upload.CanSeek ? Math.Max(0, upload.Length - upload.Position) : throw SshTransferException.ScpUploadSizeUnknown();
        byte[] path = ScpRemotePath.Resolve(urlPath);
        SshSessionChannel channel = new(transport);
        await RunStepAsync(() => OpenAsync(channel, cancellationToken), "SCP failure").ConfigureAwait(false);
        if (await StartAsync(channel, path, FileLine(createFileMode, size, path), cancellationToken).ConfigureAwait(false) is { } failure)
        {
            await IgnoringConnectionFailureAsync(() => channel.CloseAtOnceAsync(cancellationToken)).ConfigureAwait(false);
            throw failure;
        }

        TransferResult result = await new Copy(channel, upload, progress, size).RunAsync(cancellationToken).ConfigureAwait(false);
        await IgnoringConnectionFailureAsync(() => channel.CloseAsync(cancellationToken)).ConfigureAwait(false);
        return result with { Report = new TransferReport { UploadSize = result.BytesTransferred } };
    }

    /// <summary>
    /// Builds the <c>C</c> line libssh2 sends: <c>C0</c>, the permission bits in octal, the
    /// size, and the last segment of the path as its name. Measured 2026-09-29:
    /// <c>C0644 10 new.txt</c>, <c>C0600</c> for <c>--create-file-mode 0600</c>, <c>C01</c> for
    /// <c>1</c> and <c>C0644</c> for <c>0</c>.
    /// </summary>
    /// <param name="createFileMode">The permission bits asked for; 0 means curl's default.</param>
    /// <param name="size">The file's size.</param>
    /// <param name="path">The remote path's bytes.</param>
    /// <returns>The line's bytes, with its line feed.</returns>
    internal static byte[] FileLine(UnixFileMode createFileMode, long size, byte[] path)
    {
        int mode = (int)(createFileMode == 0 ? DefaultFileMode : createFileMode) & PermissionBits;
        string fields = string.Create(CultureInfo.InvariantCulture, $"C0{Convert.ToString(mode, 8)} {size} ");
        byte[] name = path[(Array.LastIndexOf(path, (byte)'/') + 1)..];
        return [.. Encoding.ASCII.GetBytes(fields), .. name, (byte)'\n'];
    }

    private static async ValueTask OpenAsync(SshSessionChannel channel, CancellationToken cancellationToken)
    {
        if (!await channel.OpenAsync(cancellationToken).ConfigureAwait(false))
        {
            throw SshTransferException.ScpUploadChannelOpenFailed(channel.OpenFailureReasonCode);
        }
    }

    // Runs scp and sends the C line; the failure, if any, for the caller to close the
    // channel on and throw.
    private static async ValueTask<SshTransferException?> StartAsync(SshSessionChannel channel, byte[] path, byte[] fileLine, CancellationToken cancellationToken)
    {
        try
        {
            await RunStepAsync(() => StartScpAsync(channel, ScpCommand.ForUpload(path), cancellationToken), "SCP failure").ConfigureAwait(false);
            await RunStepAsync(() => SendFileLineAsync(channel, fileLine, cancellationToken), InvalidAcknowledgement).ConfigureAwait(false);
            return null;
        }
        catch (SshTransferException failure)
        {
            return failure;
        }
    }

    // Measured: scp -t sends a zero byte as soon as it starts; anything else is refused.
    private static async ValueTask StartScpAsync(SshSessionChannel channel, byte[] command, CancellationToken cancellationToken)
    {
        if (!await channel.RequestExecAsync(command, cancellationToken).ConfigureAwait(false))
        {
            throw SshTransferException.ScpUploadFailed("Unable to complete request for channel-process-startup");
        }

        await ReadAcknowledgementAsync(channel, InvalidAcknowledgement, cancellationToken).ConfigureAwait(false);
    }

    // Measured: an error line, a fatal line or any other byte in answer to the C line is
    // "failed to send file".
    private static async ValueTask SendFileLineAsync(SshSessionChannel channel, byte[] fileLine, CancellationToken cancellationToken)
    {
        await channel.SendAsync(fileLine, cancellationToken).ConfigureAwait(false);
        await ReadAcknowledgementAsync(channel, "failed to send file", cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask ReadAcknowledgementAsync(SshSessionChannel channel, string refusal, CancellationToken cancellationToken)
    {
        byte[] acknowledgement = new byte[1];
        int read = await channel.ReadAsync(acknowledgement, cancellationToken).ConfigureAwait(false);
        if (read == 0 || acknowledgement[0] != 0)
        {
            throw SshTransferException.ScpUploadFailed(read == 0 ? UnexpectedChannelClose : refusal);
        }
    }

    // Measured: the connection breaking while curl waits for the first acknowledgement is
    // "SCP failure", while it waits for the second "Invalid ACK response from remote".
    private static async ValueTask RunStepAsync(Func<ValueTask> step, string brokenConnection)
    {
        try
        {
            await step().ConfigureAwait(false);
        }
        catch (Exception exception) when (SshConnectionFailure.Is(exception))
        {
            throw SshTransferException.ScpUploadFailed(brokenConnection);
        }
    }

    // A broken connection has already decided the transfer's outcome; measured: nothing
    // the server says once the bytes are sent changes it either.
    private static async ValueTask IgnoringConnectionFailureAsync(Func<ValueTask> close)
    {
        try
        {
            await close().ConfigureAwait(false);
        }
        catch (Exception exception) when (SshConnectionFailure.Is(exception))
        {
        }
    }

    // One copy of the source's bytes into the channel, counting them as they go.
    private sealed class Copy(SshSessionChannel channel, Stream upload, ITransferProgress progress, long size)
    {
        private long sent;

        internal async ValueTask<TransferResult> RunAsync(CancellationToken cancellationToken)
        {
            try
            {
                return await CopyUntilEndAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (SshConnectionFailure.Is(exception))
            {
                SshTransferException failure = SshTransferException.SshLayerError();
                return TransferResult.Failure(failure.ExitCode, failure.Message, sent);
            }
        }

        private async ValueTask<TransferResult> CopyUntilEndAsync(CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[ReadBufferSize];
            int read;
            while ((read = await ReadSourceAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                await channel.SendAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                sent += read;
                progress.ReportUploaded(sent, size);
            }

            return TransferResult.Success(sent);
        }

        // A source that fails to read ends the upload as its end does, as the SFTP upload
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
    }
}
