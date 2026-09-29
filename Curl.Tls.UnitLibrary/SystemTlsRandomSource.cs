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
        TlsNamedGroup.Secp256r1 or TlsNamedGroup.Secp384r1 or TlsNamedGroup.Secp521r1 => EcdhKeyShare.Generate(group),
        _ => new FfdheKeyShare(group, CreateFiniteFieldExponent()),
    };

    private static byte[] CreateFiniteFieldExponent()
    {
        byte[] exponent = RandomNumberGenerator.GetBytes(FiniteFieldDiffieHellman.GeneratedExponentLength);
        exponent[0] |= 0x80;
        return exponent;
    }
}
