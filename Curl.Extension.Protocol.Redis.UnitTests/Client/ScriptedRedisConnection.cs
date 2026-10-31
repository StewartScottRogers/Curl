using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Protocol.Redis.Client;

/// <summary>A fake <see cref="IConnection"/> that hands out recorded server bytes chunk by chunk and records every write.</summary>
internal sealed class ScriptedRedisConnection(params byte[][] chunks) : IConnection
{
    private readonly Queue<byte[]> reads = new(chunks);

    private int chunkOffset;

    public bool IsSecure => false;

    public EndPoint? RemoteEndPoint => null;

    public bool FailsReads { get; init; }

    public bool FailsWrites { get; init; }

    public List<byte[]> Writes { get; } = [];

    public bool IsDisposed { get; private set; }

    /// <summary>Gives the written frames as text, one string per write.</summary>
    public IEnumerable<string> WrittenText => Writes.Select(write => Encoding.UTF8.GetString(write));

    public static ScriptedRedisConnection Replaying(params string[] chunks) =>
        new([.. chunks.Select(chunk => Encoding.UTF8.GetBytes(chunk))]);

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
