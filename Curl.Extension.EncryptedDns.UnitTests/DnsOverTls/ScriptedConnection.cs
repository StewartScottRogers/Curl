using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.EncryptedDns.DnsOverTls;

/// <summary>
/// A connection that hands out <see cref="BytesToRead" /> at most <see cref="ReadChunk" /> bytes a read, then
/// reports the peer closed, and keeps every byte written.
/// </summary>
internal sealed class ScriptedConnection(bool isSecure) : IConnection
{
    private int readOffset;

    public byte[] BytesToRead { get; init; } = [];

    public int ReadChunk { get; init; } = int.MaxValue;

    public List<byte> Written { get; } = [];

    public int FlushCount { get; private set; }

    public bool IsDisposed { get; private set; }

    public bool IsSecure { get; } = isSecure;

    public EndPoint? RemoteEndPoint => null;

    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        int count = Math.Min(Math.Min(buffer.Length, ReadChunk), BytesToRead.Length - readOffset);
        BytesToRead.AsSpan(readOffset, count).CopyTo(buffer.Span);
        readOffset += count;
        return ValueTask.FromResult(count);
    }

    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        Written.AddRange(buffer.ToArray());
        return ValueTask.CompletedTask;
    }

    public ValueTask FlushAsync(CancellationToken cancellationToken)
    {
        FlushCount++;
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        return ValueTask.CompletedTask;
    }
}
