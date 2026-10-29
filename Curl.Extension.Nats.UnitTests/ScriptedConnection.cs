using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Nats;

/// <summary>A connection that gives each scripted chunk to one read, then the end of the stream, and records what is written.</summary>
internal sealed class ScriptedConnection(params byte[][] chunks) : IConnection
{
    private readonly Queue<byte[]> reads = new(chunks);

    private readonly MemoryStream writes = new();

    public bool IsSecure => false;

    public EndPoint? RemoteEndPoint => null;

    public bool IsDisposed { get; private set; }

    /// <summary>Gets everything written, as UTF-8 text.</summary>
    public string Sent => Encoding.UTF8.GetString(writes.ToArray());

    /// <summary>Gives a connection whose chunks are the given lines' UTF-8 bytes.</summary>
    public static ScriptedConnection Of(params string[] chunks) => new([.. chunks.Select(Encoding.UTF8.GetBytes)]);

    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (!reads.TryDequeue(out byte[]? chunk))
        {
            return ValueTask.FromResult(0);
        }

        chunk.CopyTo(buffer);
        return ValueTask.FromResult(chunk.Length);
    }

    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        writes.Write(buffer.Span);
        return ValueTask.CompletedTask;
    }

    public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        return ValueTask.CompletedTask;
    }
}
