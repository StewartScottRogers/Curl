using System.Security.Cryptography;

namespace Curl.Tls;

/// <summary>
/// The signature schemes (RFC 8446 section 4.2.3) the client can check or sign: in a TLS
/// 1.3 CertificateVerify, RSA-PSS with RSAE and PSS keys, ECDSA on the three NIST curves,
/// and Ed25519; in a TLS 1.2 ServerKeyExchange or CertificateVerify (RFC 5246 section
/// 7.4.1.4.1), those and RSA PKCS #1 v1.5 and ECDSA with SHA-1, ECDSA on any curve.
/// </summary>
public static class TlsSignatureScheme
{
    /// <summary><c>rsa_pkcs1_sha1</c> (TLS 1.2 only).</summary>
    public const ushort RsaPkcs1Sha1 = 0x0201;

    /// <summary><c>ecdsa_sha1</c> (TLS 1.2 only).</summary>
    public const ushort EcdsaSha1 = 0x0203;

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

    internal const string RsaEncryptionOid = "1.2.840.113549.1.1.1";
    internal const string RsaSsaPssOid = "1.2.840.113549.1.1.10";
    internal const string EcPublicKeyOid = "1.2.840.10045.2.1";
    internal const string Ed25519Oid = "1.3.101.112";
    internal const string Secp256r1Oid = "1.2.840.10045.3.1.7";
    internal const string Secp384r1Oid = "1.3.132.0.34";
    internal const string Secp521r1Oid = "1.3.132.0.35";

    private static readonly Dictionary<ushort, TlsSignatureRule> Rules = new()
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
    };

    // TLS 1.2 binds an ecdsa_* scheme to its hash only, not to a curve (RFC 8422 section 5.1.1).
    private static readonly Dictionary<ushort, TlsSignatureRule> Tls12Rules = new(Rules)
    {
        [RsaPkcs1Sha1] = new(TlsSignatureKind.RsaPkcs1, RsaEncryptionOid, null, HashAlgorithmName.SHA1),
        [RsaPkcs1Sha256] = new(TlsSignatureKind.RsaPkcs1, RsaEncryptionOid, null, HashAlgorithmName.SHA256),
        [RsaPkcs1Sha384] = new(TlsSignatureKind.RsaPkcs1, RsaEncryptionOid, null, HashAlgorithmName.SHA384),
        [RsaPkcs1Sha512] = new(TlsSignatureKind.RsaPkcs1, RsaEncryptionOid, null, HashAlgorithmName.SHA512),
        [EcdsaSha1] = new(TlsSignatureKind.Ecdsa, EcPublicKeyOid, null, HashAlgorithmName.SHA1),
        [EcdsaSecp256r1Sha256] = new(TlsSignatureKind.Ecdsa, EcPublicKeyOid, null, HashAlgorithmName.SHA256),
        [EcdsaSecp384r1Sha384] = new(TlsSignatureKind.Ecdsa, EcPublicKeyOid, null, HashAlgorithmName.SHA384),
        [EcdsaSecp521r1Sha512] = new(TlsSignatureKind.Ecdsa, EcPublicKeyOid, null, HashAlgorithmName.SHA512),
    };

    private static readonly TlsSignatureRule LegacyRsaRule = new(TlsSignatureKind.RsaMd5Sha1, RsaEncryptionOid, null, default);

    private static readonly TlsSignatureRule LegacyEcdsaRule = new(TlsSignatureKind.Ecdsa, EcPublicKeyOid, null, HashAlgorithmName.SHA1);

    /// <summary>Returns whether a TLS 1.3 CertificateVerify may carry <paramref name="scheme" /> and the client can check and sign it.</summary>
    /// <param name="scheme">The signature scheme code point.</param>
    /// <returns><see langword="true" /> for the RSA-PSS, ECDSA and Ed25519 schemes.</returns>
    public static bool IsCertificateVerifyScheme(ushort scheme) => Rules.ContainsKey(scheme);

    /// <summary>Returns whether a TLS 1.2 ServerKeyExchange or CertificateVerify may carry <paramref name="scheme" /> and the client can check and sign it.</summary>
    /// <param name="scheme">The signature scheme code point.</param>
    /// <returns><see langword="true" /> for the TLS 1.3 schemes, RSA PKCS #1 v1.5 and ECDSA with SHA-1.</returns>
    public static bool IsTls12Scheme(ushort scheme) => Tls12Rules.ContainsKey(scheme);

    /// <summary>Gets the TLS 1.0 and 1.1 signatures, RSA over MD5 and SHA-1 then ECDSA over SHA-1, for a signer to pick the one its key fits.</summary>
    internal static IReadOnlyList<TlsSignatureRule> LegacyRules { get; } = [LegacyRsaRule, LegacyEcdsaRule];

    internal static TlsSignatureRule? FindRule(ushort scheme) => Rules.GetValueOrDefault(scheme);

    internal static TlsSignatureRule? FindTls12Rule(ushort scheme) => Tls12Rules.GetValueOrDefault(scheme);

    /// <summary>
    /// Returns the one signature TLS 1.0 and 1.1 make with a key of
    /// <paramref name="keyOid" /> (RFC 4346 section 7.4.3, RFC 8422 section 5.10): RSA over
    /// MD5 and SHA-1, or ECDSA over SHA-1; any other key signs nothing.
    /// </summary>
    internal static TlsSignatureRule? FindLegacyRule(string keyOid) => keyOid switch
    {
        RsaEncryptionOid => LegacyRsaRule,
        EcPublicKeyOid => LegacyEcdsaRule,
        _ => null,
    };

    /// <summary>
    /// Returns the PKCS #1 block type 1 TLS 1.0 and 1.1 sign with RSA (RFC 2246 section
    /// 7.4.3): <c>00 01 FF..FF 00</c>, then MD5 and SHA-1 of <paramref name="content" />,
    /// <paramref name="modulusLength" /> bytes in all.
    /// </summary>
    internal static byte[] BuildMd5Sha1Block(byte[] content, int modulusLength)
    {
        byte[] hashes = [.. CryptographicOperations.HashData(HashAlgorithmName.MD5, content), .. CryptographicOperations.HashData(HashAlgorithmName.SHA1, content)];
        byte[] block = new byte[modulusLength];
        block[1] = 0x01;
        block.AsSpan(2, modulusLength - hashes.Length - 3).Fill(0xff);
        hashes.CopyTo(block, modulusLength - hashes.Length);
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
