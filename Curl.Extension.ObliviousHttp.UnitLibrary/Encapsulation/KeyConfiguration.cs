using System.Buffers.Binary;
using Curl.Cryptography;
using CryptographyHpke = Curl.Cryptography.Hpke;

namespace Curl.Extension.ObliviousHttp.Encapsulation;

/// <summary>
/// One Oblivious HTTP gateway key configuration (RFC 9458 section 3.1): its key identifier, its
/// KEM, the KEM public key, and the KDF and AEAD pairs the gateway accepts with it.
/// </summary>
public sealed class KeyConfiguration
{
    private const int KeyIdAndKemSize = 3;
    private const int SuitesLengthSize = 2;
    private const int SuiteSize = 4;

    private readonly byte[] publicKey;

    private KeyConfiguration(byte keyId, HpkeKem kem, byte[] publicKey, IReadOnlyList<SymmetricSuite> symmetricSuites)
    {
        KeyId = keyId;
        Kem = kem;
        this.publicKey = publicKey;
        SymmetricSuites = symmetricSuites;
    }

    /// <summary>The gateway's identifier for this key, echoed in every encapsulated request.</summary>
    public byte KeyId { get; }

    /// <summary>The KEM the public key belongs to.</summary>
    public HpkeKem Kem { get; }

    /// <summary>The gateway's KEM public key, pkR.</summary>
    public ReadOnlySpan<byte> PublicKey => publicKey;

    /// <summary>The KDF and AEAD pairs in the order the gateway lists them, supported here or not.</summary>
    public IReadOnlyList<SymmetricSuite> SymmetricSuites { get; }

    /// <summary>Parses exactly one key configuration.</summary>
    /// <exception cref="MalformedKeyConfigurationException"><paramref name="bytes" /> is truncated, too long or has an empty or ragged suite list.</exception>
    /// <exception cref="UnsupportedKeyConfigurationException">Its KEM is not one this library implements, so its public key cannot be delimited.</exception>
    public static KeyConfiguration Parse(ReadOnlySpan<byte> bytes) =>
        ParseIfKemSupported(bytes)
        ?? throw new UnsupportedKeyConfigurationException(
            $"The Oblivious HTTP key configuration's KEM 0x{BinaryPrimitives.ReadUInt16BigEndian(bytes[1..]):x4} is not supported.");

    /// <summary>Parses exactly one key configuration, or gives <see langword="null" /> when its KEM is not supported.</summary>
    /// <exception cref="MalformedKeyConfigurationException"><paramref name="bytes" /> is truncated, too long or has an empty or ragged suite list.</exception>
    internal static KeyConfiguration? ParseIfKemSupported(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < KeyIdAndKemSize)
        {
            throw new MalformedKeyConfigurationException("The Oblivious HTTP key configuration ends before its KEM.");
        }

        HpkeKem kem = (HpkeKem)BinaryPrimitives.ReadUInt16BigEndian(bytes[1..]);
        if (!Enum.IsDefined(kem))
        {
            return null;
        }

        int publicKeySize = CryptographyHpke.GetEncapsulatedKeySize(kem);
        ReadOnlySpan<byte> afterKem = bytes[KeyIdAndKemSize..];
        if (afterKem.Length < publicKeySize + SuitesLengthSize)
        {
            throw new MalformedKeyConfigurationException("The Oblivious HTTP key configuration ends inside its public key.");
        }

        return new KeyConfiguration(bytes[0], kem, afterKem[..publicKeySize].ToArray(), ReadSuites(afterKem[publicKeySize..]));
    }

    /// <summary>Reads the length-prefixed <c>HpkeSymmetricAlgorithms</c> list that ends the configuration.</summary>
    private static SymmetricSuite[] ReadSuites(ReadOnlySpan<byte> lengthAndSuites)
    {
        ReadOnlySpan<byte> suites = lengthAndSuites[SuitesLengthSize..];
        int suitesLength = BinaryPrimitives.ReadUInt16BigEndian(lengthAndSuites);
        if (suitesLength != suites.Length || suitesLength == 0 || suitesLength % SuiteSize != 0)
        {
            throw new MalformedKeyConfigurationException("The Oblivious HTTP key configuration's symmetric algorithms do not fill their length.");
        }

        SymmetricSuite[] symmetricSuites = new SymmetricSuite[suites.Length / SuiteSize];
        for (int index = 0; index < symmetricSuites.Length; index++)
        {
            ReadOnlySpan<byte> suite = suites.Slice(index * SuiteSize, SuiteSize);
            symmetricSuites[index] = new SymmetricSuite(
                (HpkeKdf)BinaryPrimitives.ReadUInt16BigEndian(suite),
                (HpkeAead)BinaryPrimitives.ReadUInt16BigEndian(suite[2..]));
        }

        return symmetricSuites;
    }
}
