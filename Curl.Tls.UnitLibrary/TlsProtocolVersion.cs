namespace Curl.Tls;

/// <summary>
/// The record-layer protocol versions of TLS 1.2 and below, by their wire value. TLS 1.3
/// records carry <see cref="Tls12" /> as their legacy version.
/// </summary>
public enum TlsProtocolVersion : ushort
{
    /// <summary>TLS 1.0 (RFC 2246): the MD5 and SHA-1 PRF, and CBC IVs chained from record to record.</summary>
    Tls10 = 0x0301,

    /// <summary>TLS 1.1 (RFC 4346): the MD5 and SHA-1 PRF, and an explicit IV in every CBC record.</summary>
    Tls11 = 0x0302,

    /// <summary>TLS 1.2 (RFC 5246): the suite's SHA-256 or SHA-384 PRF, explicit CBC IVs, and AEAD records.</summary>
    Tls12 = 0x0303,
}
