using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Mqtt5.Client;

/// <summary>A fake <see cref="IConnection"/> that hands out recorded server bytes chunk by chunk and records every write.</summary>
internal sealed class ScriptedMqtt5Connection(params byte[][] chunks) : IConnection
{
    private readonly Queue<byte[]> reads = new(chunks);

    public bool IsSecure => false;

    public EndPoint? RemoteEndPoint => null;

    public bool FailsReads { get; init; }

    public bool FailsWrites { get; init; }

    public List<byte[]> Writes { get; } = [];

    public bool IsDisposed { get; private set; }

    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (FailsReads)
        {
            throw new IOException("reset");
        }

        if (!reads.TryDequeue(out byte[]? chunk))
        {
            return ValueTask.FromResult(0);
        }

        chunk.CopyTo(buffer);
        return ValueTask.FromResult(chunk.Length);
    }

    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        if (FailsWrites)
        {
            throw new IOException("reset");
        }

        Writes.Add(buffer.ToArray());
        return ValueTask.CompletedTask;
    }

    public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        return ValueTask.CompletedTask;
    }
}
