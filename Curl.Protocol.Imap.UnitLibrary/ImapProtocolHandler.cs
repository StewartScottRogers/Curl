using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Imap;

/// <summary>
/// Serves the <c>imap</c> and <c>imaps</c> schemes: connects, reads the greeting, asks for
/// the server's <c>CAPABILITY</c>, upgrades with <c>STARTTLS</c> as <c>--ssl</c> and
/// <c>--ssl-reqd</c> ask, logs in with <c>AUTHENTICATE</c> or <c>LOGIN</c>, fetches the
/// message a URL such as <c>imap://host/INBOX;UID=1</c> names with <c>SELECT</c> and
/// <c>FETCH</c>, and closes with <c>LOGOUT</c>, as curl 8.21.0 does (BL-553, BL-554, BL-555).
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
/// A URL naming no message sends <c>LIST</c>, one with a search query <c>SEARCH</c>, and
/// <c>-X</c> its own command, each writing the untagged responses curl writes (BL-556). A
/// <c>-T</c> upload is appended to the URL's mailbox with <c>APPEND</c> and the
/// <c>--upload-flags</c> flags (BL-557). Cancellation leaves as an exception.
/// </para>
/// </remarks>
public sealed class ImapProtocolHandler : IProtocolHandler
{
    /// <summary>The scheme whose connection is TLS from its first byte.</summary>
    private const string ImplicitTlsScheme = "imaps";

    private static readonly string[] Schemes = ["imap", ImplicitTlsScheme];

    private readonly IConnector connector;

    private readonly ITlsProvider tlsProvider;

    private readonly ISaslAuthenticator? saslAuthenticator;

    /// <summary>
    /// Initializes a handler that serves <c>imap</c> and <c>imaps</c> and logs in only with
    /// <c>LOGIN</c>, never <c>AUTHENTICATE</c>.
    /// </summary>
    /// <param name="connector">
    /// Supplies the connection to the URL's host and port, made with TLS for <c>imaps</c>
    /// (ADR-0005). No <see cref="System.Net.Sockets.Socket" /> is ever constructed here.
    /// </param>
    /// <param name="tlsProvider">Upgrades the connection after an accepted <c>STARTTLS</c>.</param>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null" />.</exception>
    public ImapProtocolHandler(IConnector connector, ITlsProvider tlsProvider)
    {
        this.connector = connector ?? throw new ArgumentNullException(nameof(connector));
        this.tlsProvider = tlsProvider ?? throw new ArgumentNullException(nameof(tlsProvider));
    }

    /// <summary>
    /// Initializes a handler that serves <c>imap</c> and <c>imaps</c> and logs in with
    /// <c>AUTHENTICATE</c> through <paramref name="saslAuthenticator" /> when the server offers
    /// a mechanism it can use, else with <c>LOGIN</c> (ADR-0121).
    /// </summary>
    /// <param name="connector">Supplies the connection.</param>
    /// <param name="tlsProvider">Upgrades the connection after an accepted <c>STARTTLS</c>.</param>
    /// <param name="saslAuthenticator">Chooses the SASL mechanism and answers its challenges.</param>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null" />.</exception>
    public ImapProtocolHandler(IConnector connector, ITlsProvider tlsProvider, ISaslAuthenticator saslAuthenticator)
        : this(connector, tlsProvider)
    {
        this.saslAuthenticator = saslAuthenticator ?? throw new ArgumentNullException(nameof(saslAuthenticator));
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
        TransferResult result = await TransferAsync(context).ConfigureAwait(false);
        ImapDiagnosticLogLines.TransferEnded(context.DiagnosticLog, result, context.TimeProvider, started);
        return result;
    }

    /// <summary>
    /// Connects, runs the session and writes the lines <c>-v</c> ends the transfer with.
    /// </summary>
    private async ValueTask<TransferResult> TransferAsync(ITransferContext context)
    {
        CurlUrl url = context.Url;
        bool implicitTls = url.Scheme == ImplicitTlsScheme;
        var connectEvents = new ConnectionOpenedCapturingTransferEvents(context.Events);
        var target = new ConnectTarget(url.IdnHost, url.Port, implicitTls)
        {
            Proxy = context.Proxy,
            Events = connectEvents,
            DiagnosticLog = context.DiagnosticLog,
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
            var session = new ImapSession(
                new ImapControlChannel(connection, context.Events, context.CancellationToken, context.DiagnosticLog, context.DumpHeaderOutput), tlsProvider, saslAuthenticator, context, implicitTls, connectEvents.Opened);
            TransferResult result;
            await using (session.ConfigureAwait(false))
            {
                result = await session.RunAsync().ConfigureAwait(false);
            }

            ReportConnectionEnd(context.Events, result, session.Phase, target, connected.ConnectionNumber);
            return result;
        }
    }

    /// <summary>
    /// Writes the lines curl 8.21.0's <c>-v</c> ends an IMAP transfer with (BL-559): a failure's
    /// message first, when curl reports it (<see cref="ImapSessionMessages.IsWrittenByVerbose" />);
    /// then <c>Connection #N to host H:P left intact</c> for a success or a failure once the
    /// literal was through, <c>shutting down connection #N</c> for one while logged in, and
    /// <c>closing connection #N</c> for one before logging in or inside a literal.
    /// </summary>
    private static void ReportConnectionEnd(ITransferEvents events, TransferResult result, ImapSessionPhase phase, ConnectTarget target, long connectionNumber)
    {
        if (result.ExitCode != CurlExitCode.Ok && ImapSessionMessages.IsWrittenByVerbose(result.ErrorMessage!))
        {
            events.ReportInfo(result.ErrorMessage!);
        }

        events.ReportInfo(
            result.ExitCode == CurlExitCode.Ok || phase == ImapSessionPhase.Completing ? ImapInfoLines.LeftIntact(connectionNumber, target.Host, target.Port)
            : phase == ImapSessionPhase.Performing ? ImapInfoLines.ShuttingDown(connectionNumber)
            : ImapInfoLines.Closing(connectionNumber));
    }
}
