using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Tftp.Fakes;

/// <summary>
/// An <see cref="IDatagramChannel" /> that replays scripted datagrams, each with the
/// endpoint it claims to come from, and records every datagram sent and where to.
/// </summary>
/// <param name="serverEndPoint">The endpoint reported as <see cref="ServerEndPoint" />.</param>
/// <param name="script">The datagrams to hand out, in order, one per receive.</param>
/// <remarks>
/// A receive after the script runs out throws <see cref="InvalidOperationException" />, so
/// a handler that waits for more than the test scripted fails the test instead of hanging.
/// </remarks>
public sealed class ScriptedDatagramChannel(
    EndPoint serverEndPoint,
    params (byte[] Datagram, EndPoint Source)[] script) : IDatagramChannel
{
    private readonly Queue<(byte[] Datagram, EndPoint Source)> pending = new(script);

    /// <inheritdoc />
    public EndPoint ServerEndPoint { get; } = serverEndPoint;

    /// <summary>
    /// Gets every datagram sent, with its destination, in order.
    /// </summary>
    public List<(byte[] Datagram, EndPoint Destination)> Sent { get; } = [];

    /// <summary>
    /// Gets a value indicating whether the channel has been disposed.
    /// </summary>
    public bool IsDisposed { get; private set; }

    /// <inheritdoc />
    public ValueTask SendAsync(ReadOnlyMemory<byte> datagram, EndPoint destination, CancellationToken cancellationToken)
    {
        Sent.Add((datagram.ToArray(), destination));
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<DatagramReceived> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (!pending.TryDequeue(out var next))
        {
            throw new InvalidOperationException("The handler received more datagrams than the test scripted.");
        }

        next.Datagram.CopyTo(buffer);
        return ValueTask.FromResult(new DatagramReceived(next.Datagram.Length, next.Source));
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        return ValueTask.CompletedTask;
    }
}
