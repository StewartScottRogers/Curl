using System.Security.Cryptography;

namespace Curl.Tls;

/// <summary>
/// The signature schemes (RFC 8446 section 4.2.3) the client can check or sign: in a TLS
/// 1.3 CertificateVerify, RSA-PSS with RSAE and PSS keys, ECDSA on the three NIST curves
/// and the three brainpool curves (RFC 8734), Ed25519, Ed448 and ML-DSA (FIPS 204); in a
/// TLS 1.2 ServerKeyExchange or CertificateVerify (RFC 5246 section 7.4.1.4.1), the RSA-PSS,
/// Ed25519 and Ed448 ones and RSA PKCS #1 v1.5, ECDSA with any hash from SHA-1 up on any
/// curve, and DSA. ML-DSA and Ed448 are checked with <c>Curl.Cryptography</c>.
/// </summary>
public static class TlsSignatureScheme
{
    /// <summary><c>rsa_pkcs1_sha1</c> (TLS 1.2 only).</summary>
    public const ushort RsaPkcs1Sha1 = 0x0201;

    /// <summary><c>ecdsa_sha1</c> (TLS 1.2 only).</summary>
    public const ushort EcdsaSha1 = 0x0203;

    /// <summary><c>dsa_sha1</c> (TLS 1.2 only).</summary>
    public const ushort DsaSha1 = 0x0202;

    /// <summary><c>dsa_sha224</c> (TLS 1.2 only).</summary>
    public const ushort DsaSha224 = 0x0302;

    /// <summary><c>dsa_sha256</c> (TLS 1.2 only).</summary>
    public const ushort DsaSha256 = 0x0402;

    /// <summary><c>dsa_sha384</c> (TLS 1.2 only).</summary>
    public const ushort DsaSha384 = 0x0502;

    /// <summary><c>dsa_sha512</c> (TLS 1.2 only).</summary>
    public const ushort DsaSha512 = 0x0602;

    /// <summary><c>rsa_pkcs1_sha256</c>: offered for certificates only, never a TLS 1.3 CertificateVerify.</summary>
    public const ushort RsaPkcs1Sha256 = 0x0401;

    /// <summary><c>rsa_pkcs1_sha384</c> (TLS 1.2 only).</summary>
    public const ushort RsaPkcs1Sha384 = 0x0501;

    /// <summary><c>rsa_pkcs1_sha512</c> (TLS 1.2 only).</summary>
    public const ushort RsaPkcs1Sha512 = 0x0601;

    /// <summary><c>ecdsa_secp256r1_sha256</c>.</summary>
    public const ushort EcdsaSecp256r1Sha256 = 0x0403;

    /// <summary><c>ecdsa_secp384r1_sha384</c>.</summary>
    public const ushort EcdsaSecp384r1Sha384 = 0x0503;

    /// <summary><c>ecdsa_secp521r1_sha512</c>.</summary>
    public const ushort EcdsaSecp521r1Sha512 = 0x0603;

    /// <summary><c>rsa_pss_rsae_sha256</c>.</summary>
    public const ushort RsaPssRsaeSha256 = 0x0804;

    /// <summary><c>rsa_pss_rsae_sha384</c>.</summary>
    public const ushort RsaPssRsaeSha384 = 0x0805;

    /// <summary><c>rsa_pss_rsae_sha512</c>.</summary>
    public const ushort RsaPssRsaeSha512 = 0x0806;

    /// <summary><c>ed25519</c>.</summary>
    public const ushort Ed25519 = 0x0807;

    /// <summary><c>rsa_pss_pss_sha256</c>.</summary>
    public const ushort RsaPssPssSha256 = 0x0809;

    /// <summary><c>rsa_pss_pss_sha384</c>.</summary>
    public const ushort RsaPssPssSha384 = 0x080a;

    /// <summary><c>rsa_pss_pss_sha512</c>.</summary>
    public const ushort RsaPssPssSha512 = 0x080b;

    /// <summary><c>rsa_pkcs1_sha224</c> (TLS 1.2 only).</summary>
    public const ushort RsaPkcs1Sha224 = 0x0301;

    /// <summary><c>ecdsa_sha224</c> (TLS 1.2 only).</summary>
    public const ushort EcdsaSha224 = 0x0303;

    /// <summary><c>ed448</c>.</summary>
    public const ushort Ed448 = 0x0808;

    /// <summary><c>ecdsa_brainpoolP256r1tls13_sha256</c> (TLS 1.3 only, RFC 8734).</summary>
    public const ushort EcdsaBrainpoolP256r1Tls13Sha256 = 0x081a;

    /// <summary><c>ecdsa_brainpoolP384r1tls13_sha384</c> (TLS 1.3 only, RFC 8734).</summary>
    public const ushort EcdsaBrainpoolP384r1Tls13Sha384 = 0x081b;

    /// <summary><c>ecdsa_brainpoolP512r1tls13_sha512</c> (TLS 1.3 only, RFC 8734).</summary>
    public const ushort EcdsaBrainpoolP512r1Tls13Sha512 = 0x081c;

    /// <summary><c>mldsa44</c> (TLS 1.3 only).</summary>
    public const ushort MlDsa44 = 0x0904;

    /// <summary><c>mldsa65</c> (TLS 1.3 only).</summary>
    public const ushort MlDsa65 = 0x0905;

    /// <summary><c>mldsa87</c> (TLS 1.3 only).</summary>
    public const ushort MlDsa87 = 0x0906;

    internal const string RsaEncryptionOid = "1.2.840.113549.1.1.1";
    internal const string RsaSsaPssOid = "1.2.840.113549.1.1.10";
    internal const string EcPublicKeyOid = "1.2.840.10045.2.1";
    internal const string Ed25519Oid = "1.3.101.112";
    internal const string Ed448Oid = "1.3.101.113";
    internal const string MlDsa44Oid = "2.16.840.1.101.3.4.3.17";
    internal const string MlDsa65Oid = "2.16.840.1.101.3.4.3.18";
    internal const string MlDsa87Oid = "2.16.840.1.101.3.4.3.19";
    internal const string DsaOid = "1.2.840.10040.4.1";
    internal const string Secp256r1Oid = "1.2.840.10045.3.1.7";
    internal const string Secp384r1Oid = "1.3.132.0.34";
    internal const string Secp521r1Oid = "1.3.132.0.35";
    internal const string BrainpoolP256r1Oid = "1.3.36.3.3.2.8.1.1.7";
    internal const string BrainpoolP384r1Oid = "1.3.36.3.3.2.8.1.1.11";
    internal const string BrainpoolP512r1Oid = "1.3.36.3.3.2.8.1.1.13";

    /// <summary>SHA-224, which the BCL names but does not compute; <see cref="Cryptography.DsaSignature.HashData" /> does.</summary>
    internal static readonly HashAlgorithmName Sha224 = new("SHA224");

    /// <summary>The DER <c>DigestInfo</c> before a SHA-224 hash in a PKCS #1 v1.5 signature (RFC 8017 section 9.2, note 1).</summary>
    private static readonly byte[] Sha224DigestInfoPrefix = [0x30, 0x2d, 0x30, 0x0d, 0x06, 0x09, 0x60, 0x86, 0x48, 0x01, 0x65, 0x03, 0x04, 0x02, 0x04, 0x05, 0x00, 0x04, 0x1c];

    // The schemes TLS 1.3 and TLS 1.2 share; Rules adds the TLS 1.3-only ones, Tls12Rules the TLS 1.2-only ones.
    private static readonly Dictionary<ushort, TlsSignatureRule> SharedRules = new()
    {
        [RsaPssRsaeSha256] = new(TlsSignatureKind.RsaPss, RsaEncryptionOid, null, HashAlgorithmName.SHA256),
        [RsaPssRsaeSha384] = new(TlsSignatureKind.RsaPss, RsaEncryptionOid, null, HashAlgorithmName.SHA384),
        [RsaPssRsaeSha512] = new(TlsSignatureKind.RsaPss, RsaEncryptionOid, null, HashAlgorithmName.SHA512),
        [RsaPssPssSha256] = new(TlsSignatureKind.RsaPss, RsaSsaPssOid, null, HashAlgorithmName.SHA256),
        [RsaPssPssSha384] = new(TlsSignatureKind.RsaPss, RsaSsaPssOid, null, HashAlgorithmName.SHA384),
        [RsaPssPssSha512] = new(TlsSignatureKind.RsaPss, RsaSsaPssOid, null, HashAlgorithmName.SHA512),
        [EcdsaSecp256r1Sha256] = new(TlsSignatureKind.Ecdsa, EcPublicKeyOid, Secp256r1Oid, HashAlgorithmName.SHA256),
        [EcdsaSecp384r1Sha384] = new(TlsSignatureKind.Ecdsa, EcPublicKeyOid, Secp384r1Oid, HashAlgorithmName.SHA384),
        [EcdsaSecp521r1Sha512] = new(TlsSignatureKind.Ecdsa, EcPublicKeyOid, Secp521r1Oid, HashAlgorithmName.SHA512),
        [Ed25519] = new(TlsSignatureKind.Ed25519, Ed25519Oid, null, default),
        [Ed448] = new(TlsSignatureKind.Ed448, Ed448Oid, null, default),
    };

    // ML-DSA and the brainpool tls13 schemes are TLS 1.3 only (RFC 8734 section 1, OpenSSL's minimum version for them).
    private static readonly Dictionary<ushort, TlsSignatureRule> Rules = new(SharedRules)
    {
        [EcdsaBrainpoolP256r1Tls13Sha256] = new(TlsSignatureKind.Ecdsa, EcPublicKeyOid, BrainpoolP256r1Oid, HashAlgorithmName.SHA256),
        [EcdsaBrainpoolP384r1Tls13Sha384] = new(TlsSignatureKind.Ecdsa, EcPublicKeyOid, BrainpoolP384r1Oid, HashAlgorithmName.SHA384),
        [EcdsaBrainpoolP512r1Tls13Sha512] = new(TlsSignatureKind.Ecdsa, EcPublicKeyOid, BrainpoolP512r1Oid, HashAlgorithmName.SHA512),
        [MlDsa44] = new(TlsSignatureKind.MlDsa, MlDsa44Oid, null, default),
        [MlDsa65] = new(TlsSignatureKind.MlDsa, MlDsa65Oid, null, default),
        [MlDsa87] = new(TlsSignatureKind.MlDsa, MlDsa87Oid, null, default),
    };

    // TLS 1.2 binds an ecdsa_* scheme to its hash only, not to a curve (RFC 8422 section 5.1.1).
    private static readonly Dictionary<ushort, TlsSignatureRule> Tls12Rules = new(SharedRules)
    {
        [RsaPkcs1Sha224] = new(TlsSignatureKind.RsaPkcs1Sha224, RsaEncryptionOid, null, Sha224),
        [EcdsaSha224] = new(TlsSignatureKind.Ecdsa, EcPublicKeyOid, null, Sha224),
        [RsaPkcs1Sha1] = new(TlsSignatureKind.RsaPkcs1, RsaEncryptionOid, null, HashAlgorithmName.SHA1),
        [RsaPkcs1Sha256] = new(TlsSignatureKind.RsaPkcs1, RsaEncryptionOid, null, HashAlgorithmName.SHA256),
        [RsaPkcs1Sha384] = new(TlsSignatureKind.RsaPkcs1, RsaEncryptionOid, null, HashAlgorithmName.SHA384),
        [RsaPkcs1Sha512] = new(TlsSignatureKind.RsaPkcs1, RsaEncryptionOid, null, HashAlgorithmName.SHA512),
        [EcdsaSha1] = new(TlsSignatureKind.Ecdsa, EcPublicKeyOid, null, HashAlgorithmName.SHA1),
        [EcdsaSecp256r1Sha256] = new(TlsSignatureKind.Ecdsa, EcPublicKeyOid, null, HashAlgorithmName.SHA256),
        [EcdsaSecp384r1Sha384] = new(TlsSignatureKind.Ecdsa, EcPublicKeyOid, null, HashAlgorithmName.SHA384),
        [EcdsaSecp521r1Sha512] = new(TlsSignatureKind.Ecdsa, EcPublicKeyOid, null, HashAlgorithmName.SHA512),
        [DsaSha1] = new(TlsSignatureKind.Dsa, DsaOid, null, HashAlgorithmName.SHA1),
        [DsaSha224] = new(TlsSignatureKind.Dsa, DsaOid, null, Sha224),
        [DsaSha256] = new(TlsSignatureKind.Dsa, DsaOid, null, HashAlgorithmName.SHA256),
        [DsaSha384] = new(TlsSignatureKind.Dsa, DsaOid, null, HashAlgorithmName.SHA384),
        [DsaSha512] = new(TlsSignatureKind.Dsa, DsaOid, null, HashAlgorithmName.SHA512),
    };

    private static readonly TlsSignatureRule LegacyRsaRule = new(TlsSignatureKind.RsaMd5Sha1, RsaEncryptionOid, null, default);

    private static readonly TlsSignatureRule LegacyEcdsaRule = new(TlsSignatureKind.Ecdsa, EcPublicKeyOid, null, HashAlgorithmName.SHA1);

    private static readonly TlsSignatureRule LegacyDsaRule = new(TlsSignatureKind.Dsa, DsaOid, null, HashAlgorithmName.SHA1);

    /// <summary>Returns whether a TLS 1.3 CertificateVerify may carry <paramref name="scheme" /> and the client can check and sign it.</summary>
    /// <param name="scheme">The signature scheme code point.</param>
    /// <returns><see langword="true" /> for the RSA-PSS, ECDSA, Ed25519, Ed448 and ML-DSA schemes.</returns>
    public static bool IsCertificateVerifyScheme(ushort scheme) => Rules.ContainsKey(scheme);

    /// <summary>Returns whether a TLS 1.2 ServerKeyExchange or CertificateVerify may carry <paramref name="scheme" /> and the client can check and sign it.</summary>
    /// <param name="scheme">The signature scheme code point.</param>
    /// <returns><see langword="true" /> for the TLS 1.3 schemes but ML-DSA and the brainpool <c>tls13</c> ones, RSA PKCS #1 v1.5, ECDSA with SHA-1 and SHA-224, and DSA.</returns>
    public static bool IsTls12Scheme(ushort scheme) => Tls12Rules.ContainsKey(scheme);

    /// <summary>Gets the TLS 1.0 and 1.1 signatures, RSA over MD5 and SHA-1 then ECDSA over SHA-1, for a signer to pick the one its key fits.</summary>
    internal static IReadOnlyList<TlsSignatureRule> LegacyRules { get; } = [LegacyRsaRule, LegacyEcdsaRule];

    internal static TlsSignatureRule? FindRule(ushort scheme) => Rules.GetValueOrDefault(scheme);

    internal static TlsSignatureRule? FindTls12Rule(ushort scheme) => Tls12Rules.GetValueOrDefault(scheme);

    /// <summary>
    /// Returns the one signature TLS 1.0 and 1.1 make with a key of
    /// <paramref name="keyOid" /> (RFC 4346 section 7.4.3, RFC 8422 section 5.10): RSA over
    /// MD5 and SHA-1, or ECDSA or DSA over SHA-1 (RFC 2246 section 7.4.3); any other key
    /// signs nothing.
    /// </summary>
    internal static TlsSignatureRule? FindLegacyRule(string keyOid) => keyOid switch
    {
        RsaEncryptionOid => LegacyRsaRule,
        EcPublicKeyOid => LegacyEcdsaRule,
        DsaOid => LegacyDsaRule,
        _ => null,
    };

    /// <summary>
    /// Returns the PKCS #1 block type 1 TLS 1.0 and 1.1 sign with RSA (RFC 2246 section
    /// 7.4.3): <c>00 01 FF..FF 00</c>, then MD5 and SHA-1 of <paramref name="content" />,
    /// <paramref name="modulusLength" /> bytes in all.
    /// </summary>
    internal static byte[] BuildMd5Sha1Block(byte[] content, int modulusLength) =>
        PadPkcs1Type1([.. CryptographicOperations.HashData(HashAlgorithmName.MD5, content), .. CryptographicOperations.HashData(HashAlgorithmName.SHA1, content)], modulusLength);

    /// <summary>
    /// Returns the PKCS #1 block type 1 the BCL cannot build, <paramref name="modulusLength" />
    /// bytes: TLS 1.0 and 1.1's MD5 and SHA-1 block for <see cref="TlsSignatureKind.RsaMd5Sha1" />,
    /// otherwise <c>rsa_pkcs1_sha224</c>'s, <c>00 01 FF..FF 00</c> then the SHA-224
    /// <c>DigestInfo</c> of <paramref name="content" /> (RFC 8017 section 9.2).
    /// </summary>
    internal static byte[] BuildHandBuiltPkcs1Block(TlsSignatureKind kind, byte[] content, int modulusLength) => kind == TlsSignatureKind.RsaMd5Sha1
        ? BuildMd5Sha1Block(content, modulusLength)
        : PadPkcs1Type1([.. Sha224DigestInfoPrefix, .. Cryptography.DsaSignature.HashData(content, Sha224)], modulusLength);

    /// <summary>Returns the length of what <see cref="BuildHandBuiltPkcs1Block" /> pads for <paramref name="kind" />: 36 bytes of MD5 and SHA-1, or 47 of SHA-224 <c>DigestInfo</c>.</summary>
    internal static int HandBuiltPkcs1PayloadLength(TlsSignatureKind kind) =>
        kind == TlsSignatureKind.RsaMd5Sha1 ? 36 : Sha224DigestInfoPrefix.Length + 28;

    private static byte[] PadPkcs1Type1(byte[] payload, int modulusLength)
    {
        byte[] block = new byte[modulusLength];
        block[1] = 0x01;
        block.AsSpan(2, modulusLength - payload.Length - 3).Fill(0xff);
        payload.CopyTo(block, modulusLength - payload.Length);
        return block;
    }

    /// <summary>
    /// Returns the bytes a TLS 1.3 CertificateVerify signs (RFC 8446 section 4.4.3): 64
    /// spaces, the context string, a zero byte and the transcript hash.
    /// </summary>
    /// <param name="server"><see langword="true" /> for the server's CertificateVerify, <see langword="false" /> for the client's.</param>
    /// <param name="transcriptHash">The transcript hash through the Certificate message.</param>
    /// <returns>The content to sign or verify.</returns>
    public static byte[] BuildCertificateVerifyContent(bool server, ReadOnlySpan<byte> transcriptHash)
    {
        string context = server ? "TLS 1.3, server CertificateVerify" : "TLS 1.3, client CertificateVerify";
        byte[] content = new byte[64 + context.Length + 1 + transcriptHash.Length];
        content.AsSpan(0, 64).Fill(0x20);
        System.Text.Encoding.ASCII.GetBytes(context, content.AsSpan(64));
        transcriptHash.CopyTo(content.AsSpan(64 + context.Length + 1));
        return content;
    }
}
