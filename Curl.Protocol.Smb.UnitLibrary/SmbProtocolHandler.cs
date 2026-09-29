using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Smb;

/// <summary>
/// Serves the <c>smb</c> and <c>smbs</c> schemes on every platform, speaking curl 8.21.0's
/// SMBv1 (ADR-0200). So far it opens the session: parses the share from the URL's path,
/// connects, negotiates the <c>NT LM 0.12</c> dialect and sets up a session authenticated
/// with NTLMv1 responses. Connecting to the share and reading the file follow (BL-596);
/// until they do, a session the server accepts ends the transfer with exit 0 and nothing
/// written.
/// </summary>
/// <remarks>
/// The connector supplies the connection to the URL's host and port, in TLS from the
/// first byte for <c>smbs</c>; no <see cref="System.Net.Sockets.Socket" /> is ever
/// constructed here. The outcomes, in curl's order: a path with no share is refused before connecting with
/// exit 3; a connect failure is returned as the connector reported it; a transfer with no
/// user (<see cref="ITransferContext.Credentials" />) is refused once connected with exit
/// 67 and nothing sent; then <see cref="SmbSessionEstablisher" />'s outcomes. The port
/// defaults to 445 for both schemes. A server that closes the connection mid-exchange is
/// waited on until <see cref="ITransferContext.CancellationToken" /> ends the transfer, as
/// curl waits for <c>-m</c>.
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

        CurlUrl url = context.Url;
        if (SmbUrlPath.TryParse(url.AbsolutePath, out _) is { } pathError)
        {
            return TransferResult.Failure(CurlExitCode.UrlMalformat, pathError);
        }

        var target = new ConnectTarget(url.IdnHost, url.IsDefaultPort ? DefaultPort : url.Port, url.Scheme == SecureScheme)
        {
            Proxy = context.Proxy,
            Events = context.Events,
        };
        ConnectResult connect = await connector.ConnectAsync(target, context.CancellationToken).ConfigureAwait(false);
        if (connect.Connection is not { } connection)
        {
            return new TransferResult(connect.ExitCode, 0, connect.ErrorMessage) { IsConnectionRefused = connect.IsConnectionRefused };
        }

        await using (connection.ConfigureAwait(false))
        {
            return await OpenSessionAsync(connection, context).ConfigureAwait(false);
        }
    }

    private async ValueTask<TransferResult> OpenSessionAsync(IConnection connection, ITransferContext context)
    {
        if (context.Credentials is not { } credentials)
        {
            return TransferResult.Failure(CurlExitCode.LoginDenied, SmbMessages.LoginDenied);
        }

        var establisher = new SmbSessionEstablisher(connection, new SmbMessageReader(connection, context.TimeProvider), operatingSystem);
        (_, TransferResult? failure) = await establisher.EstablishAsync(
            credentials.Password,
            SmbIdentity.Split(credentials.UserName, context.Url.IdnHost),
            context.CancellationToken).ConfigureAwait(false);
        return failure ?? TransferResult.Success(0);
    }
}
