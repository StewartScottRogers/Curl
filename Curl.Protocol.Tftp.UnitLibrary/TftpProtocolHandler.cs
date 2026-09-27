using System.Text;

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
/// <c>timeout</c>, 6 by default and less under a connect timeout or maximum time
/// (RFC 2347, 2348, 2349); <c>blksize</c> is
/// <see cref="ITransferContext.TftpBlockSize" /> clamped to 8-65464, or 512 when it is not
/// given. <see cref="ITransferContext.TftpNoOptions" /> sends no options at all, on a read
/// or a write request. An option acknowledgement is answered with ACK 0 and its
/// <c>blksize</c> decides which block is the last; without one the block size is 512,
/// whatever was asked for. Every acknowledgement goes to the endpoint the packet it answers came
/// from, the server's transfer identifier, never back to port 69.
/// </para>
/// <para>
/// The write request carries the same options, its <c>timeout</c> derived the same
/// way, with <c>tsize</c> set to the upload's remaining length, or 0 when the upload
/// cannot seek. Each DATA block goes to the endpoint whose acknowledgement it follows; an option acknowledgement stands in for
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
/// A download or an upload re-sends its last packet to a silent server on curl's
/// schedule and ends with exit 7 when the read or write request goes unanswered, exit
/// 28 when the server falls silent mid-transfer or
/// <see cref="ITransferContext.MaxTime" /> passes, and exit 56 when a datagram comes
/// from an endpoint other than the server's. An upload also ends with exit 55 when the
/// server acknowledges the wrong block once too often (see <c>TftpDownload</c> and
/// <c>TftpUpload</c>).
/// </para>
/// <para>
/// Through an HTTP proxy (<see cref="ITransferContext.Proxy" /> of kind
/// <see cref="ProxyKind.Http" /> or <see cref="ProxyKind.Http10" />) no datagram is sent:
/// the MASQUE <c>connect-udp</c> request curl 8.21.0's Schannel build sends is written to
/// the proxy over <paramref name="proxyConnector" />, and the transfer ends with exit 7
/// <c>bind() failed; Invalid arguments</c>, before the file name is checked (ADR-0056,
/// rule 4; measured). Without a <paramref name="proxyConnector" /> the request is not
/// sent, but the result is the same. A proxy that cannot be reached is returned with the
/// connector's code and message unchanged.
/// </para>
/// </remarks>
/// <param name="proxyConnector">
/// Connects to the HTTP proxy the MASQUE request is sent to, or <see langword="null" /> to
/// send none.
/// </param>
/// <param name="proxyCredentialEncoding">
/// The encoding a proxy credential is base64-encoded from, the platform's (ADR-0059);
/// UTF-8 when <see langword="null" />.
/// </param>
public sealed class TftpProtocolHandler(
    IDatagramConnector connector,
    IConnector? proxyConnector = null,
    Encoding? proxyCredentialEncoding = null) : IProtocolHandler
{
    /// <summary>The port a <c>tftp://</c> URL that names none is sent to.</summary>
    private const int DefaultPort = 69;

    /// <summary>What curl 8.21.0's Schannel build reports for a <c>tftp://</c> URL through an HTTP proxy.</summary>
    private const string HttpProxyFailureMessage = "bind() failed; Invalid arguments";

    private static readonly string[] Schemes = ["tftp"];

    private readonly IDatagramConnector connector =
        connector ?? throw new ArgumentNullException(nameof(connector));

    private readonly Encoding proxyCredentialEncoding = proxyCredentialEncoding ?? Encoding.UTF8;

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
        var startTimestamp = context.TimeProvider.GetTimestamp();
        var port = context.Url.Port > 0 ? context.Url.Port : DefaultPort;

        return context.Proxy is { Kind: ProxyKind.Http or ProxyKind.Http10 } proxy
            ? await FailThroughHttpProxyAsync(context, proxy, port).ConfigureAwait(false)
            : await TransferFileAsync(context, port, startTimestamp).ConfigureAwait(false);
    }

    // Downloads or uploads the URL's file over a datagram channel to the server.
    private async ValueTask<TransferResult> TransferFileAsync(ITransferContext context, int port, long startTimestamp)
    {
        var fileName = Uri.UnescapeDataString(context.Url.AbsolutePath.TrimStart('/'));
        if (fileName.Length == 0)
        {
            return TransferResult.Failure(CurlExitCode.TftpIllegal, "Missing filename");
        }

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
                ? await new TftpUpload(context, channel, upload, startTimestamp).RunAsync(fileName).ConfigureAwait(false)
                : await new TftpDownload(context, channel, startTimestamp).RunAsync(fileName).ConfigureAwait(false);
        }
    }

    // Sends the MASQUE request to the proxy, when there is a connector to reach it, and
    // fails as curl 8.21.0 does without reading the proxy's reply.
    private async ValueTask<TransferResult> FailThroughHttpProxyAsync(ITransferContext context, ProxyEndpoint proxy, int port)
    {
        if (proxyConnector is not null)
        {
            var connected = await proxyConnector
                .ConnectAsync(new ConnectTarget(proxy.Host, proxy.Port, UseTls: false), context.CancellationToken)
                .ConfigureAwait(false);
            if (connected.Connection is not { } connection)
            {
                return TransferResult.Failure(connected.ExitCode, connected.ErrorMessage!);
            }

            await using (connection.ConfigureAwait(false))
            {
                var request = TftpMasqueRequest.Build(
                    context.Url.IdnHost, port, proxy, context.Http?.UserAgent, proxyCredentialEncoding);
                await connection.WriteAsync(request, context.CancellationToken).ConfigureAwait(false);
                await connection.FlushAsync(context.CancellationToken).ConfigureAwait(false);
            }
        }

        return TransferResult.Failure(CurlExitCode.CouldntConnect, HttpProxyFailureMessage);
    }
}
