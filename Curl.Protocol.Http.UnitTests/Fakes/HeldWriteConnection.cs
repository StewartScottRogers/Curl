using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http.Fakes;

/// <summary>
/// An <see cref="IConnection" /> whose response arrives and whose writes finish only when the
/// test says so, so a test can deliver the response while a write is still under way. Its
/// signals complete their waiters on the caller's thread, so everything the response or the
/// finished write lets run has run by the time <see cref="DeliverResponse" /> or
/// <see cref="FinishWrites" /> returns.
/// </summary>
/// <param name="response">The bytes the server sends, all in the first read.</param>
public sealed class HeldWriteConnection(byte[] response) : IConnection
{
    private readonly TaskCompletionSource responseDelivered = new();

    private readonly TaskCompletionSource writesFinished = new();

    private readonly MemoryStream written = new();

    private bool responseRead;

    /// <inheritdoc />
    public bool IsSecure => false;

    /// <inheritdoc />
    public EndPoint? RemoteEndPoint => null;

    /// <summary>
    /// Gets a value indicating whether a write was cancelled before it finished.
    /// </summary>
    public bool WriteCancelled { get; private set; }

    /// <summary>
    /// Gets every byte whose write finished.
    /// </summary>
    public byte[] Written => written.ToArray();

    /// <summary>
    /// Lets the response be read.
    /// </summary>
    public void DeliverResponse() => responseDelivered.SetResult();

    /// <summary>
    /// Finishes every write under way and every write after it.
    /// </summary>
    public void FinishWrites() => writesFinished.SetResult();

    /// <inheritdoc />
    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        await responseDelivered.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        if (responseRead)
        {
            return 0;
        }

        responseRead = true;
        response.CopyTo(buffer);
        return response.Length;
    }

    /// <inheritdoc />
    public async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        try
        {
            await writesFinished.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            WriteCancelled = true;
            throw;
        }

        written.Write(buffer.Span);
    }

    /// <inheritdoc />
    public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
