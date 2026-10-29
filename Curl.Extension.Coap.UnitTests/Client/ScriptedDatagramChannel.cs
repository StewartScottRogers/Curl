using System.Net;
using Curl.Extension.Coap.Wire;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Coap.Client;

/// <summary>
/// A fake channel to a CoAP server: records each datagram sent, and after the n-th send queues the
/// replies the script gives for it, which <see cref="ReceiveAsync" /> then returns in order.
/// </summary>
/// <param name="repliesAfterSend">The replies queued after each send, by send number from 1; a send with none queues nothing.</param>
internal sealed class ScriptedDatagramChannel(Func<int, CoapMessage, IEnumerable<byte[]>> repliesAfterSend) : IDatagramChannel
{
    private readonly Queue<byte[]> replies = new();

    private (TaskCompletionSource<DatagramReceived> Reply, Memory<byte> Buffer)? waiting;

    /// <summary>Gets each datagram sent, decoded.</summary>
    public List<CoapMessage> Sent { get; } = [];

    /// <summary>Gets a value indicating whether the channel was disposed.</summary>
    public bool IsDisposed { get; private set; }

    /// <inheritdoc />
    public EndPoint ServerEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, 5683);

    /// <inheritdoc />
    public ValueTask SendAsync(ReadOnlyMemory<byte> datagram, EndPoint destination, CancellationToken cancellationToken)
    {
        CoapMessage message = CoapMessageEncoding.Decode(datagram.Span);
        Sent.Add(message);
        foreach (byte[] reply in repliesAfterSend(Sent.Count, message))
        {
            replies.Enqueue(reply);
        }

        if (waiting is { } cancelled && cancelled.Reply.Task.IsCompleted)
        {
            waiting = null;
        }

        if (waiting is { } pending && replies.TryDequeue(out byte[]? next))
        {
            waiting = null;
            pending.Reply.TrySetResult(Copied(next, pending.Buffer));
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<DatagramReceived> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (replies.TryDequeue(out byte[]? reply))
        {
            return ValueTask.FromResult(Copied(reply, buffer));
        }

        var pending = new TaskCompletionSource<DatagramReceived>(TaskCreationOptions.RunContinuationsAsynchronously);
        waiting = (pending, buffer);
        cancellationToken.Register(() => pending.TrySetCanceled(cancellationToken));
        return new ValueTask<DatagramReceived>(pending.Task);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        return ValueTask.CompletedTask;
    }

    private DatagramReceived Copied(byte[] reply, Memory<byte> buffer)
    {
        reply.CopyTo(buffer);
        return new DatagramReceived(reply.Length, ServerEndPoint);
    }
}
