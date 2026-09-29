namespace Curl.Tls;

/// <summary>
/// The named groups (RFC 8446 section 4.2.7) the TLS 1.3 client can make a key share
/// for: X25519, the NIST curves and the RFC 7919 finite-field groups.
/// </summary>
public static class TlsNamedGroup
{
    /// <summary>secp256r1 (NIST P-256).</summary>
    public const ushort Secp256r1 = 0x0017;

    /// <summary>secp384r1 (NIST P-384).</summary>
    public const ushort Secp384r1 = 0x0018;

    /// <summary>secp521r1 (NIST P-521).</summary>
    public const ushort Secp521r1 = 0x0019;

    /// <summary>x25519 (RFC 7748).</summary>
    public const ushort X25519 = 0x001d;

    /// <summary>ffdhe2048 (RFC 7919).</summary>
    public const ushort Ffdhe2048 = 0x0100;

    /// <summary>ffdhe3072 (RFC 7919).</summary>
    public const ushort Ffdhe3072 = 0x0101;

    /// <summary>ffdhe4096 (RFC 7919).</summary>
    public const ushort Ffdhe4096 = 0x0102;

    /// <summary>ffdhe6144 (RFC 7919).</summary>
    public const ushort Ffdhe6144 = 0x0103;

    /// <summary>ffdhe8192 (RFC 7919).</summary>
    public const ushort Ffdhe8192 = 0x0104;

    /// <summary>Returns whether the client can make a key share for <paramref name="group" />.</summary>
    /// <param name="group">The named group code point.</param>
    /// <returns><see langword="true" /> for X25519, the three NIST curves and the five finite-field groups.</returns>
    public static bool CanShare(ushort group) =>
        group is X25519 or Secp256r1 or Secp384r1 or Secp521r1 or (>= Ffdhe2048 and <= Ffdhe8192);
}
