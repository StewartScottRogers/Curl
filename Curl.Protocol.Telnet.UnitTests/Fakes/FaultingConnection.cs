using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Telnet.Fakes;

/// <summary>
/// An <see cref="IConnection" /> that a reset breaks: each of <paramref name="reads" /> is
/// returned by one read, in order, and then the next read either fails with
/// <see cref="IOException" /> (<see cref="ReadFailsAfterReads" />) or never completes,
/// as a server that stays silent. With <see cref="WritesFail" /> every write fails with
/// <see cref="IOException" />, with no socket error inside.
/// </summary>
/// <param name="reads">What the server sends before the connection breaks.</param>
public sealed class FaultingConnection(params byte[][] reads) : IConnection
{
    private readonly TaskCompletionSource<int> never = new();

    private int nextRead;

    /// <summary>Gets a value indicating whether the read after the scripted ones fails.</summary>
    public bool ReadFailsAfterReads { get; init; }

    /// <summary>Gets a value indicating whether every write fails.</summary>
    public bool WritesFail { get; init; }

    /// <inheritdoc />
    public bool IsSecure => false;

    /// <inheritdoc />
    public EndPoint? RemoteEndPoint => null;

    /// <summary>Gets a value indicating whether the connection has been disposed.</summary>
    public bool IsDisposed { get; private set; }

    /// <inheritdoc />
    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (nextRead < reads.Length)
        {
            byte[] read = reads[nextRead++];
            read.CopyTo(buffer);
            return ValueTask.FromResult(read.Length);
        }

        return ReadFailsAfterReads
            ? ValueTask.FromException<int>(new IOException("Connection reset by peer."))
            : new ValueTask<int>(never.Task);
    }

    /// <inheritdoc />
    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) =>
        WritesFail
            ? ValueTask.FromException(new IOException("Connection reset by peer."))
            : ValueTask.CompletedTask;

    /// <inheritdoc />
    public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        return ValueTask.CompletedTask;
    }
}
