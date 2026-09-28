namespace Curl.Tls;

/// <summary>The AEAD key and IV derived from one traffic secret (RFC 8446 section 7.3).</summary>
/// <param name="Key">The write key, as long as the cipher suite's AEAD key.</param>
/// <param name="Iv">The write IV, twelve bytes.</param>
public sealed record Tls13TrafficKeys(byte[] Key, byte[] Iv);
