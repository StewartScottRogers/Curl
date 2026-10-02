using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Dict.Fakes;

/// <summary>
/// An <see cref="IConnection" /> that fails: every write throws
/// <paramref name="writeFailure" /> when one is given, and otherwise succeeds; each read
/// returns the next element of <paramref name="reads" /> and, once they are exhausted,
/// throws <paramref name="readFailure" />.
/// </summary>
/// <param name="writeFailure">What every write throws, or <see langword="null" /> for writes that succeed.</param>
/// <param name="readFailure">What the read after the scripted ones throws.</param>
/// <param name="reads">What the server sends before the read fails.</param>
public sealed class FailingConnection(IOException? writeFailure, IOException readFailure, params byte[][] reads) : IConnection
{
    private int nextRead;

    /// <inheritdoc />
    public bool IsSecure => false;

    /// <inheritdoc />
    public EndPoint? RemoteEndPoint => null;

    /// <inheritdoc />
    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (nextRead == reads.Length)
        {
            throw readFailure;
        }

        byte[] read = reads[nextRead++];
        read.CopyTo(buffer);
        return ValueTask.FromResult(read.Length);
    }

    /// <inheritdoc />
    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) =>
        writeFailure is null ? ValueTask.CompletedTask : throw writeFailure;

    /// <inheritdoc />
    public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
