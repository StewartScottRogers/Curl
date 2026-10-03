using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh.Connection;
using Curl.Protocol.Ssh.Transport;

namespace Curl.Protocol.Ssh.Scp;

/// <summary>
/// Downloads one file over SCP as curl 8.21.0 does through libssh2 1.11.1 (ADR-0225):
/// opens a <c>session</c> channel, runs <c>scp -pf &lt;path&gt;</c> with <c>exec</c>, reads
/// the file's header with <see cref="ScpFileHeaderReader" />, copies exactly the size the
/// header gives to the output, reporting progress, and closes the channel. No
/// acknowledgement follows the file's bytes.
/// </summary>
/// <param name="transport">The transport, after the user is authenticated.</param>
internal sealed class ScpFileDownload(SshTransport transport)
{
    /// <summary>
    /// The size curl takes as unknown: a <c>C</c> line with a size of -1 is read to the end
    /// of the channel, as measured.
    /// </summary>
    internal const long UnknownSize = -1;

    /// <summary>The most bytes one read of the channel asks for.</summary>
    internal const int ReadBufferSize = 32768;

    /// <summary>
    /// Gets where the download's <c>--trace-config ssh</c> state changes go (BL-1166);
    /// <see cref="SshStateTrace.Off" /> when not given.
    /// </summary>
    internal SshStateTrace Trace { get; init; } = SshStateTrace.Off;

    /// <summary>
    /// Downloads the file at <paramref name="urlPath" /> into <paramref name="output" />.
    /// </summary>
    /// <param name="urlPath">The URL's path, with its percent-escapes.</param>
    /// <param name="output">Where the file's bytes go.</param>
    /// <param name="progress">Told the bytes downloaded so far, and the size when known.</param>
    /// <param name="cancellationToken">Cancels the download.</param>
    /// <param name="maxFileSize">The <c>--max-filesize</c> limit; no limit when not given or 0 (BL-1328).</param>
    /// <returns>
    /// Success with the bytes downloaded; exit 18, <c>end of response with N bytes
    /// missing</c>, when the channel ends before the header's size; exit 18, <c>transfer
    /// closed with N bytes remaining to read</c>, for a negative size other than -1; exit
    /// 79, <c>Error in the SSH layer</c>, when the connection breaks during the copy; exit
    /// 63, <c>Exceeded the maximum allowed file size (N) with N bytes</c>, when a read would
    /// pass <paramref name="maxFileSize" />, after writing the bytes under it - each with
    /// the bytes written so far.
    /// </returns>
    /// <exception cref="SshTransferException">
    /// The server refused the channel or the <c>exec</c> request, or the connection broke
    /// before the header (exit 79, with libssh2's message for the step); or the header failed (<see cref="ScpFileHeaderReader.ReadFileSizeAsync" />).
    /// </exception>
    internal async ValueTask<TransferResult> DownloadAsync(
        string urlPath,
        Stream output,
        ITransferProgress progress,
        CancellationToken cancellationToken,
        long? maxFileSize = null)
    {
        Trace.Write("DO phase starts");
        Trace.Enter("SSH_SCP_TRANS_INIT");
        Trace.Enter("SSH_SCP_DOWNLOAD_INIT");
        SshSessionChannel channel = new(transport);
        await StartAsync(channel, ScpCommand.ForDownload(ScpRemotePath.Resolve(urlPath)), cancellationToken).ConfigureAwait(false);
        long size = await new ScpFileHeaderReader(channel).ReadFileSizeAsync(cancellationToken).ConfigureAwait(false);
        Trace.Rest();
        Trace.Write("DO phase is complete");
        TransferResult result = await new Copy(channel, size, new DownloadSizeLimit(maxFileSize), output, progress).RunAsync(cancellationToken).ConfigureAwait(false);
        Trace.Enter("SSH_SCP_DONE");
        Trace.Enter("SSH_SCP_CHANNEL_FREE");
        await CloseIgnoringFailureAsync(channel, cancellationToken).ConfigureAwait(false);
        Trace.Write("SCP DONE phase complete");
        Trace.Rest();
        return result;
    }

    // A connection closed or reset while either answer is awaited fails with libssh2's
    // message for that step, as measured (BL-1046).
    private static async ValueTask StartAsync(SshSessionChannel channel, byte[] command, CancellationToken cancellationToken)
    {
        if (!await RequireConnectionAsync(() => channel.OpenAsync(cancellationToken), SshTransferException.ScpChannelOpenBroken).ConfigureAwait(false))
        {
            throw SshTransferException.ScpChannelOpenFailed(channel.OpenFailureReasonCode);
        }

        if (!await RequireConnectionAsync(() => channel.RequestExecAsync(command, cancellationToken), SshTransferException.ScpExecRequestBroken).ConfigureAwait(false))
        {
            throw SshTransferException.ScpExecRequestDenied();
        }
    }

    private static async ValueTask<bool> RequireConnectionAsync(Func<ValueTask<bool>> step, Func<SshTransferException> broken)
    {
        try
        {
            return await step().ConfigureAwait(false);
        }
        catch (Exception exception) when (SshConnectionFailure.Is(exception))
        {
            throw broken();
        }
    }

    // Curl sends EOF, waits for the server's close and closes its own end; a broken
    // connection has already decided the transfer's outcome.
    private static async ValueTask CloseIgnoringFailureAsync(SshSessionChannel channel, CancellationToken cancellationToken)
    {
        try
        {
            await channel.CloseAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (SshConnectionFailure.Is(exception))
        {
        }
    }

    // One copy of the file's bytes to the output, counting them as they go; a read that
    // would pass the --max-filesize limit is cut at it and the copy fails with exit 63
    // (BL-1328).
    private sealed class Copy(SshSessionChannel channel, long size, DownloadSizeLimit maxFileSize, Stream output, ITransferProgress progress)
    {
        private readonly long? expectedSize = size == UnknownSize ? null : size;

        private long received;

        internal async ValueTask<TransferResult> RunAsync(CancellationToken cancellationToken)
        {
            try
            {
                return await CopyUntilEndAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (SshConnectionFailure.Is(exception))
            {
                SshTransferException failure = SshTransferException.SshLayerError();
                return TransferResult.Failure(failure.ExitCode, failure.Message, received);
            }
        }

        // Measured: a size of -5 reads nothing and fails as curl's transfer layer fails a
        // size it did not reach.
        private async ValueTask<TransferResult> CopyUntilEndAsync(CancellationToken cancellationToken)
        {
            if (size < UnknownSize)
            {
                return TransferResult.Failure(CurlExitCode.PartialFile, $"transfer closed with {size} bytes remaining to read", 0);
            }

            byte[] buffer = new byte[ReadBufferSize];
            while (received != size)
            {
                int read = await channel.ReadAsync(buffer.AsMemory(0, ReadLength()), cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    // Measured (BL-988): curl writes the channel's end on as an empty
                    // block, which --trace shows as 0 bytes of received data.
                    await output.WriteAsync(ReadOnlyMemory<byte>.Empty, cancellationToken).ConfigureAwait(false);
                    return EndOfChannel();
                }

                int allowed = maxFileSize.AllowedOf(received, read);
                await output.WriteAsync(buffer.AsMemory(0, allowed), cancellationToken).ConfigureAwait(false);
                received += allowed;
                progress.ReportDownloaded(received, expectedSize);
                if (allowed < read)
                {
                    return maxFileSize.Exceeded(received);
                }
            }

            return TransferResult.Success(received);
        }

        private int ReadLength() => (int)Math.Min(ReadBufferSize, (expectedSize ?? long.MaxValue) - received);

        // Measured: a file that ends 5 bytes short of its header's size is exit 18.
        private TransferResult EndOfChannel() =>
            expectedSize is { } known
                ? TransferResult.Failure(CurlExitCode.PartialFile, $"end of response with {known - received} bytes missing", received)
                : TransferResult.Success(received);
    }
}
