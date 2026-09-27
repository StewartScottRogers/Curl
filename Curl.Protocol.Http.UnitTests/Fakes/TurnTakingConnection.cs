using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http.Fakes;

/// <summary>
/// An <see cref="IConnection" /> that plays a keep-alive HTTP server: it holds each scripted
/// response back until the client writes after having read the whole of the one before, so a
/// client that reads too far cannot see the next response early, then replays it in reads
/// of at most a chosen size. After the last response it reports the peer closed.
/// </summary>
/// <param name="chunkSize">The most bytes one read returns; at least 1.</param>
/// <param name="responses">The responses, first to last, each sent as Latin-1.</param>
public sealed class TurnTakingConnection(int chunkSize, params string[] responses) : IConnection
{
    private readonly Queue<byte[]> pending = new(responses.Select(Encoding.Latin1.GetBytes));

    private readonly MemoryStream written = new();

    private byte[] current = [];

    private int offset;

    /// <inheritdoc />
    public bool IsSecure => false;

    /// <inheritdoc />
    public EndPoint? RemoteEndPoint => null;

    /// <summary>Gets every byte written so far, as Latin-1 text.</summary>
    public string Written => Encoding.Latin1.GetString(written.ToArray());

    /// <summary>Gets a value indicating whether the connection has been disposed.</summary>
    public bool IsDisposed { get; private set; }

    /// <inheritdoc />
    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        int length = Math.Min(Math.Min(chunkSize, buffer.Length), current.Length - offset);
        current.AsSpan(offset, length).CopyTo(buffer.Span);
        offset += length;
        return ValueTask.FromResult(length);
    }

    /// <inheritdoc />
    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        written.Write(buffer.Span);
        if (offset == current.Length && pending.TryDequeue(out byte[]? next))
        {
            current = next;
            offset = 0;
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        written.Dispose();
        IsDisposed = true;
        return ValueTask.CompletedTask;
    }
}
