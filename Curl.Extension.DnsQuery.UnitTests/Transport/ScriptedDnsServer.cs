using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.DnsQuery.UnitTests.Transport;

/// <summary>
/// A fake DNS server behind both of curl's connectors: over UDP it answers the n-th query datagram with the datagrams
/// the script gives for it, and over TCP it records the octets written and replays the given octets in reads of at most
/// <see cref="TcpReadSize" />. A connector built with a failure gives that failure instead.
/// </summary>
/// <param name="udpRepliesAfterSend">The datagrams queued after each UDP send, by send number from 1.</param>
/// <param name="tcpReplies">The octets the TCP connection's reads give, then end of stream.</param>
internal sealed class ScriptedDnsServer(Func<int, byte[], IEnumerable<byte[]>> udpRepliesAfterSend, byte[] tcpReplies) : IDatagramConnector, IConnector
{
    private Func<int, byte[], IEnumerable<byte[]>> UdpRepliesAfterSend => udpRepliesAfterSend;

    private byte[] TcpReplies => tcpReplies;

    /// <summary>Gets or sets what every datagram open gives in place of a channel, when not <see langword="null" />.</summary>
    public DatagramOpenResult? DatagramFailure { get; set; }

    /// <summary>Gets or sets what every TCP connect gives in place of a connection, when not <see langword="null" />.</summary>
    public ConnectResult? ConnectFailure { get; set; }

    /// <summary>Gets or sets the most octets one TCP read gives.</summary>
    public int TcpReadSize { get; set; } = int.MaxValue;

    /// <summary>Gets each UDP datagram sent.</summary>
    public List<byte[]> Datagrams { get; } = [];

    /// <summary>Gets the octets written over TCP.</summary>
    public List<byte> TcpWritten { get; } = [];

    /// <summary>Gets the host and port of each UDP open and TCP connect, in order, with the transport's name.</summary>
    public List<(string Transport, string Host, int Port)> Opened { get; } = [];

    /// <summary>Gets how many of the UDP channels and TCP connections opened were disposed.</summary>
    public int Disposed { get; private set; }

    /// <inheritdoc />
    public ValueTask<DatagramOpenResult> OpenAsync(string host, int port, CancellationToken cancellationToken)
    {
        Opened.Add(("udp", host, port));
        return ValueTask.FromResult(DatagramFailure ?? DatagramOpenResult.Opened(new Channel(this)));
    }

    /// <inheritdoc />
    public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        Opened.Add(("tcp", target.Host, target.Port));
        return ValueTask.FromResult(ConnectFailure ?? ConnectResult.Connected(new Connection(this)));
    }

    private sealed class Channel(ScriptedDnsServer server) : IDatagramChannel
    {
        private readonly Queue<byte[]> replies = new();

        public EndPoint ServerEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, 53);

        public ValueTask SendAsync(ReadOnlyMemory<byte> datagram, EndPoint destination, CancellationToken cancellationToken)
        {
            server.Datagrams.Add(datagram.ToArray());
            foreach (byte[] reply in server.UdpRepliesAfterSend(server.Datagrams.Count, datagram.ToArray()))
            {
                replies.Enqueue(reply);
            }

            return ValueTask.CompletedTask;
        }

        public async ValueTask<DatagramReceived> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            if (!replies.TryDequeue(out byte[]? reply))
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
                return default!;
            }

            reply!.CopyTo(buffer);
            return new DatagramReceived(reply.Length, ServerEndPoint);
        }

        public ValueTask DisposeAsync()
        {
            server.Disposed++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class Connection(ScriptedDnsServer server) : IConnection
    {
        private int position;

        public bool IsSecure => false;

        public EndPoint? RemoteEndPoint => null;

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            int length = Math.Min(Math.Min(buffer.Length, server.TcpReadSize), server.TcpReplies.Length - position);
            server.TcpReplies.AsMemory(position, length).CopyTo(buffer);
            position += length;
            return ValueTask.FromResult(length);
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
        {
            server.TcpWritten.AddRange(buffer.ToArray());
            return ValueTask.CompletedTask;
        }

        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync()
        {
            server.Disposed++;
            return ValueTask.CompletedTask;
        }
    }
}
