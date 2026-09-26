using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Tftp;

/// <summary>
/// Serves the <c>tftp</c> scheme: downloads a file, or uploads
/// <see cref="ITransferContext.Upload" /> when it is set, over an
/// <see cref="IDatagramChannel" /> the way curl 8.21.0 does by default.
/// </summary>
/// <param name="connector">
/// Opens the datagram channel for each transfer (ADR-0005). No <c>Socket</c> is ever
/// constructed here, so the handler's tests run entirely against a scripted channel.
/// </param>
/// <remarks>
/// <para>
/// The read request carries curl's options, <c>tsize 0</c>, <c>blksize</c> and
/// <c>timeout 6</c> (RFC 2347, 2348, 2349); <c>blksize</c> is
/// <see cref="ITransferContext.TftpBlockSize" /> clamped to 8-65464, or 512 when it is not
/// given. <see cref="ITransferContext.TftpNoOptions" /> sends no options at all, on a read
/// or a write request. An option acknowledgement is answered with ACK 0 and its
/// <c>blksize</c> decides which block is the last; without one the block size is 512,
/// whatever was asked for. Every acknowledgement goes to the endpoint the packet it answers came
/// from, the server's transfer identifier, never back to port 69.
/// </para>
/// <para>
/// The write request carries the same options with <c>tsize</c> set to the upload's
/// remaining length, or 0 when the upload cannot seek. Each DATA block goes to the
/// endpoint whose acknowledgement it follows; an option acknowledgement stands in for
/// ACK 0 and sets the block size. The first block shorter than the block size is the
/// last, so an exact multiple ends with an empty block, and the upload succeeds once that
/// block is acknowledged, writing nothing to <see cref="ITransferContext.Output" />.
/// </para>
/// <para>
/// An ERROR packet ends the transfer with the exit code and message curl reports for its
/// code. A URL with no file name is exit 71 (<see cref="CurlExitCode.TftpIllegal" />) with
/// <c>Missing filename</c>, before any channel is opened, and a channel that will not open
/// is returned with the connector's code and message unchanged.
/// </para>
/// <para>
/// Retransmission and the timeout that ends a silent transfer are not implemented yet;
/// see the library's <c>CLAUDE.md</c>.
/// </para>
/// </remarks>
public sealed class TftpProtocolHandler(IDatagramConnector connector) : IProtocolHandler
{
    /// <summary>The port a <c>tftp://</c> URL that names none is sent to.</summary>
    private const int DefaultPort = 69;

    private static readonly string[] Schemes = ["tftp"];

    private readonly IDatagramConnector connector =
        connector ?? throw new ArgumentNullException(nameof(connector));

    /// <inheritdoc />
    public IReadOnlyCollection<string> SupportedSchemes => Schemes;

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">
    /// <paramref name="context" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <see cref="ITransferContext.CancellationToken" /> was cancelled.
    /// </exception>
    public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var fileName = Uri.UnescapeDataString(context.Url.AbsolutePath.TrimStart('/'));
        if (fileName.Length == 0)
        {
            return TransferResult.Failure(CurlExitCode.TftpIllegal, "Missing filename");
        }

        var port = context.Url.Port > 0 ? context.Url.Port : DefaultPort;
        var opened = await connector
            .OpenAsync(context.Url.IdnHost, port, context.CancellationToken)
            .ConfigureAwait(false);

        if (opened.Channel is not { } channel)
        {
            return TransferResult.Failure(opened.ExitCode, opened.ErrorMessage!);
        }

        await using (channel.ConfigureAwait(false))
        {
            return context.Upload is { } upload
                ? await new TftpUpload(context, channel, upload).RunAsync(fileName).ConfigureAwait(false)
                : await new TftpDownload(context, channel).RunAsync(fileName).ConfigureAwait(false);
        }
    }
}
