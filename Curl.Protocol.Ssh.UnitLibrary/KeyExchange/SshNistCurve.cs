using System.Numerics;
using System.Security.Cryptography;

namespace Curl.Protocol.Ssh.KeyExchange;

/// <summary>
/// One of the three NIST prime curves SSH names <c>nistp256</c>, <c>nistp384</c> and
/// <c>nistp521</c> (RFC 5656 section 10.1), with the hash its <c>ecdh-sha2-*</c> and
/// <c>ecdsa-sha2-*</c> algorithms use (section 6.2.1), and the check that a public point a
/// peer sent lies on it (section 3.1, SEC 1 section 3.2.2.1). The check is done here, not
/// left to the platform's key import, so every platform refuses the same points.
/// </summary>
internal sealed class SshNistCurve
{
    private readonly BigInteger prime;

    private readonly BigInteger coefficientB;

    private SshNistCurve(string identifier, ECCurve curve, int fieldLength, HashAlgorithmName hashAlgorithm, string primeHex, string coefficientBHex)
    {
        Identifier = identifier;
        Curve = curve;
        FieldLength = fieldLength;
        HashAlgorithm = hashAlgorithm;
        prime = BigInteger.Parse("0" + primeHex, System.Globalization.NumberStyles.HexNumber);
        coefficientB = BigInteger.Parse("0" + coefficientBHex, System.Globalization.NumberStyles.HexNumber);
    }

    /// <summary>Gets P-256 (<c>secp256r1</c>) with SHA-256.</summary>
    internal static SshNistCurve NistP256 { get; } = new(
        "nistp256",
        ECCurve.NamedCurves.nistP256,
        32,
        HashAlgorithmName.SHA256,
        "FFFFFFFF00000001000000000000000000000000FFFFFFFFFFFFFFFFFFFFFFFF",
        "5AC635D8AA3A93E7B3EBBD55769886BC651D06B0CC53B0F63BCE3C3E27D2604B");

    /// <summary>Gets P-384 (<c>secp384r1</c>) with SHA-384.</summary>
    internal static SshNistCurve NistP384 { get; } = new(
        "nistp384",
        ECCurve.NamedCurves.nistP384,
        48,
        HashAlgorithmName.SHA384,
        "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFEFFFFFFFF0000000000000000FFFFFFFF",
        "B3312FA7E23EE7E4988E056BE3F82D19181D9C6EFE8141120314088F5013875AC656398D8A2ED19D2A85C8EDD3EC2AEF");

    /// <summary>Gets P-521 (<c>secp521r1</c>) with SHA-512.</summary>
    internal static SshNistCurve NistP521 { get; } = new(
        "nistp521",
        ECCurve.NamedCurves.nistP521,
        66,
        HashAlgorithmName.SHA512,
        "01FF" + new string('F', 128),
        "0051953EB9618E1C9A1F929A21A0B68540EEA2DA725B99B315F3B8B489918EF109E156193951EC7E937B1652C0BD3BB1BF073573DF883D2C34F1EF451FD46B503F00");

    /// <summary>Gets the name SSH gives the curve, as in <c>ecdsa-sha2-nistp256</c>.</summary>
    internal string Identifier { get; }

    /// <summary>Gets the curve as the BCL names it.</summary>
    internal ECCurve Curve { get; }

    /// <summary>Gets the length in bytes of one coordinate.</summary>
    internal int FieldLength { get; }

    /// <summary>Gets the hash the curve's SSH algorithms use.</summary>
    internal HashAlgorithmName HashAlgorithm { get; }

    /// <summary>
    /// Encodes a point uncompressed, <c>0x04 || X || Y</c>, each coordinate
    /// <see cref="FieldLength" /> bytes (SEC 1 section 2.3.3), as SSH's <c>Q</c> carries it.
    /// </summary>
    /// <param name="point">The point.</param>
    /// <returns>The encoding.</returns>
    internal byte[] EncodePoint(ECPoint point)
    {
        byte[] encoded = new byte[1 + (2 * FieldLength)];
        encoded[0] = 0x04;
        point.X.CopyTo(encoded.AsSpan(1 + FieldLength - point.X!.Length));
        point.Y.CopyTo(encoded.AsSpan(1 + (2 * FieldLength) - point.Y!.Length));
        return encoded;
    }

    /// <summary>
    /// Decodes a peer's uncompressed point and checks that it lies on the curve.
    /// </summary>
    /// <param name="encoded">The encoding, <c>0x04 || X || Y</c>.</param>
    /// <returns>The public key parameters.</returns>
    /// <exception cref="InvalidDataException">
    /// The encoding is not uncompressed or has the wrong length, a coordinate is not below
    /// the prime, or the point is not on the curve.
    /// </exception>
    internal ECParameters DecodePublicPoint(ReadOnlySpan<byte> encoded)
    {
        if (encoded.Length != 1 + (2 * FieldLength) || encoded[0] != 0x04)
        {
            throw new InvalidDataException($"The {Identifier} point is not an uncompressed point of {1 + (2 * FieldLength)} bytes.");
        }

        byte[] x = encoded.Slice(1, FieldLength).ToArray();
        byte[] y = encoded.Slice(1 + FieldLength, FieldLength).ToArray();
        if (!IsOnCurve(ToInteger(x), ToInteger(y)))
        {
            throw new InvalidDataException($"The {Identifier} point is not on the curve.");
        }

        return new ECParameters { Curve = Curve, Q = new ECPoint { X = x, Y = y } };
    }

    private static BigInteger ToInteger(byte[] bigEndian) => new(bigEndian, isUnsigned: true, isBigEndian: true);

    private bool IsOnCurve(BigInteger x, BigInteger y)
    {
        if (x >= prime || y >= prime)
        {
            return false;
        }

        BigInteger left = BigInteger.ModPow(y, 2, prime);
        BigInteger right = (((BigInteger.ModPow(x, 3, prime) - (3 * x) + coefficientB) % prime) + prime) % prime;
        return left == right;
    }
}
