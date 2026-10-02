using System.Net;
using System.Threading.Channels;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Mqtt.Fakes;

/// <summary>
/// An <see cref="IConnection" /> whose reads wait until the test sends the peer's next chunk,
/// so a transfer can sit idle while the test moves a clock; it records every byte written.
/// </summary>
public sealed class GatedConnection : IConnection
{
    private readonly Channel<byte[]> chunks = Channel.CreateUnbounded<byte[]>();

    private readonly MemoryStream written = new();

    private readonly Lock gate = new();

    /// <summary>
    /// Gets every byte written to the connection, in order.
    /// </summary>
    public byte[] Written
    {
        get
        {
            lock (gate)
            {
                return written.ToArray();
            }
        }
    }

    /// <inheritdoc />
    public bool IsSecure => false;

    /// <inheritdoc />
    public EndPoint? RemoteEndPoint => null;

    /// <summary>
    /// Makes <paramref name="chunk" /> the peer's next read; an empty chunk is the peer closing.
    /// </summary>
    /// <param name="chunk">The bytes the next read returns.</param>
    public void Send(byte[] chunk) => chunks.Writer.TryWrite(chunk);

    /// <inheritdoc />
    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        byte[] chunk = await chunks.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        chunk.CopyTo(buffer);
        return chunk.Length;
    }

    /// <inheritdoc />
    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            written.Write(buffer.Span);
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
