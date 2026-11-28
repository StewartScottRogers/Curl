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
/// password, or the anonymous bind (empty DN and password) without <c>-u</c>, which the
/// OpenLDAP dialect leaves out after a successful StartTLS; how it fails is
/// <see cref="LdapBind" />'s. Curl's own connection is tunnelled through
/// <see cref="ITransferContext.Proxy" /> when it is set. A connect failure writes
/// <c>closing connection #N</c> and is returned as the connector reported it, except that under
/// <c>-f</c> a proxy that refused the CONNECT with 400 or above makes it exit 22.
/// </para>
/// <para>
/// With a proxy the WinLDAP dialect always binds and searches on a second connection of its own,
/// straight to the host, <c>ldaps</c> with <c>-u</c> included; when that connection cannot be
/// opened it writes the lines it would have written before the bind and ends with exit 38, the
/// bind's <c>Server Down</c> over <c>ldaps</c>, otherwise <c>Unavailable</c> for a host that
/// does not resolve and <c>Timeout</c> for any other failure (BL-6091, ADR-1470).
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

    /// <summary>curl's generic exit 22 text, used when a proxy's <c>407</c> drew its authentication problem (BL-6087).</summary>
    private const string HttpResponseCodeSaidError = "HTTP response code said error";

    /// <summary>The schemes this handler serves, as curl 8.21.0's <c>--version</c> protocol list names them.</summary>
    private static readonly string[] Schemes = ["ldap", TlsScheme];

    private readonly IConnector connector =
        connector ?? throw new ArgumentNullException(nameof(connector));

    /// <summary>The message of the WinLDAP build's exit 8 for <c>-I</c>, which it cannot answer (BL-5397).</summary>
    private const string WeirdServerReply = "Weird server reply";

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

    /// <summary>
    /// Picks and runs the mechanism of an OpenLDAP SASL bind. The hosts give the shared authenticator;
    /// without one the handler binds with <c>DIGEST-MD5</c> or <c>PLAIN</c> only.
    /// </summary>
    public ISaslAuthenticator SaslAuthenticator { get; init; } = new LdapDigestMd5AndPlainSasl();

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

    // The refusals curl makes before it connects: control bytes in the credentials (exit 43) and, in the OpenLDAP build, malformed --login-options (exit 49, STD-184).
    private TransferResult? RefuseBeforeConnecting(ITransferContext context) =>
        RefuseControlBytes(context.Credentials)
        ?? (dialect == LdapDialect.OpenLdap && !LdapOpenLdapSaslBind.AreWellFormed(context.Mail?.LoginOptions)
            ? TransferResult.Failure(CurlExitCode.SetoptOptionSyntax, "Malformed option provided in a setopt")
            : null);

    // Reads the URL, connects, binds and searches; ExecuteAsync logs how it ended.
    private async ValueTask<TransferResult> ConnectAndSearchAsync(ITransferContext context)
    {
        if (RefuseBeforeConnecting(context) is { } optionsRefused)
        {
            return optionsRefused;
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
        (ConnectResult connect, TransferResult? connectFailure) = await ConnectAsync(target, context).ConfigureAwait(false);
        if (connect.Connection is not { } connection)
        {
            return connectFailure!;
        }

        context.Progress.ReportTransferStarted();
        TransferResult result = await SearchAsync(connection, target, context).ConfigureAwait(false);
        ReportConnectionEnd(context.Events, result, target, connect.ConnectionNumber);
        return result;
    }

    // Over ldaps with -u and no proxy WinLDAP checks the certificate against the Windows store
    // itself, so -k does not help (BL-5285); curl's own handshake is that connection, so a
    // handshake it refuses is curl's own failure (exit 60, SEC_E_UNTRUSTED_ROOT), not the bind's
    // Server Down (BL-5461). Through a proxy curl's own handshake obeys -k (measured, BL-6091).
    private async ValueTask<(ConnectResult Connect, TransferResult? Failure)> ConnectAsync(ConnectTarget target, ITransferContext context)
    {
        bool winLdapChecksCertificate = dialect == LdapDialect.WinLdap && target.UseTls && !SearchesBesideCurlsConnection(target, context);
        ConnectResult connect = await connector.ConnectAsync(winLdapChecksCertificate ? target with { TrustsSystemStoreOnly = true } : target, context.CancellationToken).ConfigureAwait(false);
        if (connect.Connection is not null)
        {
            return (connect, null);
        }

        // curl 8.22.0 closes every connection of its own it could not open (measured, BL-6087).
        context.Events.ReportInfo(LdapVerboseLines.Closing(connect.ConnectionNumber));
        return (connect, CurlConnectFailure(context, connect));
    }

    /// <summary>
    /// The failure of curl's own connect: under <c>-f</c>, exit 22 when a proxy refused the CONNECT
    /// with 400 or above, naming the status, or curl's generic text when the <c>407</c> drew its
    /// authentication problem; otherwise as the connector reported it (measured, BL-6087).
    /// </summary>
    private static TransferResult CurlConnectFailure(ITransferContext context, ConnectResult connect)
    {
        int status = connect.ProxyConnectResponseCode;
        if (!FailsOnProxyStatus(context) || status < 400)
        {
            return ConnectFailure(connect);
        }

        return TransferResult.Failure(
            CurlExitCode.HttpReturnedError,
            connect.IsProxyAuthenticationProblem ? HttpResponseCodeSaidError : $"The requested URL returned error: {status}");
    }

    /// <summary>
    /// Whether <c>-f</c> fails the transfer when a proxy refuses the CONNECT, as
    /// <see cref="ConnectTarget.FailsOnHttpErrorStatus" /> tells the connector;
    /// <c>--fail-with-body</c> does not.
    /// </summary>
    private static bool FailsOnProxyStatus(ITransferContext context) => context.Http?.Fail == HttpFailMode.Fail;

    /// <summary>
    /// Binds and searches on <paramref name="connection" />, curl's own connection, or, when
    /// <see cref="SearchesBesideCurlsConnection" /> says so, on a second connection WinLDAP opens
    /// beside it.
    /// </summary>
    private ValueTask<TransferResult> SearchAsync(IConnection connection, ConnectTarget target, ITransferContext context) =>
        SearchesBesideCurlsConnection(target, context)
            ? SearchBesideEmptyConnectionAsync(connection, target, context)
            : SearchOnAsync(connection, context);

    /// <summary>
    /// Whether the bind and search go on a second connection of WinLDAP's own: for the WinLDAP
    /// dialect over plaintext <c>ldap://</c>, without credentials, or through a proxy.
    /// </summary>
    private bool SearchesBesideCurlsConnection(ConnectTarget target, ITransferContext context) =>
        dialect == LdapDialect.WinLdap && (context.Credentials is null || !target.UseTls || target.Proxy is not null);

    /// <summary>The failure a connect that opened no connection ends the transfer with.</summary>
    private static TransferResult ConnectFailure(ConnectResult connect) =>
        new(connect.ExitCode, 0, connect.ErrorMessage) { IsConnectionRefused = connect.IsConnectionRefused };

    /// <summary>
    /// The WinLDAP dialect's bind over plaintext <c>ldap://</c> (with or without <c>-u</c>), as the
    /// logged-on user or through a proxy: curl's own connection,
    /// <paramref name="curlConnection" />, stays open and carries no bytes while WinLDAP opens a
    /// second connection to <paramref name="target" /> for the binds and the search, as curl
    /// 8.22.0's Schannel build does (CGR-234, BL-3999, ADR-0978, ADR-1470).
    /// </summary>
    private async ValueTask<TransferResult> SearchBesideEmptyConnectionAsync(IConnection curlConnection, ConnectTarget target, ITransferContext context)
    {
        await using (curlConnection.ConfigureAwait(false))
        {
            ConnectResult winLdap = await connector.ConnectAsync(WinLdapTargetOf(target), context.CancellationToken).ConfigureAwait(false);
            return winLdap.Connection is { } connection
                ? await SearchOnAsync(connection, context).ConfigureAwait(false)
                : WinLdapConnectFailure(winLdap, target, context);
        }
    }

    /// <summary>
    /// The failure WinLDAP's own connection ends the transfer with when it opens no connection: over
    /// <c>ldaps</c> WinLDAP reports its failed handshake, such as a server the Windows store does not
    /// trust whatever <c>--cacert</c> says, as the bind's <c>Server Down</c> (exit 38), as curl 8.22.0
    /// does (CGR-289, BL-4056, ADR-0994); without a proxy a plaintext connect failure is reported
    /// as curl's own. Through a proxy the lines before the bind are written first, a URL or
    /// <c>--ssl-reqd</c> refusal among them ending the transfer, and a plaintext failure is the
    /// bind's too (measured, BL-6091).
    /// </summary>
    private TransferResult WinLdapConnectFailure(ConnectResult connect, ConnectTarget target, ITransferContext context)
    {
        if (target.Proxy is null)
        {
            return target.UseTls ? LdapBind.WinLdapBindFailed(WinLdapResultText.ServerDown) : ConnectFailure(connect);
        }

        return ReadSearch(context).Failure ?? LdapBind.WinLdapBindFailed(ProxiedConnectResultCode(connect, target));
    }

    /// <summary>
    /// WinLDAP's result code when its own connection, beside a proxied one, cannot be opened:
    /// <c>Server Down</c> over <c>ldaps</c>, otherwise <c>Unavailable</c> when the host does not
    /// resolve and <c>Timeout</c> for any other failure.
    /// </summary>
    private static int ProxiedConnectResultCode(ConnectResult connect, ConnectTarget target) =>
        target.UseTls ? WinLdapResultText.ServerDown
        : connect.ExitCode == CurlExitCode.CouldntResolveHost ? WinLdapResultText.Unavailable
        : WinLdapResultText.Timeout;

    /// <summary>
    /// The target of WinLDAP's own connection: for <c>ldaps</c> the handshake sends the machine name
    /// as its server name and trusts only the Windows certificate store, whatever <c>--cacert</c>
    /// says, unless <c>-k</c> turns verification off (CGR-289, ADR-0979, ADR-0992). WinLDAP knows
    /// nothing of curl's proxy, so this connection goes straight to the host (measured, BL-6087).
    /// </summary>
    private static ConnectTarget WinLdapTargetOf(ConnectTarget target) =>
        (target.UseTls ? target with { TlsServerName = Environment.MachineName, TrustsSystemStoreOnly = true } : target)
            with
        { Events = NoTransferEvents.Instance, Proxy = null, FailsOnHttpErrorStatus = false };

    /// <summary>Reads the URL, then binds and searches on <paramref name="connection" />, which it disposes.</summary>
    private async ValueTask<TransferResult> SearchOnAsync(IConnection connection, ITransferContext context)
    {
        await using (connection.ConfigureAwait(false))
        {
            (LdapSearchParameters? search, TransferResult? refused) = ReadSearch(context);
            return search is null ? refused! : await BindAndSearchAsync(connection, context, search).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// The search the URL asks for, or the failure that ends the transfer before the bind; exactly
    /// one of the two is not <see langword="null" />.
    /// </summary>
    /// <remarks>
    /// The WinLDAP dialect first names the library and the URL, and once the URL is read, the kind
    /// of connection, then refuses <c>--ssl-reqd</c> on <c>ldap</c>.
    /// </remarks>
    private (LdapSearchParameters? Search, TransferResult? Failure) ReadSearch(ITransferContext context)
    {
        if (dialect == LdapDialect.WinLdap)
        {
            context.Events.ReportInfo(LdapVerboseLines.WinLdapVendor);
            context.Events.ReportInfo(LdapVerboseLines.Url(context.Url));
        }

        LdapUrlReading reading = dialect == LdapDialect.OpenLdap ? OpenLdapUrlReader.Read(context.Url) : WinLdapUrlReader.Read(context.Url);
        if (reading.Search is null || dialect != LdapDialect.WinLdap)
        {
            return (reading.Search, reading.Failure);
        }

        context.Events.ReportInfo(LdapVerboseLines.Establishing(context.Url.Scheme == TlsScheme));
        return StartsTls(context)
            ? (null, TransferResult.Failure(CurlExitCode.NotBuiltIn, ExplicitTlsNotSupported))
            : (reading.Search, null);
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
    /// <c>ldaps</c>, tunnelled through <see cref="ITransferContext.Proxy" /> when it is set,
    /// reporting the connect to the transfer's events.
    /// </summary>
    private static ConnectTarget TargetOf(ITransferContext context)
    {
        CurlUrl url = context.Url;
        bool useTls = url.Scheme == TlsScheme;
        int defaultPort = useTls ? DefaultTlsPort : DefaultPort;
        return new ConnectTarget(url.IdnHost, url.IsDefaultPort ? defaultPort : url.Port, useTls)
        {
            Proxy = context.Proxy,
            FailsOnHttpErrorStatus = FailsOnProxyStatus(context),
            Events = context.Events,
            DiagnosticLog = context.DiagnosticLog,
        };
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
            TransferResult? bindFailure = await StartTlsAndBindAsync(exchange, connection, context).ConfigureAwait(false);
            if (bindFailure is not null)
            {
                return bindFailure;
            }

            if (dialect == LdapDialect.OpenLdap)
            {
                context.Events.ReportInfo(LdapVerboseLines.Url(context.Url));
            }

            TransferResult searched = await LdapSearch.RunAsync(dialect, exchange, search, context).ConfigureAwait(false);
            return dialect == LdapDialect.WinLdap && context.NoBody && searched.ExitCode == CurlExitCode.Ok
                ? TransferResult.Failure(CurlExitCode.WeirdServerReply, WeirdServerReply)
                : searched;
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
    /// the transfer's credentials, except that after StartTLS succeeds a simple bind without them
    /// is not sent; the failure that ends the transfer, or <see langword="null" />.
    /// </summary>
    private async ValueTask<TransferResult?> StartTlsAndBindAsync(LdapExchange exchange, IConnection connection, ITransferContext context)
    {
        (TransferResult? startTlsFailure, int simpleBindVersion, bool secured) = StartsTls(context)
            ? await LdapStartTls.UpgradeAsync(exchange, connection, tlsProvider, context).ConfigureAwait(false)
            : (null, LdapBind.LdapVersion3, false);
        LdapBindKind bindKind = ChooseBindKind(context);
        if (startTlsFailure is not null || (secured && IsSimpleBindWithoutCredentials(bindKind, context)))
        {
            return startTlsFailure;
        }

        TransferResult? bindFailure = await BindAsync(exchange, context, bindKind, simpleBindVersion).ConfigureAwait(false);
        if (bindFailure is null)
        {
            exchange.Log.Bound(DescribeBind(bindKind, context.Credentials));
        }

        return bindFailure;
    }

    private static bool IsSimpleBindWithoutCredentials(LdapBindKind bindKind, ITransferContext context) =>
        bindKind == LdapBindKind.Simple && context.Credentials is null;

    /// <summary>Binds as <paramref name="bindKind" /> says; a simple bind starts with <paramref name="simpleBindVersion" />.</summary>
    private async ValueTask<TransferResult?> BindAsync(LdapExchange exchange, ITransferContext context, LdapBindKind bindKind, int simpleBindVersion) =>
        bindKind switch
        {
            LdapBindKind.LogonUser => await WinLdapLogonBind.BindAsync(exchange, logonTokenSource, context.Url.IdnHost, context.Url.Scheme == TlsScheme, context.CancellationToken).ConfigureAwait(false),
            LdapBindKind.Simple => await BindWithCredentialsAsync(exchange, context, simpleBindVersion).ConfigureAwait(false),
            LdapBindKind.Sasl => await LdapOpenLdapSaslBind.BindAsync(exchange, SaslAuthenticator, context).ConfigureAwait(false),
            _ => await LdapAuthenticatedBind.BindAsync(bindKind, exchange, context.Credentials!, context.Url.IdnHost, context.Url.Scheme == TlsScheme, logonTokenSource, context.CancellationToken).ConfigureAwait(false),
        };

    /// <summary>
    /// How the transfer binds: on OpenLDAP SASL when <c>AUTH=</c> login options are given or the auth mask is not exactly Basic (as with --anyauth),
    /// otherwise a simple bind (ADR-1299, ADR-1362); on WinLDAP what <see cref="LdapBindKindChoice" />
    /// picks from <c>-u</c> and the auth mask, so <c>--ntlm</c>, <c>--digest</c> and <c>--negotiate</c> never send a simple bind.
    /// </summary>
    private LdapBindKind ChooseBindKind(ITransferContext context) =>
        dialect == LdapDialect.WinLdap
            ? LdapBindKindChoice.Choose(context.Http?.AuthSchemes ?? HttpAuthSchemes.Basic, context.Credentials is not null)
            : ChooseOpenLdapBindKind(context);

    /// <summary>OpenLDAP's choice, with or without credentials: SASL when <c>AUTH=</c> login options are given or the mask is not exactly Basic, otherwise a simple bind.</summary>
    private static LdapBindKind ChooseOpenLdapBindKind(ITransferContext context) =>
        LdapOpenLdapSaslBind.HasAuthOption(context.Mail?.LoginOptions) || (context.Http?.AuthSchemes ?? HttpAuthSchemes.Basic) != HttpAuthSchemes.Basic
            ? LdapBindKind.Sasl
            : LdapBindKind.Simple;

    /// <summary>Names a bind for the diagnostic log by its kind and DN, never its password.</summary>
    private static string DescribeBind(LdapBindKind kind, NetworkCredential? credentials) =>
        kind switch
        {
            LdapBindKind.LogonUser => "as the logged-on user",
            LdapBindKind.Simple => DescribeSimpleBind(credentials),
            _ => $"with a {kind} bind as \"{credentials?.UserName}\"",
        };

    /// <summary>Names a simple bind by its DN, never its password, for the diagnostic log.</summary>
    private static string DescribeSimpleBind(NetworkCredential? credentials) =>
        credentials is null ? "anonymously" : $"with a simple bind as \"{credentials.UserName}\"";

    /// <summary>Binds with a simple bind: the transfer's credentials, or the anonymous bind without them.</summary>
    private ValueTask<TransferResult?> BindWithCredentialsAsync(LdapExchange exchange, ITransferContext context, int openLdapVersion)
    {
        NetworkCredential credentials = context.Credentials ?? new NetworkCredential(string.Empty, string.Empty);
        return LdapBind.BindAsync(dialect, exchange, credentials.UserName, credentials.Password, context.Url.Scheme == TlsScheme, context.CancellationToken, openLdapVersion);
    }
}
