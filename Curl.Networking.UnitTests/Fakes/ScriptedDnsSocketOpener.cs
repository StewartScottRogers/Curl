using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

using Curl.Protocol.Abstractions;

namespace Curl.Networking.Fakes;

/// <summary>
/// An <see cref="IDnsSocketOpener" /> in front of fake DNS servers, one <see cref="ScriptedDnsServer" />
/// per end point: it records every query sent, with its transport, server, bound local address and
/// the <see cref="TimeProvider" />'s timestamp, and answers it as the server's script says. A
/// silent server's receive waits until the resolver's timeout cancels it.
/// </summary>
/// <param name="timeProvider">Stamps each query with the time it was sent.</param>
/// <param name="servers">Each server's end point and script; an end point not listed is silent.</param>
public sealed class ScriptedDnsSocketOpener(TimeProvider timeProvider, params (IPEndPoint EndPoint, ScriptedDnsServer Server)[] servers) : IDnsSocketOpener
{
    private readonly ConcurrentQueue<SentDnsQuery> _sent = new();

    /// <summary>Gets every query sent, in the order sent.</summary>
    public IReadOnlyList<SentDnsQuery> Sent => [.. _sent];

    /// <inheritdoc />
    public IDatagramChannel OpenDatagramChannel(IPEndPoint server, IPAddress localAddress)
    {
        var script = ScriptFor(server);
        if (script.OpenFailure is { } failure)
        {
            throw failure;
        }

        return new ScriptedChannel(this, server, localAddress, script);
    }

    /// <inheritdoc />
    public ValueTask<Stream> ConnectStreamAsync(IPEndPoint server, IPAddress localAddress, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Stream>(new ScriptedTcpStream(this, server, localAddress, ScriptFor(server)));

    private ScriptedDnsServer ScriptFor(IPEndPoint server) =>
        servers.FirstOrDefault(entry => entry.EndPoint.Equals(server)).Server ?? ScriptedDnsServer.Silent;

    private void Record(string transport, IPEndPoint server, IPAddress localAddress, byte[] query) =>
        _sent.Enqueue(new SentDnsQuery(transport, server, localAddress, query, timeProvider.GetTimestamp()));

    private sealed class ScriptedChannel(ScriptedDnsSocketOpener opener, IPEndPoint server, IPAddress localAddress, ScriptedDnsServer script) : IDatagramChannel
    {
        private readonly Queue<(EndPoint Source, byte[] Datagram)> _pending = new();

        public EndPoint ServerEndPoint => server;

        public ValueTask SendAsync(ReadOnlyMemory<byte> datagram, EndPoint destination, CancellationToken cancellationToken)
        {
            var query = datagram.ToArray();
            opener.Record("udp", server, localAddress, query);
            if (script.StrayDatagram is { } stray)
            {
                _pending.Enqueue(stray(query));
            }

            if (script.AnswerOverUdp(query) is { } reply)
            {
                _pending.Enqueue((script.ReplySource ?? server, reply));
            }

            return ValueTask.CompletedTask;
        }

        public async ValueTask<DatagramReceived> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            if (script.ReceiveFailure is { } failure)
            {
                throw failure;
            }

            if (!_pending.TryDequeue(out var next))
            {
                await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
            }

            next.Datagram.CopyTo(buffer);
            return new DatagramReceived(next.Datagram.Length, next.Source);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    /// <summary>A TCP connection to a fake server: a framed query written is answered with a framed reply to read.</summary>
    private sealed class ScriptedTcpStream(ScriptedDnsSocketOpener opener, IPEndPoint server, IPAddress localAddress, ScriptedDnsServer script) : MemoryStream
    {
        private byte[] _unread = [];
        private int _readPosition;

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var query = buffer[2..].ToArray();
            opener.Record("tcp", server, localAddress, query);
            if (script.AnswerOverTcp(query) is { } reply)
            {
                _unread = new byte[2 + reply.Length];
                BinaryPrimitives.WriteUInt16BigEndian(_unread, (ushort)reply.Length);
                reply.CopyTo(_unread, 2);
            }

            return ValueTask.CompletedTask;
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var count = Math.Min(buffer.Length, _unread.Length - _readPosition);
            _unread.AsMemory(_readPosition, count).CopyTo(buffer);
            _readPosition += count;
            return ValueTask.FromResult(count);
        }
    }
}
