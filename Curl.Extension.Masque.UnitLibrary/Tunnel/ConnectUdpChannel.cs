using System.Net;
using Curl.Extension.Masque.Wire;
using Curl.HttpDatagram;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Masque.Tunnel;

/// <summary>
/// A CONNECT-UDP tunnel as a datagram channel: each UDP payload travels as one DATAGRAM
/// capsule with Context ID 0 (RFC 9298 section 5). The tunnel reaches one target, so every
/// send goes to it whatever destination it names, and every receive comes from it.
/// </summary>
/// <param name="stream">The tunnel's stream, past the upgrade.</param>
/// <param name="serverEndPoint">The UDP target the tunnel was opened to.</param>
internal sealed class ConnectUdpChannel(MasqueTunnelStream stream, EndPoint serverEndPoint) : IDatagramChannel
{
    /// <inheritdoc />
    public EndPoint ServerEndPoint => serverEndPoint;

    /// <inheritdoc />
    public ValueTask SendAsync(ReadOnlyMemory<byte> datagram, EndPoint destination, CancellationToken cancellationToken) =>
        stream.WriteCapsuleAsync(new Capsule(CapsuleType.Datagram, MasqueDatagramPayload.Wrap(datagram.Span)), cancellationToken);

    /// <inheritdoc />
    /// <remarks>A payload longer than <paramref name="buffer" /> is cut to fit it.</remarks>
    public async ValueTask<DatagramReceived> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var content = await stream.ReadDatagramContentAsync(cancellationToken).ConfigureAwait(false);
        var length = Math.Min(content.Length, buffer.Length);
        content[..length].CopyTo(buffer);
        return new DatagramReceived(length, serverEndPoint);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => stream.DisposeAsync();
}
