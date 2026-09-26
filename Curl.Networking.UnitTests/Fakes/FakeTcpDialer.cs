using System.Net;
using System.Net.Sockets;

using Curl.Protocol.Abstractions;

namespace Curl.Networking.Fakes;

/// <summary>
/// An <see cref="ITcpDialer" /> that records every end point it is asked for and answers
/// with <see cref="DialOutcome" />.
/// </summary>
public sealed class FakeTcpDialer : ITcpDialer
{
    /// <summary>
    /// Gets or sets what one dial does: return a connection, or throw. Defaults to refusing.
    /// </summary>
    public Func<IPEndPoint, IConnection> DialOutcome { get; init; } =
        _ => throw new SocketException((int)SocketError.ConnectionRefused);

    /// <summary>Gets the end points dialed, in order.</summary>
    public List<IPEndPoint> DialedEndPoints { get; } = [];

    /// <inheritdoc />
    public ValueTask<IConnection> DialAsync(IPEndPoint endPoint, CancellationToken cancellationToken)
    {
        DialedEndPoints.Add(endPoint);

        return ValueTask.FromResult(DialOutcome(endPoint));
    }
}
