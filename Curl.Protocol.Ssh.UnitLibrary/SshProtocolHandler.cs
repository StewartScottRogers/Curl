using System.Runtime.ExceptionServices;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh.Authentication;
using Curl.Protocol.Ssh.HostKeys;
using Curl.Protocol.Ssh.KeyExchange;
using Curl.Protocol.Ssh.Keys;
using Curl.Protocol.Ssh.Negotiation;
using Curl.Protocol.Ssh.Scp;
using Curl.Protocol.Ssh.Sftp;
using Curl.Protocol.Ssh.Transport;

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

        SshOptions options = context.Ssh ?? new SshOptions();
        CurlUrl url = context.Url;
        int port = url.IsDefaultPort ? DefaultPort : url.Port;
        KnownHostsFile? knownHosts = options.KnownHostsPath is { } knownHostsPath
            ? await KnownHostsFile.LoadAsync(fileSystem, knownHostsPath, context.CancellationToken).ConfigureAwait(false)
            : null;
        SshSessionTarget target = new(url.IdnHost, port, options, knownHosts);
        var connectTarget = new ConnectTarget(target.Host, target.Port, UseTls: false)
        {
            Proxy = context.Proxy,
            Events = context.Events,
            DiagnosticLog = context.DiagnosticLog,
        };
        ConnectResult connect = await connector.ConnectAsync(connectTarget, context.CancellationToken).ConfigureAwait(false);
        if (connect.Connection is not { } connection)
        {
            return new TransferResult(connect.ExitCode, 0, connect.ErrorMessage) { IsConnectionRefused = connect.IsConnectionRefused };
        }

        await using (connection.ConfigureAwait(false))
        {
            return await RunSessionAsync(context, target, connection, connect.ConnectionNumber).ConfigureAwait(false);
        }
    }

    private static async ValueTask DisconnectIgnoringFailureAsync(SshTransport transport, CancellationToken cancellationToken)
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

    // The -v lines libssh2's session start writes, then the session. A failure before the
    // transfer started, or one that ends the connection, closes it; a transfer's own
    // outcome leaves it intact (ADR-0262).
    private async ValueTask<TransferResult> RunSessionAsync(ITransferContext context, SshSessionTarget target, IConnection connection, long connectionNumber)
    {
        ITransferEvents events = context.Events;
        ReportSessionStart(context);
        SshStateTrace trace = new(events, TracesStateMachine);
        trace.Enter("SSH_INIT");
        trace.Enter("SSH_S_STARTUP");
        SshDiagnosticLog log = new(context.DiagnosticLog);
        try
        {
            TransferResult result = await HandshakeAndTransferAsync(context, target, connection, log, trace).ConfigureAwait(false);
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

    // The handshake, then the rest of the session, which always ends with DISCONNECT.
    private async ValueTask<TransferResult> HandshakeAndTransferAsync(ITransferContext context, SshSessionTarget target, IConnection connection, SshDiagnosticLog log, SshStateTrace trace)
    {
        SshAlgorithmPreferences offered = SshHostKeyChecker.NarrowHostKeys(
            preferences.WithCompression(target.Options.Compression), target.Host, target.Port, target.Options, target.KnownHosts, context.Events);
        SshTransport transport = new(connection, offered, SshAlgorithmCatalogue.Implemented, randomSource, ephemeralKeySource, log);
        SshNegotiatedHandshake handshake = await transport.NegotiateAlgorithmsAsync(context.CancellationToken).ConfigureAwait(false);
        log.HandshakeNegotiated(handshake);
        long started = context.TimeProvider.GetTimestamp();
        SshKeyExchangeResult keys = await transport.ExchangeKeysAsync(handshake, context.CancellationToken).ConfigureAwait(false);
        log.KeysExchanged(handshake.Algorithms.KeyExchange, context.TimeProvider.GetElapsedTime(started));
        // Not an await in a finally: the compiler's rethrow for one tests whether the captured
        // object is an Exception, a branch no C# code can take the other way (BL-1357).
        TransferResult? result = null;
        ExceptionDispatchInfo? failure = null;
        try
        {
            result = await AuthenticateAndTransferAsync(context, target, transport, keys.HostKey, trace).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failure = ExceptionDispatchInfo.Capture(exception);
        }

        await DisconnectIgnoringFailureAsync(transport, context.CancellationToken).ConfigureAwait(false);
        failure?.Throw();
        return result!;
    }

    private async ValueTask<TransferResult> AuthenticateAndTransferAsync(ITransferContext context, SshSessionTarget target, SshTransport transport, byte[] hostKey, SshStateTrace trace)
    {
        ITransferEvents events = context.Events;
        SshUserKeySource userKeys = new(fileSystem, readEnvironmentVariable, target.Options, credentialEncoding);
        SshUserAuthentication authentication = new(transport, credentialEncoding, userKeys, events, agentConnector) { Trace = trace };
        await authentication.RequestServiceAsync(context.CancellationToken).ConfigureAwait(false);
        transport.DiagnosticLog.HostKeyPresented(hostKey);
        CheckHostKey(hostKey, target, events, trace);
        transport.DiagnosticLog.HostKeyAccepted(target.Options, target.KnownHosts);
        await authentication.AuthenticateAsync(context.Credentials, context.CancellationToken).ConfigureAwait(false);
        trace.Enter(SshUserAuthentication.AuthDoneState);
        events.ReportInfo(SshInfoLines.AuthenticationComplete);
        bool overScp = context.Url.Scheme == ScpScheme;
        if (overScp)
        {
            events.ReportInfo(SshInfoLines.ConnectionEstablished);
            trace.Rest();
        }

        return await TransferAsync(context, transport, overScp, trace).ConfigureAwait(false);
    }

    // curl's SSH_HOSTKEY state, which with neither --hostpubsha256 nor --hostpubmd5 says it
    // checks the known-hosts file (measured, BL-1166).
    private static void CheckHostKey(byte[] hostKey, SshSessionTarget target, ITransferEvents events, SshStateTrace trace)
    {
        trace.Enter("SSH_HOSTKEY");
        if (target.Options.HostPublicKeySha256 is null && target.Options.HostPublicKeyMd5 is null)
        {
            trace.Write("no host key checksum given, checking knownhosts");
        }

        SshHostKeyChecker.Check(hostKey, target.Host, target.Port, target.Options, target.KnownHosts, events);
        trace.Enter("SSH_AUTHLIST");
    }

    // The transfer's failure message is a -v line too, as curl's failf writes it, unless
    // curl returns it without one.
    private static async ValueTask<TransferResult> TransferAsync(ITransferContext context, SshTransport transport, bool overScp, SshStateTrace trace)
    {
        TransferResult result;
        SshDiagnosticLog log = transport.DiagnosticLog;
        log.TransferStarted(context.Url.Scheme, context.Url.AbsolutePath, context.Upload is not null);
        long started = context.TimeProvider.GetTimestamp();
        try
        {
            ReceivedDataReportingStream output = new(context.Output, context.Events);
            result = overScp
                ? await TransferOverScpAsync(context, transport, output, trace).ConfigureAwait(false)
                : await TransferOverSftpAsync(context, transport, output, trace).ConfigureAwait(false);
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
            : await new ScpFileDownload(transport) { Trace = trace }.DownloadAsync(context.Url.AbsolutePath, output, context.Progress, context.CancellationToken, context.MaxFileSize).ConfigureAwait(false);

    // An upload is sent (ADR-0244); otherwise a path ending with a slash is listed and any
    // other is downloaded (ADR-0241). Each runs the -Q commands around it (ADR-0247), with
    // Windows' 32-bit C long deciding how curl reads their numbers and dates.
    private static async ValueTask<TransferResult> TransferOverSftpAsync(ITransferContext context, SshTransport transport, Stream output, SshStateTrace trace)
    {
        string urlPath = context.Url.AbsolutePath;
        SftpQuoteCommands quotes = SftpQuoteCommands.From(context, OperatingSystem.IsWindows(), trace);
        if (context.Upload is { } upload)
        {
            return await new SftpFileUpload(transport, context.Events) { Trace = trace }.UploadAsync(urlPath, SftpUploadOptions.From(context), upload, context.Progress, context.CancellationToken, quotes).ConfigureAwait(false);
        }

        return SftpRemotePath.NamesDirectory(urlPath)
            ? await new SftpDirectoryListing(transport) { Trace = trace }.ListAsync(urlPath, context.ListOnly, context.NoBody, output, context.Progress, context.CancellationToken, quotes, context.MaxFileSize).ConfigureAwait(false)
            : await new SftpFileDownload(transport) { Trace = trace }.DownloadAsync(urlPath, context.CreateFileMode, output, context.Progress, context.CancellationToken, quotes, context.Range, context.ResumeFrom, context.MaxFileSize, context.RangeText).ConfigureAwait(false);
    }

    // What the host-key check and the key files need to know about the session.
    private sealed record SshSessionTarget(string Host, int Port, SshOptions Options, KnownHostsFile? KnownHosts);
}
