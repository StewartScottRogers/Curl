using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// An <see cref="IConnection" /> whose reads complete only when the test says (<see cref="Deliver" />,
/// <see cref="ClosePeer" />, <see cref="FailRead" />), and whose writes complete at once, or only when the test
/// completes <see cref="WriteGate" />, or fail with <see cref="WriteFailure" />. It records what was written and
/// whether it was flushed and disposed.
/// </summary>
internal sealed class ControlledConnection : IConnection
{
    private TaskCompletionSource<int>? pendingRead;

    private Memory<byte> pendingBuffer;

    private readonly MemoryStream written = new();

    public bool IsSecure => false;

    public EndPoint? RemoteEndPoint => null;

    public byte[] Written => written.ToArray();

    public int FlushCount { get; private set; }

    public bool IsDisposed { get; private set; }

    /// <summary>When set, each write waits for it before it completes.</summary>
    public TaskCompletionSource? WriteGate { get; set; }

    /// <summary>When set, each write fails with it.</summary>
    public Exception? WriteFailure { get; set; }

    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        pendingBuffer = buffer;
        pendingRead = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        return new ValueTask<int>(pendingRead.Task);
    }

    public void Deliver(byte[] bytes)
    {
        bytes.CopyTo(pendingBuffer);
        pendingRead!.SetResult(bytes.Length);
    }

    public void ClosePeer() => pendingRead!.SetResult(0);

    public void FailRead(Exception failure) => pendingRead!.SetException(failure);

    public async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        if (WriteFailure is { } failure)
        {
            throw failure;
        }

        if (WriteGate is { } gate)
        {
            await gate.Task;
        }

        written.Write(buffer.Span);
    }

    public ValueTask FlushAsync(CancellationToken cancellationToken)
    {
        FlushCount++;
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        return ValueTask.CompletedTask;
    }
}
