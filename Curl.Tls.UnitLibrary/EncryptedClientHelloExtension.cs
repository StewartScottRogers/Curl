namespace Curl.Tls;

/// <summary>
/// The <c>encrypted_client_hello</c> extension (RFC 9849 section 5): in the outer
/// ClientHello the sealed inner hello, in the inner one a single <c>inner</c> byte, in a
/// HelloRetryRequest the 8-byte acceptance confirmation, and in EncryptedExtensions the
/// server's <c>retry_configs</c>.
/// </summary>
public static class EncryptedClientHelloExtension
{
    /// <summary>The length in bytes of an acceptance confirmation.</summary>
    public const int ConfirmationLength = 8;

    private const byte OuterType = 0;

    private const byte InnerType = 1;

    /// <summary>Returns an outer ClientHello's extension.</summary>
    /// <param name="suite">The HPKE suite that sealed <paramref name="payload" />.</param>
    /// <param name="configId">The <c>config_id</c> of the ECHConfig used.</param>
    /// <param name="encapsulatedKey">The HPKE <c>enc</c>; empty in the ClientHello after a HelloRetryRequest.</param>
    /// <param name="payload">The sealed <c>EncodedClientHelloInner</c> (zeros in the associated data).</param>
    /// <returns>The extension.</returns>
    public static TlsExtension EncodeOuter(EchCipherSuite suite, byte configId, byte[] encapsulatedKey, byte[] payload)
    {
        TlsWriter writer = new();
        writer.WriteUInt8(OuterType);
        writer.WriteUInt16(suite.KdfId);
        writer.WriteUInt16(suite.AeadId);
        writer.WriteUInt8(configId);
        writer.WriteOpaque(2, encapsulatedKey);
        writer.WriteOpaque(2, payload);
        return new TlsExtension(TlsExtensionType.EncryptedClientHello, writer.ToArray());
    }

    /// <summary>Returns the inner ClientHello's extension: the <c>inner</c> type alone.</summary>
    /// <returns>The extension.</returns>
    public static TlsExtension EncodeInner() => new(TlsExtensionType.EncryptedClientHello, [InnerType]);

    /// <summary>Returns EncryptedExtensions' extension carrying <paramref name="retryConfigs" />.</summary>
    /// <param name="retryConfigs">An encoded <c>ECHConfigList</c>, its length included.</param>
    /// <returns>The extension.</returns>
    public static TlsExtension EncodeRetryConfigs(byte[] retryConfigs) => new(TlsExtensionType.EncryptedClientHello, retryConfigs);

    /// <summary>Decodes EncryptedExtensions' extension: <c>retry_configs</c>, an <c>ECHConfigList</c>.</summary>
    /// <param name="data">The extension data.</param>
    /// <returns>The retry configs, or <see cref="TlsAlertDescription.DecodeError" />.</returns>
    public static TlsDecodeResult<EchConfigList> DecodeRetryConfigs(byte[] data) => EchConfigList.Decode(data);

    /// <summary>Decodes a HelloRetryRequest's extension: the 8-byte confirmation.</summary>
    /// <param name="data">The extension data.</param>
    /// <returns>The confirmation, or <see cref="TlsAlertDescription.DecodeError" /> for any other length.</returns>
    public static TlsDecodeResult<byte[]> DecodeRetryConfirmation(byte[] data)
    {
        TlsReader reader = new(data);
        return reader.Finish(reader.ReadBytes(ConfirmationLength));
    }
}
