using System.Text;
using Curl.Cryptography;

namespace Curl.Tls;

/// <summary>
/// One version <c>0xfe0d</c> <c>ECHConfig</c> (RFC 9849 section 4): the server's HPKE key
/// and suites, the longest name it serves, the public name the outer ClientHello carries,
/// and its extensions.
/// </summary>
/// <param name="ConfigId">The <c>config_id</c> the outer hello names.</param>
/// <param name="KemId">The HPKE KEM identifier (RFC 9180 section 7.1).</param>
/// <param name="PublicKey">The server's HPKE public key.</param>
/// <param name="CipherSuites">The HPKE suites the server accepts, in its order.</param>
/// <param name="MaximumNameLength">The longest name the server serves, which sizes the inner hello's padding.</param>
/// <param name="PublicName">The name the outer hello's <c>server_name</c> carries, and the name a rejection is authenticated under.</param>
/// <param name="Extensions">The <c>ECHConfigExtension</c>s.</param>
/// <param name="Encoded">The whole <c>ECHConfig</c>, version and length included: the HPKE <c>info</c> ends with it.</param>
public sealed record EchConfig(
    byte ConfigId,
    ushort KemId,
    byte[] PublicKey,
    IReadOnlyList<EchCipherSuite> CipherSuites,
    byte MaximumNameLength,
    string PublicName,
    IReadOnlyList<TlsExtension> Extensions,
    byte[] Encoded)
{
    /// <summary>The version of <c>ECHConfig</c> RFC 9849 defines, the only one decoded.</summary>
    public const ushort Version = 0xfe0d;

    private const ushort MandatoryExtensionBit = 0x8000;

    /// <summary>
    /// Returns the first of <see cref="CipherSuites" /> the client can seal with, or
    /// <see langword="null" /> when the config cannot be used: its KEM is not one
    /// <see cref="Hpke" /> supports, its public key is unusable, or it carries a mandatory
    /// extension (RFC 9849 section 4.2), none of which the client knows.
    /// </summary>
    /// <returns>The suite to offer, or <see langword="null" />.</returns>
    public EchCipherSuite? FindSupportedSuite() => IsKemSupported && !HasMandatoryExtension && IsPublicKeyUsable() ? FindFirstSupportedSuite() : null;

    private bool IsKemSupported => (HpkeKem)KemId is HpkeKem.DhkemX25519HkdfSha256 or HpkeKem.DhkemP256HkdfSha256;

    private bool HasMandatoryExtension => Extensions.Any(extension => ((ushort)extension.Type & MandatoryExtensionBit) != 0);

    /// <summary>Decodes the contents of a version <see cref="Version" /> config.</summary>
    /// <param name="contents">The <c>ECHConfigContents</c> bytes, after the version and length.</param>
    /// <returns>The config, or <see cref="TlsAlertDescription.DecodeError" /> for a malformed one.</returns>
    internal static TlsDecodeResult<EchConfig> Decode(byte[] contents)
    {
        TlsReader reader = new(contents);
        byte configId = reader.ReadUInt8();
        ushort kemId = reader.ReadUInt16();
        byte[] publicKey = reader.ReadOpaque(2);
        IReadOnlyList<ushort> suiteIds = reader.ReadUInt16List(2);
        byte maximumNameLength = reader.ReadUInt8();
        byte[] publicName = reader.ReadOpaque(1);
        IReadOnlyList<TlsExtension> extensions = TlsExtensionBlock.Read(reader);
        if (publicKey.Length == 0 || publicName.Length == 0 || suiteIds.Count == 0 || suiteIds.Count % 2 != 0)
        {
            reader.Fail(TlsAlertDescription.DecodeError);
        }

        TlsWriter encoded = new();
        encoded.WriteUInt16(Version);
        encoded.WriteOpaque(2, contents);
        EchCipherSuite[] suites = [.. suiteIds.Chunk(2).Select(pair => new EchCipherSuite(pair[0], pair[^1]))];
        return reader.Finish(new EchConfig(configId, kemId, publicKey, suites, maximumNameLength, Encoding.Latin1.GetString(publicName), extensions, encoded.ToArray()));
    }

    private EchCipherSuite? FindFirstSupportedSuite() =>
        CipherSuites.Where(suite => suite.IsSupported).Select(suite => (EchCipherSuite?)suite).FirstOrDefault();

    /// <summary>Whether HPKE can encapsulate to <see cref="PublicKey" />: the right length, a point on the curve, not of small order.</summary>
    private bool IsPublicKeyUsable()
    {
        HpkeKem kem = (HpkeKem)KemId;
        byte[] encapsulatedKey = new byte[Hpke.GetEncapsulatedKeySize(kem)];
        bool usable = Hpke.TrySetupBaseSender(kem, HpkeKdf.HkdfSha256, HpkeAead.Aes128Gcm, PublicKey, [], encapsulatedKey, out HpkeContext? context);
        context?.Dispose();
        return usable;
    }
}
