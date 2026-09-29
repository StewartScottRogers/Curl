using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Smb.Fakes;

/// <summary>
/// An <see cref="IConnection" /> that plays an SMB server from a script: each element of
/// <paramref name="reads" /> is returned by one read, in order, and once the script is
/// exhausted every read returns zero, which is the server closing. Every byte written is
/// recorded in <see cref="Sent" />.
/// </summary>
/// <param name="reads">What the server sends, one non-empty read at a time.</param>
public sealed class ScriptedConnection(params byte[][] reads) : IConnection
{
    private readonly List<byte> sent = [];

    private int nextRead;

    /// <inheritdoc />
    public bool IsSecure => false;

    /// <inheritdoc />
    public EndPoint? RemoteEndPoint => null;

    /// <summary>Gets a value indicating whether the connection has been disposed.</summary>
    public bool IsDisposed { get; private set; }

    /// <summary>Gets every byte written so far, in order.</summary>
    public byte[] Sent => [.. sent];

    /// <inheritdoc />
    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (nextRead == reads.Length)
        {
            return ValueTask.FromResult(0);
        }

        byte[] read = reads[nextRead++];
        read.CopyTo(buffer);
        return ValueTask.FromResult(read.Length);
    }

    /// <inheritdoc />
    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        sent.AddRange(buffer.Span);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        return ValueTask.CompletedTask;
    }
}
