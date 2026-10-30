using System.Formats.Asn1;
using Curl.Cryptography;

namespace Curl.Extension.MlDsaCertificates.UnitTests;

/// <summary>Builds DER X.509 certificates (RFC 5280 section 4.1) whose every field a test can bend.</summary>
internal sealed record TestCertificate
{
    internal const string Sha256WithRsa = "1.2.840.113549.1.1.11";

    internal const string RsaEncryption = "1.2.840.113549.1.1.1";

    internal string Subject { get; init; } = "leaf";

    internal string Issuer { get; init; } = "issuer";

    internal bool WithVersion { get; init; } = true;

    internal string SignatureOid { get; init; } = MlDsaAlgorithmIdentifiers.MlDsa44;

    internal bool SignatureHasParameters { get; init; }

    internal string? ToBeSignedSignatureOid { get; init; }

    internal string KeyOid { get; init; } = MlDsaAlgorithmIdentifiers.MlDsa44;

    internal bool KeyHasParameters { get; init; }

    internal byte[] PublicKey { get; init; } = [];

    internal int KeyUnusedBits { get; init; }

    internal int SignatureUnusedBits { get; init; }

    internal bool TrailingByte { get; init; }

    /// <summary>Gives a key pair of <paramref name="parameterSet" /> from a seed of <paramref name="seedByte" />.</summary>
    internal static MlDsa KeyPair(MlDsaParameterSet parameterSet, byte seedByte)
    {
        byte[] seed = new byte[MlDsa.SeedSize];
        Array.Fill(seed, seedByte);
        return MlDsa.GenerateKey(parameterSet, seed);
    }

    /// <summary>Gives the public key of <paramref name="key" />.</summary>
    internal static byte[] PublicKeyOf(MlDsa key)
    {
        byte[] publicKey = new byte[MlDsa.GetPublicKeySize(key.ParameterSet)];
        key.ExportPublicKey(publicKey);
        return publicKey;
    }

    /// <summary>Gives the DER <c>tbsCertificate</c>.</summary>
    internal byte[] ToBeSigned()
    {
        AsnWriter writer = new(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            if (WithVersion)
            {
                using (writer.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 0, isConstructed: true)))
                {
                    writer.WriteInteger(2);
                }
            }

            writer.WriteInteger(1);
            WriteAlgorithm(writer, ToBeSignedSignatureOid ?? SignatureOid, SignatureHasParameters);
            WriteName(writer, Issuer);
            using (writer.PushSequence())
            {
                writer.WriteUtcTime(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
                writer.WriteUtcTime(new DateTimeOffset(2036, 1, 1, 0, 0, 0, TimeSpan.Zero));
            }

            WriteName(writer, Subject);
            using (writer.PushSequence())
            {
                WriteAlgorithm(writer, KeyOid, KeyHasParameters);
                writer.WriteBitString(PublicKey, KeyUnusedBits);
            }
        }

        return writer.Encode();
    }

    /// <summary>Gives the DER certificate signed by <paramref name="signer" /> with an empty context (RFC 9881 section 3).</summary>
    internal byte[] SignedBy(MlDsa signer)
    {
        byte[] toBeSigned = ToBeSigned();
        byte[] signature = new byte[MlDsa.GetSignatureSize(signer.ParameterSet)];
        signer.SignDataDeterministic(toBeSigned, [], signature);
        return WithSignature(toBeSigned, signature);
    }

    /// <summary>Gives the DER certificate over <paramref name="toBeSigned" /> carrying <paramref name="signature" />.</summary>
    internal byte[] WithSignature(byte[] toBeSigned, byte[] signature)
    {
        AsnWriter writer = new(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            writer.WriteEncodedValue(toBeSigned);
            WriteAlgorithm(writer, SignatureOid, SignatureHasParameters);
            writer.WriteBitString(signature, SignatureUnusedBits);
        }

        byte[] encoded = writer.Encode();
        return TrailingByte ? [.. encoded, 0] : encoded;
    }

    private static void WriteAlgorithm(AsnWriter writer, string oid, bool hasParameters)
    {
        using (writer.PushSequence())
        {
            writer.WriteObjectIdentifier(oid);
            if (hasParameters)
            {
                writer.WriteNull();
            }
        }
    }

    private static void WriteName(AsnWriter writer, string commonName)
    {
        using (writer.PushSequence())
        using (writer.PushSetOf())
        using (writer.PushSequence())
        {
            writer.WriteObjectIdentifier("2.5.4.3");
            writer.WriteCharacterString(UniversalTagNumber.UTF8String, commonName);
        }
    }
}
