using System.Buffers.Binary;
using Curl.Extension.ObliviousHttp.Hpke;

namespace Curl.Extension.ObliviousHttp.Encapsulation;

/// <summary>
/// The <c>application/ohttp-keys</c> body (RFC 9458 section 3.2, served as RFC 9540 section 3
/// describes): key configurations, each after a two-byte length, and the choice of the first
/// one this library can use.
/// </summary>
public static class KeyConfigurationList
{
    private const int LengthPrefixSize = 2;

    /// <summary>
    /// Parses every key configuration in <paramref name="bytes" />, in order, leaving out those
    /// whose KEM this library does not implement.
    /// </summary>
    /// <exception cref="MalformedKeyConfigurationException">A length prefix or a configuration is truncated or malformed.</exception>
    public static IReadOnlyList<KeyConfiguration> Parse(ReadOnlySpan<byte> bytes)
    {
        List<KeyConfiguration> configurations = [];
        while (!bytes.IsEmpty)
        {
            if (bytes.Length < LengthPrefixSize)
            {
                throw new MalformedKeyConfigurationException("The Oblivious HTTP key configuration list ends inside a length.");
            }

            int length = BinaryPrimitives.ReadUInt16BigEndian(bytes);
            ReadOnlySpan<byte> afterLength = bytes[LengthPrefixSize..];
            if (afterLength.Length < length)
            {
                throw new MalformedKeyConfigurationException("The Oblivious HTTP key configuration list ends inside a configuration.");
            }

            KeyConfiguration? configuration = KeyConfiguration.ParseIfKemSupported(afterLength[..length]);
            if (configuration is not null)
            {
                configurations.Add(configuration);
            }

            bytes = afterLength[length..];
        }

        return configurations;
    }

    /// <summary>The first configuration, with its first KDF and AEAD pair, that this library supports.</summary>
    /// <exception cref="UnsupportedKeyConfigurationException">No configuration has a supported KDF and AEAD.</exception>
    public static KeyConfigurationChoice SelectFirstSupported(IEnumerable<KeyConfiguration> configurations)
    {
        ArgumentNullException.ThrowIfNull(configurations);
        foreach (KeyConfiguration configuration in configurations)
        {
            foreach (SymmetricSuite suite in configuration.SymmetricSuites.Where(suite => suite.IsSupported))
            {
                return new KeyConfigurationChoice(configuration, new HpkeSuite(configuration.Kem, suite.Kdf, suite.Aead));
            }
        }

        throw new UnsupportedKeyConfigurationException("No Oblivious HTTP key configuration has a KEM, KDF and AEAD this library supports.");
    }
}
