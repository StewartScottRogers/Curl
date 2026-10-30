using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.ObliviousDoh.Http;

/// <summary>A connection that answers what was written to it, computing the answer at the first read.</summary>
internal sealed class ExchangeConnection(Func<byte[], byte[]> respond) : IConnection
{
    private byte[]? response;
    private int readOffset;

    public List<byte> Written { get; } = [];

    public bool ThrowOnWrite { get; init; }

    public bool IsDisposed { get; private set; }

    public bool IsSecure => false;

    public EndPoint? RemoteEndPoint => null;

    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        response ??= respond([.. Written]);
        int count = Math.Min(buffer.Length, response.Length - readOffset);
        response.AsSpan(readOffset, count).CopyTo(buffer.Span);
        readOffset += count;
        return ValueTask.FromResult(count);
    }

    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        if (ThrowOnWrite)
        {
            throw new IOException("reset");
        }

        Written.AddRange(buffer.ToArray());
        return ValueTask.CompletedTask;
    }

    public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        return ValueTask.CompletedTask;
    }
}
