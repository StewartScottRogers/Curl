namespace Curl.Tls;

/// <summary>The <c>padding</c> extension (RFC 7685): zero bytes that bring a ClientHello to a chosen length.</summary>
public static class PaddingExtension
{
    private const int PaddingFloor = 0x100;
    private const int PaddingTarget = 0x200;
    private const int ExtensionHeaderLength = 4;

    /// <summary>Returns a <c>padding</c> extension of <paramref name="length" /> zero bytes.</summary>
    /// <param name="length">The number of zero bytes.</param>
    /// <returns>The extension.</returns>
    public static TlsExtension Encode(int length) => new(TlsExtensionType.Padding, new byte[length]);

    /// <summary>
    /// Returns the <c>padding</c> data length that brings a ClientHello of 256 to 511 bytes,
    /// handshake header included, up to 512 (the rule of RFC 7685's F5 workaround, as OpenSSL,
    /// NSS and BoringSSL apply it), at least one byte; <see langword="null" /> for any other length.
    /// </summary>
    /// <param name="unpaddedHelloLength">The encoded ClientHello's length without <c>padding</c>.</param>
    /// <returns>The data length, or <see langword="null" /> when the hello is not padded.</returns>
    public static int? DataLengthFor(int unpaddedHelloLength)
    {
        if (unpaddedHelloLength < PaddingFloor || unpaddedHelloLength >= PaddingTarget)
        {
            return null;
        }

        int padding = PaddingTarget - unpaddedHelloLength;
        return padding > ExtensionHeaderLength ? padding - ExtensionHeaderLength : 1;
    }

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
