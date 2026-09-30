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
        : this(
            connector,
            fileSystem,
            preferences,
            credentialEncoding,
            new SystemSshRandomSource(),
            new SystemSshEphemeralKeySource(),
            Environment.GetEnvironmentVariable,
            new SystemSshAgentConnector(Environment.GetEnvironmentVariable, OperatingSystem.IsWindows()))
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
        if (preferences.CryptographyBackend is { } backend)
        {
            events.ReportInfo(SshInfoLines.CryptographyBackend(backend));
        }

        events.ReportInfo(SshInfoLines.User(context.Credentials?.UserName ?? string.Empty));
        try
        {
            TransferResult result = await HandshakeAndTransferAsync(context, target, connection).ConfigureAwait(false);
            events.ReportInfo(SshInfoLines.ConnectionLeftIntact(connectionNumber, target.Host, target.Port));
            return result;
        }
        catch (SshTransferException exception)
        {
            if (exception.IsVerboseLine)
            {
                events.ReportInfo(exception.Message);
            }

            events.ReportInfo(SshInfoLines.ClosingConnection(connectionNumber));
            return TransferResult.Failure(exception.ExitCode, exception.Message);
        }
    }

    // The handshake, then the rest of the session, which always ends with DISCONNECT.
    private async ValueTask<TransferResult> HandshakeAndTransferAsync(ITransferContext context, SshSessionTarget target, IConnection connection)
    {
        SshAlgorithmPreferences offered = SshHostKeyChecker.NarrowHostKeys(
            preferences.WithCompression(target.Options.Compression), target.Host, target.Port, target.Options, target.KnownHosts, context.Events);
        SshTransport transport = new(connection, offered, SshAlgorithmCatalogue.Implemented, randomSource, ephemeralKeySource);
        SshKeyExchangeResult keys = await transport.ExchangeKeysAsync(
            await transport.NegotiateAlgorithmsAsync(context.CancellationToken).ConfigureAwait(false),
            context.CancellationToken).ConfigureAwait(false);
        try
        {
            return await AuthenticateAndTransferAsync(context, target, transport, keys.HostKey).ConfigureAwait(false);
        }
        finally
        {
            await DisconnectIgnoringFailureAsync(transport, context.CancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask<TransferResult> AuthenticateAndTransferAsync(ITransferContext context, SshSessionTarget target, SshTransport transport, byte[] hostKey)
    {
        ITransferEvents events = context.Events;
        SshUserKeySource userKeys = new(fileSystem, readEnvironmentVariable, target.Options, credentialEncoding);
        SshUserAuthentication authentication = new(transport, credentialEncoding, userKeys, events, agentConnector);
        await authentication.RequestServiceAsync(context.CancellationToken).ConfigureAwait(false);
        SshHostKeyChecker.Check(hostKey, target.Host, target.Port, target.Options, target.KnownHosts, events);
        await authentication.AuthenticateAsync(context.Credentials, context.CancellationToken).ConfigureAwait(false);
        events.ReportInfo(SshInfoLines.AuthenticationComplete);
        bool overScp = context.Url.Scheme == ScpScheme;
        if (overScp)
        {
            events.ReportInfo(SshInfoLines.ConnectionEstablished);
        }

        return await TransferAsync(context, transport, overScp).ConfigureAwait(false);
    }

    // The transfer's failure message is a -v line too, as curl's failf writes it, unless
    // curl returns it without one.
    private static async ValueTask<TransferResult> TransferAsync(ITransferContext context, SshTransport transport, bool overScp)
    {
        TransferResult result;
        try
        {
            ReceivedDataReportingStream output = new(context.Output, context.Events);
            result = overScp
                ? await TransferOverScpAsync(context, transport, output).ConfigureAwait(false)
                : await TransferOverSftpAsync(context, transport, output).ConfigureAwait(false);
        }
        catch (SshTransferException exception) when (!exception.EndsConnection)
        {
            if (exception.IsVerboseLine)
            {
                context.Events.ReportInfo(exception.Message);
            }

            return TransferResult.Failure(exception.ExitCode, exception.Message);
        }

        ReportReturnedFailure(context.Events, result);
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
    private static async ValueTask<TransferResult> TransferOverScpAsync(ITransferContext context, SshTransport transport, Stream output) =>
        context.Upload is { } upload
            ? await new ScpFileUpload(transport).UploadAsync(context.Url.AbsolutePath, context.CreateFileMode, upload, context.Progress, context.CancellationToken).ConfigureAwait(false)
            : await new ScpFileDownload(transport).DownloadAsync(context.Url.AbsolutePath, output, context.Progress, context.CancellationToken).ConfigureAwait(false);

    // An upload is sent (ADR-0244); otherwise a path ending with a slash is listed and any
    // other is downloaded (ADR-0241). Each runs the -Q commands around it (ADR-0247), with
    // Windows' 32-bit C long deciding how curl reads their numbers and dates.
    private static async ValueTask<TransferResult> TransferOverSftpAsync(ITransferContext context, SshTransport transport, Stream output)
    {
        string urlPath = context.Url.AbsolutePath;
        SftpQuoteCommands quotes = SftpQuoteCommands.From(context, OperatingSystem.IsWindows());
        if (context.Upload is { } upload)
        {
            return await new SftpFileUpload(transport).UploadAsync(urlPath, SftpUploadOptions.From(context), upload, context.Progress, context.CancellationToken, quotes).ConfigureAwait(false);
        }

        return SftpRemotePath.NamesDirectory(urlPath)
            ? await new SftpDirectoryListing(transport).ListAsync(urlPath, context.ListOnly, context.NoBody, output, context.Progress, context.CancellationToken, quotes).ConfigureAwait(false)
            : await new SftpFileDownload(transport).DownloadAsync(urlPath, context.CreateFileMode, output, context.Progress, context.CancellationToken, quotes, context.Range, context.ResumeFrom).ConfigureAwait(false);
    }

    // What the host-key check and the key files need to know about the session.
    private sealed record SshSessionTarget(string Host, int Port, SshOptions Options, KnownHostsFile? KnownHosts);
}
