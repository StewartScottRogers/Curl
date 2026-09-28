using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Imap;

/// <summary>
/// Serves the <c>imap</c> and <c>imaps</c> schemes: connects, reads the greeting, asks for
/// the server's <c>CAPABILITY</c>, upgrades with <c>STARTTLS</c> as <c>--ssl</c> and
/// <c>--ssl-reqd</c> ask, and closes with <c>LOGOUT</c>, as curl 8.21.0 does (BL-553).
/// </summary>
/// <remarks>
/// <para>
/// The connection goes to the URL's host and port (143 for <c>imap</c>, 993 for <c>imaps</c>
/// unless the URL names one), through <see cref="ITransferContext.Proxy" /> when one is set;
/// an <c>imaps</c> connection is TLS from its first byte (ADR-0121 §4). A failed connect is
/// returned as the connector reported it. Commands are tagged <c>A001</c>, <c>A002</c>, ...
/// as <see cref="ImapControlChannel" /> describes, and the conversation and the exit code of
/// each failure are described on <see cref="ImapSession" />.
/// </para>
/// <para>
/// Authentication (BL-554), fetching (BL-555), listing, searching and custom commands
/// (BL-556) and the upload (BL-557) are not implemented yet: once the session is open the
/// handler closes it with <c>LOGOUT</c> and reports success. Cancellation leaves as an
/// exception.
/// </para>
/// </remarks>
/// <param name="connector">
/// Supplies the connection to the URL's host and port, made with TLS for <c>imaps</c>
/// (ADR-0005). No <see cref="System.Net.Sockets.Socket" /> is ever constructed here.
/// </param>
/// <param name="tlsProvider">Upgrades the connection after an accepted <c>STARTTLS</c>.</param>
public sealed class ImapProtocolHandler(IConnector connector, ITlsProvider tlsProvider) : IProtocolHandler
{
    /// <summary>The scheme whose connection is TLS from its first byte.</summary>
    private const string ImplicitTlsScheme = "imaps";

    private static readonly string[] Schemes = ["imap", ImplicitTlsScheme];

    private readonly IConnector connector = connector ?? throw new ArgumentNullException(nameof(connector));

    private readonly ITlsProvider tlsProvider = tlsProvider ?? throw new ArgumentNullException(nameof(tlsProvider));

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
        var session = new ImapSession(
            new ImapControlChannel(connection, context.CancellationToken), tlsProvider, context, implicitTls);
        await using (session.ConfigureAwait(false))
        {
            return await session.RunAsync().ConfigureAwait(false);
        }
    }
}
