using System.Security.Cryptography;
using Curl.Cryptography;

namespace Curl.Tls;

/// <summary>
/// An ECDHE share on brainpoolP256r1, brainpoolP384r1 or brainpoolP512r1 for TLS 1.2
/// (RFC 7027, RFC 8422 section 5.10) over <c>Curl.Cryptography</c>'s hand-built
/// <see cref="BrainpoolEcdh" />: the public value is the uncompressed point
/// <c>04 || X || Y</c> and the shared secret the X coordinate, both at the curve's full
/// coordinate length. A peer value that is not a point of the curve gives no secret.
/// </summary>
public sealed class BrainpoolKeyShare : Tls13KeyShare
{
    private readonly BrainpoolCurve curve;
    private readonly byte[] privateKey;

    /// <summary>Creates the share of <paramref name="privateKey" /> on <paramref name="group" />'s curve.</summary>
    /// <param name="group">The named group: brainpoolP256r1, brainpoolP384r1 or brainpoolP512r1.</param>
    /// <param name="privateKey">The big-endian private scalar, in [1, q - 1] and the curve's coordinate length.</param>
    /// <exception cref="ArgumentOutOfRangeException">The group is not a brainpool curve.</exception>
    /// <exception cref="ArgumentException">The private key has the wrong length or is out of range.</exception>
    public BrainpoolKeyShare(ushort group, byte[] privateKey)
        : base(group, PublicKeyOf(CurveOf(group), privateKey))
    {
        curve = CurveOf(group);
        this.privateKey = [.. privateKey];
    }

    /// <summary>Generates a fresh key pair on <paramref name="group" />'s curve.</summary>
    /// <param name="group">The named group: brainpoolP256r1, brainpoolP384r1 or brainpoolP512r1.</param>
    /// <returns>The share.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The group is not a brainpool curve.</exception>
    public static BrainpoolKeyShare Generate(ushort group)
    {
        BrainpoolCurve curve = CurveOf(group);
        byte[] privateKey = new byte[BrainpoolEcdh.GetPrivateKeyLength(curve)];
        try
        {
            BrainpoolEcdh.GeneratePrivateKey(curve, privateKey);
            return new BrainpoolKeyShare(group, privateKey);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(privateKey);
        }
    }

    /// <inheritdoc />
    public override byte[]? ComputeSharedSecret(byte[] peerPublicKey)
    {
        ArgumentNullException.ThrowIfNull(peerPublicKey);
        byte[] sharedSecret = new byte[BrainpoolEcdh.GetSharedSecretLength(curve)];
        return BrainpoolEcdh.TryComputeSharedSecret(curve, privateKey, peerPublicKey, sharedSecret) ? sharedSecret : null;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        CryptographicOperations.ZeroMemory(privateKey);
        base.Dispose(disposing);
    }

    private static BrainpoolCurve CurveOf(ushort group) => group switch
    {
        TlsNamedGroup.BrainpoolP256r1 => BrainpoolCurve.BrainpoolP256r1,
        TlsNamedGroup.BrainpoolP384r1 => BrainpoolCurve.BrainpoolP384r1,
        TlsNamedGroup.BrainpoolP512r1 => BrainpoolCurve.BrainpoolP512r1,
        _ => throw new ArgumentOutOfRangeException(nameof(group), group, "The group is not a brainpool curve."),
    };

    private static byte[] PublicKeyOf(BrainpoolCurve curve, byte[] privateKey)
    {
        ArgumentNullException.ThrowIfNull(privateKey);
        byte[] publicKey = new byte[BrainpoolEcdh.GetPublicKeyLength(curve)];
        BrainpoolEcdh.ComputePublicKey(curve, privateKey, publicKey);
        return publicKey;
    }
}
