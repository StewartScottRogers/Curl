using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// A fake datagram connector that opens one channel to <paramref name="server" />, which
/// receives each of <paramref name="replies" /> in turn from <paramref name="replier" />, as a
/// TFTP server answers from a port of its own, and ignores what is sent to it.
/// </summary>
/// <param name="server">The channel's server end point, where the request goes.</param>
/// <param name="replier">The end point every reply comes from.</param>
/// <param name="replies">The datagrams the channel receives, in order.</param>
internal sealed class ScriptedDatagramConnector(IPEndPoint server, IPEndPoint replier, params byte[][] replies) : IDatagramConnector
{
    /// <inheritdoc />
    public ValueTask<DatagramOpenResult> OpenAsync(string host, int port, CancellationToken cancellationToken) =>
        ValueTask.FromResult(DatagramOpenResult.Opened(new ScriptedDatagramChannel(server, replier, new Queue<byte[]>(replies))));

    private sealed class ScriptedDatagramChannel(IPEndPoint server, IPEndPoint replier, Queue<byte[]> replies) : IDatagramChannel
    {
        public EndPoint ServerEndPoint => server;

        public ValueTask SendAsync(ReadOnlyMemory<byte> datagram, EndPoint destination, CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;

        public ValueTask<DatagramReceived> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            byte[] reply = replies.Dequeue();
            reply.CopyTo(buffer);

            return ValueTask.FromResult(new DatagramReceived(reply.Length, replier));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
