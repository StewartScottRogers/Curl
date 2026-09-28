using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Pop3;

/// <summary>
/// Serves the <c>pop3</c> and <c>pop3s</c> schemes: connects, reads the greeting, asks for
/// the capabilities with <c>CAPA</c>, upgrades with <c>STLS</c> as <c>--ssl</c> and
/// <c>--ssl-reqd</c> ask, lists the maildrop with <c>LIST</c> or retrieves the URL's message
/// with <c>RETR</c>, and closes with <c>QUIT</c>, as curl 8.21.0 does (BL-547, BL-549).
/// </summary>
/// <remarks>
/// <para>
/// The connection goes to the URL's host and port (110 for <c>pop3</c>, 995 for
/// <c>pop3s</c> unless the URL names one), through <see cref="ITransferContext.Proxy" /> when
/// one is set; a <c>pop3s</c> connection is TLS from its first byte (ADR-0121 §4). A failed
/// connect is returned as the connector reported it. The conversation and the exit code of
/// each failure are described on <see cref="Pop3Session" />.
/// </para>
/// <para>
/// Authentication (BL-548) and custom commands, <c>-l</c> and <c>-I</c> (BL-550) are not
/// implemented yet: the session lists or retrieves without logging in. Cancellation leaves
/// as an exception.
/// </para>
/// </remarks>
public sealed class Pop3ProtocolHandler : IProtocolHandler
{
    /// <summary>The scheme whose connection is TLS from its first byte.</summary>
    private const string ImplicitTlsScheme = "pop3s";

    private static readonly string[] Schemes = ["pop3", ImplicitTlsScheme];

    private readonly IConnector connector;

    private readonly ITlsProvider tlsProvider;

    /// <summary>
    /// Initializes a handler that serves <c>pop3</c> and <c>pop3s</c>.
    /// </summary>
    /// <param name="connector">
    /// Supplies the connection to the URL's host and port, made with TLS for <c>pop3s</c>
    /// (ADR-0005). No <see cref="System.Net.Sockets.Socket" /> is ever constructed here.
    /// </param>
    /// <param name="tlsProvider">Upgrades the connection after an accepted <c>STLS</c>.</param>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null" />.</exception>
    public Pop3ProtocolHandler(IConnector connector, ITlsProvider tlsProvider)
    {
        this.connector = connector ?? throw new ArgumentNullException(nameof(connector));
        this.tlsProvider = tlsProvider ?? throw new ArgumentNullException(nameof(tlsProvider));
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
        bool implicitTls = url.Scheme == ImplicitTlsScheme;
        var target = new ConnectTarget(url.IdnHost, url.Port, implicitTls)
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
            return await RunSessionAsync(connection, context, implicitTls).ConfigureAwait(false);
        }
    }

    private async ValueTask<TransferResult> RunSessionAsync(IConnection connection, ITransferContext context, bool implicitTls)
    {
        var session = new Pop3Session(
            new Pop3ControlChannel(connection, context.CancellationToken), tlsProvider, context, implicitTls);
        await using (session.ConfigureAwait(false))
        {
            return await session.RunAsync().ConfigureAwait(false);
        }
    }
}
