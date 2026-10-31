namespace Curl.Extended.Protocol.S3.Http;

/// <summary>A body that ends when the server closes the connection (RFC 9112 §6.3, rule 8).</summary>
/// <param name="reader">The connection's reader.</param>
internal sealed class S3CloseDelimitedBodyStream(S3HttpConnectionReader reader) : S3HttpBodyStream
{
    /// <inheritdoc/>
    protected override ValueTask<int> ReadBodyAsync(Memory<byte> buffer, CancellationToken cancellationToken) =>
        reader.ReadAsync(buffer, cancellationToken);
}
