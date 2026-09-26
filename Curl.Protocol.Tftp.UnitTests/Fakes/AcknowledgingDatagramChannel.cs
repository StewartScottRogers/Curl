using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Tftp.Fakes;

/// <summary>
/// An <see cref="IDatagramChannel" /> that plays a TFTP server receiving an upload: it
/// answers the write request with ACK 0 and each DATA block with its ACK, from the
/// transfer endpoint, and records every DATA block with whether it was sent only after
/// the block before it was acknowledged.
/// </summary>
/// <param name="serverEndPoint">The endpoint reported as <see cref="ServerEndPoint" />.</param>
/// <param name="transferEndPoint">The endpoint every ACK comes from.</param>
/// <remarks>
/// A receive when nothing awaits an ACK throws <see cref="InvalidOperationException" />,
/// so a handler that waits for more than the server owes it fails the test instead of
/// hanging.
/// </remarks>
public sealed class AcknowledgingDatagramChannel(EndPoint serverEndPoint, EndPoint transferEndPoint) : IDatagramChannel
{
    /// <summary>The opcode of a write request.</summary>
    private const byte WriteRequestOpcode = 2;

    /// <summary>The length of a DATA packet's opcode and block number, and of an ACK.</summary>
    private const int HeaderLength = 4;

    private readonly Queue<ushort> acknowledgementsOwed = new();
    private readonly List<byte> payload = [];
    private int lastAcknowledgedBlock = -1;

    /// <inheritdoc />
    public EndPoint ServerEndPoint { get; } = serverEndPoint;

    /// <summary>
    /// Gets the block number, payload length, and whether the previous block had been
    /// acknowledged and the block went to the transfer endpoint, for every DATA packet.
    /// </summary>
    public List<(int Block, int Length, bool SentAfterPreviousAck)> DataBlocks { get; } = [];

    /// <summary>
    /// Gets the payloads of every DATA packet sent, concatenated.
    /// </summary>
    public byte[] Payload => [.. payload];

    /// <inheritdoc />
    public ValueTask SendAsync(ReadOnlyMemory<byte> datagram, EndPoint destination, CancellationToken cancellationToken)
    {
        var bytes = datagram.Span;
        if (bytes[1] == WriteRequestOpcode)
        {
            acknowledgementsOwed.Enqueue(0);
            return ValueTask.CompletedTask;
        }

        var block = (ushort)((bytes[2] << 8) | bytes[3]);
        var sentAfterPreviousAck = lastAcknowledgedBlock == block - 1
            && acknowledgementsOwed.Count == 0
            && destination.Equals(transferEndPoint);
        DataBlocks.Add((block, bytes.Length - HeaderLength, sentAfterPreviousAck));
        payload.AddRange(bytes[HeaderLength..]);
        acknowledgementsOwed.Enqueue(block);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<DatagramReceived> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (!acknowledgementsOwed.TryDequeue(out var block))
        {
            throw new InvalidOperationException("The handler waited for an ACK when nothing awaited one.");
        }

        lastAcknowledgedBlock = block;
        byte[] acknowledgement = [0, 4, (byte)(block >> 8), (byte)block];
        acknowledgement.CopyTo(buffer);
        return ValueTask.FromResult(new DatagramReceived(HeaderLength, transferEndPoint));
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
