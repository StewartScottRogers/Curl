namespace Curl.Conformance.Extended.Harness;

/// <summary>A reference server's end of one connection, recording each read and write in its <see cref="WireExchange" />.</summary>
public sealed class ReferenceConnection
{
    private readonly Stream stream;

    private readonly WireExchange exchange;

    internal ReferenceConnection(Stream stream, WireExchange exchange)
    {
        this.stream = stream;
        this.exchange = exchange;
    }

    /// <summary>Reads what curl sent next.</summary>
    /// <param name="buffer">Where the bytes go.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The number of bytes read; 0 once curl has closed the connection.</returns>
    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        int read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        exchange.RecordClient(buffer.Span[..read]);
        return read;
    }

    /// <summary>Sends <paramref name="bytes" /> to curl.</summary>
    /// <param name="bytes">The bytes to send.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that ends once the bytes are written.</returns>
    public async ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        exchange.RecordServer(bytes.Span);
        await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}
