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
/// Authentication (BL-541), the upload (BL-542) and the commands sent without one (BL-543)
/// are not implemented yet: once the session is open the handler closes it with
/// <c>QUIT</c> and reports success. Cancellation leaves as an exception.
/// </para>
/// </remarks>
public sealed class SmtpProtocolHandler : IProtocolHandler
{
    /// <summary>The scheme whose connection is TLS from its first byte.</summary>
    private const string ImplicitTlsScheme = "smtps";

    private static readonly string[] Schemes = ["smtp", ImplicitTlsScheme];

    private readonly IConnector connector;

    private readonly ITlsProvider tlsProvider;

    private readonly Func<string> localHostName;

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
        : this(connector, tlsProvider, Dns.GetHostName)
    {
    }

    /// <summary>
    /// Initializes a handler that names <paramref name="localHostName" />'s answer in
    /// <c>EHLO</c> for a URL with an empty path, so a test need not depend on the machine.
    /// </summary>
    /// <param name="connector">Supplies the connection.</param>
    /// <param name="tlsProvider">Upgrades the connection after an accepted <c>STARTTLS</c>.</param>
    /// <param name="localHostName">Supplies the local machine's host name.</param>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null" />.</exception>
    internal SmtpProtocolHandler(IConnector connector, ITlsProvider tlsProvider, Func<string> localHostName)
    {
        this.connector = connector ?? throw new ArgumentNullException(nameof(connector));
        this.tlsProvider = tlsProvider ?? throw new ArgumentNullException(nameof(tlsProvider));
        this.localHostName = localHostName ?? throw new ArgumentNullException(nameof(localHostName));
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
            // curl decodes the path once connected, so a malformed one still costs a connect.
            return SmtpEhloDomain.Read(url, localHostName) is { } domain
                ? await RunSessionAsync(connection, context, domain, implicitTls).ConfigureAwait(false)
                : TransferResult.Failure(CurlExitCode.UrlMalformat, SmtpSessionMessages.MalformedUrl);
        }
    }

    private async ValueTask<TransferResult> RunSessionAsync(IConnection connection, ITransferContext context, string domain, bool implicitTls)
    {
        var session = new SmtpSession(
            new SmtpControlChannel(connection, context.CancellationToken), tlsProvider, context, domain, implicitTls);
        await using (session.ConfigureAwait(false))
        {
            return await session.RunAsync().ConfigureAwait(false);
        }
    }
}
