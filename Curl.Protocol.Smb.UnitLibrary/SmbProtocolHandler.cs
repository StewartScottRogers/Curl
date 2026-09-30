using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Smb;

/// <summary>
/// Serves the <c>smb</c> and <c>smbs</c> schemes on every platform, speaking curl 8.21.0's
/// SMBv1 (ADR-0200) to download a file, or with <c>-T</c> to upload one: parses the share
/// and file from the URL's path, connects, negotiates the <c>NT LM 0.12</c> dialect, sets
/// up a session authenticated with NTLMv1 responses, then connects to the share, reads the
/// file to the output or writes the upload source to it, created or truncated, and
/// disconnects (<see cref="SmbFileTransfer" />).
/// </summary>
/// <remarks>
/// The connector supplies the connection to the URL's host and port, in TLS from the
/// first byte for <c>smbs</c>; no <see cref="System.Net.Sockets.Socket" /> is ever
/// constructed here. The outcomes, in curl's order: a path with no share is refused before connecting with
/// exit 3; a connect failure is returned as the connector reported it; a transfer with no
/// user (<see cref="ITransferContext.Credentials" />) is refused once connected with exit
/// 67 and nothing sent; then <see cref="SmbSessionEstablisher" />'s outcomes; then an
/// upload source that cannot seek, such as <c>-T -</c>'s standard input, is refused with
/// exit 55 and nothing more sent, as curl refuses an upload of unknown size; then
/// <see cref="SmbFileTransfer" />'s. The port
/// defaults to 445 for both schemes. A server that closes the connection mid-exchange is
/// waited on until <see cref="ITransferContext.CancellationToken" /> ends the transfer, as
/// curl waits for <c>-m</c>. Each step is written to
/// <see cref="ITransferContext.DiagnosticLog" /> under the <c>smb</c> component
/// (<see cref="SmbTransferLog" />).
/// </remarks>
public sealed class SmbProtocolHandler : IProtocolHandler
{
    private const int DefaultPort = 445;

    private const string SecureScheme = "smbs";

    private static readonly string[] Schemes = ["smb", SecureScheme];

    private readonly IConnector connector;

    private readonly string operatingSystem;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmbProtocolHandler" /> class that sends
    /// the host triple of the platform this process runs on.
    /// </summary>
    /// <param name="connector">Supplies the connection to the URL's host and port.</param>
    public SmbProtocolHandler(IConnector connector)
        : this(connector, SmbCurlOperatingSystem.Current)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SmbProtocolHandler" /> class that sends
    /// <paramref name="operatingSystem" /> as curl's host triple.
    /// </summary>
    /// <param name="connector">Supplies the connection to the URL's host and port.</param>
    /// <param name="operatingSystem">The host triple to send in the session setup.</param>
    /// <exception cref="ArgumentNullException"><paramref name="connector" /> is <see langword="null" />.</exception>
    internal SmbProtocolHandler(IConnector connector, string operatingSystem)
    {
        this.connector = connector ?? throw new ArgumentNullException(nameof(connector));
        this.operatingSystem = operatingSystem;
    }

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

        long started = context.TimeProvider.GetTimestamp();
        TransferResult result = await ConnectAndTransferAsync(context).ConfigureAwait(false);
        new SmbTransferLog(context.DiagnosticLog).Ended(result, context.TimeProvider.GetElapsedTime(started));
        return result;
    }

    private async ValueTask<TransferResult> ConnectAndTransferAsync(ITransferContext context)
    {
        CurlUrl url = context.Url;
        if (SmbUrlPath.TryParse(url.AbsolutePath, out SmbUrlPath? path) is { } pathError)
        {
            return TransferResult.Failure(CurlExitCode.UrlMalformat, pathError);
        }

        var target = new ConnectTarget(url.IdnHost, url.IsDefaultPort ? DefaultPort : url.Port, url.Scheme == SecureScheme)
        {
            Proxy = context.Proxy,
            Events = context.Events,
            DiagnosticLog = context.DiagnosticLog,
        };
        ConnectResult connect = await connector.ConnectAsync(target, context.CancellationToken).ConfigureAwait(false);
        if (connect.Connection is not { } connection)
        {
            return new TransferResult(connect.ExitCode, 0, connect.ErrorMessage) { IsConnectionRefused = connect.IsConnectionRefused };
        }

        await using (connection.ConfigureAwait(false))
        {
            return await TransferAsync(connection, context, path!).ConfigureAwait(false);
        }
    }

    private async ValueTask<TransferResult> TransferAsync(IConnection connection, ITransferContext context, SmbUrlPath path)
    {
        if (context.Credentials is not { } credentials)
        {
            return TransferResult.Failure(CurlExitCode.LoginDenied, SmbMessages.LoginDenied);
        }

        var transferLog = new SmbTransferLog(context.DiagnosticLog);
        var reader = new SmbMessageReader(connection, context.TimeProvider, transferLog);
        var establisher = new SmbSessionEstablisher(connection, reader, operatingSystem, transferLog);
        (ushort userId, TransferResult? failure) = await establisher.EstablishAsync(
            credentials.Password,
            SmbIdentity.Split(credentials.UserName, context.Url.IdnHost),
            context.CancellationToken).ConfigureAwait(false);
        if (failure is not null)
        {
            return failure;
        }

        return context.Upload is { CanSeek: false }
            ? TransferResult.Failure(CurlExitCode.SendError, SmbMessages.UploadSizeUnknown)
            : await new SmbFileTransfer(connection, reader, userId, context)
                .TransferAsync(context.Url.IdnHost, path)
                .ConfigureAwait(false);
    }
}
