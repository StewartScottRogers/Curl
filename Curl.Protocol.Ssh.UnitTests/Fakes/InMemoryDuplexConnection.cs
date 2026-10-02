using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ssh.Fakes;

/// <summary>
/// One end of an in-memory byte stream between two <see cref="IConnection" />s: what one end
/// writes the other reads, in order. Disposing an end is a close: the other end then reads
/// the end of the stream, and a write to an end whose peer has closed throws
/// <see cref="IOException" />, as a socket's does.
/// </summary>
public sealed class InMemoryDuplexConnection : IConnection
{
    private readonly CancelSafeChunkQueue inbound;

    private readonly CancelSafeChunkQueue outbound;

    private InMemoryDuplexConnection? peer;

    private ReadOnlyMemory<byte> pending;

    private volatile bool isClosed;

    private InMemoryDuplexConnection(CancelSafeChunkQueue inbound, CancelSafeChunkQueue outbound)
    {
        this.inbound = inbound;
        this.outbound = outbound;
    }

    /// <inheritdoc />
    public bool IsSecure => false;

    /// <inheritdoc />
    public EndPoint? RemoteEndPoint => null;

    /// <summary>
    /// Creates the two ends of one stream.
    /// </summary>
    /// <returns>The client's end and the server's end.</returns>
    public static (InMemoryDuplexConnection Client, InMemoryDuplexConnection Server) CreatePair()
    {
        CancelSafeChunkQueue toServer = new();
        CancelSafeChunkQueue toClient = new();
        InMemoryDuplexConnection client = new(toClient, toServer);
        InMemoryDuplexConnection server = new(toServer, toClient);
        client.peer = server;
        server.peer = client;
        return (client, server);
    }

    /// <inheritdoc />
    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (pending.IsEmpty)
        {
            if (await inbound.DequeueAsync(cancellationToken).ConfigureAwait(false) is not { } chunk)
            {
                return 0;
            }

            pending = chunk;
        }

        int taken = Math.Min(buffer.Length, pending.Length);
        pending[..taken].CopyTo(buffer);
        pending = pending[taken..];
        return taken;
    }

    /// <inheritdoc />
    /// <exception cref="IOException">The other end has closed.</exception>
    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        if (isClosed || peer!.isClosed || !outbound.TryEnqueue(buffer.ToArray()))
        {
            throw new IOException("The in-memory peer has closed the connection.");
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        isClosed = true;
        outbound.Complete();
        return ValueTask.CompletedTask;
    }
}
