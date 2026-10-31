using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Ldap;

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
/// <para>
/// The bind, the search, each message sent and search reply received, and the transfer's end
/// are written to <see cref="ITransferContext.DiagnosticLog" /> under the <c>ldap</c>
/// component (<see cref="LdapTransferLog" />).
/// </para>
/// </remarks>
/// <param name="logonTokenSource">Produces the logged-on user's tokens for the WinLDAP dialect's bind without <c>-u</c>.</param>
/// <param name="tlsProvider">
/// Performs the OpenLDAP dialect's StartTLS handshake for <c>--ssl</c> and <c>--ssl-reqd</c>
/// (<see cref="LdapStartTls" />); without one, a StartTLS the server accepts fails with exit 64.
/// </param>
public sealed class LdapProtocolHandler(IConnector connector, LdapDialect dialect, ILdapLogonTokenSource logonTokenSource, ITlsProvider? tlsProvider) : IProtocolHandler
{
    /// <summary>The user name or password held a byte below 0x20 (exit 43), curl 8.22.0's text (BL-2179).</summary>
    private const string CredentialsHaveControlCode = "control code detected in credentials";

    /// <summary>The WinLDAP build's refusal of <c>--ssl-reqd</c> on <c>ldap</c> (exit 4), measured from curl 8.22.0 (BL-3542).</summary>
    private const string ExplicitTlsNotSupported = "LDAP local: explicit TLS not supported";

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
        : this(connector, dialect, new NegotiateLogonTokenSource(), null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LdapProtocolHandler" /> class with no TLS
    /// provider for StartTLS.
    /// </summary>
    /// <param name="connector">Supplies the connection to the URL's host and port.</param>
    /// <param name="dialect">The build to answer as.</param>
    /// <param name="logonTokenSource">Produces the logged-on user's tokens for the WinLDAP dialect's bind without <c>-u</c>.</param>
    public LdapProtocolHandler(IConnector connector, LdapDialect dialect, ILdapLogonTokenSource logonTokenSource)
        : this(connector, dialect, logonTokenSource, null)
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

        long startTimestamp = context.TimeProvider.GetTimestamp();
        TransferResult result = await ConnectAndSearchAsync(context).ConfigureAwait(false);
        new LdapTransferLog(context.DiagnosticLog).Ended(result, context.TimeProvider.GetElapsedTime(startTimestamp));
        return result;
    }

    /// <summary>
    /// The exit 43 curl 8.22.0 refuses <paramref name="credentials" /> with before it connects when the
    /// user name or password holds a byte below 0x20, or <see langword="null" /> when neither does (BL-2179).
    /// </summary>
    private static TransferResult? RefuseControlBytes(NetworkCredential? credentials) =>
        credentials is not null && (credentials.UserName.Any(character => character < ' ') || credentials.Password.Any(character => character < ' '))
            ? TransferResult.Failure(CurlExitCode.BadFunctionArgument, CredentialsHaveControlCode)
            : null;

    // Reads the URL, connects, binds and searches; ExecuteAsync logs how it ended.
    private async ValueTask<TransferResult> ConnectAndSearchAsync(ITransferContext context)
    {
        if (RefuseControlBytes(context.Credentials) is { } credentialsRefused)
        {
            return credentialsRefused;
        }

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
            return ConnectFailure(connect);
        }

        context.Progress.ReportTransferStarted();
        TransferResult result = await SearchAsync(connection, target, context).ConfigureAwait(false);
        ReportConnectionEnd(context.Events, result, target, connect.ConnectionNumber);
        return result;
    }

    /// <summary>
    /// Binds and searches on <paramref name="connection" />, curl's own connection, or, for the WinLDAP
    /// dialect's bind as the logged-on user, on a second connection WinLDAP opens beside it.
    /// </summary>
    private ValueTask<TransferResult> SearchAsync(IConnection connection, ConnectTarget target, ITransferContext context) =>
        dialect == LdapDialect.WinLdap && context.Credentials is null
            ? SearchBesideEmptyConnectionAsync(connection, target, context)
            : SearchOnAsync(connection, context);

    /// <summary>The failure a connect that opened no connection ends the transfer with.</summary>
    private static TransferResult ConnectFailure(ConnectResult connect) =>
        new(connect.ExitCode, 0, connect.ErrorMessage) { IsConnectionRefused = connect.IsConnectionRefused };

    /// <summary>
    /// The WinLDAP dialect's bind as the logged-on user: curl's own connection,
    /// <paramref name="curlConnection" />, stays open and carries no bytes while WinLDAP opens a
    /// second connection to <paramref name="target" /> for the binds and the search, as curl
    /// 8.22.0's Schannel build does without <c>-u</c> (CGR-234, BL-3999, ADR-0978).
    /// </summary>
    private async ValueTask<TransferResult> SearchBesideEmptyConnectionAsync(IConnection curlConnection, ConnectTarget target, ITransferContext context)
    {
        await using (curlConnection.ConfigureAwait(false))
        {
            ConnectResult winLdap = await connector.ConnectAsync(WinLdapTargetOf(target), context.CancellationToken).ConfigureAwait(false);
            return winLdap.Connection is { } connection
                ? await SearchOnAsync(connection, context).ConfigureAwait(false)
                : WinLdapConnectFailure(winLdap, target);
        }
    }

    /// <summary>
    /// The failure WinLDAP's own connection ends the transfer with when it opens no connection: over
    /// <c>ldaps</c> WinLDAP reports its failed handshake, such as a server the Windows store does not
    /// trust whatever <c>--cacert</c> says, as the bind's <c>Server Down</c> (exit 38), as curl 8.22.0
    /// does (CGR-289, BL-4056, ADR-0994); a plaintext connect failure is reported as curl's own.
    /// </summary>
    private static TransferResult WinLdapConnectFailure(ConnectResult connect, ConnectTarget target) =>
        target.UseTls ? LdapBind.WinLdapBindFailed(WinLdapResultText.ServerDown) : ConnectFailure(connect);

    /// <summary>
    /// The target of WinLDAP's own connection: for <c>ldaps</c> the handshake sends the machine name
    /// as its server name and trusts only the Windows certificate store, whatever <c>--cacert</c>
    /// says, unless <c>-k</c> turns verification off (CGR-289, ADR-0979, ADR-0992); a plaintext
    /// target is unchanged.
    /// </summary>
    private static ConnectTarget WinLdapTargetOf(ConnectTarget target) =>
        target.UseTls ? target with { TlsServerName = Environment.MachineName, TrustsSystemStoreOnly = true } : target;
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
                if (StartsTls(context))
                {
                    return TransferResult.Failure(CurlExitCode.NotBuiltIn, ExplicitTlsNotSupported);
                }
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
        return new ConnectTarget(url.IdnHost, url.IsDefaultPort ? defaultPort : url.Port, useTls) { Events = context.Events, DiagnosticLog = context.DiagnosticLog };
    }

    /// <summary>
    /// Starts TLS when <c>--ssl</c> or <c>--ssl-reqd</c> asks for it on the OpenLDAP dialect, then
    /// binds with the transfer's credentials; without them anonymously on OpenLDAP and as the
    /// logged-on user on WinLDAP; then runs the search.
    /// </summary>
    private async ValueTask<TransferResult> BindAndSearchAsync(IConnection connection, ITransferContext context, LdapSearchParameters search)
    {
        var exchange = new LdapExchange(connection, new LdapBerWriter(dialect), new LdapTransferLog(context.DiagnosticLog));
        await using (exchange.ConfigureAwait(false))
        {
            bool bindsAsLogonUser = dialect == LdapDialect.WinLdap && context.Credentials is null;
            TransferResult? bindFailure = await StartTlsAndBindAsync(exchange, connection, context, bindsAsLogonUser).ConfigureAwait(false);
            if (bindFailure is not null)
            {
                return bindFailure;
            }

            exchange.Log.Bound(bindsAsLogonUser ? "as the logged-on user" : DescribeSimpleBind(context.Credentials));

            if (dialect == LdapDialect.OpenLdap)
            {
                context.Events.ReportInfo(LdapVerboseLines.Url(context.Url));
            }

            return await LdapSearch.RunAsync(dialect, exchange, search, context).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Whether <c>--ssl</c> or <c>--ssl-reqd</c> asks a plaintext <c>ldap</c> URL to start TLS:
    /// the WinLDAP build refuses <c>--ssl-reqd</c> and binds in plaintext under <c>--ssl</c>; the
    /// OpenLDAP build sends StartTLS for both. An <c>ldaps</c> URL is TLS from the first byte.
    /// </summary>
    private bool StartsTls(ITransferContext context) =>
        context.Url.Scheme != TlsScheme
        && (dialect == LdapDialect.OpenLdap ? context.SslLevel != TransportSecurityLevel.None : context.SslLevel == TransportSecurityLevel.Required);

    /// <summary>
    /// Starts TLS when <see cref="StartsTls" /> says so, then binds as the logged-on user or with
    /// the transfer's credentials; the failure that ends the transfer, or <see langword="null" />.
    /// </summary>
    private async ValueTask<TransferResult?> StartTlsAndBindAsync(LdapExchange exchange, IConnection connection, ITransferContext context, bool bindsAsLogonUser)
    {
        if (StartsTls(context) && await LdapStartTls.UpgradeAsync(exchange, connection, tlsProvider, context).ConfigureAwait(false) is { } startTlsFailure)
        {
            return startTlsFailure;
        }

        return bindsAsLogonUser
            ? await WinLdapLogonBind.BindAsync(exchange, logonTokenSource, context.Url.IdnHost, context.Url.Scheme == TlsScheme, context.CancellationToken).ConfigureAwait(false)
            : await BindWithCredentialsAsync(exchange, context).ConfigureAwait(false);
    }

    /// <summary>Names a simple bind by its DN, never its password, for the diagnostic log.</summary>
    private static string DescribeSimpleBind(NetworkCredential? credentials) =>
        credentials is null ? "anonymously" : $"with a simple bind as \"{credentials.UserName}\"";

    /// <summary>Binds with a simple bind: the transfer's credentials, or the anonymous bind without them.</summary>
    private ValueTask<TransferResult?> BindWithCredentialsAsync(LdapExchange exchange, ITransferContext context)
    {
        NetworkCredential credentials = context.Credentials ?? new NetworkCredential(string.Empty, string.Empty);
        return LdapBind.BindAsync(dialect, exchange, credentials.UserName, credentials.Password, context.CancellationToken);
    }
}
