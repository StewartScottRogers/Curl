using System.Threading.Channels;
using Curl.Extension.WireGuard.IpStack;

namespace Curl.Extension.WireGuard.UnitTests.IpStack;

/// <summary>An <see cref="IIpPacketChannel" /> a test feeds packets into and reads the stack's packets out of.</summary>
internal sealed class ScriptedPacketChannel : IIpPacketChannel
{
    private readonly Channel<ReadOnlyMemory<byte>> inbound = Channel.CreateUnbounded<ReadOnlyMemory<byte>>();
    private readonly Channel<byte[]> sent = Channel.CreateUnbounded<byte[]>();
    private int sends;
    private Exception? closedWith;

    /// <summary>The one-based number of the send that throws, or <see langword="null" /> when none does.</summary>
    public int? FailingSendNumber { get; set; }

    /// <inheritdoc />
    public ValueTask SendAsync(ReadOnlyMemory<byte> packet, CancellationToken cancellationToken)
    {
        if (Interlocked.Increment(ref sends) == FailingSendNumber)
        {
            throw new InvalidOperationException("The scripted send failed.");
        }

        sent.Writer.TryWrite(packet.ToArray());
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask<ReadOnlyMemory<byte>> ReceiveAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await inbound.Reader.ReadAsync(cancellationToken);
        }
        catch (ChannelClosedException)
        {
            throw closedWith!;
        }
    }

    /// <summary>How many packets the stack has tried to send.</summary>
    public int SendCount => Volatile.Read(ref sends);

    /// <summary>Queues a packet for the stack to receive.</summary>
    /// <param name="packet">The packet's bytes.</param>
    public void Deliver(byte[] packet) => inbound.Writer.TryWrite(packet);

    /// <summary>Closes the inbound side, so the stack's next receive throws <paramref name="exception" />.</summary>
    /// <param name="exception">What the receive throws.</param>
    public void Close(Exception exception)
    {
        closedWith = exception;
        inbound.Writer.Complete();
    }

    /// <summary>Waits up to ten seconds for the next packet the stack sends.</summary>
    /// <returns>The packet's bytes.</returns>
    public async Task<byte[]> NextSentAsync() => await sent.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
}
