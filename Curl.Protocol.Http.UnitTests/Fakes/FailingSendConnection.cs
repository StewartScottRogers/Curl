using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http.Fakes;

/// <summary>
/// An <see cref="IConnection" /> that accepts a number of writes and then fails the next
/// write, or every flush, with a chosen exception, as a connection the peer reset while the
/// request was sent does. Its reads report the peer closed.
/// </summary>
/// <param name="failure">The exception the failing write or flush throws.</param>
/// <param name="writesBeforeFailure">
/// How many writes succeed before one fails; <see langword="null" /> to let every write
/// succeed and fail the flush instead.
/// </param>
public sealed class FailingSendConnection(IOException failure, int? writesBeforeFailure) : IConnection
{
    private int writes;

    /// <inheritdoc />
    public bool IsSecure => false;

    /// <inheritdoc />
    public EndPoint? RemoteEndPoint => null;

    /// <inheritdoc />
    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) => ValueTask.FromResult(0);

    /// <inheritdoc />
    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        if (writes++ == writesBeforeFailure)
        {
            throw failure;
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask FlushAsync(CancellationToken cancellationToken) =>
        writesBeforeFailure is null ? throw failure : ValueTask.CompletedTask;

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
