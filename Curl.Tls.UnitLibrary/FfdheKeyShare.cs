using Curl.Cryptography;

namespace Curl.Tls;

/// <summary>
/// A key share on one of the RFC 7919 finite-field groups (RFC 8446 section 4.2.8.1): the
/// public value and the shared secret are both left-padded to the prime's length.
/// </summary>
public sealed class FfdheKeyShare : Tls13KeyShare
{
    private readonly FiniteFieldDiffieHellman key;

    /// <summary>Creates the share of <paramref name="privateExponent" /> on <paramref name="group" />.</summary>
    /// <param name="group">The named group, ffdhe2048 to ffdhe8192.</param>
    /// <param name="privateExponent">The private exponent, big-endian.</param>
    /// <exception cref="ArgumentOutOfRangeException">The group is not a finite-field group.</exception>
    public FfdheKeyShare(ushort group, byte[] privateExponent)
        : this(group, new FiniteFieldDiffieHellman(GroupOf(group), privateExponent))
    {
    }

    private FfdheKeyShare(ushort group, FiniteFieldDiffieHellman key)
        : base(group, PublicValueOf(key)) => this.key = key;

    /// <inheritdoc />
    public override byte[]? ComputeSharedSecret(byte[] peerPublicKey)
    {
        ArgumentNullException.ThrowIfNull(peerPublicKey);
        byte[] sharedSecret = new byte[key.Group.PrimeLength];
        return peerPublicKey.Length == key.Group.PrimeLength && key.TryComputeSharedSecret(peerPublicKey, sharedSecret)
            ? sharedSecret
            : null;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        key.Dispose();
        base.Dispose(disposing);
    }

    private static byte[] PublicValueOf(FiniteFieldDiffieHellman key)
    {
        byte[] publicValue = new byte[key.Group.PrimeLength];
        key.ComputePublicValue(publicValue);
        return publicValue;
    }

    private static FiniteFieldDiffieHellmanGroup GroupOf(ushort group) => group switch
    {
        TlsNamedGroup.Ffdhe2048 => FiniteFieldDiffieHellmanGroup.Ffdhe2048,
        TlsNamedGroup.Ffdhe3072 => FiniteFieldDiffieHellmanGroup.Ffdhe3072,
        TlsNamedGroup.Ffdhe4096 => FiniteFieldDiffieHellmanGroup.Ffdhe4096,
        TlsNamedGroup.Ffdhe6144 => FiniteFieldDiffieHellmanGroup.Ffdhe6144,
        TlsNamedGroup.Ffdhe8192 => FiniteFieldDiffieHellmanGroup.Ffdhe8192,
        _ => throw new ArgumentOutOfRangeException(nameof(group), group, "The group is not a finite-field group."),
    };
}
