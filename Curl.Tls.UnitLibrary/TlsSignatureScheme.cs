using System.Security.Cryptography;

namespace Curl.Tls;

/// <summary>
/// The TLS 1.3 signature schemes (RFC 8446 section 4.2.3) a CertificateVerify can carry
/// and that the client can check or sign: RSA-PSS with RSAE and PSS keys, ECDSA on the
/// three NIST curves, and Ed25519.
/// </summary>
public static class TlsSignatureScheme
{
    /// <summary><c>rsa_pkcs1_sha256</c>: offered for certificates only, never a TLS 1.3 CertificateVerify.</summary>
    public const ushort RsaPkcs1Sha256 = 0x0401;

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

    /// <summary>Returns whether a TLS 1.3 CertificateVerify may carry <paramref name="scheme" /> and the client can check and sign it.</summary>
    /// <param name="scheme">The signature scheme code point.</param>
    /// <returns><see langword="true" /> for the RSA-PSS, ECDSA and Ed25519 schemes.</returns>
    public static bool IsCertificateVerifyScheme(ushort scheme) => Rules.ContainsKey(scheme);

    internal static TlsSignatureRule? FindRule(ushort scheme) => Rules.GetValueOrDefault(scheme);

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
