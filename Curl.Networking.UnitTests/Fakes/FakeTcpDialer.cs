using System.Net;
using System.Net.Sockets;

using Curl.Protocol.Abstractions;

namespace Curl.Networking.Fakes;

/// <summary>
/// An <see cref="ITcpDialer" /> that records every end point it is asked for and answers
/// with <see cref="DialOutcome" />, bound locally to <see cref="LocalEndPoint" />.
/// </summary>
public sealed class FakeTcpDialer : ITcpDialer
{
    /// <summary>
    /// Gets or sets what one dial does: return a connection, or throw. Defaults to refusing.
    /// </summary>
    public Func<IPEndPoint, IConnection> DialOutcome { get; init; } =
        _ => throw new SocketException((int)SocketError.ConnectionRefused);

    /// <summary>Gets or sets the local end point every successful dial reports.</summary>
    public IPEndPoint LocalEndPoint { get; init; } = new(IPAddress.Loopback, 50000);

    /// <summary>Gets the end points dialed, in order.</summary>
    public List<IPEndPoint> DialedEndPoints { get; } = [];

    /// <inheritdoc />
    public ValueTask<DialedTcpConnection> DialAsync(IPEndPoint endPoint, CancellationToken cancellationToken)
    {
        DialedEndPoints.Add(endPoint);

        return ValueTask.FromResult(new DialedTcpConnection(DialOutcome(endPoint), LocalEndPoint));
    }

    /// <summary>
    /// Gets or sets what one Unix socket dial does: return a connection, or throw. Defaults to
    /// refusing.
    /// </summary>
    public Func<UnixSocketAddress, IConnection> UnixSocketDialOutcome { get; init; } =
        _ => throw new SocketException((int)SocketError.ConnectionRefused);

    /// <summary>Gets the Unix sockets dialed, in order.</summary>
    public List<UnixSocketAddress> DialedUnixSockets { get; } = [];

    /// <inheritdoc />
    public ValueTask<IConnection> DialUnixSocketAsync(UnixSocketAddress address, CancellationToken cancellationToken)
    {
        DialedUnixSockets.Add(address);

        return ValueTask.FromResult(UnixSocketDialOutcome(address));
    }
}
