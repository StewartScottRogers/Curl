using System.Net;
using System.Threading.Channels;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance.Http3Server;

/// <summary>
/// One client's datagram channel to the HTTP/3 stand-in: each datagram the client sends goes to
/// its <see cref="Http3ServerSession" />, and the datagrams the session answers with wait, in
/// order, for the client's receives. Every answer comes from <see cref="ServerEndPoint" />. No
/// socket is opened.
/// </summary>
/// <param name="session">The session serving this client, which the channel disposes.</param>
/// <param name="serverEndPoint">The address and port the client opened the channel to.</param>
internal sealed class Http3ServerChannel(Http3ServerSession session, EndPoint serverEndPoint) : IDatagramChannel
{
    private readonly Channel<byte[]> replies = Channel.CreateUnbounded<byte[]>();

    /// <inheritdoc />
    public EndPoint ServerEndPoint { get; } = serverEndPoint;

    /// <inheritdoc />
    public async ValueTask SendAsync(ReadOnlyMemory<byte> datagram, EndPoint destination, CancellationToken cancellationToken)
    {
        foreach (byte[] reply in await session.ReceiveAsync(datagram, cancellationToken).ConfigureAwait(false))
        {
            replies.Writer.TryWrite(reply);
        }
    }

    /// <inheritdoc />
    public async ValueTask<DatagramReceived> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        byte[] datagram = await replies.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        datagram.CopyTo(buffer);
        return new DatagramReceived(datagram.Length, ServerEndPoint);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        session.Dispose();
        return ValueTask.CompletedTask;
    }
}
