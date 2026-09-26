using System.Net;
using System.Net.Sockets;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// The production <see cref="IConnector" /> for TCP: resolves the host, dials each
/// address in the resolver's order until one connects, and hands the connection to
/// <see cref="ITlsProvider" /> when the target asks for TLS.
/// </summary>
/// <param name="dnsResolver">Resolves the target host to addresses.</param>
/// <param name="tcpDialer">Opens a plaintext connection to one address.</param>
/// <param name="tlsProvider">Upgrades the connection when <see cref="ConnectTarget.UseTls" /> is set.</param>
/// <param name="timeProvider">Measures the connect time curl reports in its exit 7 message.</param>
public sealed class TcpConnector(
    IDnsResolver dnsResolver,
    ITcpDialer tcpDialer,
    ITlsProvider tlsProvider,
    TimeProvider timeProvider) : IConnector
{
    /// <inheritdoc />
    /// <remarks>
    /// The messages are curl 8.21.0's: <c>Could not resolve host: &lt;host&gt;</c> for
    /// exit 6, and <c>Failed to connect to &lt;host&gt;:&lt;port&gt; after &lt;n&gt; ms:
    /// Could not connect to server</c> for exit 7, where <c>n</c> is the time spent
    /// dialing as measured by the injected <see cref="TimeProvider" />. When
    /// <see cref="ConnectTarget.UseTls" /> is set, the <see cref="ITlsProvider" />'s result
    /// is returned as it is, so a failed handshake keeps the exit code and message the
    /// provider chose.
    /// </remarks>
    public async ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);

        var addresses = await dnsResolver.ResolveAsync(target.Host, cancellationToken).ConfigureAwait(false);
        if (addresses.Count == 0)
        {
            return ConnectResult.Failed(
                CurlExitCode.CouldntResolveHost,
                $"Could not resolve host: {target.Host}");
        }

        var dialStarted = timeProvider.GetTimestamp();
        var connection = await DialFirstReachableAsync(addresses, target.Port, cancellationToken).ConfigureAwait(false);
        if (connection is null)
        {
            var elapsedMilliseconds = (long)timeProvider.GetElapsedTime(dialStarted).TotalMilliseconds;
            return ConnectResult.Failed(
                CurlExitCode.CouldntConnect,
                $"Failed to connect to {target.Host}:{target.Port} after {elapsedMilliseconds} ms: Could not connect to server");
        }

        if (!target.UseTls)
        {
            return ConnectResult.Connected(connection);
        }

        return await tlsProvider.AuthenticateAsClientAsync(connection, target.Host, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<IConnection?> DialFirstReachableAsync(
        IReadOnlyList<IPAddress> addresses,
        int port,
        CancellationToken cancellationToken)
    {
        foreach (var address in addresses)
        {
            try
            {
                return await tcpDialer.DialAsync(new IPEndPoint(address, port), cancellationToken).ConfigureAwait(false);
            }
            catch (SocketException)
            {
                // curl moves on to the next address; only when every one fails is it exit 7.
            }
        }

        return null;
    }
}
