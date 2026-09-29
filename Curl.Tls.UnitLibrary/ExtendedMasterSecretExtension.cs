namespace Curl.Tls;

/// <summary>
/// The <c>extended_master_secret</c> extension (RFC 7627 section 5.1): empty, it offers or
/// accepts a master secret bound to the handshake transcript.
/// </summary>
public static class ExtendedMasterSecretExtension
{
    /// <summary>Returns the empty <c>extended_master_secret</c> extension.</summary>
    /// <returns>The extension.</returns>
    public static TlsExtension Encode() => new(TlsExtensionType.ExtendedMasterSecret, []);

    /// <summary>Decodes <c>extended_master_secret</c> data, which must be empty.</summary>
    /// <param name="data">The extension data.</param>
    /// <returns><see langword="null" />, or <see cref="TlsAlertDescription.DecodeError" /> when the data is not empty.</returns>
    public static TlsAlertDescription? Decode(byte[] data) => new TlsReader(data).Finish(true).Alert;
}
