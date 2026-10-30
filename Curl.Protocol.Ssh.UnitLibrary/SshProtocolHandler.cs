using System.Runtime.ExceptionServices;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh.Authentication;
using Curl.Protocol.Ssh.HostKeys;
using Curl.Protocol.Ssh.Keys;
using Curl.Protocol.Ssh.Scp;
using Curl.Protocol.Ssh.Sftp;
using Curl.Ssh;
using Curl.Ssh.KeyExchange;
using Curl.Ssh.Negotiation;
using Curl.Ssh.Transport;

namespace Curl.Protocol.Ssh;

/// <summary>
/// Serves the <c>scp</c> and <c>sftp</c> schemes as curl 8.21.0 does through libssh2 1.11.1
/// (ADR-0122): narrows the host-key list from the known-hosts file, connects,
/// runs the transport's handshake, requests <c>ssh-userauth</c>, checks the server's host
/// key, authenticates the user, uploads <see cref="ITransferContext.Upload" /> with
/// <see cref="SftpFileUpload" /> or <see cref="ScpFileUpload" /> when there is one, and otherwise downloads
/// the URL's file with <see cref="SftpFileDownload" />
/// or <see cref="ScpFileDownload" /> - or, for an <c>sftp</c> path ending with a slash, lists
/// the directory with <see cref="SftpDirectoryListing" /> - into
/// <see cref="ITransferContext.Output" />, and ends the session with
/// <c>SSH_MSG_DISCONNECT</c>.
/// </summary>
/// <remarks>
/// The connector supplies the connection to the URL's host and port (22 by default); no
/// <see cref="System.Net.Sockets.Socket" /> is ever constructed here. Every
/// <see cref="SshTransferException" /> becomes a failed <see cref="TransferResult" /> with its
/// exit code and message; a connect failure is returned as the connector reported it.
/// </remarks>
public sealed class SshProtocolHandler : IProtocolHandler
{
    /// <summary>The port both schemes default to.</summary>
    internal const int DefaultPort = 22;

    /// <summary>The <c>SSH_MSG_DISCONNECT</c> reason curl sends at the end of every session: <c>SSH_DISCONNECT_BY_APPLICATION</c>.</summary>
    internal const uint DisconnectByApplication = 11;

    /// <summary>The description curl's <c>DISCONNECT</c> carries (ADR-0215).</summary>
    internal const string DisconnectDescription = "Shutdown";

    private const string ScpScheme = "scp";

    private static readonly string[] Schemes = [ScpScheme, "sftp"];

    private readonly IConnector connector;

    private readonly IFileSystem fileSystem;

    private readonly SshAlgorithmPreferences preferences;

    private readonly Encoding credentialEncoding;

    private readonly ISshRandomSource randomSource;

    private readonly ISshEphemeralKeySource ephemeralKeySource;

    private readonly Func<string, string?> readEnvironmentVariable;

    private readonly ISshAgentConnector agentConnector;

    /// <summary>
    /// Initializes a new instance of the <see cref="SshProtocolHandler" /> class that draws
    /// its random bytes and ephemeral keys from the system, reads <c>HOME</c> from the
    /// process's environment, and finds the ssh-agent where this platform's curl looks.
    /// </summary>
    /// <param name="connector">Supplies the connection to the URL's host and port.</param>
    /// <param name="fileSystem">Where the known-hosts file and the user's key files are opened.</param>
    /// <param name="preferences">
    /// The algorithms offered: <see cref="SshAlgorithmPreferences.WindowsReference" /> on
    /// Windows and <see cref="SshAlgorithmPreferences.OpenSslReference" /> elsewhere, as the
    /// platform's curl offers them.
    /// </param>
    /// <param name="credentialEncoding">
    /// How the user name, password and <c>--pass</c> become bytes: ADR-0022's
    /// <c>CredentialEncoding.ForPlatform</c>.
    /// </param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null" />.</exception>
    public SshProtocolHandler(IConnector connector, IFileSystem fileSystem, SshAlgorithmPreferences preferences, Encoding credentialEncoding)
        : this(connector, fileSystem, preferences, credentialEncoding, Environment.GetEnvironmentVariable)
    {
    }

    // Converts Environment.GetEnvironmentVariable to a delegate once: the compiler caches each
    // method-group conversion behind a null check, and a second conversion of the same method
    // shares the first's cache, so its null branch could never run (BL-1357).
    private SshProtocolHandler(IConnector connector, IFileSystem fileSystem, SshAlgorithmPreferences preferences, Encoding credentialEncoding, Func<string, string?> readEnvironmentVariable)
        : this(
            connector,
            fileSystem,
            preferences,
            credentialEncoding,
            new SystemSshRandomSource(),
            new SystemSshEphemeralKeySource(),
            readEnvironmentVariable,
            PlatformSshAgentConnector.Create(readEnvironmentVariable, OperatingSystem.IsWindows(), new WindowsPageantWindow()))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SshProtocolHandler" /> class with every
    /// seam given.
    /// </summary>
    /// <param name="connector">Supplies the connection to the URL's host and port.</param>
    /// <param name="fileSystem">Where the known-hosts file and the user's key files are opened.</param>
    /// <param name="preferences">The algorithms offered.</param>
    /// <param name="credentialEncoding">How the user name, password and <c>--pass</c> become bytes.</param>
    /// <param name="randomSource">Where cookies and padding come from.</param>
    /// <param name="ephemeralKeySource">Where the key exchange's ephemeral keys come from.</param>
    /// <param name="readEnvironmentVariable">Reads <c>HOME</c> for curl's default key files.</param>
    /// <param name="agentConnector">Opens the connection to the user's ssh-agent.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null" />.</exception>
    internal SshProtocolHandler(
        IConnector connector,
        IFileSystem fileSystem,
        SshAlgorithmPreferences preferences,
        Encoding credentialEncoding,
        ISshRandomSource randomSource,
        ISshEphemeralKeySource ephemeralKeySource,
        Func<string, string?> readEnvironmentVariable,
        ISshAgentConnector agentConnector)
    {
        this.connector = connector ?? throw new ArgumentNullException(nameof(connector));
        this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        this.preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
        this.credentialEncoding = credentialEncoding ?? throw new ArgumentNullException(nameof(credentialEncoding));
        this.randomSource = randomSource;
        this.ephemeralKeySource = ephemeralKeySource;
        this.readEnvironmentVariable = readEnvironmentVariable;
        this.agentConnector = agentConnector;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<string> SupportedSchemes => Schemes;

    /// <summary>
    /// Gets a value indicating whether each transfer writes curl 8.21.0's <c>--trace-config ssh</c>
    /// lines, <c>[SSH] ...</c>, from the session's state changes through the transfer's events
    /// (<see cref="SshStateTrace" />, BL-1166).
    /// </summary>
    public bool TracesStateMachine { get; init; }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="context" /> is <see langword="null" />.</exception>
    /// <exception cref="OperationCanceledException"><see cref="ITransferContext.CancellationToken" /> was cancelled.</exception>
    public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        SshTransferClock clock = new(context.TimeProvider);
        using SshConnectPhaseLimit connectPhase = new(context);
        SshOptions options = context.Ssh ?? new SshOptions();
        CurlUrl url = context.Url;
        int port = url.IsDefaultPort ? DefaultPort : url.Port;
        KnownHostsFile? knownHosts = options.KnownHostsPath is { } knownHostsPath
            ? await KnownHostsFile.LoadAsync(fileSystem, knownHostsPath, context.CancellationToken).ConfigureAwait(false)
            : null;
        SshSessionTarget target = new(url.IdnHost, port, options, knownHosts, clock);
        return clock.Stamp(await ConnectAndRunSessionAsync(context, target, connectPhase).ConfigureAwait(false));
    }

    // Connects, then runs the session the connection already holds, or a new one.
    private async ValueTask<TransferResult> ConnectAndRunSessionAsync(ITransferContext context, SshSessionTarget target, SshConnectPhaseLimit connectPhase)
    {
        var connectTarget = new ConnectTarget(target.Host, target.Port, UseTls: false)
        {
            Proxy = context.Proxy,
            Events = context.Events,
            DiagnosticLog = context.DiagnosticLog,
            PoolScheme = context.Url.Scheme,
        };
        string loginKey = SshKeptSession.LoginKeyOf(context);
        ConnectResult connect = await ConnectServingAsync(connectTarget, loginKey, context.CancellationToken).ConfigureAwait(false);
        target.Clock.Connect = connect.Timings;
        if (connect.Connection is not { } connection)
        {
            return new TransferResult(connect.ExitCode, 0, connect.ErrorMessage) { IsConnectionRefused = connect.IsConnectionRefused };
        }

        context.Progress.ReportTransferStarted();
        await using (connection.ConfigureAwait(false))
        {
            return connect.IsReused && connection.Session is SshKeptSession kept
                ? await RunKeptSessionAsync(context, target, connection, kept, connect.ConnectionNumber, connectPhase).ConfigureAwait(false)
                : CountNewConnection(await RunNewSessionAsync(context, target, connection, connect.ConnectionNumber, connectPhase, loginKey).ConfigureAwait(false), connection);
        }
    }

    /// <summary>
    /// Sends <c>SSH_MSG_DISCONNECT</c> 11 <c>Shutdown</c>, as curl ends every session; a server
    /// already gone is ignored.
    /// </summary>
    /// <param name="transport">The session's transport.</param>
    /// <param name="cancellationToken">Cancels the send.</param>
    /// <returns>A task that completes once the message is sent or the server found gone.</returns>
    internal static async ValueTask DisconnectIgnoringFailureAsync(SshTransport transport, CancellationToken cancellationToken)
    {
        SshWireWriter disconnect = new();
        disconnect.WriteByte(SshMessageNumber.Disconnect);
        disconnect.WriteUInt32(DisconnectByApplication);
        disconnect.WriteString(Encoding.ASCII.GetBytes(DisconnectDescription));
        disconnect.WriteString([]);
        try
        {
            await transport.PacketWriter.WriteAsync(disconnect.ToArray(), cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            // The server has already gone; the transfer's outcome is decided.
        }
    }

    // The -v lines libssh2's session start writes, then the session.
    private ValueTask<TransferResult> RunNewSessionAsync(ITransferContext context, SshSessionTarget target, IConnection connection, long connectionNumber, SshConnectPhaseLimit connectPhase, string loginKey)
    {
        ReportSessionStart(context);
        SshStateTrace trace = new(context.Events, TracesStateMachine);
        trace.Enter("SSH_INIT");
        trace.Enter("SSH_S_STARTUP");
        return RunSessionAsync(context, target, connectionNumber, connectPhase, trace, log => HandshakeAndTransferAsync(context, target, connection, log, trace, connectPhase.Token, loginKey));
    }

    // A connection a transfer opened and left for the connection cache counts in
    // %{num_connects}; a reused one does not (CGR-130). A failure's count is left to the
    // engine, as it was before connections were kept.
    private static TransferResult CountNewConnection(TransferResult result, IConnection connection) =>
        connection.Session is SshKeptSession ? result with { Report = ReportOf(result) with { ConnectionCount = 1 } } : result;

    // The result's report, or a new one; a download leaves no report of its own, so its bytes
    // go in DownloadSize, the source of %{size_download} and %{speed_download} (measured,
    // BL-3971).
    private static TransferReport ReportOf(TransferResult result) =>
        result.Report ?? new TransferReport { DownloadSize = result.BytesTransferred };

    /// <summary>
    /// Connects, and closes a reused connection whose session another login authenticated and
    /// connects again, as curl reuses an SSH connection only for the same user, password and
    /// key files (ADR-0865).
    /// </summary>
    private async ValueTask<ConnectResult> ConnectServingAsync(ConnectTarget target, string loginKey, CancellationToken cancellationToken)
    {
        ConnectResult connected = await connector.ConnectAsync(target, cancellationToken).ConfigureAwait(false);
        while (connected is { IsReused: true, Connection: { } connection } && !(connection.Session is SshKeptSession kept && kept.Serves(loginKey)))
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            connected = await connector.ConnectAsync(target, cancellationToken).ConfigureAwait(false);
        }

        return connected;
    }

    // A reused session's transfer: no handshake, no authentication and, over SFTP, the
    // session and home directory the connection already holds, as curl 8.22.0 reuses it
    // (CGR-130).
    private ValueTask<TransferResult> RunKeptSessionAsync(ITransferContext context, SshSessionTarget target, IConnection connection, SshKeptSession kept, long connectionNumber, SshConnectPhaseLimit connectPhase)
    {
        kept.UseConnection(connection);
        SshStateTrace trace = new(context.Events, TracesStateMachine);
        return RunSessionAsync(context, target, connectionNumber, connectPhase, trace, _ => TransferAndKeepOrDisconnectAsync(context, connection, kept, () => TransferAsync(context, kept.Transport, context.Url.Scheme == ScpScheme, trace, kept.Sftp, target.Clock)));
    }

    private static readonly TimeSpan ShutDownTimeout = TimeSpan.FromSeconds(5);

    // Runs the transfer, then leaves the session on the connection for the run's connection
    // cache after a success, and otherwise closes the SFTP channel and ends the session with
    // DISCONNECT, as it ends when the connection cannot be kept (ADR-0865).
    // Not an await in a finally: the compiler's rethrow for one tests whether the captured
    // object is an Exception, a branch no C# code can take the other way (BL-1357).
    private static async ValueTask<TransferResult> TransferAndKeepOrDisconnectAsync(ITransferContext context, IConnection connection, SshKeptSession session, Func<ValueTask<TransferResult>> transfer)
    {
        TransferResult? result = null;
        ExceptionDispatchInfo? failure = null;
        try
        {
            result = await transfer().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failure = ExceptionDispatchInfo.Capture(exception);
        }

        if (result is { IsSuccess: true } && connection.TryHoldSession(session))
        {
            connection.MarkReusable();
            return result;
        }

        // A transfer stopped by --max-time or --speed-limit has a cancelled token, yet curl still closes
        // the SFTP channel and sends DISCONNECT, so the close runs on a fresh token bounded by a few
        // seconds (CGR-270).
        using CancellationTokenSource shutDown = new(ShutDownTimeout, context.TimeProvider);
        await session.ShutDownAsync(shutDown.Token).ConfigureAwait(false);
        failure?.Throw();
        return result!;
    }

    // A failure before the transfer started, or one that ends the connection, closes it; a
    // transfer's own outcome leaves it intact (ADR-0262). The handshake and the
    // authentication are held to --connect-timeout (CGR-010, BL-3539).
    private static async ValueTask<TransferResult> RunSessionAsync(ITransferContext context, SshSessionTarget target, long connectionNumber, SshConnectPhaseLimit connectPhase, SshStateTrace trace, Func<SshDiagnosticLog, ValueTask<TransferResult>> session)
    {
        ITransferEvents events = context.Events;
        SshDiagnosticLog log = new(new SshTransportLog(context.DiagnosticLog));
        try
        {
            TransferResult result = await session(log).ConfigureAwait(false);
            events.ReportInfo(FailedWhileTransferring(result, ListsDirectory(context))
                ? SshInfoLines.ClosingConnection(connectionNumber)
                : SshInfoLines.ConnectionLeftIntact(connectionNumber, target.Host, target.Port));
            return result;
        }
        catch (SshTransferException exception)
        {
            log.Failed(exception);
            if (exception.IsVerboseLine)
            {
                events.ReportInfo(exception.Message);
            }

            trace.Fail(exception.ExitCode);
            events.ReportInfo(SshInfoLines.ClosingConnection(connectionNumber));
            return TransferResult.Failure(exception.ExitCode, exception.Message);
        }
        catch (OperationCanceledException) when (connectPhase.HasPassed)
        {
            TransferResult timedOut = connectPhase.Failure();
            events.ReportInfo(timedOut.ErrorMessage!);
            trace.Fail(timedOut.ExitCode);
            events.ReportInfo(SshInfoLines.ClosingConnection(connectionNumber));
            return timedOut;
        }
    }

    // libssh2.c ssh_connect's lines: the backend, the user, and, through an HTTPS proxy
    // only, that libssh2 sends through the proxy's TLS tunnel (BL-1124).
    private void ReportSessionStart(ITransferContext context)
    {
        ITransferEvents events = context.Events;
        if (preferences.CryptographyBackend is { } backend)
        {
            events.ReportInfo(SshInfoLines.CryptographyBackend(backend));
        }

        events.ReportInfo(SshInfoLines.User(context.Credentials?.UserName ?? string.Empty));
        if (context.Proxy?.Kind == ProxyKind.Https)
        {
            events.ReportInfo(SshInfoLines.UsingHttpsProxy);
        }
    }

    /// <summary>
    /// Tells whether a transfer's outcome is a failure while its bytes were moving, after
    /// which curl closes the connection ("Transfer returned error") rather than leaving it
    /// intact: a short file (exit 18) or the connection lost during a file's bytes
    /// (<c>Error in the SSH layer</c>), measured 2026-10-01 for <c>scp</c> (BL-988). An
    /// <c>sftp</c> directory is read in curl's state machine before that phase, so its
    /// failures leave the connection intact, as every failure thrown before the bytes does.
    /// </summary>
    /// <param name="result">The transfer's outcome.</param>
    /// <param name="listsDirectory">Whether the transfer listed an <c>sftp</c> directory.</param>
    /// <returns><see langword="true" /> when the connection is closed.</returns>
    internal static bool FailedWhileTransferring(TransferResult result, bool listsDirectory) =>
        result.ExitCode == CurlExitCode.PartialFile
        || (result.ErrorMessage == SshTransferException.SshLayerErrorMessage && !listsDirectory);

    private static bool ListsDirectory(ITransferContext context) =>
        context.Url.Scheme != ScpScheme && context.Upload is null && SftpRemotePath.NamesDirectory(context.Url.AbsolutePath);

    // The handshake, then the rest of the session, which is kept for the connection cache or
    // ends with DISCONNECT.
    private async ValueTask<TransferResult> HandshakeAndTransferAsync(ITransferContext context, SshSessionTarget target, IConnection connection, SshDiagnosticLog log, SshStateTrace trace, CancellationToken connectPhase, string loginKey)
    {
        SshAlgorithmPreferences offered = SshHostKeyChecker.NarrowHostKeys(
            preferences.WithCompression(target.Options.Compression), target.Host, target.Port, target.Options, target.KnownHosts, context.Events);
        SshSessionConnection sessionConnection = new(connection);
        SshTransport transport = new(sessionConnection, offered, SshAlgorithmCatalogue.Implemented, randomSource, ephemeralKeySource, log.Transport);
        SshKeyExchangeResult keys = await ExchangeKeysAsync(context, transport, log, connectPhase).ConfigureAwait(false);
        SshKeptSession session = new(transport, sessionConnection, new SftpSessionSlot(keepsSession: true), loginKey);
        return await TransferAndKeepOrDisconnectAsync(context, connection, session, () => AuthenticateAndTransferAsync(context, target, session, keys.HostKey, trace, connectPhase)).ConfigureAwait(false);
    }

    // The identification exchange and the key exchange. Cancelled once the server's
    // identification has arrived, the session still ends with DISCONNECT, as curl 8.22.0's
    // does when --connect-timeout passes there (measured 2026-10-26, BL-3539).
    private static async ValueTask<SshKeyExchangeResult> ExchangeKeysAsync(ITransferContext context, SshTransport transport, SshDiagnosticLog log, CancellationToken connectPhase)
    {
        // Not an await in a catch, for the reason HandshakeAndTransferAsync gives (BL-1357).
        SshKeyExchangeResult? keys = null;
        ExceptionDispatchInfo? cancelled = null;
        try
        {
            SshNegotiatedHandshake handshake = await transport.NegotiateAlgorithmsAsync(connectPhase).ConfigureAwait(false);
            log.Transport.HandshakeNegotiated(handshake);
            long started = context.TimeProvider.GetTimestamp();
            keys = await transport.ExchangeKeysAsync(handshake, connectPhase).ConfigureAwait(false);
            log.Transport.KeysExchanged(handshake.Algorithms.KeyExchange, context.TimeProvider.GetElapsedTime(started));
        }
        catch (OperationCanceledException exception) when (transport.HasServerIdentificationArrived)
        {
            cancelled = ExceptionDispatchInfo.Capture(exception);
        }

        if (cancelled is not null)
        {
            await DisconnectIgnoringFailureAsync(transport, context.CancellationToken).ConfigureAwait(false);
        }

        cancelled?.Throw();
        return keys!;
    }

    private async ValueTask<TransferResult> AuthenticateAndTransferAsync(ITransferContext context, SshSessionTarget target, SshKeptSession session, byte[] hostKey, SshStateTrace trace, CancellationToken connectPhase)
    {
        ITransferEvents events = context.Events;
        SshTransport transport = session.Transport;
        SshUserKeySource userKeys = new(fileSystem, readEnvironmentVariable, target.Options, credentialEncoding);
        SshUserAuthentication authentication = new(transport, credentialEncoding, userKeys, events, agentConnector) { Trace = trace };
        await authentication.RequestServiceAsync(connectPhase).ConfigureAwait(false);
        transport.DiagnosticLog.HostKeyPresented(hostKey);
        await CheckHostKeyAsync(hostKey, target, events, trace, connectPhase).ConfigureAwait(false);
        new SshDiagnosticLog(transport.DiagnosticLog).HostKeyAccepted(target.Options, target.KnownHosts);
        await authentication.AuthenticateAsync(context.Credentials, connectPhase).ConfigureAwait(false);
        trace.Enter(SshUserAuthentication.AuthDoneState);
        events.ReportInfo(SshInfoLines.AuthenticationComplete);
        target.Clock.MarkAuthenticated();
        bool overScp = context.Url.Scheme == ScpScheme;
        if (overScp)
        {
            events.ReportInfo(SshInfoLines.ConnectionEstablished);
            trace.Rest();
        }

        return await TransferAsync(context, transport, overScp, trace, session.Sftp, target.Clock).ConfigureAwait(false);
    }

    // curl's SSH_HOSTKEY state, which with neither --hostpubsha256 nor --hostpubmd5 says it
    // checks the known-hosts file (measured, BL-1166), and writes it back there when
    // CURLOPT_SSH_KEYFUNCTION asked for it (ADR-0626).
    private async ValueTask CheckHostKeyAsync(byte[] hostKey, SshSessionTarget target, ITransferEvents events, SshStateTrace trace, CancellationToken cancellationToken)
    {
        trace.Enter("SSH_HOSTKEY");
        if (target.Options.HostPublicKeySha256 is null && target.Options.HostPublicKeyMd5 is null)
        {
            trace.Write("no host key checksum given, checking knownhosts");
        }

        if (SshHostKeyChecker.Check(hostKey, target.Host, target.Port, target.Options, target.KnownHosts, events) is { } rewritten)
        {
            await rewritten.WriteAsync(fileSystem, target.Options.KnownHostsPath!, events, cancellationToken).ConfigureAwait(false);
        }

        trace.Enter("SSH_AUTHLIST");
    }

    // The transfer's failure message is a -v line too, as curl's failf writes it, unless
    // curl returns it without one.
    private static async ValueTask<TransferResult> TransferAsync(ITransferContext context, SshTransport transport, bool overScp, SshStateTrace trace, SftpSessionSlot sftp, SshTransferClock clock)
    {
        TransferResult result;
        SshDiagnosticLog log = new(transport.DiagnosticLog);
        log.TransferStarted(context.Url.Scheme, context.Url.AbsolutePath, context.Upload is not null);
        long started = context.TimeProvider.GetTimestamp();
        clock.MarkTransferReady();
        try
        {
            ReceivedDataReportingStream output = new(context.Output, context.Events) { Written = clock.MarkByteReceived };
            result = overScp
                ? await TransferOverScpAsync(context, transport, output, trace).ConfigureAwait(false)
                : await TransferOverSftpAsync(context, transport, output, trace, sftp).ConfigureAwait(false);
        }
        catch (SshTransferException exception) when (!exception.EndsConnection)
        {
            log.Failed(exception);
            if (exception.IsVerboseLine)
            {
                context.Events.ReportInfo(exception.Message);
            }

            trace.Fail(exception.ExitCode);
            return TransferResult.Failure(exception.ExitCode, exception.Message);
        }

        log.TransferEnded(result, context.TimeProvider.GetElapsedTime(started));
        ReportReturnedFailure(context.Events, result);
        if (!result.IsSuccess)
        {
            trace.Fail(result.ExitCode);
        }

        return result;
    }

    /// <summary>
    /// Writes a failure a transfer returned rather than threw as a <c>-v</c> line, as curl's
    /// <c>failf</c> writes it; <c>Error in the SSH layer</c>, which curl returns without a
    /// message of its own, and a success write nothing (ADR-0262).
    /// </summary>
    /// <param name="events">Where the line goes.</param>
    /// <param name="result">The transfer's outcome.</param>
    internal static void ReportReturnedFailure(ITransferEvents events, TransferResult result)
    {
        if (result.ErrorMessage is { } message && message != SshTransferException.SshLayerErrorMessage)
        {
            events.ReportInfo(message);
        }
    }

    // An upload is sent (ADR-0258); otherwise the file is downloaded (ADR-0225).
    private static async ValueTask<TransferResult> TransferOverScpAsync(ITransferContext context, SshTransport transport, Stream output, SshStateTrace trace) =>
        context.Upload is { } upload
            ? await new ScpFileUpload(transport, context.Events) { Trace = trace }.UploadAsync(context.Url.AbsolutePath, context.CreateFileMode, upload, context.Progress, context.CancellationToken).ConfigureAwait(false)
            : await new ScpFileDownload(transport) { Trace = trace, NoBody = context.NoBody }.DownloadAsync(context.Url.AbsolutePath, output, context.Progress, context.CancellationToken, context.MaxFileSize).ConfigureAwait(false);

    // An upload is sent (ADR-0244); otherwise a path ending with a slash is listed and any
    // other is downloaded (ADR-0241). Each runs the -Q commands around it (ADR-0247), with
    // Windows' 32-bit C long deciding how curl reads their numbers and dates.
    private static async ValueTask<TransferResult> TransferOverSftpAsync(ITransferContext context, SshTransport transport, Stream output, SshStateTrace trace, SftpSessionSlot sftp)
    {
        string urlPath = context.Url.AbsolutePath;
        SftpQuoteCommands quotes = SftpQuoteCommands.From(context, OperatingSystem.IsWindows(), trace);
        if (context.Upload is { } upload)
        {
            return await new SftpFileUpload(transport, context.Events) { Trace = trace, Sessions = sftp }.UploadAsync(urlPath, SftpUploadOptions.From(context), upload, context.Progress, context.CancellationToken, quotes).ConfigureAwait(false);
        }

        return SftpRemotePath.NamesDirectory(urlPath)
            ? await new SftpDirectoryListing(transport) { Trace = trace, Sessions = sftp }.ListAsync(urlPath, context.ListOnly, context.NoBody, output, context.Progress, context.CancellationToken, quotes, context.MaxFileSize).ConfigureAwait(false)
            : await new SftpFileDownload(transport) { Trace = trace, NoBody = context.NoBody, Sessions = sftp }.DownloadAsync(urlPath, context.CreateFileMode, output, context.Progress, context.CancellationToken, quotes, context.Range, context.ResumeFrom, context.MaxFileSize, context.RangeText).ConfigureAwait(false);
    }

    // What the host-key check and the key files need to know about the session, and the
    // clock that records the transfer's -w times.
    private sealed record SshSessionTarget(string Host, int Port, SshOptions Options, KnownHostsFile? KnownHosts, SshTransferClock Clock);

    // The points in time curl's libssh2 backend records for -w (CGR-107, BL-3957): the
    // connector's name lookup and connect, appconnect at "Authentication complete", where
    // curl's SSH_AUTH_DONE sets TIMER_APPCONNECT, pretransfer as the transfer starts,
    // starttransfer at the first byte written to the output, and the total as the handler
    // returns, a failure's included. A transfer that fails before its first byte, such as a
    // missing file, has no pretransfer, as curl sets it only once its DO phase is done
    // (measured, BL-3957).
    private sealed class SshTransferClock(TimeProvider timeProvider)
    {
        private readonly long started = timeProvider.GetTimestamp();

        private long? authenticated;

        private long? transferReady;

        private long? firstByteReceived;

        public ConnectTimings? Connect { get; set; }

        public void MarkAuthenticated() => authenticated = timeProvider.GetTimestamp();

        public void MarkTransferReady() => transferReady = timeProvider.GetTimestamp();

        public void MarkByteReceived() => firstByteReceived ??= timeProvider.GetTimestamp();

        public TransferResult Stamp(TransferResult result)
        {
            ConnectTimings connect = (Connect ?? new ConnectTimings(started, null, null, null)) with { TlsHandshakeCompleted = authenticated };
            long? requestReady = result.IsSuccess || firstByteReceived is not null ? transferReady : null;
            TransferTimings timings = new(started, connect, requestReady, null, firstByteReceived, timeProvider.GetTimestamp());
            return result with { Report = ReportOf(result) with { Timings = timings } };
        }
    }
}
