using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Pop3;

/// <summary>
/// Serves the <c>pop3</c> and <c>pop3s</c> schemes: connects, reads the greeting, asks for
/// the capabilities with <c>CAPA</c>, upgrades with <c>STLS</c> as <c>--ssl</c> and
/// <c>--ssl-reqd</c> ask, logs in with SASL <c>AUTH</c>, <c>APOP</c> or <c>USER</c>/<c>PASS</c>,
/// lists the maildrop with <c>LIST</c>, retrieves the URL's message with <c>RETR</c> or sends
/// <c>-X</c>'s command, and closes with <c>QUIT</c>, as curl 8.21.0 does (BL-547 to BL-550); a
/// successful transfer leaves the connection logged in for the next URL with the same login
/// instead, which sends <c>QUIT</c> only when the run ends (<see cref="Pop3KeptConnection" />, BL-3167).
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
/// <c>-X</c>, <c>-l</c> and <c>-I</c> behave as <see cref="Pop3Command" /> describes; like
/// curl's, <c>-I</c> changes nothing. Cancellation leaves as an exception.
/// </para>
/// </remarks>
public sealed class Pop3ProtocolHandler : IProtocolHandler
{
    /// <summary>The scheme whose connection is TLS from its first byte.</summary>
    private const string ImplicitTlsScheme = "pop3s";

    private static readonly string[] Schemes = ["pop3", ImplicitTlsScheme];

    private readonly IConnector connector;

    private readonly ITlsProvider tlsProvider;

    private readonly ISaslAuthenticator? saslAuthenticator;

    /// <summary>
    /// Initializes a handler that serves <c>pop3</c> and <c>pop3s</c>.
    /// </summary>
    /// <param name="connector">
    /// Supplies the connection to the URL's host and port, made with TLS for <c>pop3s</c>
    /// (ADR-0005). No <see cref="System.Net.Sockets.Socket" /> is ever constructed here.
    /// </param>
    /// <param name="tlsProvider">Upgrades the connection after an accepted <c>STLS</c>.</param>
    /// <param name="saslAuthenticator">
    /// Chooses and runs the SASL mechanism for <c>AUTH</c>, or <see langword="null" /> to log
    /// in only with <c>APOP</c> or <c>USER</c>/<c>PASS</c> (ADR-0121).
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="connector" /> or <paramref name="tlsProvider" /> is <see langword="null" />.
    /// </exception>
    public Pop3ProtocolHandler(IConnector connector, ITlsProvider tlsProvider, ISaslAuthenticator? saslAuthenticator = null)
    {
        this.connector = connector ?? throw new ArgumentNullException(nameof(connector));
        this.tlsProvider = tlsProvider ?? throw new ArgumentNullException(nameof(tlsProvider));
        this.saslAuthenticator = saslAuthenticator;
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
        using var connectPhase = new Pop3ConnectPhaseLimit(context, started);
        TransferResult result = await TransferAsync(context, connectPhase).ConfigureAwait(false);
        Pop3DiagnosticLogLines.TransferEnded(context.DiagnosticLog, result, context.TimeProvider, started);
        return result;
    }

    /// <summary>
    /// Whether <paramref name="value" /> holds a byte below 0x20, which curl 8.22.0 refuses in a
    /// user name or password: a CR or LF would end a command line and let a second command follow it.
    /// </summary>
    private static bool HasControlByte(string? value) => value is not null && value.Any(character => character < ' ');

    /// <summary>
    /// Refuses credentials holding a control byte, then connects and transfers.
    /// </summary>
    private async ValueTask<TransferResult> TransferAsync(ITransferContext context, Pop3ConnectPhaseLimit connectPhase)
    {
        if (HasControlByte(context.Credentials?.UserName) || HasControlByte(context.Credentials?.Password))
        {
            // curl 8.22.0 refuses such credentials before it connects, so no command is sent (BL-2177).
            return TransferResult.Failure(CurlExitCode.BadFunctionArgument, Pop3SessionMessages.CredentialsHaveControlCode);
        }

        return await ConnectAndTransferAsync(context, connectPhase).ConfigureAwait(false);
    }

    /// <summary>
    /// Connects, or reuses a kept connection for the same login, runs the session on it and
    /// writes the lines <c>-v</c> ends the transfer with.
    /// </summary>
    private async ValueTask<TransferResult> ConnectAndTransferAsync(ITransferContext context, Pop3ConnectPhaseLimit connectPhase)
    {
        CurlUrl url = context.Url;
        bool implicitTls = url.Scheme == ImplicitTlsScheme;
        var connectEvents = new ConnectionOpenedCapturingTransferEvents(context.Events);
        var target = new ConnectTarget(url.IdnHost, url.Port, implicitTls)
        {
            Proxy = context.Proxy,
            Events = connectEvents,
            DiagnosticLog = context.DiagnosticLog,
            PoolScheme = url.Scheme,
        };
        string loginKey = Pop3KeptConnection.LoginKeyOf(context);
        ConnectResult connected = await ConnectAsync(target, loginKey, context.CancellationToken).ConfigureAwait(false);
        if (connected.Connection is not { } connection)
        {
            return new TransferResult(connected.ExitCode, 0, connected.ErrorMessage)
            {
                IsConnectionRefused = connected.IsConnectionRefused,
            };
        }

        context.Progress.ReportTransferStarted();
        await using (connection.ConfigureAwait(false))
        {
            Pop3KeptConnection? reused = connected.IsReused ? (connection.Session as Pop3KeptConnection)?.ResumedOn(connection) : null;
            Pop3KeptConnection kept = reused ?? new Pop3KeptConnection(connection, loginKey, context.TimeProvider);
            var session = new Pop3Session(
                new Pop3ControlChannel(connection, context.Events, context.CancellationToken, context.DiagnosticLog, context.DumpHeaderOutput), tlsProvider, context, implicitTls, saslAuthenticator, connectEvents.Opened, kept, reused is not null, connectPhase);
            TransferResult result;
            await using (session.ConfigureAwait(false))
            {
                result = await session.RunAsync().ConfigureAwait(false);
            }

            ReportConnectionEnd(context.Events, result, session.IsOpen, target, connected.ConnectionNumber);
            return result;
        }
    }

    /// <summary>
    /// Connects, which the run's connection cache may serve from a connection a previous URL
    /// left logged in (BL-3167); one kept for another login is closed, with its <c>QUIT</c>, and
    /// the connect tried again, as curl reuses a POP3 connection only for the same login.
    /// </summary>
    private async ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, string loginKey, CancellationToken cancellationToken)
    {
        ConnectResult connected = await connector.ConnectAsync(target, cancellationToken).ConfigureAwait(false);
        while (connected is { IsReused: true, Connection: { Session: Pop3KeptConnection kept } connection } && !kept.Serves(loginKey))
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            connected = await connector.ConnectAsync(target, cancellationToken).ConfigureAwait(false);
        }

        return connected;
    }

    /// <summary>
    /// Writes the lines curl 8.21.0's <c>-v</c> ends a POP3 transfer with (BL-552): a success
    /// ends with <c>Connection #N to host H:P left intact</c>; a failure with its message, when
    /// curl reports it (<see cref="Pop3SessionMessages.IsWrittenByVerbose" />), then
    /// <c>shutting down connection #N</c> once the session was logged in and
    /// <c>closing connection #N</c> before or after a send failure, which sends no <c>QUIT</c>
    /// (BL-1252).
    /// </summary>
    private static void ReportConnectionEnd(ITransferEvents events, TransferResult result, bool sessionOpen, ConnectTarget target, long connectionNumber)
    {
        sessionOpen &= result.ExitCode != CurlExitCode.SendError;
        if (result.ExitCode == CurlExitCode.Ok)
        {
            events.ReportInfo(Pop3ConnectionInfoLines.LeftIntact(connectionNumber, target.Host, target.Port));
            return;
        }

        if (Pop3SessionMessages.IsWrittenByVerbose(result.ErrorMessage!))
        {
            events.ReportInfo(result.ErrorMessage!);
        }

        events.ReportInfo(sessionOpen ? Pop3ConnectionInfoLines.ShuttingDown(connectionNumber) : Pop3ConnectionInfoLines.Closing(connectionNumber));
    }
}
