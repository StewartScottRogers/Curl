using System.Collections.Frozen;
using System.Formats.Asn1;
using System.Numerics;
using System.Security.Cryptography;

namespace Curl.Networking;

/// <summary>
/// Reads the <c>RSASSA-PSS-params</c> of a signature algorithm (RFC 4055, section 3.1) and says
/// whether <see cref="RSASignaturePadding.Pss" /> can verify such a signature (STD-197).
/// </summary>
internal static class RsaPssSignatureParameters
{
    private const string Mgf1Oid = "1.2.840.113549.1.1.8";

    // The DEFAULT of hashAlgorithm, and of the hash maskGenAlgorithm's MGF1 uses.
    private const string Sha1Oid = "1.3.14.3.2.26";

    private static readonly FrozenDictionary<string, (HashAlgorithmName Name, int Length)> Hashes =
        new Dictionary<string, (HashAlgorithmName Name, int Length)>
        {
            [Sha1Oid] = (HashAlgorithmName.SHA1, 20),
            ["2.16.840.1.101.3.4.2.1"] = (HashAlgorithmName.SHA256, 32),
            ["2.16.840.1.101.3.4.2.2"] = (HashAlgorithmName.SHA384, 48),
            ["2.16.840.1.101.3.4.2.3"] = (HashAlgorithmName.SHA512, 64),
        }.ToFrozenDictionary();

    /// <summary>
    /// Returns the signature's hash when the parameters name SHA-1, SHA-256, SHA-384 or SHA-512,
    /// MGF1 over that same hash, a salt as long as the hash and trailer field 1 - the one shape
    /// <see cref="RSASignaturePadding.Pss" /> verifies.
    /// </summary>
    /// <param name="parameters">The DER of the algorithm identifier's parameters.</param>
    /// <returns>The hash, or <see langword="null" /> for any other or malformed parameters.</returns>
    public static HashAlgorithmName? ReadVerifiableHash(ReadOnlyMemory<byte> parameters)
    {
        try
        {
            var fields = new AsnReader(parameters, AsnEncodingRules.DER).ReadSequence();
            var hashOid = ReadHashOid(ReadExplicit(fields, 0));
            var maskHashOid = ReadMaskHashOid(ReadExplicit(fields, 1));
            var saltLength = ReadInteger(ReadExplicit(fields, 2), absent: 20);
            var trailerField = ReadInteger(ReadExplicit(fields, 3), absent: 1);
            fields.ThrowIfNotEmpty();
            return Hashes.TryGetValue(hashOid, out var hash) && maskHashOid == hashOid && saltLength == hash.Length && trailerField == BigInteger.One
                ? hash.Name
                : null;
        }
        catch (AsnContentException)
        {
            return null;
        }
    }

    // The content of the explicitly tagged field [number], or null when the next field is not it.
    private static AsnReader? ReadExplicit(AsnReader fields, int number)
    {
        var tag = new Asn1Tag(TagClass.ContextSpecific, number, isConstructed: true);
        return fields.HasData && fields.PeekTag() == tag ? fields.ReadSequence(tag) : null;
    }

    private static string ReadHashOid(AsnReader? field) =>
        field is null ? Sha1Oid : field.ReadSequence().ReadObjectIdentifier();

    // The hash MGF1 uses, or null for a mask generation function that is not MGF1.
    private static string? ReadMaskHashOid(AsnReader? field)
    {
        if (field is null)
        {
            return Sha1Oid;
        }

        var maskGeneration = field.ReadSequence();
        return maskGeneration.ReadObjectIdentifier() == Mgf1Oid ? maskGeneration.ReadSequence().ReadObjectIdentifier() : null;
    }

    private static BigInteger ReadInteger(AsnReader? field, int absent) =>
        field is null ? absent : field.ReadInteger();
}
