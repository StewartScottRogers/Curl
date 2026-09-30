using System.Net;
using System.Threading.Channels;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http.Fakes;

/// <summary>
/// An <see cref="IConnection" /> whose reads wait for the bytes a test pushes, one pushed chunk
/// per read, so a test decides when the server's bytes arrive while several HTTP/2 streams wait
/// for them (BL-717). <see cref="Close" /> ends the reads; <see cref="Fail" /> makes the next
/// read throw.
/// </summary>
public sealed class PushedBytesConnection : IConnection
{
    private readonly Channel<Func<byte[]>> chunks = Channel.CreateUnbounded<Func<byte[]>>();

    private readonly MemoryStream written = new();

    private readonly Lock gate = new();

    private readonly List<(int Count, TaskCompletionSource Reached)> readWaiters = [];

    /// <inheritdoc />
    public bool IsSecure => false;

    /// <inheritdoc />
    public EndPoint? RemoteEndPoint => null;

    /// <summary>Gets every byte written so far.</summary>
    public byte[] Written
    {
        get
        {
            lock (gate)
            {
                return written.ToArray();
            }
        }
    }

    /// <summary>Gets how many reads have been asked for.</summary>
    public int ReadCount { get; private set; }

    /// <summary>Waits until <paramref name="count" /> reads have been asked for.</summary>
    /// <param name="count">The number of reads.</param>
    /// <returns>A task that completes when they have.</returns>
    public Task ReadsAskedAsync(int count)
    {
        lock (gate)
        {
            TaskCompletionSource reached = new(TaskCreationOptions.RunContinuationsAsynchronously);
            readWaiters.Add((count, reached));
            SignalReads();
            return reached.Task;
        }
    }

    /// <summary>Makes <paramref name="bytes" /> the answer to the next read.</summary>
    /// <param name="bytes">The bytes.</param>
    public void Push(byte[] bytes) => chunks.Writer.TryWrite(() => bytes);

    /// <summary>Makes the next read return zero, the server's close.</summary>
    public void Close() => chunks.Writer.TryWrite(() => []);

    /// <summary>Makes the next read throw <paramref name="exception" />.</summary>
    /// <param name="exception">The exception.</param>
    public void Fail(Exception exception) => chunks.Writer.TryWrite(() => throw exception);

    /// <inheritdoc />
    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            ReadCount++;
            SignalReads();
        }

        byte[] chunk = (await chunks.Reader.ReadAsync(cancellationToken))();
        chunk.CopyTo(buffer);
        return chunk.Length;
    }

    /// <inheritdoc />
    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            written.Write(buffer.Span);
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private void SignalReads()
    {
        foreach ((int count, TaskCompletionSource reached) in readWaiters.Where(waiter => waiter.Count <= ReadCount))
        {
            reached.TrySetResult();
        }
    }
}
