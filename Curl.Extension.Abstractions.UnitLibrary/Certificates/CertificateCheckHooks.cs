using System.Runtime.CompilerServices;

namespace Curl.Extension.Abstractions.Certificates;

/// <summary>
/// The certificate check hooks of the extensions one option group activated (ADR-1016). A host builds them
/// from the group's <see cref="ExtensionOptionValues" /> and passes <see cref="ConnectorOver" /> and
/// <see cref="TlsProviderOver" /> to the engine. Every activated check runs, in the order of the activators,
/// on each chain curl accepted; the first refusal fails the connect with exit 60. A chain curl refused is
/// returned untouched, so no check can accept it. With no extension option given it holds no hook and gives
/// curl's connector and TLS provider back unchanged.
/// </summary>
public sealed class CertificateCheckHooks
{
    private CertificateCheckHooks(IReadOnlyList<ICertificateCheckHook> hooks) => Hooks = hooks;

    /// <summary>Gets the hooks of an option group that activated no extension.</summary>
    public static CertificateCheckHooks None { get; } = new([]);

    /// <summary>Gets the activated hooks, in the order their checks run.</summary>
    public IReadOnlyList<ICertificateCheckHook> Hooks { get; }

    /// <summary>Gives the hooks the extensions' activators return for <paramref name="values" />.</summary>
    /// <param name="values">The extension options the option group gave.</param>
    /// <param name="activators">One per extension: its hook when its option was given, otherwise <see langword="null" />.</param>
    /// <returns>The activated hooks, or <see cref="None" />'s empty list.</returns>
    public static CertificateCheckHooks ActivatedBy(ExtensionOptionValues values, IEnumerable<Func<ExtensionOptionValues, ICertificateCheckHook?>> activators) =>
        new([.. activators.Select(activator => activator(values)).OfType<ICertificateCheckHook>()]);

    /// <summary>Gives the connector whose TLS connections the hooks check.</summary>
    /// <param name="curlConnector">The connector curl would use.</param>
    /// <returns><paramref name="curlConnector" /> itself without a hook, otherwise a connector that checks its chains.</returns>
    public IConnector ConnectorOver(IConnector curlConnector) =>
        Hooks.Count == 0 ? curlConnector : new CertificateCheckingConnector(curlConnector, this);

    /// <summary>Gives the TLS provider whose in-place upgrades the hooks check.</summary>
    /// <param name="curlTlsProvider">The TLS provider curl would use.</param>
    /// <returns><paramref name="curlTlsProvider" /> itself without a hook, otherwise a provider that checks its chains.</returns>
    public ITlsProvider TlsProviderOver(ITlsProvider curlTlsProvider) =>
        Hooks.Count == 0 ? curlTlsProvider : new CertificateCheckingTlsProvider(curlTlsProvider, this);

    /// <summary>
    /// Keeps the port of each connection the checking connector opened, so an in-place upgrade of it is
    /// checked with that port (ADR-1141). Weak, so a connection's entry goes with the connection.
    /// </summary>
    private readonly ConditionalWeakTable<IConnection, ConnectTarget> _targetsOfConnections = new();

    /// <summary>Remembers the port of a connect the checking connector made, then runs every check on its chain.</summary>
    /// <param name="result">The connect's result.</param>
    /// <param name="target">The host and port the connect was made to.</param>
    /// <param name="cancellationToken">Cancels the checks.</param>
    /// <returns>What <see cref="CheckedAsync" /> returns for <paramref name="result" />.</returns>
    internal ValueTask<ConnectResult> CheckedConnectAsync(ConnectResult result, ConnectTarget target, CancellationToken cancellationToken)
    {
        if (result.Connection is { } connection)
        {
            _targetsOfConnections.AddOrUpdate(connection, target);
        }

        return CheckedAsync(result, target.Host, target.Port, handshook: false, cancellationToken);
    }

    /// <summary>Gives the port the checking connector opened <paramref name="connection" /> to.</summary>
    /// <param name="connection">The plaintext connection an in-place upgrade secures.</param>
    /// <returns>The port, or <see langword="null" /> when the checking connector did not open it.</returns>
    internal int? PortOf(IConnection connection) =>
        _targetsOfConnections.TryGetValue(connection, out ConnectTarget? target) ? target.Port : null;

    /// <summary>Runs every check on the chain of a connect curl accepted.</summary>
    /// <param name="result">The connect's result.</param>
    /// <param name="host">The host name the chain was verified against.</param>
    /// <param name="port">The port the connection was made to, or <see langword="null" /> when it is unknown.</param>
    /// <param name="handshook">Whether the connect was a TLS handshake even when it reported no certificate.</param>
    /// <param name="cancellationToken">Cancels the checks.</param>
    /// <returns><paramref name="result" /> when it failed, was reused, had no TLS or every check let it stand; otherwise exit 60 with the refusal, after its connection is disposed.</returns>
    internal async ValueTask<ConnectResult> CheckedAsync(ConnectResult result, string host, int? port, bool handshook, CancellationToken cancellationToken)
    {
        if (result.Connection is not { } connection || result.IsReused || (!handshook && result.PeerCertificates.Count == 0))
        {
            return result;
        }

        foreach (ICertificateCheckHook hook in Hooks)
        {
            if (await hook.RefusalOfAsync(result.PeerCertificates, host, port, cancellationToken).ConfigureAwait(false) is { } refusal)
            {
                await connection.DisposeAsync().ConfigureAwait(false);
                return ConnectResult.Failed(CurlExitCode.PeerFailedVerification, refusal);
            }
        }

        return result;
    }
}
