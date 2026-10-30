using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Masque.Tunnel;

/// <summary>
/// A connection to a fake MASQUE proxy: each read hands back the next queued chunk (a chunk
/// longer than the buffer over several reads), a read past the last chunk is the end of the
/// stream, and every write is recorded.
/// </summary>
internal sealed class ScriptedProxyConnection : IConnection
{
    private readonly Queue<byte[]> _chunks = new();
    private readonly List<byte> _written = [];

    /// <summary>Gets or sets the exception every write throws, or <see langword="null" /> to record writes.</summary>
    public Exception? WriteFailure { get; set; }

    /// <summary>Gets or sets the exception every read throws, or <see langword="null" /> to play the chunks.</summary>
    public Exception? ReadFailure { get; set; }

    public bool IsDisposed { get; private set; }

    public bool IsSecure => false;

    public EndPoint? RemoteEndPoint => null;

    public byte[] Written => [.. _written];

    public void Enqueue(params byte[][] chunks)
    {
        foreach (var chunk in chunks)
        {
            _chunks.Enqueue(chunk);
        }
    }

    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (ReadFailure is not null)
        {
            throw ReadFailure;
        }

        if (!_chunks.TryDequeue(out var chunk))
        {
            return ValueTask.FromResult(0);
        }

        var length = Math.Min(chunk.Length, buffer.Length);
        chunk.AsSpan(0, length).CopyTo(buffer.Span);
        if (length < chunk.Length)
        {
            var rest = new Queue<byte[]>([chunk[length..], .. _chunks]);
            _chunks.Clear();
            foreach (var queued in rest)
            {
                _chunks.Enqueue(queued);
            }
        }

        return ValueTask.FromResult(length);
    }

    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        if (WriteFailure is not null)
        {
            throw WriteFailure;
        }

        _written.AddRange(buffer.ToArray());
        return ValueTask.CompletedTask;
    }

    public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        return ValueTask.CompletedTask;
    }
}
