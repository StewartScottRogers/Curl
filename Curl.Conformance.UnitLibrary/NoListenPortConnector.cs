using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// An <see cref="IConnector"/> on which <see cref="NoListenPort"/>, upstream's
/// <c>%NOLISTENPORT</c>, and <see cref="PortOne"/> refuse every connection, as a loopback port
/// nothing listens on does, and which hands every other connection to the server it wraps. No
/// socket is opened.
/// </summary>
/// <remarks>
/// <c>runtests.pl</c> at <c>curl-8_21_0</c> gives <c>%NOLISTENPORT</c> the value 47, a port it
/// expects nothing to listen on, and test1233 names port 1 in its <c>229</c> reply "assuming there
/// is nothing listening on port 1". A connection to either here ends as a refused TCP connect does:
/// exit 7, marked refused, with curl's message for it.
/// </remarks>
/// <param name="backend">The server every connection not to <see cref="NoListenPort"/> or <see cref="PortOne"/> reaches.</param>
public sealed class NoListenPortConnector(IConnector backend) : IConnector
{
    /// <summary>The port nothing listens on, <c>%NOLISTENPORT</c>.</summary>
    public const int NoListenPort = 47;

    /// <summary>Port 1, which upstream's cases name as a port nothing listens on (test1233's <c>EPSV</c> reply).</summary>
    public const int PortOne = 1;

    /// <summary>Refuses a connection to <see cref="NoListenPort"/> or <see cref="PortOne"/>; opens any other on the wrapped server.</summary>
    /// <param name="target">The host and port to connect to.</param>
    /// <param name="cancellationToken">Passed to the wrapped server.</param>
    /// <returns>The refused result, or the wrapped server's result.</returns>
    public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
        target.Port is NoListenPort or PortOne
            ? ValueTask.FromResult(ConnectResult.Refused($"Failed to connect to {target.Host}:{target.Port} after 0 ms: Could not connect to server"))
            : backend.ConnectAsync(target, cancellationToken);
}
