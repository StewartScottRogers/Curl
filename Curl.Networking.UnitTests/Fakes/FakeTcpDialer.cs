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
    /// Gets or sets what one bound dial does before it connects: nothing by default, or throw, as a
    /// bind that fails does.
    /// </summary>
    public Action<IPEndPoint, int> BindOutcome { get; init; } = (_, _) => { };

    /// <summary>Gets each bound dial, in order: where it went, the local end point asked for and the port count.</summary>
    public List<(IPEndPoint EndPoint, IPEndPoint LocalEndPoint, int LocalPortCount)> BoundDials { get; } = [];

    /// <inheritdoc />
    public ValueTask<DialedTcpConnection> DialFromAsync(IPEndPoint endPoint, IPEndPoint localEndPoint, int localPortCount, CancellationToken cancellationToken)
    {
        BoundDials.Add((endPoint, localEndPoint, localPortCount));
        BindOutcome(localEndPoint, localPortCount);

        return DialAsync(endPoint, cancellationToken);
    }

    /// <summary>Gets the events each bound dial given them was given, in order.</summary>
    public List<ITransferEvents> BoundDialEvents { get; } = [];

    /// <inheritdoc />
    public ValueTask<DialedTcpConnection> DialFromAsync(IPEndPoint endPoint, IPEndPoint localEndPoint, int localPortCount, ITransferEvents events, CancellationToken cancellationToken)
    {
        BoundDialEvents.Add(events);

        return DialFromAsync(endPoint, localEndPoint, localPortCount, cancellationToken);
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
