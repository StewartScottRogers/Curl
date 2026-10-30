namespace Curl.Extension.AzureStorage.Http;

/// <summary>A body that ends when the server closes the connection (RFC 9112 §6.3, rule 8).</summary>
/// <param name="reader">The connection's reader.</param>
internal sealed class AzureStorageCloseDelimitedBodyStream(AzureStorageHttpConnectionReader reader) : AzureStorageHttpBodyStream
{
    /// <inheritdoc/>
    protected override ValueTask<int> ReadBodyAsync(Memory<byte> buffer, CancellationToken cancellationToken) =>
        reader.ReadAsync(buffer, cancellationToken);
}
