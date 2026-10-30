using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Grpc.Client;

/// <summary>A connection that gives its scripted bytes to reads, then the end of the stream, and records what is written.</summary>
internal sealed class ScriptedConnection(byte[] reply) : IConnection
{
    private readonly MemoryStream reads = new(reply);
    private readonly MemoryStream writes = new();

    public bool IsSecure => false;

    public EndPoint? RemoteEndPoint => null;

    public bool IsDisposed { get; private set; }

    /// <summary>Gets everything written.</summary>
    public byte[] Sent => writes.ToArray();

    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) =>
        ValueTask.FromResult(reads.Read(buffer.Span));

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
