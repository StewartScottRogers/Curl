using Curl.Protocol.Abstractions;

namespace Curl.Extension.Masque.Tunnel;

/// <summary>
/// A connector that forwards to the one bound to it later: curl's, which <see cref="MasqueConnectionHook" />
/// receives only after the engine has taken its datagram connector.
/// </summary>
internal sealed class LateBoundConnector : IConnector
{
    private volatile IConnector? _bound;

    /// <summary>Binds the connector every later connect goes through.</summary>
    /// <param name="connector">curl's connector.</param>
    public void Bind(IConnector connector) => _bound = connector;

    /// <summary>Connects through the bound connector, or fails with exit 7 when none is bound yet.</summary>
    /// <param name="target">The proxy to connect to.</param>
    /// <param name="cancellationToken">Cancels the connect.</param>
    /// <returns>The bound connector's result.</returns>
    public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
        _bound is { } bound
            ? bound.ConnectAsync(target, cancellationToken)
            : ValueTask.FromResult(ConnectResult.Failed(CurlExitCode.CouldntConnect, "MASQUE proxy is not reachable before curl's connector is set"));
}
