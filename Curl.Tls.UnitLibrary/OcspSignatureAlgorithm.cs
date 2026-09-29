using System.Formats.Asn1;
using System.Security.Cryptography;

namespace Curl.Tls;

/// <summary>
/// Turns the X.509 <c>AlgorithmIdentifier</c>s an OCSP response and a responder
/// certificate are signed with into the <see cref="TlsSignatureRule" /> that checks them:
/// RSA PKCS #1 v1.5 and ECDSA with SHA-1, SHA-256, SHA-384 and SHA-512 (RFC 4055, RFC
/// 5758), RSASSA-PSS with its hash from the parameters (RFC 4055 section 3.1), and Ed25519
/// (RFC 8410); and names the hashes an OCSP <c>CertID</c> may use.
/// </summary>
internal static class OcspSignatureAlgorithm
{
    private static readonly Asn1Tag PssHashTag = new(TagClass.ContextSpecific, 0, true);

    private static readonly Dictionary<string, HashAlgorithmName> Hashes = new()
    {
        ["1.3.14.3.2.26"] = HashAlgorithmName.SHA1,
        ["2.16.840.1.101.3.4.2.1"] = HashAlgorithmName.SHA256,
        ["2.16.840.1.101.3.4.2.2"] = HashAlgorithmName.SHA384,
        ["2.16.840.1.101.3.4.2.3"] = HashAlgorithmName.SHA512,
    };

    private static readonly Dictionary<string, TlsSignatureRule> Rules = new()
    {
        ["1.2.840.113549.1.1.5"] = new(TlsSignatureKind.RsaPkcs1, TlsSignatureScheme.RsaEncryptionOid, null, HashAlgorithmName.SHA1),
        ["1.2.840.113549.1.1.11"] = new(TlsSignatureKind.RsaPkcs1, TlsSignatureScheme.RsaEncryptionOid, null, HashAlgorithmName.SHA256),
        ["1.2.840.113549.1.1.12"] = new(TlsSignatureKind.RsaPkcs1, TlsSignatureScheme.RsaEncryptionOid, null, HashAlgorithmName.SHA384),
        ["1.2.840.113549.1.1.13"] = new(TlsSignatureKind.RsaPkcs1, TlsSignatureScheme.RsaEncryptionOid, null, HashAlgorithmName.SHA512),
        ["1.2.840.10045.4.1"] = new(TlsSignatureKind.Ecdsa, TlsSignatureScheme.EcPublicKeyOid, null, HashAlgorithmName.SHA1),
        ["1.2.840.10045.4.3.2"] = new(TlsSignatureKind.Ecdsa, TlsSignatureScheme.EcPublicKeyOid, null, HashAlgorithmName.SHA256),
        ["1.2.840.10045.4.3.3"] = new(TlsSignatureKind.Ecdsa, TlsSignatureScheme.EcPublicKeyOid, null, HashAlgorithmName.SHA384),
        ["1.2.840.10045.4.3.4"] = new(TlsSignatureKind.Ecdsa, TlsSignatureScheme.EcPublicKeyOid, null, HashAlgorithmName.SHA512),
        [TlsSignatureScheme.Ed25519Oid] = new(TlsSignatureKind.Ed25519, TlsSignatureScheme.Ed25519Oid, null, default),
    };

    /// <summary>Returns the hash an OCSP <c>CertID</c>'s <c>hashAlgorithm</c> OID names, or <see langword="null" /> for one the client does not know.</summary>
    /// <param name="oid">The hash algorithm OID.</param>
    /// <returns>The hash, or <see langword="null" />.</returns>
    public static HashAlgorithmName? FindHash(string oid) => Hashes.TryGetValue(oid, out HashAlgorithmName hash) ? hash : null;

    /// <summary>Returns the rule that checks a signature made with <paramref name="algorithmIdentifier" /> by <paramref name="signer" />.</summary>
    /// <param name="algorithmIdentifier">The DER <c>AlgorithmIdentifier</c>.</param>
    /// <param name="signer">The signer's public key; an RSASSA-PSS signature binds to its key OID.</param>
    /// <returns>The rule, or <see langword="null" /> for an algorithm the client does not know.</returns>
    /// <exception cref="AsnContentException">The bytes are not an <c>AlgorithmIdentifier</c>.</exception>
    public static TlsSignatureRule? FindRule(byte[] algorithmIdentifier, TlsCertificatePublicKey signer)
    {
        AsnReader algorithm = new AsnReader(algorithmIdentifier, AsnEncodingRules.DER).ReadSequence();
        string oid = algorithm.ReadObjectIdentifier();
        return oid == TlsSignatureScheme.RsaSsaPssOid
            ? FindRsaPssRule(algorithm.ReadSequence(), signer.AlgorithmOid)
            : Rules.GetValueOrDefault(oid);
    }

    /// <summary>RFC 4055's <c>RSASSA-PSS-params</c>: the hash is SHA-1 unless the parameters name another.</summary>
    private static TlsSignatureRule? FindRsaPssRule(AsnReader parameters, string keyOid)
    {
        HashAlgorithmName? hash = parameters.HasData && parameters.PeekTag().HasSameClassAndValue(PssHashTag)
            ? FindHash(parameters.ReadSequence(PssHashTag).ReadSequence().ReadObjectIdentifier())
            : HashAlgorithmName.SHA1;
        string ruleKeyOid = keyOid == TlsSignatureScheme.RsaSsaPssOid ? keyOid : TlsSignatureScheme.RsaEncryptionOid;
        return hash is { } name ? new TlsSignatureRule(TlsSignatureKind.RsaPss, ruleKeyOid, null, name) : null;
    }
}
