using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http.Fakes;

/// <summary>
/// An <see cref="IConnection" /> whose scripted response cannot be read until a given number
/// of request bytes has been written, as a server that answers only once the whole request
/// has arrived. Reads before then wait, which is what lets a test see a request wait for
/// <c>100 Continue</c>.
/// </summary>
public sealed class GatedConnection : IConnection
{
    private readonly byte[] response;

    private readonly int chunkSize;

    private readonly int releaseAfter;

    private readonly MemoryStream written = new();

    private readonly TaskCompletionSource released = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly Lock gate = new();

    private readonly List<(int Length, TaskCompletionSource Reached)> writeWaiters = [];

    private int responseOffset;

    /// <summary>
    /// Initializes a new instance of the <see cref="GatedConnection" /> class.
    /// </summary>
    /// <param name="response">The bytes the server sends.</param>
    /// <param name="chunkSize">The most bytes one read returns; 1 or more.</param>
    /// <param name="releaseAfter">
    /// How many request bytes must be written before the response can be read; 0 lets it be
    /// read at once.
    /// </param>
    public GatedConnection(byte[] response, int chunkSize, int releaseAfter)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentOutOfRangeException.ThrowIfLessThan(chunkSize, 1);

        this.response = response;
        this.chunkSize = chunkSize;
        this.releaseAfter = releaseAfter;
        if (releaseAfter == 0)
        {
            released.SetResult();
        }
    }

    /// <inheritdoc />
    public bool IsSecure => false;

    /// <inheritdoc />
    public EndPoint? RemoteEndPoint => null;

    /// <summary>
    /// Gets every byte written so far.
    /// </summary>
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

    /// <summary>
    /// Returns a task that completes once at least <paramref name="length" /> bytes have been
    /// written.
    /// </summary>
    /// <param name="length">The byte count to wait for.</param>
    /// <returns>The task.</returns>
    public Task WrittenAtLeastAsync(int length)
    {
        lock (gate)
        {
            if (written.Length >= length)
            {
                return Task.CompletedTask;
            }

            TaskCompletionSource reached = new(TaskCreationOptions.RunContinuationsAsynchronously);
            writeWaiters.Add((length, reached));
            return reached.Task;
        }
    }

    /// <inheritdoc />
    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        await released.Task.WaitAsync(cancellationToken);
        int length = Math.Min(Math.Min(chunkSize, buffer.Length), response.Length - responseOffset);
        response.AsSpan(responseOffset, length).CopyTo(buffer.Span);
        responseOffset += length;
        return length;
    }

    /// <inheritdoc />
    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            written.Write(buffer.Span);
            foreach ((int length, TaskCompletionSource reached) in writeWaiters.Where(waiter => written.Length >= waiter.Length))
            {
                reached.TrySetResult();
            }

            if (written.Length >= releaseAfter)
            {
                released.TrySetResult();
            }
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        released.TrySetResult();
        return ValueTask.CompletedTask;
    }
}
