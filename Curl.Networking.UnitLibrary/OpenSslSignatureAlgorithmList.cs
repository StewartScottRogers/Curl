using System.Collections.Frozen;

using Curl.Tls;

namespace Curl.Networking;

/// <summary>
/// Reads curl's <c>--sigalgs</c> value as OpenSSL 3.5's <c>SSL_CTX_set1_sigalgs_list</c>
/// reads it (measured against Ubuntu's curl 8.18.0 with OpenSSL 3.5.5, BL-709, ADR-0284):
/// names separated by <c>:</c>, matched without regard to case, each either a
/// <c>signature+hash</c> pair such as <c>ECDSA+SHA256</c> or a scheme's own name such as
/// <c>rsa_pss_rsae_sha256</c>. A repeated scheme is offered once, and the SHA-1 schemes
/// OpenSSL's default security level forbids are left out.
/// </summary>
internal static class OpenSslSignatureAlgorithmList
{
    private static readonly FrozenDictionary<string, ushort> SchemesByName = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase)
    {
        ["RSA+SHA1"] = TlsSignatureScheme.RsaPkcs1Sha1,
        ["RSA+SHA224"] = 0x0301,
        ["RSA+SHA256"] = TlsSignatureScheme.RsaPkcs1Sha256,
        ["RSA+SHA384"] = TlsSignatureScheme.RsaPkcs1Sha384,
        ["RSA+SHA512"] = TlsSignatureScheme.RsaPkcs1Sha512,
        ["DSA+SHA1"] = TlsSignatureScheme.DsaSha1,
        ["DSA+SHA224"] = TlsSignatureScheme.DsaSha224,
        ["DSA+SHA256"] = TlsSignatureScheme.DsaSha256,
        ["DSA+SHA384"] = TlsSignatureScheme.DsaSha384,
        ["DSA+SHA512"] = TlsSignatureScheme.DsaSha512,
        ["ECDSA+SHA1"] = TlsSignatureScheme.EcdsaSha1,
        ["ECDSA+SHA224"] = 0x0303,
        ["ECDSA+SHA256"] = TlsSignatureScheme.EcdsaSecp256r1Sha256,
        ["ECDSA+SHA384"] = TlsSignatureScheme.EcdsaSecp384r1Sha384,
        ["ECDSA+SHA512"] = TlsSignatureScheme.EcdsaSecp521r1Sha512,
        ["RSA-PSS+SHA256"] = TlsSignatureScheme.RsaPssRsaeSha256,
        ["RSA-PSS+SHA384"] = TlsSignatureScheme.RsaPssRsaeSha384,
        ["RSA-PSS+SHA512"] = TlsSignatureScheme.RsaPssRsaeSha512,
        ["PSS+SHA256"] = TlsSignatureScheme.RsaPssRsaeSha256,
        ["PSS+SHA384"] = TlsSignatureScheme.RsaPssRsaeSha384,
        ["PSS+SHA512"] = TlsSignatureScheme.RsaPssRsaeSha512,
        ["rsa_pkcs1_sha1"] = TlsSignatureScheme.RsaPkcs1Sha1,
        ["rsa_pkcs1_sha224"] = 0x0301,
        ["rsa_pkcs1_sha256"] = TlsSignatureScheme.RsaPkcs1Sha256,
        ["rsa_pkcs1_sha384"] = TlsSignatureScheme.RsaPkcs1Sha384,
        ["rsa_pkcs1_sha512"] = TlsSignatureScheme.RsaPkcs1Sha512,
        ["dsa_sha1"] = TlsSignatureScheme.DsaSha1,
        ["dsa_sha224"] = TlsSignatureScheme.DsaSha224,
        ["dsa_sha256"] = TlsSignatureScheme.DsaSha256,
        ["dsa_sha384"] = TlsSignatureScheme.DsaSha384,
        ["dsa_sha512"] = TlsSignatureScheme.DsaSha512,
        ["ecdsa_sha1"] = TlsSignatureScheme.EcdsaSha1,
        ["ecdsa_sha224"] = 0x0303,
        ["ecdsa_secp256r1_sha256"] = TlsSignatureScheme.EcdsaSecp256r1Sha256,
        ["ecdsa_secp384r1_sha384"] = TlsSignatureScheme.EcdsaSecp384r1Sha384,
        ["ecdsa_secp521r1_sha512"] = TlsSignatureScheme.EcdsaSecp521r1Sha512,
        ["rsa_pss_rsae_sha256"] = TlsSignatureScheme.RsaPssRsaeSha256,
        ["rsa_pss_rsae_sha384"] = TlsSignatureScheme.RsaPssRsaeSha384,
        ["rsa_pss_rsae_sha512"] = TlsSignatureScheme.RsaPssRsaeSha512,
        ["rsa_pss_pss_sha256"] = TlsSignatureScheme.RsaPssPssSha256,
        ["rsa_pss_pss_sha384"] = TlsSignatureScheme.RsaPssPssSha384,
        ["rsa_pss_pss_sha512"] = TlsSignatureScheme.RsaPssPssSha512,
        ["ed25519"] = TlsSignatureScheme.Ed25519,
        ["ed448"] = 0x0808,
        ["ecdsa_brainpoolP256r1tls13_sha256"] = 0x081a,
        ["ecdsa_brainpoolP384r1tls13_sha384"] = 0x081b,
        ["ecdsa_brainpoolP512r1tls13_sha512"] = 0x081c,
        ["mldsa44"] = 0x0904,
        ["mldsa65"] = 0x0905,
        ["mldsa87"] = 0x0906,
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    // SHA-1 signatures, which OpenSSL 3's default security level leaves out of the offer.
    private static readonly FrozenSet<ushort> Sha1Schemes =
        new[] { TlsSignatureScheme.RsaPkcs1Sha1, TlsSignatureScheme.DsaSha1, TlsSignatureScheme.EcdsaSha1 }.ToFrozenSet();

    /// <summary>Reads a <c>--sigalgs</c> value.</summary>
    /// <param name="value">The value, verbatim.</param>
    /// <returns>
    /// The schemes to offer, in order, without SHA-1 (so possibly none), or
    /// <see langword="null" /> when OpenSSL refuses the value: an empty entry or an unknown name.
    /// </returns>
    public static IReadOnlyList<ushort>? Parse(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var schemes = new List<ushort>();
        foreach (var name in value.Split(':'))
        {
            if (!SchemesByName.TryGetValue(name, out var scheme))
            {
                return null;
            }

            schemes.Add(scheme);
        }

        return [.. schemes.Distinct().Where(scheme => !Sha1Schemes.Contains(scheme))];
    }
}
