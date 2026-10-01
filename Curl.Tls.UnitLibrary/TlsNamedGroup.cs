namespace Curl.Tls;

/// <summary>
/// The named groups (RFC 8446 section 4.2.7) the TLS 1.3 client can make a key share
/// for - X25519, x448, the NIST curves, the brainpool <c>tls13</c> curves (RFC 8734), the
/// RFC 7919 finite-field groups, pure ML-KEM and the three ML-KEM hybrids - and the
/// brainpool curves TLS 1.2 ECDHE also agrees on (RFC 7027).
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

    /// <summary>brainpoolP256r1tls13 (RFC 8734), brainpoolP256r1 for TLS 1.3 only.</summary>
    public const ushort BrainpoolP256r1Tls13 = 0x001f;

    /// <summary>brainpoolP384r1tls13 (RFC 8734), brainpoolP384r1 for TLS 1.3 only.</summary>
    public const ushort BrainpoolP384r1Tls13 = 0x0020;

    /// <summary>brainpoolP512r1tls13 (RFC 8734), brainpoolP512r1 for TLS 1.3 only.</summary>
    public const ushort BrainpoolP512r1Tls13 = 0x0021;

    /// <summary>MLKEM512, pure ML-KEM-512 (draft-ietf-tls-mlkem).</summary>
    public const ushort MlKem512 = 0x0200;

    /// <summary>MLKEM768, pure ML-KEM-768 (draft-ietf-tls-mlkem).</summary>
    public const ushort MlKem768 = 0x0201;

    /// <summary>MLKEM1024, pure ML-KEM-1024 (draft-ietf-tls-mlkem).</summary>
    public const ushort MlKem1024 = 0x0202;

    /// <summary>SecP256r1MLKEM768, the secp256r1 and ML-KEM-768 hybrid (draft-ietf-tls-ecdhe-mlkem).</summary>
    public const ushort SecP256r1MlKem768 = 0x11eb;

    /// <summary>X25519MLKEM768, the ML-KEM-768 and X25519 hybrid (draft-ietf-tls-ecdhe-mlkem).</summary>
    public const ushort X25519MlKem768 = 0x11ec;

    /// <summary>SecP384r1MLKEM1024, the secp384r1 and ML-KEM-1024 hybrid (draft-ietf-tls-ecdhe-mlkem).</summary>
    public const ushort SecP384r1MlKem1024 = 0x11ed;

    /// <summary>Returns whether the TLS 1.3 client can make a key share for <paramref name="group" />.</summary>
    /// <param name="group">The named group code point.</param>
    /// <returns>
    /// <see langword="true" /> for X25519, x448, the three NIST curves, the three brainpool
    /// <c>tls13</c> curves, the five finite-field groups, the three pure ML-KEM groups and
    /// the three ML-KEM hybrids.
    /// </returns>
    public static bool CanShare(ushort group) => IsTls13CurveGroup(group) || group is >= Ffdhe2048 and <= Ffdhe8192 || IsMlKemGroup(group);

    /// <summary>Returns whether the TLS 1.2 and below client agrees ECDHE on <paramref name="group" />.</summary>
    /// <param name="group">The named group code point.</param>
    /// <returns><see langword="true" /> for X25519, x448, the three NIST curves and the three brainpool curves.</returns>
    public static bool IsTls12EcdheGroup(ushort group) =>
        group is X25519 or X448 or (>= Secp256r1 and <= Secp521r1) or (>= BrainpoolP256r1 and <= BrainpoolP512r1);

    // X25519, x448, the NIST curves and the brainpool tls13 curves.
    private static bool IsTls13CurveGroup(ushort group) =>
        group is X25519 or X448 or (>= Secp256r1 and <= Secp521r1) or (>= BrainpoolP256r1Tls13 and <= BrainpoolP512r1Tls13);

    // Pure ML-KEM and the three ML-KEM hybrids.
    private static bool IsMlKemGroup(ushort group) =>
        group is (>= MlKem512 and <= MlKem1024) or (>= SecP256r1MlKem768 and <= SecP384r1MlKem1024);
}
