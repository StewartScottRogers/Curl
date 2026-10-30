using System.Numerics;
using System.Security.Cryptography;

namespace Curl.Extension.PrivacyPass.Tokens;

/// <summary>
/// The NIST P-384 group (SEC 2 §2.5.1, FIPS 186-5): <c>y^2 = x^3 - 3x + b</c> over the prime field of <see cref="Prime"/>,
/// point addition and scalar multiplication in Jacobian coordinates, the SEC1 compressed encoding of an element and
/// the 48-byte big-endian encoding of a scalar, as RFC 9497 §4.4 serializes them.
/// </summary>
public static class P384Group
{
    /// <summary>The length of an encoded element, <c>Ne</c>: a SEC1 compressed point.</summary>
    public const int ElementLength = 49;

    /// <summary>The length of an encoded scalar, <c>Ns</c>.</summary>
    public const int ScalarLength = 48;

    /// <summary>Gets the field prime <c>p</c>.</summary>
    public static BigInteger Prime { get; } = FromHex("FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFEFFFFFFFF0000000000000000FFFFFFFF");

    /// <summary>Gets the group order <c>n</c>.</summary>
    public static BigInteger Order { get; } = FromHex("FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFC7634D81F4372DDF581A0DB248B0A77AECEC196ACCC52973");

    /// <summary>Gets the curve coefficient <c>a</c>, <c>-3</c> modulo <see cref="Prime"/>.</summary>
    public static BigInteger A { get; } = Prime - 3;

    /// <summary>Gets the curve coefficient <c>b</c>.</summary>
    public static BigInteger B { get; } = FromHex("B3312FA7E23EE7E4988E056BE3F82D19181D9C6EFE8141120314088F5013875AC656398D8A2ED19D2A85C8EDD3EC2AEF");

    /// <summary>Gets the generator <c>G</c>.</summary>
    public static P384Point Generator { get; } = new(
        FromHex("AA87CA22BE8B05378EB1C71EF320AD746E1D3B628BA79B9859F741E082542A385502F25DBF55296C3A545E3872760AB7"),
        FromHex("3617DE4A96262C6F5D9E98BF9292DC29F8F41DBD289A147CE9DA3113B5F0B8C00A60B1CE1D7E819D7A431D7C90EA0E5F"));

    /// <summary>Gives <paramref name="left"/> + <paramref name="right"/>.</summary>
    /// <param name="left">One point.</param>
    /// <param name="right">The other point.</param>
    /// <returns>The sum.</returns>
    public static P384Point Add(P384Point left, P384Point right) => ToAffine(Add(ToJacobian(left), ToJacobian(right)));

    /// <summary>Gives <paramref name="scalar"/> · <paramref name="point"/> by double-and-add.</summary>
    /// <param name="scalar">The scalar, reduced modulo <see cref="Order"/> first.</param>
    /// <param name="point">The point.</param>
    /// <returns>The product.</returns>
    public static P384Point Multiply(BigInteger scalar, P384Point point)
    {
        var k = Reduce(scalar, Order);
        var addend = ToJacobian(point);
        var result = JacobianPoint.Identity;
        for (var bit = (int)k.GetBitLength() - 1; bit >= 0; bit--)
        {
            result = Double(result);
            if (!(k >> bit).IsEven)
            {
                result = Add(result, addend);
            }
        }

        return ToAffine(result);
    }

    /// <summary>Encodes <paramref name="point"/> in the SEC1 compressed form: <c>0x02</c> or <c>0x03</c> by y's parity, then x.</summary>
    /// <param name="point">The point, not the identity.</param>
    /// <returns>The <see cref="ElementLength"/> bytes.</returns>
    /// <exception cref="CryptographicException">The point is the identity, which has no encoding (RFC 9497 §2.1).</exception>
    public static byte[] Encode(P384Point point)
    {
        ArgumentNullException.ThrowIfNull(point);
        if (point.IsIdentity)
        {
            throw new CryptographicException("The identity element has no encoding.");
        }

        return [(byte)(point.Y.IsEven ? 0x02 : 0x03), .. EncodeScalar(point.X)];
    }

    /// <summary>Decodes a SEC1 compressed point and checks it is on the curve (RFC 9497 §2.1 DeserializeElement).</summary>
    /// <param name="encoded">The encoded point.</param>
    /// <returns>The point, or <see langword="null"/> when the bytes are not a compressed P-384 point.</returns>
    public static P384Point? Decode(ReadOnlySpan<byte> encoded)
    {
        if (encoded.Length != ElementLength || (encoded[0] | 1) != 0x03)
        {
            return null;
        }

        var x = new BigInteger(encoded[1..], isUnsigned: true, isBigEndian: true);
        return x < Prime ? PointOfParity(x, isYOdd: encoded[0] == 0x03) : null;
    }

    /// <summary>Encodes <paramref name="scalar"/> as <see cref="ScalarLength"/> big-endian bytes.</summary>
    /// <param name="scalar">The scalar, in [0, 2^384).</param>
    /// <returns>The bytes.</returns>
    public static byte[] EncodeScalar(BigInteger scalar)
    {
        var bytes = scalar.ToByteArray(isUnsigned: true, isBigEndian: true);
        var padded = new byte[ScalarLength];
        bytes.CopyTo(padded, ScalarLength - bytes.Length);
        return padded;
    }

    /// <summary>Decodes a <see cref="ScalarLength"/>-byte big-endian scalar (RFC 9497 §2.1 DeserializeScalar).</summary>
    /// <param name="encoded">The bytes.</param>
    /// <returns>The scalar, or <see langword="null"/> when the length is wrong or the value is not below <see cref="Order"/>.</returns>
    public static BigInteger? DecodeScalar(ReadOnlySpan<byte> encoded)
    {
        var scalar = new BigInteger(encoded, isUnsigned: true, isBigEndian: true);
        return encoded.Length == ScalarLength && scalar < Order ? scalar : null;
    }

    /// <summary>Gives a square root of <paramref name="value"/> modulo <see cref="Prime"/>, <c>value^((p+1)/4)</c> as p ≡ 3 (mod 4).</summary>
    /// <param name="value">The value, in [0, p).</param>
    /// <returns>The root, or <see langword="null"/> when <paramref name="value"/> is not a square.</returns>
    internal static BigInteger? SquareRoot(BigInteger value)
    {
        var root = BigInteger.ModPow(value, (Prime + 1) / 4, Prime);
        return root * root % Prime == value ? root : null;
    }

    /// <summary>Gives <c>x^3 + a·x + b</c> modulo <see cref="Prime"/>.</summary>
    /// <param name="x">The x-coordinate, in [0, p).</param>
    /// <returns>The value y^2 must take.</returns>
    internal static BigInteger CurveRightHandSide(BigInteger x) => ((x * x % Prime * x) + (A * x) + B) % Prime;

    /// <summary>Gives <paramref name="value"/>'s inverse modulo <see cref="Prime"/>, or zero for zero (RFC 9380 §4 inv0).</summary>
    /// <param name="value">The value, in [0, p).</param>
    /// <returns>The inverse.</returns>
    internal static BigInteger Inverse(BigInteger value) => BigInteger.ModPow(value, Prime - 2, Prime);

    /// <summary>Gives <paramref name="value"/> modulo <paramref name="modulus"/> in [0, modulus).</summary>
    /// <param name="value">The value.</param>
    /// <param name="modulus">The modulus.</param>
    /// <returns>The residue.</returns>
    internal static BigInteger Reduce(BigInteger value, BigInteger modulus)
    {
        var residue = value % modulus;
        return residue.Sign < 0 ? residue + modulus : residue;
    }

    private static BigInteger FromHex(string hex) => new(Convert.FromHexString(hex), isUnsigned: true, isBigEndian: true);

    private static BigInteger Field(BigInteger value) => Reduce(value, Prime);

    // The curve point with x-coordinate x whose y has the given parity, or null when x is on no point.
    private static P384Point? PointOfParity(BigInteger x, bool isYOdd) =>
        SquareRoot(CurveRightHandSide(x)) is { } root ? new(x, root.IsEven != isYOdd ? root : Prime - root) : null;

    private static JacobianPoint ToJacobian(P384Point point) =>
        point.IsIdentity ? JacobianPoint.Identity : new(point.X, point.Y, BigInteger.One);

    private static P384Point ToAffine(JacobianPoint point)
    {
        if (point.Z.IsZero)
        {
            return P384Point.Identity;
        }

        var zInverse = Inverse(point.Z);
        var zInverseSquared = zInverse * zInverse % Prime;
        return new(point.X * zInverseSquared % Prime, point.Y * zInverseSquared % Prime * zInverse % Prime);
    }

    // dbl-2001-b for a = -3; the identity (Z = 0) doubles to Z3 = Y^2 - Y^2 - 0 = 0 with no special case.
    private static JacobianPoint Double(JacobianPoint point)
    {
        var delta = point.Z * point.Z % Prime;
        var gamma = point.Y * point.Y % Prime;
        var beta = point.X * gamma % Prime;
        var alpha = 3 * Field(point.X - delta) * (point.X + delta) % Prime;
        var x3 = Field((alpha * alpha) - (8 * beta));
        var z3 = Field(((point.Y + point.Z) * (point.Y + point.Z)) - gamma - delta);
        var y3 = Field((alpha * Field((4 * beta) - x3)) - (8 * gamma % Prime * gamma));
        return new(x3, y3, z3);
    }

    // add-2007-bl, falling back to doubling for equal points and giving the identity for opposite ones.
    private static JacobianPoint Add(JacobianPoint left, JacobianPoint right)
    {
        if (left.Z.IsZero || right.Z.IsZero)
        {
            return left.Z.IsZero ? right : left;
        }

        var z1z1 = left.Z * left.Z % Prime;
        var z2z2 = right.Z * right.Z % Prime;
        var u1 = left.X * z2z2 % Prime;
        var s1 = left.Y * right.Z % Prime * z2z2 % Prime;
        var h = Field((right.X * z1z1) - u1);
        var r = Field(2 * ((right.Y * left.Z % Prime * z1z1) - s1));
        if (h.IsZero)
        {
            return r.IsZero ? Double(left) : JacobianPoint.Identity;
        }

        var i = 4 * h * h % Prime;
        var j = h * i % Prime;
        var v = u1 * i % Prime;
        var x3 = Field((r * r) - j - (2 * v));
        var y3 = Field((r * Field(v - x3)) - (2 * s1 * j));
        var z3 = Field((((left.Z + right.Z) * (left.Z + right.Z)) - z1z1 - z2z2) * h);
        return new(x3, y3, z3);
    }

    private readonly record struct JacobianPoint(BigInteger X, BigInteger Y, BigInteger Z)
    {
        public static JacobianPoint Identity { get; } = new(BigInteger.One, BigInteger.One, BigInteger.Zero);
    }
}
