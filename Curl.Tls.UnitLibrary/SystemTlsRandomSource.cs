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
        >= TlsNamedGroup.Ffdhe2048 and <= TlsNamedGroup.Ffdhe8192 => new FfdheKeyShare(group, CreateFiniteFieldExponent()),
        _ => CreateMlKemShare(group),
    };

    /// <summary>A pure ML-KEM or ML-KEM hybrid share, otherwise a Weierstrass curve share.</summary>
    private static Tls13KeyShare CreateMlKemShare(ushort group) => group switch
    {
        TlsNamedGroup.X25519MlKem768 => X25519MlKem768KeyShare.Generate(),
        TlsNamedGroup.SecP256r1MlKem768 or TlsNamedGroup.SecP384r1MlKem1024 => EcdhMlKemKeyShare.Generate(group),
        >= TlsNamedGroup.MlKem512 and <= TlsNamedGroup.MlKem1024 => MlKemKeyShare.Generate(group),
        _ => CreateWeierstrassCurveShare(group),
    };

    /// <summary>
    /// A brainpool share on a brainpool curve (TLS 1.2's or a <c>tls13</c> group; x25519 and
    /// x448, between them, are matched first), otherwise a NIST curve share, which refuses any
    /// other group.
    /// </summary>
    private static Tls13KeyShare CreateWeierstrassCurveShare(ushort group) =>
        group is >= TlsNamedGroup.BrainpoolP256r1 and <= TlsNamedGroup.BrainpoolP512r1Tls13 ? BrainpoolKeyShare.Generate(group) : EcdhKeyShare.Generate(group);

    private static byte[] CreateFiniteFieldExponent()
    {
        byte[] exponent = RandomNumberGenerator.GetBytes(FiniteFieldDiffieHellman.GeneratedExponentLength);
        exponent[0] |= 0x80;
        return exponent;
    }
}
