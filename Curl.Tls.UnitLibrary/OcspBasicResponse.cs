using System.Formats.Asn1;
using System.Numerics;

namespace Curl.Tls;

/// <summary>
/// A DER <c>BasicOCSPResponse</c> (RFC 6960 section 4.2.1), read out of the
/// <c>OCSPResponse</c> a server staples: the signed <c>tbsResponseData</c>, its signature,
/// the responder's ID (by name or by key hash), the single responses, and the
/// certificates that came with it.
/// </summary>
/// <param name="ResponseData">The DER <c>tbsResponseData</c>, the bytes the responder signed.</param>
/// <param name="SignatureAlgorithm">The DER <c>AlgorithmIdentifier</c> of the responder's signature.</param>
/// <param name="Signature">The contents of the <c>signature</c> bit string.</param>
/// <param name="ResponderName">The DER <c>Name</c> of a <c>byName</c> responder ID, otherwise <see langword="null" />.</param>
/// <param name="ResponderKeyHash">The SHA-1 key hash of a <c>byKey</c> responder ID, otherwise <see langword="null" />.</param>
/// <param name="Responses">The single responses, in order.</param>
/// <param name="Certificates">The DER certificates in <c>certs</c>, which may hold a delegated responder's.</param>
internal sealed record OcspBasicResponse(
    byte[] ResponseData,
    byte[] SignatureAlgorithm,
    byte[] Signature,
    byte[]? ResponderName,
    byte[]? ResponderKeyHash,
    IReadOnlyList<OcspSingleResponse> Responses,
    IReadOnlyList<byte[]> Certificates)
{
    /// <summary>The <c>responseStatus</c> of a response that carries <c>responseBytes</c>.</summary>
    public const int Successful = 0;

    private const string BasicResponseOid = "1.3.6.1.5.5.7.48.1.1";

    private static readonly Asn1Tag ResponseBytesTag = new(TagClass.ContextSpecific, 0, true);
    private static readonly Asn1Tag VersionTag = new(TagClass.ContextSpecific, 0, true);
    private static readonly Asn1Tag ByNameTag = new(TagClass.ContextSpecific, 1, true);
    private static readonly Asn1Tag ByKeyTag = new(TagClass.ContextSpecific, 2, true);
    private static readonly Asn1Tag ResponseExtensionsTag = new(TagClass.ContextSpecific, 1, true);
    private static readonly Asn1Tag CertificatesTag = new(TagClass.ContextSpecific, 0, true);

    /// <summary>Reads a DER <c>OCSPResponse</c>.</summary>
    /// <param name="ocspResponse">The DER <c>OCSPResponse</c>.</param>
    /// <param name="responseStatus">The <c>responseStatus</c>.</param>
    /// <returns>The basic response, or <see langword="null" /> when <paramref name="responseStatus" /> is not <see cref="Successful" />.</returns>
    /// <exception cref="AsnContentException">The bytes are not a DER <c>OCSPResponse</c> carrying a <c>BasicOCSPResponse</c>.</exception>
    public static OcspBasicResponse? Read(byte[] ocspResponse, out int responseStatus)
    {
        AsnReader outer = new(ocspResponse, AsnEncodingRules.DER);
        AsnReader response = outer.ReadSequence();
        outer.ThrowIfNotEmpty();
        responseStatus = ReadEnumerated(response);
        if (responseStatus != Successful)
        {
            return null;
        }

        AsnReader responseBytes = response.ReadSequence(ResponseBytesTag).ReadSequence();
        response.ThrowIfNotEmpty();
        if (responseBytes.ReadObjectIdentifier() != BasicResponseOid)
        {
            throw new AsnContentException("The OCSP response is not a BasicOCSPResponse.");
        }

        byte[] basic = responseBytes.ReadOctetString();
        responseBytes.ThrowIfNotEmpty();
        return ReadBasic(basic);
    }

    /// <summary>Reads an <c>ENUMERATED</c> value, clamped to <see cref="int" />.</summary>
    /// <param name="reader">The reader positioned at the value.</param>
    /// <returns>The value.</returns>
    /// <exception cref="AsnContentException">The next value is not an <c>ENUMERATED</c>.</exception>
    internal static int ReadEnumerated(AsnReader reader) =>
        (int)BigInteger.Clamp(new BigInteger(reader.ReadEnumeratedBytes().Span, isBigEndian: true), int.MinValue, int.MaxValue);

    private static OcspBasicResponse ReadBasic(byte[] basic)
    {
        AsnReader outer = new(basic, AsnEncodingRules.DER);
        AsnReader sequence = outer.ReadSequence();
        outer.ThrowIfNotEmpty();
        byte[] responseData = sequence.ReadEncodedValue().ToArray();
        byte[] signatureAlgorithm = sequence.ReadEncodedValue().ToArray();
        byte[] signature = sequence.ReadBitString(out _);
        List<byte[]> certificates = ReadCertificates(sequence);
        sequence.ThrowIfNotEmpty();

        AsnReader data = new AsnReader(responseData, AsnEncodingRules.DER).ReadSequence();
        if (data.PeekTag().HasSameClassAndValue(VersionTag))
        {
            data.ReadEncodedValue();
        }

        (byte[]? name, byte[]? keyHash) = ReadResponderId(data);
        data.ReadGeneralizedTime();
        AsnReader list = data.ReadSequence();
        List<OcspSingleResponse> responses = [];
        while (list.HasData)
        {
            responses.Add(OcspSingleResponse.Read(list));
        }

        if (data.HasData)
        {
            data.ReadSequence(ResponseExtensionsTag);
        }

        data.ThrowIfNotEmpty();
        return new OcspBasicResponse(responseData, signatureAlgorithm, signature, name, keyHash, responses, certificates);
    }

    private static (byte[]? Name, byte[]? KeyHash) ReadResponderId(AsnReader data)
    {
        if (data.PeekTag().HasSameClassAndValue(ByNameTag))
        {
            AsnReader byName = data.ReadSequence(ByNameTag);
            byte[] name = byName.ReadEncodedValue().ToArray();
            byName.ThrowIfNotEmpty();
            return (name, null);
        }

        AsnReader byKey = data.ReadSequence(ByKeyTag);
        byte[] keyHash = byKey.ReadOctetString();
        byKey.ThrowIfNotEmpty();
        return (null, keyHash);
    }

    private static List<byte[]> ReadCertificates(AsnReader sequence)
    {
        List<byte[]> certificates = [];
        if (sequence.HasData)
        {
            AsnReader list = sequence.ReadSequence(CertificatesTag).ReadSequence();
            while (list.HasData)
            {
                certificates.Add(list.ReadEncodedValue().ToArray());
            }
        }

        return certificates;
    }
}
