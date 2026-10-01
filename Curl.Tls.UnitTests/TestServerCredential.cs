using System.Formats.Asn1;
using System.Numerics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Curl.Cryptography;
using X509CertificateRequest = System.Security.Cryptography.X509Certificates.CertificateRequest;

namespace Curl.Tls;

/// <summary>
/// A generated certificate, its signing key and the signature scheme it signs with, for
/// the in-memory server (and as a client certificate). RSA and ECDSA certificates come
/// from the BCL's <see cref="X509CertificateRequest" />; the RSA-PSS and Ed25519 keys, which
/// it cannot certify, get a certificate whose <c>SubjectPublicKeyInfo</c> is written here
/// and whose issuer signature is made with a throwaway ECDSA key - the client never
/// checks it, the verifier does.
/// </summary>
internal sealed record TestServerCredential(byte[] Certificate, TlsSigningKey SigningKey, ushort Scheme)
{
    /// <summary>Gets the RSA private key of an <see cref="Rsa" /> credential, for TLS 1.2 RSA key exchange and TLS 1.0 and 1.1 signatures.</summary>
    public RSA? RsaKey { get; init; }

    public static TestServerCredential Rsa(ushort scheme)
    {
        RSA key = RSA.Create(2048);
        return new(SelfSigned(new X509CertificateRequest("CN=rsa", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)), new RsaTlsSigningKey(key), scheme)
        {
            RsaKey = key,
        };
    }

    /// <summary>A certificate whose <c>SubjectPublicKeyInfo</c> says <paramref name="algorithmOid" /> over <paramref name="keyBits" />, with no usable private key.</summary>
    public static TestServerCredential Foreign(string algorithmOid, byte[] keyBits) =>
        new(WithForeignKey("CN=foreign", new PublicKey(new Oid(algorithmOid), null, new AsnEncodedData(keyBits))), new Ed25519TlsSigningKey(new byte[32]), TlsSignatureScheme.Ed25519);

    /// <summary>Gets a value indicating whether the server signs with <see cref="TestDsaKey" /> rather than <see cref="SigningKey" />, which is then a placeholder.</summary>
    public bool SignsWithDsa { get; init; }

    /// <summary>A DSA certificate over <see cref="TestDsaKey" />, whose signatures the server makes itself: the library signs nothing with DSA.</summary>
    public static TestServerCredential Dsa(ushort scheme)
    {
        PublicKey publicKey = new(new Oid(TlsSignatureScheme.DsaOid), new AsnEncodedData(TestDsaKey.EncodeDomainParameters()), new AsnEncodedData(TestDsaKey.EncodePublicKey()));
        return new(WithForeignKey("CN=dsa", publicKey), new Ed25519TlsSigningKey(new byte[32]), scheme) { SignsWithDsa = true };
    }

    /// <summary>Gets the brainpool ECDSA key the server signs with, rather than <see cref="SigningKey" />, which is then a placeholder.</summary>
    public BrainpoolEcdsa? BrainpoolKey { get; init; }

    /// <summary>A brainpool ECDSA certificate on <paramref name="curve" />, whose signatures the server makes with <c>Curl.Cryptography</c>'s <see cref="BrainpoolEcdsa" />.</summary>
    public static TestServerCredential Brainpool(BrainpoolCurve curve, string curveOid, ushort scheme)
    {
        byte[] privateKey = new byte[BrainpoolEcdh.GetPrivateKeyLength(curve)];
        BrainpoolEcdh.GeneratePrivateKey(curve, privateKey);
        BrainpoolEcdsa key = new(curve, privateKey);
        byte[] point = new byte[BrainpoolEcdh.GetPublicKeyLength(curve)];
        key.ExportPublicKey(point);
        AsnWriter parameters = new(AsnEncodingRules.DER);
        parameters.WriteObjectIdentifier(curveOid);
        PublicKey publicKey = new(new Oid(TlsSignatureScheme.EcPublicKeyOid), new AsnEncodedData(parameters.Encode()), new AsnEncodedData(point));
        return new(WithForeignKey("CN=brainpool", publicKey), new Ed25519TlsSigningKey(new byte[32]), scheme) { BrainpoolKey = key };
    }

    /// <summary>Gets the signer of a key the library cannot sign with (Ed448, ML-DSA), rather than <see cref="SigningKey" />, which is then a placeholder.</summary>
    public Func<byte[], byte[]>? ContentSigner { get; init; }

    /// <summary>An Ed448 certificate, whose signatures the server makes with <c>Curl.Cryptography</c>'s <see cref="Cryptography.Ed448" />.</summary>
    public static TestServerCredential Ed448()
    {
        byte[] privateKey = new byte[Cryptography.Ed448.PrivateKeySize];
        Cryptography.Ed448.GeneratePrivateKey(privateKey);
        byte[] rawPublicKey = new byte[Cryptography.Ed448.PublicKeySize];
        Cryptography.Ed448.ComputePublicKey(privateKey, rawPublicKey);
        PublicKey publicKey = new(new Oid(TlsSignatureScheme.Ed448Oid), null, new AsnEncodedData(rawPublicKey));
        return new(WithForeignKey("CN=ed448", publicKey), new Ed25519TlsSigningKey(new byte[32]), TlsSignatureScheme.Ed448)
        {
            ContentSigner = content =>
            {
                byte[] signature = new byte[Cryptography.Ed448.SignatureSize];
                Cryptography.Ed448.Sign(privateKey, content, signature);
                return signature;
            },
        };
    }

    /// <summary>An ML-DSA certificate of <paramref name="parameterSet" />, whose signatures the server makes with <c>Curl.Cryptography</c>'s <see cref="Cryptography.MlDsa" />.</summary>
    public static TestServerCredential MlDsa(MlDsaParameterSet parameterSet, string algorithmOid, ushort scheme)
    {
        Cryptography.MlDsa key = Cryptography.MlDsa.GenerateKey(parameterSet);
        byte[] rawPublicKey = new byte[Cryptography.MlDsa.GetPublicKeySize(parameterSet)];
        key.ExportPublicKey(rawPublicKey);
        PublicKey publicKey = new(new Oid(algorithmOid), null, new AsnEncodedData(rawPublicKey));
        return new(WithForeignKey("CN=mldsa", publicKey), new Ed25519TlsSigningKey(new byte[32]), scheme)
        {
            ContentSigner = content =>
            {
                byte[] signature = new byte[Cryptography.MlDsa.GetSignatureSize(parameterSet)];
                key.SignData(content, [], signature);
                return signature;
            },
        };
    }

    /// <summary>Signs <paramref name="content" /> by <paramref name="rule" /> with this credential's key.</summary>
    public byte[] Sign(TlsSignatureRule rule, byte[] content) => this switch
    {
        { SignsWithDsa: true } => TestDsaKey.Sign(rule.Hash, content),
        { BrainpoolKey: { } brainpool } => SignBrainpool(brainpool, rule.Hash, content),
        { ContentSigner: { } signer } => signer(content),
        _ => SigningKey.SignByRule(rule, content),
    };

    /// <summary>Signs a TLS 1.3 CertificateVerify's <paramref name="content" /> with <see cref="Scheme" />: by hand for a key the library cannot sign with, otherwise with <see cref="SigningKey" />, which refuses a scheme that does not fit it.</summary>
    public byte[] SignCertificateVerify(byte[] content) => BrainpoolKey is not null || ContentSigner is not null
        ? Sign(TlsSignatureScheme.FindRule(Scheme)!, content)
        : SigningKey.Sign(Scheme, content);

    /// <summary>Signs with a brainpool key and encodes r and s as the DER <c>ECDSA-Sig-Value</c> TLS carries.</summary>
    private static byte[] SignBrainpool(BrainpoolEcdsa key, HashAlgorithmName hash, byte[] content)
    {
        byte[] rs = new byte[key.SignatureLength];
        key.SignHash(DsaSignature.HashData(content, hash), hash, rs);
        AsnWriter writer = new(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            writer.WriteInteger(new BigInteger(rs.AsSpan(0, rs.Length / 2), isUnsigned: true, isBigEndian: true));
            writer.WriteInteger(new BigInteger(rs.AsSpan(rs.Length / 2), isUnsigned: true, isBigEndian: true));
        }

        return writer.Encode();
    }

    public static TestServerCredential RsaPss(ushort scheme)
    {
        RSA key = RSA.Create(2048);
        PublicKey publicKey = new(new Oid(TlsSignatureScheme.RsaSsaPssOid), null, new AsnEncodedData(key.ExportRSAPublicKey()));
        return new(WithForeignKey("CN=rsa-pss", publicKey), new RsaTlsSigningKey(key, certifiedAsPss: true), scheme);
    }

    public static TestServerCredential Ecdsa(ECCurve curve, ushort scheme)
    {
        ECDsa key = ECDsa.Create(curve);
        return new(SelfSigned(new X509CertificateRequest("CN=ecdsa", key, HashAlgorithmName.SHA256)), new EcdsaTlsSigningKey(key), scheme);
    }

    public static TestServerCredential Ed25519()
    {
        byte[] privateKey = RandomNumberGenerator.GetBytes(Cryptography.Ed25519.PrivateKeySize);
        byte[] rawPublicKey = new byte[Cryptography.Ed25519.PublicKeySize];
        Cryptography.Ed25519.ComputePublicKey(privateKey, rawPublicKey);
        PublicKey publicKey = new(new Oid(TlsSignatureScheme.Ed25519Oid), null, new AsnEncodedData(rawPublicKey));
        return new(WithForeignKey("CN=ed25519", publicKey), new Ed25519TlsSigningKey(privateKey), TlsSignatureScheme.Ed25519);
    }

    private static byte[] SelfSigned(X509CertificateRequest request)
    {
        using X509Certificate2 certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));
        return certificate.RawData;
    }

    private static byte[] WithForeignKey(string subject, PublicKey publicKey)
    {
        using ECDsa issuerKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        X500DistinguishedName name = new(subject);
        X509CertificateRequest request = new(name, publicKey, HashAlgorithmName.SHA256);
        using X509Certificate2 certificate = request.Create(
            name,
            X509SignatureGenerator.CreateForECDsa(issuerKey),
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(30),
            [1]);
        return certificate.RawData;
    }
}
