using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Amqp1.Client;

/// <summary>A fake <see cref="IConnection"/> that hands out recorded server bytes chunk by chunk and records every write.</summary>
internal sealed class ScriptedAmqp1Connection(params byte[][] chunks) : IConnection
{
    private readonly Queue<byte[]> reads = new(chunks);

    public bool IsSecure => false;

    public EndPoint? RemoteEndPoint => null;

    public bool FailsReads { get; init; }

    public bool FailsWrites { get; init; }

    public List<byte[]> Writes { get; } = [];

    public bool IsDisposed { get; private set; }

    /// <summary>The zero-based read that waits for <see cref="ReleaseHeldRead"/> before it returns, if any.</summary>
    public int? HoldsRead { get; init; }

    private readonly TaskCompletionSource released = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly TaskCompletionSource held = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private int readCount;

    /// <summary>Completes once the read <see cref="HoldsRead"/> names is waiting.</summary>
    public Task ReadHeld => held.Task;

    public void ReleaseHeldRead() => released.TrySetResult();

    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (readCount++ == HoldsRead)
        {
            held.TrySetResult();
            await released.Task.ConfigureAwait(false);
        }

        return await ReadNextChunkAsync(buffer).ConfigureAwait(false);
    }

    private ValueTask<int> ReadNextChunkAsync(Memory<byte> buffer)
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
