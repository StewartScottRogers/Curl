namespace Curl.Cryptography;

/// <summary>
/// Points on edwards448, the untwisted Edwards curve x^2 + y^2 = 1 + d x^2 y^2,
/// d = -39081, over GF(2^448 - 2^224 - 1) that Ed448 signs on (RFC 8032 section 5.2). A
/// point is <see cref="PointLength" /> limbs in a caller's <see cref="Span{T}" />: the
/// projective coordinates X, Y, Z (x = X / Z, y = Y / Z), each a <see cref="Field448" />
/// element.
/// </summary>
/// <remarks>
/// Addition, scalar multiplication and encoding are constant-time: d is not a square, so
/// the addition formula is complete and also doubles, and scalar multiplication visits
/// every scalar bit with the same operations and swaps by mask. <see cref="TryDecode" />
/// is not; it reads only public keys.
/// </remarks>
internal static class Edwards448
{
    /// <summary>The number of limbs in one point.</summary>
    public const int PointLength = 3 * Field448.LimbCount;

    /// <summary>The number of bytes in a point's encoding.</summary>
    public const int EncodedLength = 57;

    /// <summary>
    /// (p - 3) / 4 = 2^446 - 2^222 - 1, little-endian: the square-root exponent of RFC 8032
    /// section 5.2.3 (every bit from 445 down to 0 set but bit 222).
    /// </summary>
    private static readonly byte[] SquareRootExponent = CreateSquareRootExponent();

    /// <summary>The curve constant d = -39081.</summary>
    private static readonly long[] CurveConstant = CreateCurveConstant();

    /// <summary>The base point B, decoded from its encoding (RFC 8032 section 5.2, x even).</summary>
    private static readonly long[] BasePoint = DecodeBasePoint();

    /// <summary>B's y-coordinate, little-endian, which is also B's encoding with the sign bit 0.</summary>
    private static ReadOnlySpan<byte> BasePointEncoding =>
    [
        0x14, 0xFA, 0x30, 0xF2, 0x5B, 0x79, 0x08, 0x98, 0xAD, 0xC8, 0xD7, 0x4E, 0x2C, 0x13, 0xBD, 0xFD,
        0xC4, 0x39, 0x7C, 0xE6, 0x1C, 0xFF, 0xD3, 0x3A, 0xD7, 0xC2, 0xA0, 0x05, 0x1E, 0x9C, 0x78, 0x87,
        0x40, 0x98, 0xA3, 0x6C, 0x73, 0x73, 0xEA, 0x4B, 0x62, 0xC7, 0xC9, 0x56, 0x37, 0x20, 0x76, 0x88,
        0x24, 0xBC, 0xB6, 0x6E, 0x71, 0x46, 0x3F, 0x69, 0x00,
    ];

    /// <summary>Sets <paramref name="point" /> to the neutral element (0, 1).</summary>
    public static void SetNeutral(Span<long> point)
    {
        point.Clear();
        Y(point)[0] = 1;
        Z(point)[0] = 1;
    }

    /// <summary>Sets <paramref name="point" /> to its negation (-x, y).</summary>
    public static void Negate(Span<long> point) => Field448.Negate(X(point), X(point));

    /// <summary>
    /// Sets <paramref name="point" /> to <paramref name="point" /> + <paramref name="addend" />
    /// with the complete projective formula of RFC 8032 section 5.2.4, which also doubles;
    /// <paramref name="addend" /> may be <paramref name="point" /> itself.
    /// </summary>
    public static void Add(Span<long> point, ReadOnlySpan<long> addend)
    {
        const int Limbs = Field448.LimbCount;
        Span<long> scratch = stackalloc long[9 * Limbs];
        Span<long> a = scratch[..Limbs];
        Span<long> b = scratch.Slice(Limbs, Limbs);
        Span<long> c = scratch.Slice(2 * Limbs, Limbs);
        Span<long> d = scratch.Slice(3 * Limbs, Limbs);
        Span<long> e = scratch.Slice(4 * Limbs, Limbs);
        Span<long> f = scratch.Slice(5 * Limbs, Limbs);
        Span<long> g = scratch.Slice(6 * Limbs, Limbs);
        Span<long> h = scratch.Slice(7 * Limbs, Limbs);
        Span<long> term = scratch.Slice(8 * Limbs, Limbs);
        try
        {
            Field448.Multiply(a, Z(point), Z(addend));
            Field448.Square(b, a);
            Field448.Multiply(c, X(point), X(addend));
            Field448.Multiply(d, Y(point), Y(addend));
            Field448.Multiply(e, c, d);
            Field448.Multiply(e, e, CurveConstant);
            Field448.Subtract(f, b, e);
            Field448.Add(g, b, e);
            Field448.Add(h, X(point), Y(point));
            Field448.Add(term, X(addend), Y(addend));
            Field448.Multiply(h, h, term);
            Field448.Subtract(h, h, c);
            Field448.Subtract(h, h, d);
            Field448.Subtract(term, d, c);
            Field448.Multiply(X(point), a, f);
            Field448.Multiply(X(point), X(point), h);
            Field448.Multiply(Y(point), a, g);
            Field448.Multiply(Y(point), Y(point), term);
            Field448.Multiply(Z(point), f, g);
        }
        finally
        {
            Field448.Clear(scratch);
        }
    }

    /// <summary>
    /// Sets <paramref name="result" /> to [<paramref name="scalar" />]<paramref name="point" />
    /// for a 57-byte little-endian scalar, by a double-and-add ladder over all 456 bits that
    /// swaps by mask. <paramref name="result" /> must not overlap <paramref name="point" />.
    /// </summary>
    public static void ScalarMultiply(Span<long> result, ReadOnlySpan<long> point, ReadOnlySpan<byte> scalar)
    {
        Span<long> addend = stackalloc long[PointLength];
        try
        {
            point.CopyTo(addend);
            SetNeutral(result);
            for (int bit = (8 * Scalar448.EncodedLength) - 1; bit >= 0; bit--)
            {
                uint scalarBit = (uint)(scalar[bit >> 3] >> (bit & 7)) & 1u;
                ConditionalSwap(result, addend, scalarBit);
                Add(addend, result);
                Add(result, result);
                ConditionalSwap(result, addend, scalarBit);
            }
        }
        finally
        {
            Field448.Clear(addend);
        }
    }

    /// <summary>Sets <paramref name="result" /> to [<paramref name="scalar" />]B.</summary>
    public static void ScalarMultiplyBase(Span<long> result, ReadOnlySpan<byte> scalar) =>
        ScalarMultiply(result, BasePoint, scalar);

    /// <summary>
    /// Encodes <paramref name="point" /> as RFC 8032 section 5.2.2 specifies: y as 56
    /// little-endian bytes, then a byte holding the lowest bit of x in its top bit.
    /// </summary>
    public static void Encode(Span<byte> encoded, ReadOnlySpan<long> point)
    {
        Span<long> scratch = stackalloc long[3 * Field448.LimbCount];
        Span<long> inverse = scratch[..Field448.LimbCount];
        Span<long> x = scratch.Slice(Field448.LimbCount, Field448.LimbCount);
        Span<long> y = scratch.Slice(2 * Field448.LimbCount, Field448.LimbCount);
        try
        {
            Field448.Invert(inverse, Z(point));
            Field448.Multiply(x, X(point), inverse);
            Field448.Multiply(y, Y(point), inverse);
            Field448.Encode(encoded[..Field448.EncodedLength], y);
            encoded[EncodedLength - 1] = (byte)(Field448.Parity(x) << 7);
        }
        finally
        {
            Field448.Clear(scratch);
        }
    }

    /// <summary>
    /// Decodes <paramref name="encoded" /> into <paramref name="point" /> as RFC 8032
    /// section 5.2.3 specifies. Not constant-time: it reads public keys only.
    /// </summary>
    /// <returns>
    /// <c>false</c> when any of the last byte's low seven bits is set, when y is not below
    /// p, when x^2 = (y^2 - 1) / (d y^2 - 1) has no square root, or when x is 0 and its
    /// sign bit is 1; otherwise <c>true</c>.
    /// </returns>
    public static bool TryDecode(Span<long> point, ReadOnlySpan<byte> encoded)
    {
        Span<byte> reencoded = stackalloc byte[EncodedLength];
        Field448.Decode(Y(point), encoded);
        Field448.Encode(reencoded, Y(point));
        reencoded[EncodedLength - 1] = (byte)(encoded[EncodedLength - 1] & 0x80);
        if (!reencoded.SequenceEqual(encoded) || !TryRecoverX(X(point), Y(point)))
        {
            return false;
        }

        uint sign = (uint)encoded[EncodedLength - 1] >> 7;
        Span<long> zero = stackalloc long[Field448.LimbCount];
        Field448.SetSmall(zero, 0);
        if (sign == 1 && Field448.AreEqual(X(point), zero))
        {
            return false;
        }

        if (Field448.Parity(X(point)) != sign)
        {
            Field448.Negate(X(point), X(point));
        }

        Field448.SetSmall(Z(point), 1);
        return true;
    }

    /// <summary>
    /// Sets <paramref name="x" /> to u^3 v (u^5 v^3)^((p - 3) / 4), u = y^2 - 1,
    /// v = d y^2 - 1, the candidate square root of u / v (RFC 8032 section 5.2.3, step 2).
    /// </summary>
    /// <returns><c>false</c> when v x^2 differs from u, so u / v is not a square.</returns>
    private static bool TryRecoverX(Span<long> x, ReadOnlySpan<long> y)
    {
        const int Limbs = Field448.LimbCount;
        Span<long> scratch = stackalloc long[5 * Limbs];
        Span<long> u = scratch[..Limbs];
        Span<long> v = scratch.Slice(Limbs, Limbs);
        Span<long> uCubed = scratch.Slice(2 * Limbs, Limbs);
        Span<long> term = scratch.Slice(3 * Limbs, Limbs);
        Span<long> one = scratch.Slice(4 * Limbs, Limbs);
        Field448.SetSmall(one, 1);
        Field448.Square(u, y);
        Field448.Multiply(v, u, CurveConstant);
        Field448.Subtract(u, u, one);
        Field448.Subtract(v, v, one);
        Field448.Square(uCubed, u);
        Field448.Multiply(uCubed, uCubed, u);
        Field448.Square(term, v);
        Field448.Multiply(term, term, v);
        Field448.Multiply(term, term, uCubed);
        Field448.Multiply(term, term, u);
        Field448.Multiply(term, term, u);
        Field448.PowerByPublicExponent(term, term, SquareRootExponent);
        Field448.Multiply(x, uCubed, v);
        Field448.Multiply(x, x, term);
        Field448.Square(term, x);
        Field448.Multiply(term, term, v);
        return Field448.AreEqual(term, u);
    }

    /// <summary>Swaps two points by mask when the lowest bit of <paramref name="bit" /> is <c>1</c>.</summary>
    private static void ConditionalSwap(Span<long> left, Span<long> right, uint bit)
    {
        for (int offset = 0; offset < PointLength; offset += Field448.LimbCount)
        {
            Field448.ConditionalSwap(
                left.Slice(offset, Field448.LimbCount),
                right.Slice(offset, Field448.LimbCount),
                bit);
        }
    }

    private static Span<long> X(Span<long> point) => point[..Field448.LimbCount];

    private static Span<long> Y(Span<long> point) => point.Slice(Field448.LimbCount, Field448.LimbCount);

    private static Span<long> Z(Span<long> point) => point.Slice(2 * Field448.LimbCount, Field448.LimbCount);

    private static ReadOnlySpan<long> X(ReadOnlySpan<long> point) => point[..Field448.LimbCount];

    private static ReadOnlySpan<long> Y(ReadOnlySpan<long> point) => point.Slice(Field448.LimbCount, Field448.LimbCount);

    private static ReadOnlySpan<long> Z(ReadOnlySpan<long> point) => point.Slice(2 * Field448.LimbCount, Field448.LimbCount);

    private static byte[] CreateSquareRootExponent()
    {
        byte[] exponent = new byte[Field448.EncodedLength];
        exponent.AsSpan().Fill(0xFF);
        exponent[27] = 0xBF;
        exponent[^1] = 0x3F;
        return exponent;
    }

    private static long[] CreateCurveConstant()
    {
        long[] constant = new long[Field448.LimbCount];
        constant[0] = -39081;
        return constant;
    }

    private static long[] DecodeBasePoint()
    {
        long[] point = new long[PointLength];
        _ = TryDecode(point, BasePointEncoding);
        return point;
    }
}
