using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Curl.Tls;

namespace Curl.Networking.Fakes.Tls13Server;

/// <summary>
/// Writes a DER <c>OCSPResponse</c> (RFC 6960 section 4.2.1) with <see cref="AsnWriter" />
/// for one certificate, with every field the tests vary settable: the response status,
/// the <c>CertID</c> and its hash, the certificate status, the times, the responder ID,
/// the signer and the certificates carried.
/// </summary>
internal sealed record OcspResponseBuilder(byte[] Certificate, byte[] Issuer, DateTimeOffset Now)
{
    public const string Sha1Oid = "1.3.14.3.2.26";
    public const string Sha256Oid = "2.16.840.1.101.3.4.2.1";
    public const string EcdsaSha256Oid = "1.2.840.10045.4.3.2";
    public const string RsaSha256Oid = "1.2.840.113549.1.1.11";
    public const string RsaPssOid = "1.2.840.113549.1.1.10";

    private const string BasicResponseOid = "1.3.6.1.5.5.7.48.1.1";
    private const string NonceOid = "1.3.6.1.5.5.7.48.1.2";

    private enum Enumerated
    {
    }

    public int ResponseStatus { get; init; }

    public bool OmitResponseBytes { get; init; }

    public string ResponseType { get; init; } = BasicResponseOid;

    public OcspStapleStatus CertStatus { get; init; } = OcspStapleStatus.Good;

    /// <summary>Gets the CRL reason of a revoked status, or <see langword="null" /> to give none.</summary>
    public int? RevocationReason { get; init; }

    public string CertIdHashOid { get; init; } = Sha1Oid;

    public byte[]? SerialNumber { get; init; }

    public byte[]? IssuerNameHash { get; init; }

    public byte[]? IssuerKeyHash { get; init; }

    public TimeSpan ThisUpdateOffset { get; init; } = TimeSpan.FromHours(-1);

    /// <summary>Gets <c>nextUpdate</c> relative to <see cref="Now" />, or <see langword="null" /> to omit it.</summary>
    public TimeSpan? NextUpdateOffset { get; init; } = TimeSpan.FromDays(1);

    public bool IncludeVersion { get; init; }

    public bool IncludeResponseExtensions { get; init; }

    public bool IncludeSingleExtensions { get; init; }

    /// <summary>Gets the DER <c>Name</c> of a <c>byName</c> responder ID; <see cref="ResponderKeyHash" /> is used when it is <see langword="null" />.</summary>
    public byte[]? ResponderName { get; init; }

    public byte[]? ResponderKeyHash { get; init; }

    /// <summary>Gets the DER <c>AlgorithmIdentifier</c> and the signing function.</summary>
    public (byte[] Algorithm, Func<byte[], byte[]> Sign) Signer { get; init; }

    public IReadOnlyList<byte[]> Certificates { get; init; } = [];

    public bool CorruptSignature { get; init; }

    public static (byte[] Algorithm, Func<byte[], byte[]> Sign) EcdsaSigner(ECDsa key) =>
        (AlgorithmIdentifier(EcdsaSha256Oid, null), data => key.SignData(data, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence));

    public static (byte[] Algorithm, Func<byte[], byte[]> Sign) RsaSigner(RSA key) =>
        (AlgorithmIdentifier(RsaSha256Oid, null), data => key.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));

    /// <summary>An RSASSA-PSS signer; <paramref name="hashOid" /> <see langword="null" /> leaves the parameters empty, which means SHA-1.</summary>
    public static (byte[] Algorithm, Func<byte[], byte[]> Sign) RsaPssSigner(RSA key, string? hashOid, HashAlgorithmName hash) =>
        (PssAlgorithmIdentifier(hashOid), data => key.SignData(data, hash, RSASignaturePadding.Pss));

    public static byte[] AlgorithmIdentifier(string oid, byte[]? parameters)
    {
        AsnWriter writer = new(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            writer.WriteObjectIdentifier(oid);
            if (parameters is not null)
            {
                writer.WriteEncodedValue(parameters);
            }
        }

        return writer.Encode();
    }

    public static byte[] PssAlgorithmIdentifier(string? hashOid)
    {
        AsnWriter parameters = new(AsnEncodingRules.DER);
        using (parameters.PushSequence())
        {
            if (hashOid is not null)
            {
                using (parameters.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 0)))
                {
                    parameters.WriteEncodedValue(AlgorithmIdentifier(hashOid, null));
                }
            }
        }

        return AlgorithmIdentifier(RsaPssOid, parameters.Encode());
    }

    public byte[] Build()
    {
        AsnWriter writer = new(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            writer.WriteEnumeratedValue((Enumerated)ResponseStatus);
            if (!OmitResponseBytes)
            {
                using (writer.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 0)))
                using (writer.PushSequence())
                {
                    writer.WriteObjectIdentifier(ResponseType);
                    writer.WriteOctetString(BuildBasic());
                }
            }
        }

        return writer.Encode();
    }

    private byte[] BuildBasic()
    {
        byte[] responseData = BuildResponseData();
        byte[] signature = Signer.Sign(responseData);
        if (CorruptSignature)
        {
            signature[^1] ^= 0x01;
        }

        AsnWriter writer = new(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            writer.WriteEncodedValue(responseData);
            writer.WriteEncodedValue(Signer.Algorithm);
            writer.WriteBitString(signature);
            if (Certificates.Count > 0)
            {
                using (writer.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 0)))
                using (writer.PushSequence())
                {
                    foreach (byte[] certificate in Certificates)
                    {
                        writer.WriteEncodedValue(certificate);
                    }
                }
            }
        }

        return writer.Encode();
    }

    private byte[] BuildResponseData()
    {
        AsnWriter writer = new(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            if (IncludeVersion)
            {
                using (writer.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 0)))
                {
                    writer.WriteInteger(0);
                }
            }

            WriteResponderId(writer);
            writer.WriteGeneralizedTime(Now.AddMinutes(-1), omitFractionalSeconds: true);
            using (writer.PushSequence())
            {
                WriteSingleResponse(writer);
            }

            if (IncludeResponseExtensions)
            {
                using (writer.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 1)))
                {
                    WriteNonceExtensions(writer);
                }
            }
        }

        return writer.Encode();
    }

    private void WriteResponderId(AsnWriter writer)
    {
        if (ResponderName is not null)
        {
            using (writer.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 1)))
            {
                writer.WriteEncodedValue(ResponderName);
            }

            return;
        }

        using (writer.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 2)))
        {
            writer.WriteOctetString(ResponderKeyHash);
        }
    }

    private void WriteSingleResponse(AsnWriter writer)
    {
        using X509Certificate2 certificate = X509CertificateLoader.LoadCertificate(Certificate);
        using X509Certificate2 issuer = X509CertificateLoader.LoadCertificate(Issuer);
        HashAlgorithmName hash = CertIdHashOid == Sha1Oid ? HashAlgorithmName.SHA1 : HashAlgorithmName.SHA256;
        using (writer.PushSequence())
        {
            using (writer.PushSequence())
            {
                using (writer.PushSequence())
                {
                    writer.WriteObjectIdentifier(CertIdHashOid);
                    writer.WriteNull();
                }

                writer.WriteOctetString(IssuerNameHash ?? CryptographicOperations.HashData(hash, issuer.SubjectName.RawData));
                writer.WriteOctetString(IssuerKeyHash ?? CryptographicOperations.HashData(hash, issuer.PublicKey.EncodedKeyValue.RawData));
                writer.WriteIntegerUnsigned(SerialNumber ?? certificate.SerialNumberBytes.Span);
            }

            WriteCertStatus(writer);
            writer.WriteGeneralizedTime(Now + ThisUpdateOffset, omitFractionalSeconds: true);
            if (NextUpdateOffset is { } nextUpdate)
            {
                using (writer.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 0)))
                {
                    writer.WriteGeneralizedTime(Now + nextUpdate, omitFractionalSeconds: true);
                }
            }

            if (IncludeSingleExtensions)
            {
                using (writer.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 1)))
                {
                    WriteNonceExtensions(writer);
                }
            }
        }
    }

    private void WriteCertStatus(AsnWriter writer)
    {
        if (CertStatus == OcspStapleStatus.Good)
        {
            writer.WriteNull(new Asn1Tag(TagClass.ContextSpecific, 0));
            return;
        }

        if (CertStatus == OcspStapleStatus.Unknown)
        {
            writer.WriteNull(new Asn1Tag(TagClass.ContextSpecific, 2));
            return;
        }

        if (CertStatus == OcspStapleStatus.Malformed)
        {
            // No CertStatus choice has tag [3].
            writer.WriteNull(new Asn1Tag(TagClass.ContextSpecific, 3));
            return;
        }

        using (writer.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 1)))
        {
            writer.WriteGeneralizedTime(Now.AddDays(-2), omitFractionalSeconds: true);
            if (RevocationReason is { } reason)
            {
                using (writer.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 0)))
                {
                    writer.WriteEnumeratedValue((Enumerated)reason);
                }
            }
        }
    }

    private static void WriteNonceExtensions(AsnWriter writer)
    {
        using (writer.PushSequence())
        using (writer.PushSequence())
        {
            writer.WriteObjectIdentifier(NonceOid);
            writer.WriteOctetString([0x04, 0x02, 0x01, 0x02]);
        }
    }
}
