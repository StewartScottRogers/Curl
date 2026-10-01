using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ftp;

/// <summary>
/// Serves the <c>ftp</c> and <c>ftps</c> schemes: logs in, opens a passive or active data
/// connection and downloads the file the URL names, lists the directory a URL ending in
/// <c>/</c> names, or with <c>-T</c> uploads to the file the URL names, as curl 8.21.0 does.
/// </summary>
/// <remarks>
/// <para>
/// The login is <c>anonymous</c> with the password <c>ftp@example.com</c>, curl's
/// defaults, unless <see cref="ITransferContext.Credentials" /> names a user. The whole
/// conversation, and which failure ends with which exit code, is described on
/// <see cref="FtpSession" />; every step was measured against curl 8.21.0 with
/// <c>Record-CurlExchange.ps1 -Ftp</c> (BL-431). A refused login is exit 67
/// (<see cref="CurlExitCode.LoginDenied" />) with <c>Access denied: 430</c> for a
/// <c>430</c> reply. Once logged in or refused, the result's
/// <see cref="TransferReport.ResponseCode" /> is the code of the last reply read before
/// <c>QUIT</c>, as curl 8.21.0 reports <c>%{response_code}</c> for FTP (BL-392).
/// </para>
/// <para>
/// When <see cref="ITransferContext.Proxy" /> is set both connections are tunnelled
/// through it; the connector opens the tunnels (ADR-0056). A failed connect is returned as
/// the connector reported it. <see cref="ITransferContext.Range" />,
/// <see cref="ITransferContext.ResumeFrom" /> and <see cref="ITransferContext.NoBody" />
/// are honoured as curl 8.21.0 honours <c>-r</c>, <c>-C</c> and <c>-I</c> (BL-438), and
/// <see cref="ITransferContext.Upload" /> is sent with <c>STOR</c>, or <c>APPE</c> when
/// <c>-C</c> resumes it (BL-439). <see cref="ITransferContext.FtpDisableEpsv" />,
/// <see cref="ITransferContext.FtpSkipPasvIp" />, <see cref="ITransferContext.FtpFileMethod" />,
/// <see cref="ITransferContext.FtpCreateDirectories" />, <see cref="ITransferContext.ListOnly" />
/// and <see cref="ITransferContext.QuoteCommands" /> are honoured as curl 8.21.0 honours
/// <c>--disable-epsv</c>, <c>--no-ftp-skip-pasv-ip</c>, <c>--ftp-method</c>,
/// <c>--ftp-create-dirs</c>, <c>-l</c> and <c>-Q</c> (BL-436).
/// </para>
/// <para>
/// <see cref="ITransferContext.FtpPort" /> and <see cref="ITransferContext.FtpUseEprt" />
/// select active mode as <c>-P</c> and <c>--disable-eprt</c> do; an <c>ftps</c> URL makes
/// the control connection TLS from the start, and <see cref="ITransferContext.SslLevel" />
/// and <see cref="ITransferContext.FtpSslControlOnly" /> upgrade an <c>ftp</c> one with
/// <c>AUTH</c> as <c>--ssl</c>, <c>--ssl-reqd</c> and <c>--ftp-ssl-control</c> do (BL-437,
/// ADR-0102); a host name given to <c>-P</c> is resolved through the injected
/// <see cref="IDnsResolver" /> (BL-466, ADR-0108), after the injected
/// <see cref="INetworkInterfaceLookup" /> finds no interface of that name (BL-474, ADR-0110).
/// The other FTP-only options are not implemented yet.
/// Cancellation leaves as an exception.
/// </para>
/// </remarks>
public sealed class FtpProtocolHandler : IProtocolHandler
{
    /// <summary>The port an <c>ftp</c> URL without one connects to.</summary>
    private const int DefaultPort = 21;

    /// <summary>The port an <c>ftps</c> URL without one connects to.</summary>
    private const int DefaultSecurePort = 990;

    /// <summary>The scheme whose control connection is TLS from its first byte.</summary>
    private const string ImplicitTlsScheme = "ftps";

    /// <summary>
    /// The one scheme a handler built with a connector only serves, as curl 8.21.0's
    /// <c>--version</c> protocol list names it.
    /// </summary>
    private static readonly string[] PlaintextSchemes = ["ftp"];

    /// <summary>The schemes a handler that can secure its connections serves.</summary>
    private static readonly string[] Schemes = ["ftp", ImplicitTlsScheme];

    private readonly IConnector connector;

    private readonly IConnector dataConnector;

    private readonly IConnectionListener listener;

    private readonly ITlsProvider tlsProvider;

    private readonly IDnsResolver dnsResolver;

    private readonly INetworkInterfaceLookup interfaceLookup;

    private readonly string[] schemes;

    /// <summary>
    /// Initializes a handler that serves <c>ftp</c> in passive mode over plaintext only: with
    /// no listener, <c>-P</c> fails with exit 30, and with no TLS, an accepted <c>AUTH</c> or
    /// <c>PROT P</c> fails with exit 64.
    /// </summary>
    /// <param name="connector">
    /// Supplies the control connection to the URL's host and port (21 unless the URL names
    /// one), and the data connection to the same host and the port the server offers
    /// (ADR-0005). No <see cref="System.Net.Sockets.Socket" /> is ever constructed here.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="connector" /> is <see langword="null" />.</exception>
    public FtpProtocolHandler(IConnector connector)
        : this(connector, connector, UnavailableConnectionListener.Instance, UnavailableTlsProvider.Instance, UnavailableDnsResolver.Instance, UnavailableNetworkInterfaceLookup.Instance, PlaintextSchemes)
    {
    }

    /// <summary>
    /// Initializes a handler that serves <c>ftp</c> and <c>ftps</c>, in passive or active
    /// mode, over plaintext or TLS (ADR-0102).
    /// </summary>
    /// <param name="connector">
    /// Supplies the control connection, made with TLS for <c>ftps</c> (port 990 unless the URL
    /// names one), and the passive data connection.
    /// </param>
    /// <param name="listener">Binds the port the server connects back to under <c>-P</c>.</param>
    /// <param name="tlsProvider">
    /// Upgrades the control connection after an accepted <c>AUTH</c>, and each data connection
    /// after an accepted <c>PROT P</c>.
    /// </param>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null" />.</exception>
    public FtpProtocolHandler(IConnector connector, IConnectionListener listener, ITlsProvider tlsProvider)
        : this(connector, listener, tlsProvider, UnavailableDnsResolver.Instance)
    {
    }

    /// <summary>
    /// Initializes a handler that serves <c>ftp</c> and <c>ftps</c>, in passive or active
    /// mode, over plaintext or TLS, and resolves a host name given to <c>-P</c> to the address
    /// it listens on and announces (ADR-0108).
    /// </summary>
    /// <param name="connector">
    /// Supplies the control connection, made with TLS for <c>ftps</c> (port 990 unless the URL
    /// names one), and the passive data connection.
    /// </param>
    /// <param name="listener">Binds the port the server connects back to under <c>-P</c>.</param>
    /// <param name="tlsProvider">
    /// Upgrades the control connection after an accepted <c>AUTH</c>, and each data connection
    /// after an accepted <c>PROT P</c>.
    /// </param>
    /// <param name="dnsResolver">Resolves a <c>-P</c> host name; its first address is the one used.</param>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null" />.</exception>
    public FtpProtocolHandler(IConnector connector, IConnectionListener listener, ITlsProvider tlsProvider, IDnsResolver dnsResolver)
        : this(connector, listener, tlsProvider, dnsResolver, UnavailableNetworkInterfaceLookup.Instance)
    {
    }

    /// <summary>
    /// Initializes a handler that serves <c>ftp</c> and <c>ftps</c>, in passive or active
    /// mode, over plaintext or TLS, and takes a name given to <c>-P</c> as a network
    /// interface's address when <paramref name="interfaceLookup" /> finds an interface of that
    /// name, otherwise as a host name to resolve (ADR-0108, ADR-0110).
    /// </summary>
    /// <param name="connector">
    /// Supplies the control connection, made with TLS for <c>ftps</c> (port 990 unless the URL
    /// names one), and the passive data connection.
    /// </param>
    /// <param name="listener">Binds the port the server connects back to under <c>-P</c>.</param>
    /// <param name="tlsProvider">
    /// Upgrades the control connection after an accepted <c>AUTH</c>, and each data connection
    /// after an accepted <c>PROT P</c>.
    /// </param>
    /// <param name="dnsResolver">Resolves a <c>-P</c> host name; its first address is the one used.</param>
    /// <param name="interfaceLookup">
    /// Finds a <c>-P</c> interface name's addresses; the first of the control connection's
    /// family and IPv6 scope is the one used.
    /// </param>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null" />.</exception>
    public FtpProtocolHandler(
        IConnector connector,
        IConnectionListener listener,
        ITlsProvider tlsProvider,
        IDnsResolver dnsResolver,
        INetworkInterfaceLookup interfaceLookup)
        : this(connector, connector, listener, tlsProvider, dnsResolver, interfaceLookup, Schemes)
    {
    }

    /// <summary>
    /// Initializes a handler that serves <c>ftp</c> and <c>ftps</c> as the five-argument
    /// constructor does, but opens each passive data connection through
    /// <paramref name="dataConnector" />, one that does not hold the connect to
    /// <c>--connect-timeout</c>, as curl 8.21.0 does not (measured, BL-797).
    /// </summary>
    /// <param name="connector">
    /// Supplies the control connection, made with TLS for <c>ftps</c> (port 990 unless the URL
    /// names one).
    /// </param>
    /// <param name="dataConnector">Supplies the passive data connection.</param>
    /// <param name="listener">Binds the port the server connects back to under <c>-P</c>.</param>
    /// <param name="tlsProvider">
    /// Upgrades the control connection after an accepted <c>AUTH</c>, and each data connection
    /// after an accepted <c>PROT P</c>.
    /// </param>
    /// <param name="dnsResolver">Resolves a <c>-P</c> host name; its first address is the one used.</param>
    /// <param name="interfaceLookup">
    /// Finds a <c>-P</c> interface name's addresses; the first of the control connection's
    /// family and IPv6 scope is the one used.
    /// </param>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null" />.</exception>
    public FtpProtocolHandler(
        IConnector connector,
        IConnector dataConnector,
        IConnectionListener listener,
        ITlsProvider tlsProvider,
        IDnsResolver dnsResolver,
        INetworkInterfaceLookup interfaceLookup)
        : this(connector, dataConnector, listener, tlsProvider, dnsResolver, interfaceLookup, Schemes)
    {
    }

    private FtpProtocolHandler(
        IConnector connector,
        IConnector dataConnector,
        IConnectionListener listener,
        ITlsProvider tlsProvider,
        IDnsResolver dnsResolver,
        INetworkInterfaceLookup interfaceLookup,
        string[] schemes)
    {
        ArgumentNullException.ThrowIfNull(connector);
        ArgumentNullException.ThrowIfNull(dataConnector);
        ArgumentNullException.ThrowIfNull(listener);
        ArgumentNullException.ThrowIfNull(tlsProvider);
        ArgumentNullException.ThrowIfNull(dnsResolver);
        ArgumentNullException.ThrowIfNull(interfaceLookup);
        this.connector = connector;
        this.dataConnector = dataConnector;
        this.listener = listener;
        this.tlsProvider = tlsProvider;
        this.dnsResolver = dnsResolver;
        this.interfaceLookup = interfaceLookup;
        this.schemes = schemes;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<string> SupportedSchemes => schemes;

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
        CurlUrl url = context.Url;
        bool implicitTls = url.Scheme == ImplicitTlsScheme;
        int defaultPort = implicitTls ? DefaultSecurePort : DefaultPort;
        var target = new ConnectTarget(url.IdnHost, url.IsDefaultPort ? defaultPort : url.Port, implicitTls)
        {
            Proxy = context.Proxy,
            Events = context.Events,
            DiagnosticLog = context.DiagnosticLog,
        };
        ConnectResult connected = await connector.ConnectAsync(target, context.CancellationToken).ConfigureAwait(false);
        if (connected.Connection is not { } connection)
        {
            var failed = new TransferResult(connected.ExitCode, 0, connected.ErrorMessage)
            {
                IsConnectionRefused = connected.IsConnectionRefused,
            };
            new FtpDiagnosticLog(context.DiagnosticLog).Failed(failed);
            return failed;
        }

        // Past the TCP connect, so -m's runner ends a stall with curl's Operation message (BL-512).
        context.Progress.ReportTransferStarted();
        await using (connection.ConfigureAwait(false))
        {
            var name = new FtpControlConnectionName(connected.ConnectionNumber, target.Host, target.Port);
            return await TransferAsync(connection, name, context, implicitTls, started).ConfigureAwait(false);
        }
    }

    private async ValueTask<TransferResult> TransferAsync(IConnection control, FtpControlConnectionName name, ITransferContext context, bool implicitTls, long started)
    {
        var connections = new FtpSessionConnections(dataConnector, listener, tlsProvider, dnsResolver, interfaceLookup);
        using var connectPhase = new FtpConnectPhaseLimit(context, started);
        var session = new FtpSession(connections, new FtpControlChannel(control, context.Events, connectPhase.Token, new FtpDiagnosticLog(context.DiagnosticLog)), name, context, implicitTls, connectPhase);
        await using (session.ConfigureAwait(false))
        {
            return await session.RunAsync().ConfigureAwait(false);
        }
    }
}
