using System.Numerics;
using System.Security.Cryptography;

namespace Curl.Tls;

/// <summary>
/// The NIST prime curves P-256, P-384 and P-521 (FIPS 186-5, SEC 2), with the constants
/// that check a peer's public point lies on the curve (RFC 8446 section 4.2.8.2). The
/// check runs on public values only, so it needs no constant-time arithmetic, and it
/// refuses a bad point the same way on every platform before the BCL sees it.
/// </summary>
internal sealed class NistCurve
{
    private static readonly NistCurve P256 = new(
        ECCurve.NamedCurves.nistP256,
        32,
        "ffffffff00000001000000000000000000000000ffffffffffffffffffffffff",
        "5ac635d8aa3a93e7b3ebbd55769886bc651d06b0cc53b0f63bce3c3e27d2604b");

    private static readonly NistCurve P384 = new(
        ECCurve.NamedCurves.nistP384,
        48,
        "fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffeffffffff0000000000000000ffffffff",
        "b3312fa7e23ee7e4988e056be3f82d19181d9c6efe8141120314088f5013875ac656398d8a2ed19d2a85c8edd3ec2aef");

    private static readonly NistCurve P521 = new(
        ECCurve.NamedCurves.nistP521,
        66,
        "01ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff",
        "0051953eb9618e1c9a1f929a21a0b68540eea2da725b99b315f3b8b489918ef109e156193951ec7e937b1652c0bd3bb1bf073573df883d2c34f1ef451fd46b503f00");

    private readonly BigInteger prime;
    private readonly BigInteger b;

    private NistCurve(ECCurve curve, int coordinateLength, string primeHex, string bHex)
    {
        Curve = curve;
        CoordinateLength = coordinateLength;
        prime = new BigInteger(Convert.FromHexString(primeHex), isUnsigned: true, isBigEndian: true);
        b = new BigInteger(Convert.FromHexString(bHex), isUnsigned: true, isBigEndian: true);
    }

    /// <summary>Gets the BCL's named curve.</summary>
    public ECCurve Curve { get; }

    /// <summary>Gets the length in bytes of one coordinate.</summary>
    public int CoordinateLength { get; }

    /// <summary>Returns the curve of <paramref name="group" />.</summary>
    /// <param name="group">secp256r1, secp384r1 or secp521r1.</param>
    /// <returns>The curve.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The group is not a NIST curve.</exception>
    public static NistCurve Of(ushort group) => group switch
    {
        TlsNamedGroup.Secp256r1 => P256,
        TlsNamedGroup.Secp384r1 => P384,
        TlsNamedGroup.Secp521r1 => P521,
        _ => throw new ArgumentOutOfRangeException(nameof(group), group, "The group is not a NIST curve."),
    };

    /// <summary>Returns whether (<paramref name="x" />, <paramref name="y" />) is a point on the curve: both below the prime, and y² = x³ - 3x + b.</summary>
    /// <param name="x">The X coordinate, big-endian.</param>
    /// <param name="y">The Y coordinate, big-endian.</param>
    /// <returns><see langword="true" /> when the point is on the curve.</returns>
    public bool Contains(ReadOnlySpan<byte> x, ReadOnlySpan<byte> y)
    {
        BigInteger px = new(x, isUnsigned: true, isBigEndian: true);
        BigInteger py = new(y, isUnsigned: true, isBigEndian: true);
        if (px >= prime || py >= prime)
        {
            return false;
        }

        // x³ - 3x is at least -2 for x ≥ 0 and b is far above 2, so the remainder is never negative.
        return BigInteger.Remainder(py * py, prime) == BigInteger.Remainder((px * px * px) - (3 * px) + b, prime);
    }
}
