using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ftp.Fakes;

/// <summary>
/// A connection that answers its reads from a script, one scripted chunk per read, and then
/// goes silent: the next read waits until its token is cancelled, as a server that stops
/// answering does. What is written to it is kept.
/// </summary>
/// <param name="reads">The chunks the reads return, in order, before the silence.</param>
public sealed class StallingConnection(params byte[][] reads) : IConnection
{
    private readonly Queue<byte[]> pendingReads = new(reads);

    private readonly List<byte> sent = [];

    private readonly TaskCompletionSource stalled = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Gets a task that completes once a read found the script used up.</summary>
    public Task Stalled => stalled.Task;

    public bool IsSecure => false;

    public EndPoint? RemoteEndPoint => null;

    /// <summary>Gets every byte written so far.</summary>
    public byte[] Sent => [.. sent];

    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (pendingReads.TryDequeue(out byte[]? read))
        {
            read.CopyTo(buffer);
            return read.Length;
        }

        stalled.TrySetResult();
        await Task.Delay(Timeout.Infinite, cancellationToken);
        throw new InvalidOperationException("An infinite delay ended without being cancelled.");
    }

    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        sent.AddRange(buffer.Span);
        return ValueTask.CompletedTask;
    }

    public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
