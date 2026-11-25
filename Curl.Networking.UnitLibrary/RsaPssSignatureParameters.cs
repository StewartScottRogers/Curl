using System.Collections.Frozen;
using System.Formats.Asn1;
using System.Numerics;
using System.Security.Cryptography;

namespace Curl.Networking;

/// <summary>
/// The <c>RSASSA-PSS-params</c> of a signature algorithm (RFC 4055, section 3.1), in the shapes
/// <see cref="Curl.Cryptography.RsaPssVerification" /> checks a signature with (STD-197, ADR-1376).
/// </summary>
/// <param name="Hash">The hash of the message.</param>
/// <param name="MaskHash">The hash MGF1 uses.</param>
/// <param name="SaltLength">The salt's length in bytes.</param>
internal sealed record RsaPssSignatureParameters(HashAlgorithmName Hash, HashAlgorithmName MaskHash, int SaltLength)
{
    private const string Mgf1Oid = "1.2.840.113549.1.1.8";

    // The DEFAULT of hashAlgorithm, and of the hash maskGenAlgorithm's MGF1 uses.
    private const string Sha1Oid = "1.3.14.3.2.26";

    private static readonly FrozenDictionary<string, HashAlgorithmName> Hashes =
        new Dictionary<string, HashAlgorithmName>
        {
            [Sha1Oid] = HashAlgorithmName.SHA1,
            ["2.16.840.1.101.3.4.2.1"] = HashAlgorithmName.SHA256,
            ["2.16.840.1.101.3.4.2.2"] = HashAlgorithmName.SHA384,
            ["2.16.840.1.101.3.4.2.3"] = HashAlgorithmName.SHA512,
        }.ToFrozenDictionary();

    /// <summary>
    /// Reads parameters that name SHA-1, SHA-256, SHA-384 or SHA-512, MGF1 over any of those, a
    /// salt length from zero to <see cref="int.MaxValue" /> and trailer field 1.
    /// </summary>
    /// <param name="parameters">The DER of the algorithm identifier's parameters.</param>
    /// <returns>The parameters, or <see langword="null" /> for any other or malformed ones.</returns>
    public static RsaPssSignatureParameters? Read(ReadOnlyMemory<byte> parameters)
    {
        try
        {
            var fields = new AsnReader(parameters, AsnEncodingRules.DER).ReadSequence();
            var hashOid = ReadHashOid(ReadExplicit(fields, 0));
            var maskHashOid = ReadMaskHashOid(ReadExplicit(fields, 1));
            var saltLength = ReadInteger(ReadExplicit(fields, 2), absent: 20);
            var trailerField = ReadInteger(ReadExplicit(fields, 3), absent: 1);
            fields.ThrowIfNotEmpty();
            return trailerField == BigInteger.One ? Admit(hashOid, maskHashOid, saltLength) : null;
        }
        catch (AsnContentException)
        {
            return null;
        }
    }

    private static RsaPssSignatureParameters? Admit(string hashOid, string maskHashOid, BigInteger saltLength) =>
        Hashes.TryGetValue(hashOid, out var hash) && Hashes.TryGetValue(maskHashOid, out var maskHash)
            && saltLength >= 0 && saltLength <= int.MaxValue
            ? new(hash, maskHash, (int)saltLength)
            : null;

    // The content of the explicitly tagged field [number], or null when the next field is not it.
    private static AsnReader? ReadExplicit(AsnReader fields, int number)
    {
        var tag = new Asn1Tag(TagClass.ContextSpecific, number, isConstructed: true);
        return fields.HasData && fields.PeekTag() == tag ? fields.ReadSequence(tag) : null;
    }

    private static string ReadHashOid(AsnReader? field) =>
        field is null ? Sha1Oid : field.ReadSequence().ReadObjectIdentifier();

    // The OID of the hash MGF1 uses, or empty for a mask generation function that is not MGF1.
    private static string ReadMaskHashOid(AsnReader? field)
    {
        if (field is null)
        {
            return Sha1Oid;
        }

        var maskGeneration = field.ReadSequence();
        return maskGeneration.ReadObjectIdentifier() == Mgf1Oid ? maskGeneration.ReadSequence().ReadObjectIdentifier() : string.Empty;
    }

    private static BigInteger ReadInteger(AsnReader? field, int absent) =>
        field is null ? absent : field.ReadInteger();
}
