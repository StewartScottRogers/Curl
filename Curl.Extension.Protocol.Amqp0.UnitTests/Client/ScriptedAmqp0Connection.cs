using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Protocol.Amqp0.Client;

/// <summary>A fake <see cref="IConnection"/> that hands out recorded server bytes chunk by chunk and records every write.</summary>
internal sealed class ScriptedAmqp0Connection(params byte[][] chunks) : IConnection
{
    private readonly Queue<byte[]> reads = new(chunks);

    public bool IsSecure => false;

    public EndPoint? RemoteEndPoint => null;

    public bool FailsReads { get; init; }

    /// <summary>The number of writes that succeed before every later one fails; -1 for none failing.</summary>
    public int FailsWritesAfter { get; init; } = -1;

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
        if (FailsWritesAfter >= 0 && Writes.Count >= FailsWritesAfter)
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
