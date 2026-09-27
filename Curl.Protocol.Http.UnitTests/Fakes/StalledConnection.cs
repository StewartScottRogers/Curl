using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http.Fakes;

/// <summary>
/// An <see cref="IConnection" /> whose server sends part of a response and then goes silent:
/// it accepts every write, replays the response in reads of at most a chosen size, and then
/// holds the next read open until its token is cancelled, as a stalled server does.
/// </summary>
/// <param name="response">The bytes the server sends before it stalls.</param>
/// <param name="chunkSize">The most bytes one read returns; 1 or more.</param>
public sealed class StalledConnection(byte[] response, int chunkSize) : IConnection
{
    private readonly TaskCompletionSource stalled = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private int responseOffset;

    /// <inheritdoc />
    public bool IsSecure => false;

    /// <inheritdoc />
    public EndPoint? RemoteEndPoint => null;

    /// <summary>
    /// Gets a task that completes when a read has found the response spent and started to wait.
    /// </summary>
    public Task Stalled => stalled.Task;

    /// <inheritdoc />
    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (responseOffset == response.Length)
        {
            stalled.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }

        int length = Math.Min(Math.Min(chunkSize, buffer.Length), response.Length - responseOffset);
        response.AsSpan(responseOffset, length).CopyTo(buffer.Span);
        responseOffset += length;
        return length;
    }

    /// <inheritdoc />
    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) => ValueTask.CompletedTask;

    /// <inheritdoc />
    public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
