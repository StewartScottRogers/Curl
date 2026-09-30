using System.Security.Cryptography;
using Curl.Cryptography;
using Curl.Protocol.Ssh.KeyExchange;

namespace Curl.Protocol.Ssh.Fakes;

/// <summary>
/// Both sides' ephemeral keys for a scripted key exchange. The client's are handed to the
/// transport through <see cref="ISshEphemeralKeySource" />; the P-256 keys and the
/// finite-field exponents are fixed, so the exchange hash of an exchange that uses them is
/// the same on every run and platform.
/// </summary>
internal sealed class TestEphemeralKeys : ISshEphemeralKeySource
{
    private static readonly ECParameters ClientNistP256 = Fixed(
        "C789537BF318C2F567B2195F24F56592841E8A32A40E260A11A93560F4297EB4",
        "1FFA6C6EFB08595399A35D5BAC386361D426597AB09585C350B5CCCAC1CED7EE",
        "C30EECE741220B27CF128C77F450FE5F6E384ADB16F7F0013666D4F9E20883A6");

    private static readonly ECParameters ServerNistP256 = Fixed(
        "F2103660E843B39EDED3664D5527B90A5EB64218A609BC003524EB0E65E138A5",
        "126DA589D3C20E18D857021C949B73A002B991BBAE0BAC4F7F7CF1FB0C8BC99D",
        "2891CD03F05D3C1E4FE3B8D7EFEFA814984DC5FFAA4AA16FB5EACD4AD9BE96B4");

    private readonly Dictionary<string, ECParameters> client = new()
    {
        ["nistP256"] = ClientNistP256,
        ["nistP384"] = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP384).ExportParameters(true),
        ["nistP521"] = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP521).ExportParameters(true),
    };

    private readonly Dictionary<string, ECParameters> server = new()
    {
        ["nistP256"] = ServerNistP256,
        ["nistP384"] = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP384).ExportParameters(true),
        ["nistP521"] = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP521).ExportParameters(true),
    };

    /// <summary>Gets the client's finite-field private exponent.</summary>
    internal static byte[] ClientExponent { get; } = Convert.FromHexString("8123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF");

    /// <summary>Gets the server's finite-field private exponent.</summary>
    internal static byte[] ServerExponent { get; } = Convert.FromHexString("9FEDCBA9876543210FEDCBA9876543210FEDCBA9876543210FEDCBA987654321");

    /// <summary>Gets the client's fixed X25519 private key (RFC 7748 section 6.1's Alice).</summary>
    internal static byte[] ClientX25519 { get; } = Convert.FromHexString("77076D0A7318A57D3C16C17251B26645DF4C2F87EBC0992AB177FBA51DB92C2A");

    /// <summary>Gets the server's fixed X25519 private key.</summary>
    internal static byte[] ServerX25519 { get; } = Convert.FromHexString("5DAB087E624A8A4B79E17F8B83800EE66F3BB1292618B6FD1C2F8B27FF88E0EB");

    /// <summary>Gets the client's fixed ML-KEM seed d.</summary>
    internal static byte[] ClientMlKemSeedD { get; } = Convert.FromHexString("7C9935A0B07694AA0C6D10E4DB6B1ADD2FD81A25CCB148032DCD739936737F2D");

    /// <summary>Gets the client's fixed ML-KEM seed z.</summary>
    internal static byte[] ClientMlKemSeedZ { get; } = Convert.FromHexString("B505D7CFAD1B497499323C8686325E4792F267AAFA3F87CA60D01CB54F29202A");

    /// <summary>Gets the server's fixed ML-KEM encapsulation message m.</summary>
    internal static byte[] ServerMlKemMessage { get; } = Convert.FromHexString("147C03F7A5BEBBA406C8FAE1874D7F13C80EFE79A3A9A874CC09FE76F6997615");

    /// <summary>
    /// Gets the client's fixed sntrup761 key pair, generated from the first seeded
    /// <see cref="Random" /> stream whose g is invertible, so it is the same on every run.
    /// </summary>
    internal static (byte[] PublicKey, byte[] SecretKey) ClientSntrup761 { get; } = FixedSntrup761KeyPair();

    /// <summary>Gets the server's fixed sntrup761 encapsulation randomness.</summary>
    internal static byte[] ServerSntrup761Random { get; } = SeededBytes(761, Sntrup761.EncapsulationRandomSize);

    /// <summary>Gets how many elliptic-curve keys the transport asked for.</summary>
    internal int EllipticCurveKeysCreated { get; private set; }

    /// <summary>Gets the client's key on a curve.</summary>
    internal ECParameters Client(ECCurve curve) => client[curve.Oid.FriendlyName!];

    /// <summary>Gets the server's key on a curve.</summary>
    internal ECParameters Server(ECCurve curve) => server[curve.Oid.FriendlyName!];

    /// <inheritdoc />
    public ECDiffieHellman CreateEllipticCurveKey(ECCurve curve)
    {
        EllipticCurveKeysCreated++;
        return ECDiffieHellman.Create(Client(curve));
    }

    /// <inheritdoc />
    public FiniteFieldDiffieHellman CreateFiniteFieldKey(FiniteFieldDiffieHellmanGroup group) =>
        new(group, ClientExponent);

    /// <inheritdoc />
    public void CreateX25519PrivateKey(Span<byte> privateKey) => ClientX25519.CopyTo(privateKey);

    /// <inheritdoc />
    public MlKem CreateMlKemKey(MlKemParameterSet parameterSet) =>
        MlKem.GenerateKey(parameterSet, ClientMlKemSeedD, ClientMlKemSeedZ);

    /// <inheritdoc />
    public void CreateSntrup761KeyPair(Span<byte> publicKey, Span<byte> secretKey)
    {
        ClientSntrup761.PublicKey.CopyTo(publicKey);
        ClientSntrup761.SecretKey.CopyTo(secretKey);
    }

    private static byte[] SeededBytes(int seed, int length)
    {
        byte[] bytes = new byte[length];
        new Random(seed).NextBytes(bytes);
        return bytes;
    }

    private static (byte[] PublicKey, byte[] SecretKey) FixedSntrup761KeyPair()
    {
        byte[] publicKey = new byte[Sntrup761.PublicKeySize];
        byte[] secretKey = new byte[Sntrup761.SecretKeySize];
        int seed = 0;
        while (!Sntrup761.TryGenerateKeyPair(SeededBytes(seed, Sntrup761.KeyGenerationRandomSize), publicKey, secretKey))
        {
            seed++;
        }

        return (publicKey, secretKey);
    }

    private static ECParameters Fixed(string d, string x, string y) => new()
    {
        Curve = ECCurve.NamedCurves.nistP256,
        D = Convert.FromHexString(d),
        Q = new ECPoint { X = Convert.FromHexString(x), Y = Convert.FromHexString(y) },
    };
}
