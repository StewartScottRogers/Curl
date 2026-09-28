using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// A connector whose one connection replays scripted server reads, one per
/// <see cref="IConnection.ReadAsync" />, and then stalls: the next read signals
/// <see cref="Stalled" /> and waits until its token is cancelled, as a server that sends a few
/// bytes and goes silent without closing does. Writes are taken and dropped.
/// </summary>
/// <param name="reads">The chunks the connection reads before it stalls, in order.</param>
internal sealed class StallingConnector(params byte[][] reads) : IConnector
{
    private readonly TaskCompletionSource stalled = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Gets a task that completes when a read has found nothing more to replay.</summary>
    public Task Stalled => stalled.Task;

    /// <inheritdoc />
    public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
        ValueTask.FromResult(ConnectResult.Connected(new StallingConnection(new Queue<byte[]>(reads), stalled)));

    private sealed class StallingConnection(Queue<byte[]> pendingReads, TaskCompletionSource stalled) : IConnection
    {
        public bool IsSecure => false;

        public EndPoint? RemoteEndPoint => null;

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

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
