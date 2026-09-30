using System.Security.Cryptography;
using System.Text;
using Curl.Cryptography;

namespace Curl.Tls;

/// <summary>
/// An ECH server's key and config for the tests: a fixed X25519 key, or a P-256 key, and
/// the <c>ECHConfigList</c> that publishes it, written field by field from RFC 9849 section 4.
/// </summary>
internal sealed record EchTestConfig(HpkeKem Kem, byte[] PrivateKey, byte[] PublicKey, byte ConfigId, string PublicName, byte MaximumNameLength, EchCipherSuite Suite)
{
    public const string DefaultPublicName = "public.example";

    private static readonly byte[] FixedX25519PrivateKey = [.. Enumerable.Range(1, 32).Select(value => (byte)value)];

    /// <summary>Gets the encoded <c>ECHConfigList</c> holding this one config.</summary>
    public byte[] ConfigList => EncodeList(Encode());

    /// <summary>Gets the decoded list.</summary>
    public EchConfigList Decoded => EchConfigList.Decode(ConfigList).Value;

    public static EchTestConfig X25519(EchCipherSuite? suite = null, byte maximumNameLength = 32)
    {
        byte[] publicKey = new byte[Cryptography.X25519.KeySize];
        Cryptography.X25519.ComputePublicKey(FixedX25519PrivateKey, publicKey);
        return new EchTestConfig(HpkeKem.DhkemX25519HkdfSha256, FixedX25519PrivateKey, publicKey, 0x2a, DefaultPublicName, maximumNameLength, suite ?? EchCipherSuite.Grease);
    }

    public static EchTestConfig P256(EchCipherSuite suite)
    {
        using ECDiffieHellman key = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        ECParameters parameters = key.ExportParameters(true);
        byte[] publicKey = [0x04, .. parameters.Q.X!, .. parameters.Q.Y!];
        return new EchTestConfig(HpkeKem.DhkemP256HkdfSha256, parameters.D!, publicKey, 0x07, DefaultPublicName, 0, suite);
    }

    /// <summary>Returns one <c>ECHConfig</c>: version, length, then the contents.</summary>
    public static byte[] EncodeConfig(
        ushort version,
        byte configId,
        ushort kemId,
        byte[] publicKey,
        IReadOnlyList<ushort> suiteIds,
        byte maximumNameLength,
        string publicName,
        IReadOnlyList<TlsExtension>? extensions = null)
    {
        TlsWriter contents = new();
        contents.WriteUInt8(configId);
        contents.WriteUInt16(kemId);
        contents.WriteOpaque(2, publicKey);
        contents.WriteUInt16List(2, suiteIds);
        contents.WriteUInt8(maximumNameLength);
        contents.WriteOpaque(1, Encoding.Latin1.GetBytes(publicName));
        TlsExtensionBlock.Write(contents, extensions ?? []);
        TlsWriter config = new();
        config.WriteUInt16(version);
        config.WriteOpaque(2, contents.ToArray());
        return config.ToArray();
    }

    /// <summary>Returns an <c>ECHConfigList</c> of <paramref name="configs" />.</summary>
    public static byte[] EncodeList(params byte[][] configs)
    {
        TlsWriter list = new();
        list.WriteOpaque(2, [.. configs.SelectMany(config => config)]);
        return list.ToArray();
    }

    public byte[] Encode() =>
        EncodeConfig(EchConfig.Version, ConfigId, (ushort)Kem, PublicKey, [Suite.KdfId, Suite.AeadId], MaximumNameLength, PublicName);
}
