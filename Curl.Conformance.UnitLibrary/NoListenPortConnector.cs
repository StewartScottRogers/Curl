using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// An <see cref="IConnector"/> on which <see cref="NoListenPort"/>, upstream's
/// <c>%NOLISTENPORT</c>, refuses every connection, as a loopback port nothing listens on does,
/// and which hands every other connection to the server it wraps. No socket is opened.
/// </summary>
/// <remarks>
/// <c>runtests.pl</c> at <c>curl-8_21_0</c> gives <c>%NOLISTENPORT</c> the value 47, a port it
/// expects nothing to listen on. A connection to it here ends as a refused TCP connect does:
/// exit 7, marked refused, with curl's message for it.
/// </remarks>
/// <param name="backend">The server every connection not to <see cref="NoListenPort"/> reaches.</param>
public sealed class NoListenPortConnector(IConnector backend) : IConnector
{
    /// <summary>The port nothing listens on, <c>%NOLISTENPORT</c>.</summary>
    public const int NoListenPort = 47;

    /// <summary>Refuses a connection to <see cref="NoListenPort"/>; opens any other on the wrapped server.</summary>
    /// <param name="target">The host and port to connect to.</param>
    /// <param name="cancellationToken">Passed to the wrapped server.</param>
    /// <returns>The refused result, or the wrapped server's result.</returns>
    public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
        target.Port == NoListenPort
            ? ValueTask.FromResult(ConnectResult.Refused($"Failed to connect to {target.Host}:{target.Port} after 0 ms: Could not connect to server"))
            : backend.ConnectAsync(target, cancellationToken);
}
