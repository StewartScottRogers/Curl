using System.Net;
using System.Net.Sockets;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Telnet.Fakes;

/// <summary>
/// An <see cref="IConnection" /> whose writes start failing as a send to a closed socket
/// does: each of <paramref name="reads" /> is returned by one read, in order, then every
/// read returns zero, the server closing (or, with <see cref="StaysOpen" />, never
/// completes). The first <see cref="SuccessfulWrites" /> writes are taken and recorded in
/// <see cref="Writes" />, one entry per write; every later one fails with
/// <see cref="IOException" /> wrapping <see cref="Failure" />.
/// </summary>
/// <param name="reads">What the server sends, one read at a time.</param>
public sealed class SocketFailingConnection(params byte[][] reads) : IConnection
{
    private readonly TaskCompletionSource<int> never = new();

    private int nextRead;

    /// <summary>Gets how many writes are taken before every write fails.</summary>
    public int SuccessfulWrites { get; init; } = int.MaxValue;

    /// <summary>Gets a value indicating whether the read after the scripted ones never completes.</summary>
    public bool StaysOpen { get; init; }

    /// <summary>Gets the socket error inside every failed write's <see cref="IOException" />.</summary>
    public SocketException Failure { get; } = new((int)SocketError.ConnectionAborted);

    /// <summary>Gets every write attempted, in order, the failed ones included.</summary>
    public List<byte[]> Writes { get; } = [];

    /// <inheritdoc />
    public bool IsSecure => false;

    /// <inheritdoc />
    public EndPoint? RemoteEndPoint => null;

    /// <inheritdoc />
    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (nextRead < reads.Length)
        {
            byte[] read = reads[nextRead++];
            read.CopyTo(buffer);
            return ValueTask.FromResult(read.Length);
        }

        return StaysOpen ? new ValueTask<int>(never.Task) : ValueTask.FromResult(0);
    }

    /// <inheritdoc />
    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        Writes.Add(buffer.ToArray());
        return Writes.Count > SuccessfulWrites
            ? ValueTask.FromException(new IOException("Unable to write data to the transport connection.", Failure))
            : ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
