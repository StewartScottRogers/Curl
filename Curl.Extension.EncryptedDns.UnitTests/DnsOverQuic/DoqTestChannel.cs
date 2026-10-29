using System.Net;
using System.Threading.Channels;
using Curl.Protocol.Abstractions;
using Curl.Quic;

namespace Curl.Extension.EncryptedDns.DnsOverQuic;

/// <summary>
/// A datagram channel wired to an in-memory <see cref="QuicTestServer" /> that plays a DoQ server: every client
/// bidirectional stream the client ends with FIN is handed to <see cref="Answer" />, and the frames it returns go
/// back to the client in one 1-RTT packet. Keeps what the client sent on each stream and the CONNECTION_CLOSE it ended with.
/// </summary>
internal sealed class DoqTestChannel(QuicTestServer server) : IDatagramChannel
{
    private readonly Channel<byte[]> inbound = Channel.CreateUnbounded<byte[]>();
    private readonly Lock gate = new();
    private int framesSeen;

    /// <summary>Gets the frames the server answers a finished query stream with.</summary>
    public Func<ulong, byte[], QuicFrame[]> Answer { get; init; } = (_, _) => [];

    /// <summary>Gets the bytes the client sent on each stream, by stream ID, in the order the streams finished.</summary>
    public List<(ulong StreamId, byte[] Query)> Queries { get; } = [];

    /// <summary>Gets the CONNECTION_CLOSE the client sent, if any.</summary>
    public QuicConnectionCloseFrame? Close { get; private set; }

    public EndPoint ServerEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, 853);

    public ValueTask SendAsync(ReadOnlyMemory<byte> datagram, EndPoint destination, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            foreach (byte[] answer in server.Receive(datagram.ToArray()))
            {
                inbound.Writer.TryWrite(answer);
            }

            AnswerFinishedStreams();
        }

        return ValueTask.CompletedTask;
    }

    public async ValueTask<DatagramReceived> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        byte[] datagram = await inbound.Reader.ReadAsync(cancellationToken);
        datagram.CopyTo(buffer);
        return new DatagramReceived(datagram.Length, ServerEndPoint);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private void AnswerFinishedStreams()
    {
        List<QuicFrame> frames = [.. server.ClientPackets.Where(packet => packet.Type == QuicPacketType.OneRtt).SelectMany(packet => packet.Frames)];
        Close ??= frames.OfType<QuicConnectionCloseFrame>().FirstOrDefault();
        foreach (QuicStreamFrame finished in frames.Skip(framesSeen).OfType<QuicStreamFrame>().Where(frame => frame.IsFin))
        {
            byte[] query = [.. frames.OfType<QuicStreamFrame>().Where(frame => frame.StreamId == finished.StreamId).OrderBy(frame => frame.Offset).DistinctBy(frame => frame.Offset).SelectMany(frame => frame.Data.ToArray())];
            Queries.Add((finished.StreamId, query));
            if (Answer(finished.StreamId, query) is { Length: > 0 } answer)
            {
                inbound.Writer.TryWrite(server.Protect(QuicPacketType.OneRtt, answer));
            }
        }

        framesSeen = frames.Count;
    }
}
