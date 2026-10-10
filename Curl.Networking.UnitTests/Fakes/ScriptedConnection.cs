using System.Net;

using Curl.Protocol.Abstractions;

namespace Curl.Networking.Fakes;

/// <summary>
/// An <see cref="IConnection" /> that answers reads from fixed bytes, then reports the peer
/// closed, and records every byte written, how often it was flushed and whether it was
/// disposed.
/// </summary>
/// <param name="bytesToRead">The bytes the peer sends, in order.</param>
public sealed class ScriptedConnection(byte[] bytesToRead) : IConnection
{
    private int _readPosition;

    /// <summary>
    /// Gets every byte written, in order.
    /// </summary>
    public List<byte> Written { get; } = [];

    /// <summary>
    /// Gets how many times <see cref="FlushAsync" /> was called.
    /// </summary>
    public int FlushCount { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the connection was disposed.
    /// </summary>
    public bool IsDisposed { get; private set; }

    /// <summary>
    /// Gets the exception every read throws, or <see langword="null" /> to answer reads.
    /// </summary>
    public Exception? ReadException { get; init; }

    /// <summary>
    /// Gets the exception a read throws once every scripted byte has been read, or
    /// <see langword="null" /> to report the peer closed instead.
    /// </summary>
    public Exception? ExceptionAfterScript { get; init; }

    /// <summary>
    /// Gets the number of bytes not yet read.
    /// </summary>
    public int UnreadCount => bytesToRead.Length - _readPosition;

    /// <summary>
    /// Gets or sets the value <see cref="IConnection.HasPeerClosed" /> reports: whether a
    /// liveness probe would find the peer gone.
    /// </summary>
    public bool HasPeerClosed { get; set; }

    /// <inheritdoc />
    public bool IsSecure => false;

    /// <inheritdoc />
    public EndPoint? RemoteEndPoint => null;

    /// <inheritdoc />
    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (ReadException is not null)
        {
            throw ReadException;
        }

        if (UnreadCount == 0 && ExceptionAfterScript is not null)
        {
            throw ExceptionAfterScript;
        }

        var count = Math.Min(buffer.Length, UnreadCount);
        bytesToRead.AsMemory(_readPosition, count).CopyTo(buffer);
        _readPosition += count;
        return ValueTask.FromResult(count);
    }

    /// <inheritdoc />
    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        Written.AddRange(buffer.ToArray());
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask FlushAsync(CancellationToken cancellationToken)
    {
        FlushCount++;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        return ValueTask.CompletedTask;
    }
}
