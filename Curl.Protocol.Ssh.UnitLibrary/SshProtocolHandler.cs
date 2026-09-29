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
/// key, authenticates the user, downloads the URL's file with <see cref="SftpFileDownload" />
/// or <see cref="ScpFileDownload" /> into <see cref="ITransferContext.Output" />, and ends the
/// session with <c>SSH_MSG_DISCONNECT</c>.
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

    /// <summary>
    /// Initializes a new instance of the <see cref="SshProtocolHandler" /> class that draws
    /// its random bytes and ephemeral keys from the system and reads <c>HOME</c> from the
    /// process's environment.
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
        : this(connector, fileSystem, preferences, credentialEncoding, new SystemSshRandomSource(), new SystemSshEphemeralKeySource(), Environment.GetEnvironmentVariable)
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
    /// <exception cref="ArgumentNullException">An argument is <see langword="null" />.</exception>
    internal SshProtocolHandler(
        IConnector connector,
        IFileSystem fileSystem,
        SshAlgorithmPreferences preferences,
        Encoding credentialEncoding,
        ISshRandomSource randomSource,
        ISshEphemeralKeySource ephemeralKeySource,
        Func<string, string?> readEnvironmentVariable)
    {
        this.connector = connector ?? throw new ArgumentNullException(nameof(connector));
        this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        this.preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
        this.credentialEncoding = credentialEncoding ?? throw new ArgumentNullException(nameof(credentialEncoding));
        this.randomSource = randomSource;
        this.ephemeralKeySource = ephemeralKeySource;
        this.readEnvironmentVariable = readEnvironmentVariable;
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
        try
        {
            SshAlgorithmPreferences offered = SshHostKeyChecker.NarrowHostKeys(preferences.WithCompression(options.Compression), target.Host, port, options, knownHosts);
            return await ConnectAndTransferAsync(context, target, offered).ConfigureAwait(false);
        }
        catch (SshTransferException exception)
        {
            return TransferResult.Failure(exception.ExitCode, exception.Message);
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

    private async ValueTask<TransferResult> ConnectAndTransferAsync(ITransferContext context, SshSessionTarget target, SshAlgorithmPreferences offered)
    {
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
            return await RunSessionAsync(context, target, new SshTransport(connection, offered, SshAlgorithmCatalogue.Implemented, randomSource, ephemeralKeySource)).ConfigureAwait(false);
        }
    }

    // The handshake, then the rest of the session, which always ends with DISCONNECT.
    private async ValueTask<TransferResult> RunSessionAsync(ITransferContext context, SshSessionTarget target, SshTransport transport)
    {
        SshKeyExchangeResult keys = await transport.ExchangeKeysAsync(
            await transport.NegotiateAlgorithmsAsync(context.CancellationToken).ConfigureAwait(false),
            context.CancellationToken).ConfigureAwait(false);
        TransferResult result;
        try
        {
            result = await AuthenticateAndDownloadAsync(context, target, transport, keys.HostKey).ConfigureAwait(false);
        }
        catch (SshTransferException exception)
        {
            result = TransferResult.Failure(exception.ExitCode, exception.Message);
        }

        await DisconnectIgnoringFailureAsync(transport, context.CancellationToken).ConfigureAwait(false);
        return result;
    }

    private async ValueTask<TransferResult> AuthenticateAndDownloadAsync(ITransferContext context, SshSessionTarget target, SshTransport transport, byte[] hostKey)
    {
        SshUserKeySource userKeys = new(fileSystem, readEnvironmentVariable, target.Options, credentialEncoding);
        SshUserAuthentication authentication = new(transport, credentialEncoding, userKeys);
        await authentication.RequestServiceAsync(context.CancellationToken).ConfigureAwait(false);
        SshHostKeyChecker.Check(hostKey, target.Host, target.Port, target.Options, target.KnownHosts);
        await authentication.AuthenticateAsync(context.Credentials, context.CancellationToken).ConfigureAwait(false);
        return context.Url.Scheme == ScpScheme
            ? await new ScpFileDownload(transport).DownloadAsync(context.Url.AbsolutePath, context.Output, context.Progress, context.CancellationToken).ConfigureAwait(false)
            : await new SftpFileDownload(transport).DownloadAsync(context.Url.AbsolutePath, context.CreateFileMode, context.Output, context.Progress, context.CancellationToken).ConfigureAwait(false);
    }

    // What the host-key check and the key files need to know about the session.
    private sealed record SshSessionTarget(string Host, int Port, SshOptions Options, KnownHostsFile? KnownHosts);
}
