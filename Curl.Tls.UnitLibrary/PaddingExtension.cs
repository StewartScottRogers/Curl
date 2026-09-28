namespace Curl.Tls;

/// <summary>The <c>padding</c> extension (RFC 7685): zero bytes that bring a ClientHello to a chosen length.</summary>
public static class PaddingExtension
{
    /// <summary>Returns a <c>padding</c> extension of <paramref name="length" /> zero bytes.</summary>
    /// <param name="length">The number of zero bytes.</param>
    /// <returns>The extension.</returns>
    public static TlsExtension Encode(int length) => new(TlsExtensionType.Padding, new byte[length]);

    /// <summary>Decodes <c>padding</c> data, which must be all zero.</summary>
    /// <param name="data">The extension data.</param>
    /// <returns>The padding length, or <see cref="TlsAlertDescription.IllegalParameter" /> when a byte is not zero.</returns>
    public static TlsDecodeResult<int> Decode(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return data.AsSpan().ContainsAnyExcept((byte)0)
            ? TlsDecodeResult<int>.Failure(TlsAlertDescription.IllegalParameter)
            : TlsDecodeResult<int>.Success(data.Length);
    }
}
