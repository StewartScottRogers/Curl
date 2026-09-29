using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ldap;

/// <summary>
/// Serves the <c>ldap</c> and <c>ldaps</c> schemes as the reference build
/// <see cref="LdapDialect" /> names does (ADR-0166): reads the URL, connects, binds, runs the
/// search the URL names, and leaves with an UnbindRequest.
/// </summary>
/// <param name="connector">
/// Supplies the connection to the URL's host and port, TLS from the first byte for
/// <c>ldaps</c> (ADR-0005). No <see cref="System.Net.Sockets.Socket" /> is ever constructed here.
/// </param>
/// <param name="dialect">The build to answer as, chosen from the platform by the composition root.</param>
/// <remarks>
/// <para>
/// The port defaults to 389 for <c>ldap</c> and 636 for <c>ldaps</c>. The bind is a simple
/// LDAPv3 bind with <see cref="ITransferContext.Credentials" />' user name as the DN and its
/// password, or the anonymous bind (empty DN and password) without <c>-u</c>; how it fails
/// is <see cref="LdapBind" />'s. A connect failure is returned as the connector reported it.
/// </para>
/// <para>
/// The URL is read as the build reads it (<see cref="WinLdapUrlReader" />,
/// <see cref="OpenLdapUrlReader" />): the OpenLDAP build refuses a bad URL with exit 3 before it
/// connects, the Windows build after connecting and before it sends a byte. Once bound, the
/// search runs as <see cref="LdapSearch" /> describes, writing each entry to
/// <see cref="ITransferContext.Output" /> as the build writes it.
/// </para>
/// <para>
/// Every <c>-v</c> line either build writes is reported to <see cref="ITransferContext.Events" />
/// as <see cref="LdapVerboseLines" /> describes, and each piece of an entry written to the
/// output as received data.
/// </para>
/// <para>
/// Without <c>-u</c> the WinLDAP dialect binds as the logged-on user as
/// <see cref="WinLdapLogonBind" /> describes - the rootDSE reads, then the <c>GSS-SPNEGO</c>
/// or Sicily NTLM bind - with tokens from <paramref name="logonTokenSource" />. The rest of
/// the session, search and UnbindRequest, is then signed and sealed with the bind's keys as
/// <see cref="LdapSaslSecurityLayer" /> describes, as WinLDAP does.
/// </para>
/// </remarks>
/// <param name="logonTokenSource">Produces the logged-on user's tokens for the WinLDAP dialect's bind without <c>-u</c>.</param>
public sealed class LdapProtocolHandler(IConnector connector, LdapDialect dialect, ILdapLogonTokenSource logonTokenSource) : IProtocolHandler
{
    private const int DefaultPort = 389;

    private const int DefaultTlsPort = 636;

    private const string TlsScheme = "ldaps";

    /// <summary>The schemes this handler serves, as curl 8.21.0's <c>--version</c> protocol list names them.</summary>
    private static readonly string[] Schemes = ["ldap", TlsScheme];

    private readonly IConnector connector =
        connector ?? throw new ArgumentNullException(nameof(connector));

    private readonly LdapDialect dialect = Enum.IsDefined(dialect)
        ? dialect
        : throw new ArgumentOutOfRangeException(nameof(dialect), dialect, "The LDAP dialect is WinLdap or OpenLdap.");

    private readonly ILdapLogonTokenSource logonTokenSource =
        logonTokenSource ?? throw new ArgumentNullException(nameof(logonTokenSource));

    /// <summary>
    /// Initializes a new instance of the <see cref="LdapProtocolHandler" /> class whose
    /// WinLDAP dialect binds without <c>-u</c> with the logged-on user's tokens from
    /// <see cref="NegotiateLogonTokenSource" />.
    /// </summary>
    /// <param name="connector">Supplies the connection to the URL's host and port.</param>
    /// <param name="dialect">The build to answer as.</param>
    public LdapProtocolHandler(IConnector connector, LdapDialect dialect)
        : this(connector, dialect, new NegotiateLogonTokenSource())
    {
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

        // The OpenLDAP build reads the URL before it connects and again to search; the Windows
        // build reads it only once connected.
        if (dialect == LdapDialect.OpenLdap && OpenLdapUrlReader.Read(context.Url).Failure is { } refused)
        {
            LdapVerboseLines.ReportFailure(context.Events, refused);
            context.Events.ReportInfo(LdapVerboseLines.Closing(LdapVerboseLines.NoConnection));
            return refused;
        }

        ConnectTarget target = TargetOf(context);
        ConnectResult connect = await connector.ConnectAsync(target, context.CancellationToken).ConfigureAwait(false);
        if (connect.Connection is not { } connection)
        {
            return new TransferResult(connect.ExitCode, 0, connect.ErrorMessage) { IsConnectionRefused = connect.IsConnectionRefused };
        }

        context.Progress.ReportTransferStarted();
        TransferResult result = await SearchOnAsync(connection, context).ConfigureAwait(false);
        ReportConnectionEnd(context.Events, result, target, connect.ConnectionNumber);
        return result;
    }

    /// <summary>Reads the URL, then binds and searches on <paramref name="connection" />, which it disposes.</summary>
    /// <remarks>The WinLDAP dialect first names the library and the URL, and once the URL is read, the kind of connection.</remarks>
    private async ValueTask<TransferResult> SearchOnAsync(IConnection connection, ITransferContext context)
    {
        await using (connection.ConfigureAwait(false))
        {
            if (dialect == LdapDialect.WinLdap)
            {
                context.Events.ReportInfo(LdapVerboseLines.WinLdapVendor);
                context.Events.ReportInfo(LdapVerboseLines.Url(context.Url));
            }

            LdapUrlReading reading = dialect == LdapDialect.OpenLdap ? OpenLdapUrlReader.Read(context.Url) : WinLdapUrlReader.Read(context.Url);
            if (reading.Search is not { } search)
            {
                return reading.Failure!;
            }

            if (dialect == LdapDialect.WinLdap)
            {
                context.Events.ReportInfo(LdapVerboseLines.Establishing(context.Url.Scheme == TlsScheme));
            }

            return await BindAndSearchAsync(connection, context, search).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Writes the lines each build's <c>-v</c> ends a transfer with: a failure's message, unless
    /// it is curl's text for the exit code; then <c>shutting down connection #N</c> from WinLDAP's
    /// build, and from the OpenLDAP build <c>Connection #N to host H:P left intact</c> after a
    /// success and <c>closing connection #N</c> after a failure.
    /// </summary>
    private void ReportConnectionEnd(ITransferEvents events, TransferResult result, ConnectTarget target, long connectionNumber)
    {
        LdapVerboseLines.ReportFailure(events, result);
        events.ReportInfo(
            dialect == LdapDialect.WinLdap ? LdapVerboseLines.ShuttingDown(connectionNumber)
            : result.ExitCode == CurlExitCode.Ok ? LdapVerboseLines.LeftIntact(connectionNumber, target.Host, target.Port)
            : LdapVerboseLines.Closing(connectionNumber));
    }

    /// <summary>
    /// The host and port the transfer's URL names, with TLS from the first byte for
    /// <c>ldaps</c>, reporting the connect to the transfer's events.
    /// </summary>
    private static ConnectTarget TargetOf(ITransferContext context)
    {
        CurlUrl url = context.Url;
        bool useTls = url.Scheme == TlsScheme;
        int defaultPort = useTls ? DefaultTlsPort : DefaultPort;
        return new ConnectTarget(url.IdnHost, url.IsDefaultPort ? defaultPort : url.Port, useTls) { Events = context.Events };
    }

    /// <summary>
    /// Binds with the transfer's credentials; without them anonymously on OpenLDAP and as the
    /// logged-on user on WinLDAP; then runs the search.
    /// </summary>
    private async ValueTask<TransferResult> BindAndSearchAsync(IConnection connection, ITransferContext context, LdapSearchParameters search)
    {
        var exchange = new LdapExchange(connection, new LdapBerWriter(dialect));
        await using (exchange.ConfigureAwait(false))
        {
            TransferResult? bindFailure = dialect == LdapDialect.WinLdap && context.Credentials is null
                ? await WinLdapLogonBind.BindAsync(exchange, logonTokenSource, context.Url.IdnHost, context.CancellationToken).ConfigureAwait(false)
                : await BindWithCredentialsAsync(exchange, context).ConfigureAwait(false);
            if (bindFailure is not null)
            {
                return bindFailure;
            }

            if (dialect == LdapDialect.OpenLdap)
            {
                context.Events.ReportInfo(LdapVerboseLines.Url(context.Url));
            }

            return await LdapSearch.RunAsync(dialect, exchange, search, context).ConfigureAwait(false);
        }
    }

    /// <summary>Binds with a simple bind: the transfer's credentials, or the anonymous bind without them.</summary>
    private ValueTask<TransferResult?> BindWithCredentialsAsync(LdapExchange exchange, ITransferContext context)
    {
        NetworkCredential credentials = context.Credentials ?? new NetworkCredential(string.Empty, string.Empty);
        return LdapBind.BindAsync(dialect, exchange, credentials.UserName, credentials.Password, context.CancellationToken);
    }
}
