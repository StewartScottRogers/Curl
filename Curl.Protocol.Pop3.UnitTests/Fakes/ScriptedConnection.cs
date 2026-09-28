using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Pop3.Fakes;

/// <summary>
/// An <see cref="IConnection" /> that plays one side of a POP3 server from a script:
/// each element of <paramref name="reads" /> is returned by one read, in order, and once
/// the script is exhausted every read returns zero, which is the server closing - or
/// throws an <see cref="IOException" /> when <see cref="FailReadsWhenExhausted" /> is set.
/// Every byte written is recorded in <see cref="Sent" />.
/// </summary>
/// <param name="reads">What the server sends, one non-empty read at a time.</param>
public sealed class ScriptedConnection(params byte[][] reads) : IConnection
{
    private readonly List<byte> sent = [];

    private int nextRead;

    private int writes;

    /// <summary>
    /// Gets or sets how many writes succeed before every later one throws an
    /// <see cref="IOException" />; <see cref="int.MaxValue" />, the default, for no limit.
    /// </summary>
    public int WritesBeforeFailure { get; set; } = int.MaxValue;

    /// <summary>
    /// Gets or sets the exception a write past <see cref="WritesBeforeFailure" /> throws;
    /// an <see cref="IOException" />, the default, as a reset connection does.
    /// </summary>
    public Exception WriteFailure { get; set; } = new IOException("The connection was reset.");

    /// <summary>
    /// Gets or sets a value indicating whether a read past the script throws an
    /// <see cref="IOException" /> instead of returning zero.
    /// </summary>
    public bool FailReadsWhenExhausted { get; set; }

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
            return FailReadsWhenExhausted
                ? ValueTask.FromException<int>(new IOException("The connection was reset."))
                : ValueTask.FromResult(0);
        }

        byte[] read = reads[nextRead];
        int count = Math.Min(read.Length, buffer.Length);
        read.AsSpan(0, count).CopyTo(buffer.Span);
        if (count == read.Length)
        {
            nextRead++;
        }
        else
        {
            reads[nextRead] = read[count..];
        }

        return ValueTask.FromResult(count);
    }

    /// <inheritdoc />
    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        if (writes++ >= WritesBeforeFailure)
        {
            return ValueTask.FromException(WriteFailure);
        }

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
