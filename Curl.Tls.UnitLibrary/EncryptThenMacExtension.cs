namespace Curl.Tls;

/// <summary>
/// The <c>encrypt_then_mac</c> extension (RFC 7366 section 2): empty, it offers or accepts
/// CBC records whose MAC covers the ciphertext.
/// </summary>
public static class EncryptThenMacExtension
{
    /// <summary>Returns the empty <c>encrypt_then_mac</c> extension.</summary>
    /// <returns>The extension.</returns>
    public static TlsExtension Encode() => new(TlsExtensionType.EncryptThenMac, []);

    /// <summary>Decodes <c>encrypt_then_mac</c> data, which must be empty.</summary>
    /// <param name="data">The extension data.</param>
    /// <returns><see langword="null" />, or <see cref="TlsAlertDescription.DecodeError" /> when the data is not empty.</returns>
    public static TlsAlertDescription? Decode(byte[] data) => new TlsReader(data).Finish(true).Alert;
}
