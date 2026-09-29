using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using X509CertificateRequest = System.Security.Cryptography.X509Certificates.CertificateRequest;

namespace Curl.Tls;

/// <summary>
/// A test CA (ECDSA P-256) and a leaf it issued for <c>localhost</c>, made with the BCL's
/// <see cref="X509CertificateRequest" />, plus delegated OCSP responders and a
/// <see cref="OcspResponseBuilder" /> that signs as the CA by default.
/// </summary>
internal sealed class OcspTestPki : IDisposable
{
    public const string OcspSigningOid = "1.3.6.1.5.5.7.3.9";

    public OcspTestPki()
    {
        X509CertificateRequest caRequest = new("CN=Test OCSP CA", CaKey, HashAlgorithmName.SHA256);
        caRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        Ca = caRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));
        Leaf = Issue(new X509CertificateRequest("CN=localhost", LeafKey, HashAlgorithmName.SHA256), [0x01, 0x23, 0x45]);
    }

    public ECDsa CaKey { get; } = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    public X509Certificate2 Ca { get; }

    public ECDsa LeafKey { get; } = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    public X509Certificate2 Leaf { get; }

    /// <summary>Gets the chain a server presents: the leaf, then the CA.</summary>
    public IReadOnlyList<byte[]> Chain => [Leaf.RawData, Ca.RawData];

    public TestServerCredential LeafCredential => new(Leaf.RawData, new EcdsaTlsSigningKey(LeafKey), TlsSignatureScheme.EcdsaSecp256r1Sha256);

    /// <summary>A response builder for the leaf, signed by the CA and naming it by name.</summary>
    public OcspResponseBuilder Response(DateTimeOffset now) => new(Leaf.RawData, Ca.RawData, now)
    {
        Signer = OcspResponseBuilder.EcdsaSigner(CaKey),
        ResponderName = Ca.SubjectName.RawData,
    };

    /// <summary>A responder certificate the CA issues, with the given extended key usages.</summary>
    public X509Certificate2 IssueResponder(ECDsa key, params string[] keyUsages)
    {
        X509CertificateRequest request = new("CN=Test OCSP Responder", key, HashAlgorithmName.SHA256);
        if (keyUsages.Length > 0)
        {
            OidCollection usages = [];
            foreach (string usage in keyUsages)
            {
                usages.Add(new Oid(usage));
            }

            request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(usages, true));
        }

        return Issue(request, [0x77]);
    }

    /// <summary>A responder certificate with OCSP signing that names the CA as issuer but is signed by another key.</summary>
    public X509Certificate2 ForgeResponder(ECDsa key)
    {
        X509CertificateRequest request = new("CN=Forged OCSP Responder", key, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid(OcspSigningOid)], false));
        using ECDsa forger = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return request.Create(Ca.SubjectName, X509SignatureGenerator.CreateForECDsa(forger), DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30), [0x78]);
    }

    /// <summary>
    /// Rebuilds <paramref name="certificate" /> with or without the <c>version</c> field,
    /// with an optional <c>issuerUniqueID</c> and with or without its extensions, keeping
    /// the rest (the outer signature no longer matches).
    /// </summary>
    public static byte[] Rewrite(byte[] certificate, bool version, bool issuerUniqueId, bool extensions)
    {
        AsnReader outer = new AsnReader(certificate, AsnEncodingRules.DER).ReadSequence();
        AsnReader tbs = outer.ReadSequence();
        byte[] algorithm = outer.ReadEncodedValue().ToArray();
        byte[] signature = outer.ReadEncodedValue().ToArray();
        byte[] versionField = tbs.ReadEncodedValue().ToArray();
        List<byte[]> fields = [];
        for (int field = 0; field < 6; field++)
        {
            fields.Add(tbs.ReadEncodedValue().ToArray());
        }

        byte[]? extensionsField = tbs.HasData ? tbs.ReadEncodedValue().ToArray() : null;
        AsnWriter writer = new(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            using (writer.PushSequence())
            {
                if (version)
                {
                    writer.WriteEncodedValue(versionField);
                }

                fields.ForEach(field => writer.WriteEncodedValue(field));
                if (issuerUniqueId)
                {
                    writer.WriteBitString([0x5a], 0, new Asn1Tag(TagClass.ContextSpecific, 1));
                }

                if (extensions && extensionsField is not null)
                {
                    writer.WriteEncodedValue(extensionsField);
                }
            }

            writer.WriteEncodedValue(algorithm);
            writer.WriteEncodedValue(signature);
        }

        return writer.Encode();
    }

    public void Dispose()
    {
        Ca.Dispose();
        Leaf.Dispose();
        CaKey.Dispose();
        LeafKey.Dispose();
    }

    private X509Certificate2 Issue(X509CertificateRequest request, byte[] serial)
    {
        using X509Certificate2 issued = request.Create(Ca, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(29), serial);
        return X509CertificateLoader.LoadCertificate(issued.RawData);
    }
}
