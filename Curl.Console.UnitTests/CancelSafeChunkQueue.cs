using System.Collections.Concurrent;

namespace Curl.Console;

/// <summary>
/// A first-in, first-out queue of byte chunks for one reader, in a <see cref="ConcurrentQueue{T}" />
/// counted by a <see cref="SemaphoreSlim" />: a read that is cancelled takes no count, so the chunk
/// written as it is cancelled stays queued for the next read. <see cref="Complete" /> ends it: the
/// chunks queued before it are still read, then every read returns <see langword="null" />.
/// </summary>
/// <remarks>
/// Not a <c>System.Threading.Channels</c> channel: on .NET 10.0.12 an unbounded channel can lose an
/// item written just after a waiting read is cancelled, and a test waiting for it then hangs
/// (BL-1067, BL-1068).
/// </remarks>
internal sealed class CancelSafeChunkQueue
{
    private readonly ConcurrentQueue<byte[]?> chunks = new();

    private readonly SemaphoreSlim count = new(0);

    private readonly Lock gate = new();

    private bool isCompleted;

    /// <summary>Queues <paramref name="chunk" /> unless the queue is completed.</summary>
    /// <param name="chunk">The chunk.</param>
    /// <returns><see langword="true" /> when queued; <see langword="false" /> after <see cref="Complete" />.</returns>
    public bool TryEnqueue(byte[] chunk)
    {
        lock (gate)
        {
            if (isCompleted)
            {
                return false;
            }

            chunks.Enqueue(chunk);
            count.Release();
            return true;
        }
    }

    /// <summary>Ends the queue after the chunks already in it; later enqueues are refused.</summary>
    public void Complete()
    {
        lock (gate)
        {
            if (!isCompleted)
            {
                isCompleted = true;
                chunks.Enqueue(null);
                count.Release();
            }
        }
    }

    /// <summary>Waits for the next chunk.</summary>
    /// <param name="cancellationToken">Cancels the wait without taking a chunk.</param>
    /// <returns>The chunk, or <see langword="null" /> once the queue is completed and empty.</returns>
    public async ValueTask<byte[]?> DequeueAsync(CancellationToken cancellationToken)
    {
        await count.WaitAsync(cancellationToken).ConfigureAwait(false);
        chunks.TryDequeue(out byte[]? chunk);
        if (chunk is null)
        {
            // The end marker stays, so every later read sees the end too.
            chunks.Enqueue(null);
            count.Release();
        }

        return chunk;
    }
}
