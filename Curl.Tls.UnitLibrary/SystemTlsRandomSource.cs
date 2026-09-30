using System.Security.Cryptography;
using Curl.Cryptography;

namespace Curl.Tls;

/// <summary>
/// The production <see cref="ITlsRandomSource" />: bytes and private keys from the
/// operating system's cryptographic random number generator.
/// </summary>
public sealed class SystemTlsRandomSource : ITlsRandomSource
{
    private SystemTlsRandomSource()
    {
    }

    /// <summary>Gets the one instance.</summary>
    public static SystemTlsRandomSource Instance { get; } = new();

    /// <inheritdoc />
    public void Fill(Span<byte> destination) => RandomNumberGenerator.Fill(destination);

    /// <inheritdoc />
    /// <exception cref="ArgumentOutOfRangeException">The client cannot share <paramref name="group" />.</exception>
    public Tls13KeyShare CreateKeyShare(ushort group) => group switch
    {
        TlsNamedGroup.X25519 => new X25519KeyShare(RandomNumberGenerator.GetBytes(X25519.KeySize)),
        TlsNamedGroup.X448 => new X448KeyShare(RandomNumberGenerator.GetBytes(X448.KeySize)),
        TlsNamedGroup.X25519MlKem768 => X25519MlKem768KeyShare.Generate(),
        >= TlsNamedGroup.Ffdhe2048 and <= TlsNamedGroup.Ffdhe8192 => new FfdheKeyShare(group, CreateFiniteFieldExponent()),
        _ => CreateWeierstrassCurveShare(group),
    };

    /// <summary>A brainpool share on a brainpool curve, otherwise a NIST curve share, which refuses any other group.</summary>
    private static Tls13KeyShare CreateWeierstrassCurveShare(ushort group) =>
        group is >= TlsNamedGroup.BrainpoolP256r1 and <= TlsNamedGroup.BrainpoolP512r1 ? BrainpoolKeyShare.Generate(group) : EcdhKeyShare.Generate(group);

    private static byte[] CreateFiniteFieldExponent()
    {
        byte[] exponent = RandomNumberGenerator.GetBytes(FiniteFieldDiffieHellman.GeneratedExponentLength);
        exponent[0] |= 0x80;
        return exponent;
    }
}
