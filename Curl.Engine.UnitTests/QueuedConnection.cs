using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// An <see cref="IConnection" /> whose reads take the chunks the test queued (<see cref="Enqueue" />), in order,
/// keeping what does not fit for the next read; a read with nothing queued waits for the next chunk, an empty chunk
/// reports the server closing, and <see cref="FailReads" /> makes the next read fail. Writes are recorded and complete
/// at once, or fail with <see cref="WriteFailure" />.
/// </summary>
internal sealed class QueuedConnection : IConnection
{
    private readonly Queue<byte[]> chunks = new();

    private readonly MemoryStream written = new();

    private ReadOnlyMemory<byte> unread;

    private TaskCompletionSource? arrived;

    private bool failsReads;

    public bool IsSecure => false;

    public EndPoint? RemoteEndPoint => null;

    public byte[] Written => written.ToArray();

    public bool IsDisposed { get; private set; }

    /// <summary>When set, each write fails with it.</summary>
    public Exception? WriteFailure { get; set; }

    public void Enqueue(params byte[][] queued)
    {
        foreach (byte[] chunk in queued)
        {
            chunks.Enqueue(chunk);
        }

        arrived?.TrySetResult();
    }

    public void FailReads()
    {
        failsReads = true;
        arrived?.TrySetResult();
    }

    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        while (unread.IsEmpty && chunks.Count == 0 && !failsReads)
        {
            arrived = new TaskCompletionSource();
            await arrived.Task.WaitAsync(cancellationToken);
        }

        if (failsReads)
        {
            throw new IOException("read failed");
        }

        if (unread.IsEmpty)
        {
            unread = chunks.Dequeue();
            if (unread.IsEmpty)
            {
                return 0;
            }
        }

        int count = Math.Min(unread.Length, buffer.Length);
        unread[..count].CopyTo(buffer);
        unread = unread[count..];
        return count;
    }

    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        if (WriteFailure is { } failure)
        {
            throw failure;
        }

        written.Write(buffer.Span);
        return ValueTask.CompletedTask;
    }

    public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        return ValueTask.CompletedTask;
    }
}
