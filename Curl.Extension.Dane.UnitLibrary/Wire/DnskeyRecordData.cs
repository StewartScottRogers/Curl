namespace Curl.Extension.Dane.Wire;

/// <summary>
/// DNSKEY RDATA (RFC 4034 section 2.1): a zone's public key.
/// </summary>
/// <param name="Flags">The flags field (section 2.1.1): bit 7 Zone Key, bit 15 Secure Entry Point.</param>
/// <param name="Protocol">The protocol field (section 2.1.2), always 3.</param>
/// <param name="Algorithm">The algorithm number (section 2.1.3), such as 8 RSA/SHA-256 or 15 Ed25519.</param>
/// <param name="PublicKey">The public key (section 2.1.4), in the algorithm's format.</param>
public sealed record DnskeyRecordData(ushort Flags, byte Protocol, byte Algorithm, byte[] PublicKey) : DnsRecordData;
