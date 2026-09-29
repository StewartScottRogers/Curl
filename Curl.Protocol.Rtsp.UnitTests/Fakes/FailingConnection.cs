using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Rtsp.Fakes;

/// <summary>
/// An <see cref="IConnection" /> whose writes, flushes or reads throw the exception given, as a
/// connection the peer reset or that broke does.
/// </summary>
/// <param name="writeFailure">Thrown by every write, or <see langword="null" /> to accept writes.</param>
/// <param name="flushFailure">Thrown by every flush, or <see langword="null" /> to accept flushes.</param>
/// <param name="readFailure">Thrown by every read, or <see langword="null" /> to read the end of the stream.</param>
public sealed class FailingConnection(IOException? writeFailure = null, IOException? flushFailure = null, IOException? readFailure = null) : IConnection
{
    /// <inheritdoc />
    public bool IsSecure => false;

    /// <inheritdoc />
    public EndPoint? RemoteEndPoint => null;

    /// <inheritdoc />
    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) =>
        readFailure is null ? ValueTask.FromResult(0) : ValueTask.FromException<int>(readFailure);

    /// <inheritdoc />
    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) =>
        writeFailure is null ? ValueTask.CompletedTask : ValueTask.FromException(writeFailure);

    /// <inheritdoc />
    public ValueTask FlushAsync(CancellationToken cancellationToken) =>
        flushFailure is null ? ValueTask.CompletedTask : ValueTask.FromException(flushFailure);

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
