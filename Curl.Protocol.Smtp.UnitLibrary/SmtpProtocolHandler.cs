using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Smtp;

/// <summary>
/// Serves the <c>smtp</c> and <c>smtps</c> schemes: connects, reads the greeting, greets
/// with <c>EHLO</c> (or <c>HELO</c>), upgrades with <c>STARTTLS</c> as <c>--ssl</c> and
/// <c>--ssl-reqd</c> ask, and closes with <c>QUIT</c>, as curl 8.21.0 does (BL-540).
/// </summary>
/// <remarks>
/// <para>
/// The connection goes to the URL's host and port (25 for <c>smtp</c>, 465 for <c>smtps</c>
/// unless the URL names one), through <see cref="ITransferContext.Proxy" /> when one is set;
/// an <c>smtps</c> connection is TLS from its first byte (ADR-0121 §4). A failed connect is
/// returned as the connector reported it. The <c>EHLO</c> domain is the URL's path
/// (<see cref="SmtpEhloDomain" />), and the conversation and the exit code of each failure
/// are described on <see cref="SmtpSession" />.
/// </para>
/// <para>
/// With <c>-T</c> and at least one <c>--mail-rcpt</c> the open session sends the message
/// (<see cref="SmtpMailTransaction" />, BL-542), after authenticating with <c>AUTH</c> when
/// the handler was given an <see cref="ISaslAuthenticator" /> (<see cref="SmtpSaslAuthentication" />,
/// BL-541). Without an upload or without a recipient it sends <c>VRFY</c>, <c>EXPN</c>,
/// <c>HELP</c> or the <c>-X</c> command and writes the replies to the output
/// (<see cref="SmtpCommandTransfer" />, BL-543). Cancellation leaves as an exception.
/// </para>
/// </remarks>
public sealed class SmtpProtocolHandler : IProtocolHandler
{
    /// <summary>The scheme whose connection is TLS from its first byte.</summary>
    private const string ImplicitTlsScheme = "smtps";

    private static readonly string[] Schemes = ["smtp", ImplicitTlsScheme];

    /// <summary>Names this machine in <c>EHLO</c> for a URL with an empty path.</summary>
    private static readonly Func<string> MachineHostName = Dns.GetHostName;

    private readonly IConnector connector;

    private readonly ITlsProvider tlsProvider;

    private readonly ISaslAuthenticator? saslAuthenticator;

    private readonly Func<string> localHostName;

    private readonly SmtpCommandLineText commandLineText;

    /// <summary>
    /// Initializes a handler that serves <c>smtp</c> and <c>smtps</c>.
    /// </summary>
    /// <param name="connector">
    /// Supplies the connection to the URL's host and port, made with TLS for <c>smtps</c>
    /// (ADR-0005). No <see cref="System.Net.Sockets.Socket" /> is ever constructed here.
    /// </param>
    /// <param name="tlsProvider">Upgrades the connection after an accepted <c>STARTTLS</c>.</param>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null" />.</exception>
    public SmtpProtocolHandler(IConnector connector, ITlsProvider tlsProvider)
        : this(connector, tlsProvider, saslAuthenticator: null, MachineHostName)
    {
    }

    /// <summary>
    /// Initializes a handler that serves <c>smtp</c> and <c>smtps</c> and authenticates with
    /// <c>AUTH</c> through <paramref name="saslAuthenticator" /> (ADR-0121).
    /// </summary>
    /// <param name="connector">Supplies the connection.</param>
    /// <param name="tlsProvider">Upgrades the connection after an accepted <c>STARTTLS</c>.</param>
    /// <param name="saslAuthenticator">Chooses the SASL mechanism and answers its challenges.</param>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null" />.</exception>
    public SmtpProtocolHandler(IConnector connector, ITlsProvider tlsProvider, ISaslAuthenticator saslAuthenticator)
        : this(connector, tlsProvider, saslAuthenticator ?? throw new ArgumentNullException(nameof(saslAuthenticator)), MachineHostName)
    {
    }

    /// <summary>
    /// Initializes a handler that names <paramref name="localHostName" />'s answer in
    /// <c>EHLO</c> for a URL with an empty path, so a test need not depend on the machine.
    /// </summary>
    /// <param name="connector">Supplies the connection.</param>
    /// <param name="tlsProvider">Upgrades the connection after an accepted <c>STARTTLS</c>.</param>
    /// <param name="saslAuthenticator">
    /// Chooses the SASL mechanism, or <see langword="null" /> for a handler that never sends
    /// <c>AUTH</c>.
    /// </param>
    /// <param name="localHostName">Supplies the local machine's host name.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="connector" />, <paramref name="tlsProvider" /> or
    /// <paramref name="localHostName" /> is <see langword="null" />.
    /// </exception>
    internal SmtpProtocolHandler(
        IConnector connector, ITlsProvider tlsProvider, ISaslAuthenticator? saslAuthenticator, Func<string> localHostName)
        : this(connector, tlsProvider, saslAuthenticator, localHostName, SmtpCommandLineText.Platform)
    {
    }

    /// <summary>
    /// Initializes a handler that sends addresses in <paramref name="commandLineText" />'s
    /// argv bytes, so a test can pin each platform's bytes on any host.
    /// </summary>
    /// <param name="connector">Supplies the connection.</param>
    /// <param name="tlsProvider">Upgrades the connection after an accepted <c>STARTTLS</c>.</param>
    /// <param name="saslAuthenticator">
    /// Chooses the SASL mechanism, or <see langword="null" /> for a handler that never sends
    /// <c>AUTH</c>.
    /// </param>
    /// <param name="localHostName">Supplies the local machine's host name.</param>
    /// <param name="commandLineText">The argv encoding of the platform being matched.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="connector" />, <paramref name="tlsProvider" />,
    /// <paramref name="localHostName" /> or <paramref name="commandLineText" /> is
    /// <see langword="null" />.
    /// </exception>
    internal SmtpProtocolHandler(
        IConnector connector,
        ITlsProvider tlsProvider,
        ISaslAuthenticator? saslAuthenticator,
        Func<string> localHostName,
        SmtpCommandLineText commandLineText)
    {
        this.connector = connector ?? throw new ArgumentNullException(nameof(connector));
        this.tlsProvider = tlsProvider ?? throw new ArgumentNullException(nameof(tlsProvider));
        this.saslAuthenticator = saslAuthenticator;
        this.localHostName = localHostName ?? throw new ArgumentNullException(nameof(localHostName));
        this.commandLineText = commandLineText ?? throw new ArgumentNullException(nameof(commandLineText));
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
        SmtpDiagnosticLogLines.TransferEnded(context.DiagnosticLog, result, context.TimeProvider, started);
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
            var channel = new SmtpControlChannel(connection, context.Events, context.CancellationToken, context.DiagnosticLog);

            // curl decodes the path once connected, so a malformed one still costs a connect.
            TransferResult result = SmtpEhloDomain.Read(url, localHostName) is { } domain
                ? await RunSessionAsync(channel, context, domain, implicitTls, connectEvents.Opened).ConfigureAwait(false)
                : TransferResult.Failure(CurlExitCode.UrlMalformat, SmtpSessionMessages.MalformedUrl);
            ReportConnectionEnd(context.Events, result, channel.QuitSent, target, connected.ConnectionNumber);
            return result;
        }
    }

    /// <summary>
    /// Writes the lines curl 8.21.0's <c>-v</c> ends an SMTP transfer with (BL-546): a failure's
    /// message, but for <c>Login denied</c>, which curl only makes <c>curl: (67)</c> of (BL-1061),
    /// then <c>shutting down connection #N</c> when <c>QUIT</c> was sent and
    /// <c>closing connection #N</c> when it was not; a success ends with
    /// <c>Connection #N to host H:P left intact</c>.
    /// </summary>
    private static void ReportConnectionEnd(ITransferEvents events, TransferResult result, bool quitSent, ConnectTarget target, long connectionNumber)
    {
        if (result.ExitCode == CurlExitCode.Ok)
        {
            events.ReportInfo(SmtpConnectionInfoLines.LeftIntact(connectionNumber, target.Host, target.Port));
            return;
        }

        if (result.ErrorMessage != SmtpSessionMessages.LoginDenied)
        {
            events.ReportInfo(result.ErrorMessage!);
        }

        events.ReportInfo(quitSent ? SmtpConnectionInfoLines.ShuttingDown(connectionNumber) : SmtpConnectionInfoLines.Closing(connectionNumber));
    }

    private async ValueTask<TransferResult> RunSessionAsync(
        SmtpControlChannel channel, ITransferContext context, string domain, bool implicitTls, ConnectionOpenedEvent? opened)
    {
        var session = new SmtpSession(channel, tlsProvider, saslAuthenticator, context, domain, implicitTls, commandLineText, opened);
        await using (session.ConfigureAwait(false))
        {
            return await session.RunAsync().ConfigureAwait(false);
        }
    }
}
