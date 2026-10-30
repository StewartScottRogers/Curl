using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Text;
using Curl.Extension.Dane.Wire;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Dane.Lookup;

/// <summary>
/// A recursive resolver that replays recorded replies, keyed by question, over a fake UDP channel and a fake TCP
/// connection, with no network: each reply is encoded with the query's ID, and a question it has no reply for gets
/// an empty NOERROR answer.
/// </summary>
internal sealed class FakeDnsServer(ManualTimeProvider clock) : IDatagramConnector, IConnector
{
    public static readonly IPEndPoint ServerEndPoint = new(IPAddress.Loopback, 53);

    private readonly Dictionary<(string Name, DnssecRecordType Type), DnsReply> replies = [];

    public ManualTimeProvider Clock => clock;

    public List<string> UdpQuestions { get; } = [];

    public List<string> TcpQuestions { get; } = [];

    /// <summary>Gets or sets datagrams sent before each UDP reply, such as one from another address.</summary>
    public List<(byte[] Datagram, EndPoint From)> StrayDatagrams { get; } = [];

    public bool IsSilentOverUdp { get; set; }

    public bool OpenFails { get; set; }

    public bool ConnectFails { get; set; }

    /// <summary>Gets or sets how many octets of a TCP reply are sent before the connection closes; null sends it all.</summary>
    public int? TcpReplyLength { get; set; }

    public void Reply(string name, DnssecRecordType type, DnsReply reply) => replies[(name.ToLowerInvariant(), type)] = reply;

    public ValueTask<DatagramOpenResult> OpenAsync(string host, int port, CancellationToken cancellationToken) =>
        ValueTask.FromResult(OpenFails
            ? DatagramOpenResult.Failed(CurlExitCode.CouldntConnect, "refused")
            : DatagramOpenResult.Opened(new FakeChannel(this)));

    public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
        ValueTask.FromResult(ConnectFails
            ? ConnectResult.Failed(CurlExitCode.CouldntConnect, "refused")
            : ConnectResult.Connected(new FakeConnection(this)));

    private byte[] Answer(byte[] query, bool overUdp)
    {
        (string name, DnssecRecordType type, int end) = ReadQuestion(query);
        (overUdp ? UdpQuestions : TcpQuestions).Add($"{name} {type}");
        DnsReply reply = replies.GetValueOrDefault((name.ToLowerInvariant(), type), DnsReply.Empty);
        return overUdp && reply.IsTruncatedOverUdp
            ? DnsMessageWriter.Write(query, end, DnsReply.Empty with { IsTruncatedOverUdp = true })
            : DnsMessageWriter.Write(query, end, reply);
    }

    private static (string Name, DnssecRecordType Type, int End) ReadQuestion(byte[] query)
    {
        StringBuilder name = new();
        int offset = 12;
        for (; query[offset] != 0; offset += query[offset] + 1)
        {
            name.Append(Encoding.ASCII.GetString(query, offset + 1, query[offset])).Append('.');
        }

        DnssecRecordType type = (DnssecRecordType)BinaryPrimitives.ReadUInt16BigEndian(query.AsSpan(offset + 1));
        return (name.Length == 0 ? "." : name.ToString(), type, offset + 5);
    }

    private sealed class FakeChannel(FakeDnsServer server) : IDatagramChannel
    {
        private readonly Queue<(byte[] Datagram, EndPoint From)> pending = [];

        public EndPoint ServerEndPoint => FakeDnsServer.ServerEndPoint;

        public ValueTask SendAsync(ReadOnlyMemory<byte> datagram, EndPoint destination, CancellationToken cancellationToken)
        {
            foreach ((byte[] Datagram, EndPoint From) stray in server.StrayDatagrams)
            {
                pending.Enqueue(stray);
            }

            byte[] query = datagram.ToArray();
            if (!server.IsSilentOverUdp)
            {
                byte[] answer = server.Answer(query, overUdp: true);
                pending.Enqueue((answer, destination));
            }

            return ValueTask.CompletedTask;
        }

        public async ValueTask<DatagramReceived> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            if (pending.TryDequeue(out (byte[] Datagram, EndPoint From) next))
            {
                next.Datagram.CopyTo(buffer);
                return new DatagramReceived(next.Datagram.Length, next.From);
            }

            Task neverAnswered = Task.Delay(Timeout.Infinite, cancellationToken);
            server.Clock.Advance(TimeSpan.FromMinutes(1));
            await neverAnswered;
            throw new UnreachableException();
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeConnection(FakeDnsServer server) : IConnection
    {
        private byte[] toRead = [];

        private int readOffset;

        public bool IsSecure => false;

        public EndPoint? RemoteEndPoint => null;

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
        {
            byte[] answer = server.Answer(buffer[2..].ToArray(), overUdp: false);
            byte[] framed = [(byte)(answer.Length >> 8), (byte)answer.Length, .. answer];
            toRead = framed[..(server.TcpReplyLength ?? framed.Length)];
            return ValueTask.CompletedTask;
        }

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            int count = Math.Min(Math.Min(buffer.Length, toRead.Length - readOffset), 100);
            toRead.AsSpan(readOffset, count).CopyTo(buffer.Span);
            readOffset += count;
            return ValueTask.FromResult(count);
        }

        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

/// <summary>One recorded reply: its RCODE, its answer and authority sections, and whether UDP gets it truncated.</summary>
internal sealed record DnsReply(int ResponseCode, IReadOnlyList<DnsResourceRecord> Answers, IReadOnlyList<DnsResourceRecord> Authority)
{
    public static readonly DnsReply Empty = new(0, [], []);

    public bool IsTruncatedOverUdp { get; init; }
}
