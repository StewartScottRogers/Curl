using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Tftp.Fakes;

/// <summary>
/// A datagram channel that answers each receive from a script in which a
/// <see langword="null" /> datagram is one silence: the clock is advanced to the next timer,
/// so the handler's wait ends unanswered and it re-sends, and the script goes on.
/// </summary>
/// <param name="serverEndPoint">The endpoint the request is sent to.</param>
/// <param name="clock">The clock the handler waits on.</param>
/// <param name="script">The datagrams and silences, in order.</param>
public sealed class PausingDatagramChannel(
    EndPoint serverEndPoint,
    ManualTimeProvider clock,
    params (byte[]? Datagram, EndPoint Source)[] script) : IDatagramChannel
{
    private readonly Queue<(byte[]? Datagram, EndPoint Source)> pending = new(script);

    /// <inheritdoc />
    public EndPoint ServerEndPoint { get; } = serverEndPoint;

    /// <summary>Gets every datagram sent, in order.</summary>
    public List<byte[]> Sent { get; } = [];

    /// <inheritdoc />
    public ValueTask SendAsync(ReadOnlyMemory<byte> datagram, EndPoint destination, CancellationToken cancellationToken)
    {
        Sent.Add(datagram.ToArray());
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<DatagramReceived> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var next = pending.Dequeue();
        if (next.Datagram is null)
        {
            clock.AdvanceToNextTimer();
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException("The silence did not end the handler's wait.");
        }

        // A datagram longer than the buffer is cut to it, as a Linux or macOS socket cuts it.
        var length = Math.Min(next.Datagram.Length, buffer.Length);
        next.Datagram.AsSpan(0, length).CopyTo(buffer.Span);
        return ValueTask.FromResult(new DatagramReceived(length, next.Source));
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
