using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Tftp.Fakes;

/// <summary>
/// An <see cref="IDatagramChannel" /> that replays scripted datagrams and then falls
/// silent: a receive with nothing left to hand out moves <paramref name="clock" /> to its
/// next timer, as the real wait would, until that cancels the receive. Every datagram
/// sent is recorded with where it went and when.
/// </summary>
/// <param name="serverEndPoint">The endpoint reported as <see cref="ServerEndPoint" />.</param>
/// <param name="clock">The clock the transfer under test waits on.</param>
/// <param name="script">The datagrams to hand out, in order, one per receive.</param>
/// <remarks>
/// A silent receive that no timer will ever cancel throws
/// <see cref="InvalidOperationException" />, so a handler that would wait forever fails
/// the test instead of hanging.
/// </remarks>
public sealed class FallsSilentDatagramChannel(
    EndPoint serverEndPoint,
    ManualTimeProvider clock,
    params (byte[] Datagram, EndPoint Source)[] script) : IDatagramChannel
{
    private readonly Queue<(byte[] Datagram, EndPoint Source)> pending = new(script);

    /// <inheritdoc />
    public EndPoint ServerEndPoint { get; } = serverEndPoint;

    /// <summary>
    /// Gets every datagram sent, with its destination and the clock's time, in order.
    /// </summary>
    public List<(byte[] Datagram, EndPoint Destination, TimeSpan At)> Sent { get; } = [];

    /// <inheritdoc />
    public ValueTask SendAsync(ReadOnlyMemory<byte> datagram, EndPoint destination, CancellationToken cancellationToken)
    {
        Sent.Add((datagram.ToArray(), destination, clock.Now));
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<DatagramReceived> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (pending.TryDequeue(out var next))
        {
            next.Datagram.CopyTo(buffer);
            return ValueTask.FromResult(new DatagramReceived(next.Datagram.Length, next.Source));
        }

        while (clock.AdvanceToNextTimer())
        {
            cancellationToken.ThrowIfCancellationRequested();
        }

        throw new InvalidOperationException("The handler waited on a silent channel with no timer to end the wait.");
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
