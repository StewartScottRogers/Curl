using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Kafka.Client;

/// <summary>A fake <see cref="IConnection"/> that hands out recorded server bytes chunk by chunk and records every write.</summary>
internal sealed class ScriptedKafkaConnection(params byte[][] chunks) : IConnection
{
    private readonly Queue<byte[]> reads = new(chunks);

    private int chunkOffset;

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

        if (!reads.TryPeek(out byte[]? chunk))
        {
            return ValueTask.FromResult(0);
        }

        int length = Math.Min(chunk.Length - chunkOffset, buffer.Length);
        chunk.AsSpan(chunkOffset, length).CopyTo(buffer.Span);
        chunkOffset += length;
        if (chunkOffset == chunk.Length)
        {
            reads.Dequeue();
            chunkOffset = 0;
        }

        return ValueTask.FromResult(length);
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
