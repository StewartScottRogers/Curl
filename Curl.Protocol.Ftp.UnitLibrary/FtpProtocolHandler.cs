using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ftp;

/// <summary>
/// Serves the <c>ftp</c> scheme: logs in, enters passive mode and downloads the file the
/// URL names, or lists the directory a URL ending in <c>/</c> names, as curl 8.21.0 does.
/// </summary>
/// <param name="connector">
/// Supplies the control connection to the URL's host and port (21 unless the URL names
/// one), and the data connection to the same host and the port the server offers
/// (ADR-0005). No <see cref="System.Net.Sockets.Socket" /> is ever constructed here.
/// </param>
/// <remarks>
/// <para>
/// The login is <c>anonymous</c> with the password <c>ftp@example.com</c>, curl's
/// defaults, unless <see cref="ITransferContext.Credentials" /> names a user. The whole
/// conversation, and which failure ends with which exit code, is described on
/// <see cref="FtpDownloadSession" />; every step was measured against curl 8.21.0 with
/// <c>Record-CurlExchange.ps1 -Ftp</c> (BL-431). A refused login is exit 67
/// (<see cref="CurlExitCode.LoginDenied" />) with <c>Access denied: 430</c> for a
/// <c>430</c> reply.
/// </para>
/// <para>
/// When <see cref="ITransferContext.Proxy" /> is set both connections are tunnelled
/// through it; the connector opens the tunnels (ADR-0056). A failed connect is returned as
/// the connector reported it. <see cref="ITransferContext.Range" />,
/// <see cref="ITransferContext.ResumeFrom" /> and <see cref="ITransferContext.NoBody" />
/// are honoured as curl 8.21.0 honours <c>-r</c>, <c>-C</c> and <c>-I</c> (BL-438).
/// Uploads, <c>ftps</c>, active mode and every FTP-only option are not implemented yet.
/// Cancellation leaves as an exception.
/// </para>
/// </remarks>
public sealed class FtpProtocolHandler(IConnector connector) : IProtocolHandler
{
    /// <summary>The port an <c>ftp</c> URL without one connects to.</summary>
    private const int DefaultPort = 21;

    /// <summary>
    /// The one scheme this handler serves, as curl 8.21.0's <c>--version</c> protocol list
    /// names it.
    /// </summary>
    private static readonly string[] Schemes = ["ftp"];

    private readonly IConnector connector =
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

        CurlUrl url = context.Url;
        var target = new ConnectTarget(url.IdnHost, url.IsDefaultPort ? DefaultPort : url.Port, false)
        {
            Proxy = context.Proxy,
            Events = context.Events,
        };
        ConnectResult connected = await connector.ConnectAsync(target, context.CancellationToken).ConfigureAwait(false);
        if (connected.Connection is not { } connection)
        {
            return new TransferResult(connected.ExitCode, 0, connected.ErrorMessage)
            {
                IsConnectionRefused = connected.IsConnectionRefused,
            };
        }

        await using (connection.ConfigureAwait(false))
        {
            return await DownloadAsync(connection, context).ConfigureAwait(false);
        }
    }

    private async ValueTask<TransferResult> DownloadAsync(IConnection control, ITransferContext context)
    {
        var session = new FtpDownloadSession(connector, new FtpControlChannel(control, context.CancellationToken), context);
        await using (session.ConfigureAwait(false))
        {
            return await session.RunAsync().ConfigureAwait(false);
        }
    }
}
