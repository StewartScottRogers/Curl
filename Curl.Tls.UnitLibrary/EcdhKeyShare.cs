using System.Security.Cryptography;

namespace Curl.Tls;

/// <summary>
/// A key share on secp256r1, secp384r1 or secp521r1 (RFC 8446 section 4.2.8.2): the
/// public value is the uncompressed point <c>04 || X || Y</c> and the shared secret is the
/// X coordinate, both at the curve's full coordinate length.
/// </summary>
public sealed class EcdhKeyShare : Tls13KeyShare
{
    private const byte UncompressedPoint = 0x04;

    private readonly ECDiffieHellman key;

    /// <summary>Creates the share of <paramref name="key" />, which it takes ownership of.</summary>
    /// <param name="group">The named group, secp256r1, secp384r1 or secp521r1.</param>
    /// <param name="key">A private key on that group's curve.</param>
    /// <exception cref="ArgumentOutOfRangeException">The group is not a NIST curve.</exception>
    public EcdhKeyShare(ushort group, ECDiffieHellman key)
        : base(group, PublicKeyOf(NistCurve.Of(group), key)) => this.key = key;

    /// <summary>Generates a fresh key pair on <paramref name="group" />'s curve.</summary>
    /// <param name="group">The named group, secp256r1, secp384r1 or secp521r1.</param>
    /// <returns>The share.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The group is not a NIST curve.</exception>
    public static EcdhKeyShare Generate(ushort group) => new(group, ECDiffieHellman.Create(NistCurve.Of(group).Curve));

    /// <inheritdoc />
    public override byte[]? ComputeSharedSecret(byte[] peerPublicKey)
    {
        ArgumentNullException.ThrowIfNull(peerPublicKey);
        NistCurve curve = NistCurve.Of(Group);
        int length = curve.CoordinateLength;
        if (peerPublicKey.Length != 1 + (2 * length) || peerPublicKey[0] != UncompressedPoint)
        {
            return null;
        }

        byte[] x = peerPublicKey[1..(1 + length)];
        byte[] y = peerPublicKey[(1 + length)..];
        if (!curve.Contains(x, y))
        {
            return null;
        }

        using ECDiffieHellman peerKey = ECDiffieHellman.Create(new ECParameters { Curve = curve.Curve, Q = new ECPoint { X = x, Y = y } });
        return key.DeriveRawSecretAgreement(peerKey.PublicKey);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        key.Dispose();
        base.Dispose(disposing);
    }

    private static byte[] PublicKeyOf(NistCurve curve, ECDiffieHellman key)
    {
        ArgumentNullException.ThrowIfNull(key);
        int length = curve.CoordinateLength;
        ECParameters parameters = key.ExportParameters(false);
        byte[] publicKey = new byte[1 + (2 * length)];
        publicKey[0] = UncompressedPoint;
        parameters.Q.X!.CopyTo(publicKey, 1 + length - parameters.Q.X.Length);
        parameters.Q.Y!.CopyTo(publicKey, publicKey.Length - parameters.Q.Y.Length);
        return publicKey;
    }
}
