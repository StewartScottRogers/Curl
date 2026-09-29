namespace Curl.Tls;

/// <summary>
/// The named groups (RFC 8446 section 4.2.7) the TLS 1.3 client can make a key share
/// for - X25519, x448, the NIST curves, the RFC 7919 finite-field groups and the
/// X25519MLKEM768 hybrid - and the brainpool curves TLS 1.2 ECDHE also agrees on
/// (RFC 7027).
/// </summary>
public static class TlsNamedGroup
{
    /// <summary>secp256r1 (NIST P-256).</summary>
    public const ushort Secp256r1 = 0x0017;

    /// <summary>secp384r1 (NIST P-384).</summary>
    public const ushort Secp384r1 = 0x0018;

    /// <summary>secp521r1 (NIST P-521).</summary>
    public const ushort Secp521r1 = 0x0019;

    /// <summary>brainpoolP256r1 (RFC 7027), a TLS 1.2 ECDHE group.</summary>
    public const ushort BrainpoolP256r1 = 0x001a;

    /// <summary>brainpoolP384r1 (RFC 7027), a TLS 1.2 ECDHE group.</summary>
    public const ushort BrainpoolP384r1 = 0x001b;

    /// <summary>brainpoolP512r1 (RFC 7027), a TLS 1.2 ECDHE group.</summary>
    public const ushort BrainpoolP512r1 = 0x001c;

    /// <summary>x25519 (RFC 7748).</summary>
    public const ushort X25519 = 0x001d;

    /// <summary>x448 (RFC 7748).</summary>
    public const ushort X448 = 0x001e;

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

    /// <summary>X25519MLKEM768, the ML-KEM-768 and X25519 hybrid (draft-ietf-tls-ecdhe-mlkem).</summary>
    public const ushort X25519MlKem768 = 0x11ec;

    /// <summary>Returns whether the TLS 1.3 client can make a key share for <paramref name="group" />.</summary>
    /// <param name="group">The named group code point.</param>
    /// <returns><see langword="true" /> for X25519, x448, the three NIST curves, the five finite-field groups and X25519MLKEM768.</returns>
    public static bool CanShare(ushort group) =>
        group is X25519 or X448 or X25519MlKem768 or Secp256r1 or Secp384r1 or Secp521r1 or (>= Ffdhe2048 and <= Ffdhe8192);

    /// <summary>Returns whether the TLS 1.2 and below client agrees ECDHE on <paramref name="group" />.</summary>
    /// <param name="group">The named group code point.</param>
    /// <returns><see langword="true" /> for X25519, x448, the three NIST curves and the three brainpool curves.</returns>
    public static bool IsTls12EcdheGroup(ushort group) =>
        group is X25519 or X448 or (>= Secp256r1 and <= Secp521r1) or (>= BrainpoolP256r1 and <= BrainpoolP512r1);
}
