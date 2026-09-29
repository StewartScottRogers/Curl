using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Curl.Cryptography;

/// <summary>
/// Points on a brainpool curve y^2 = x^3 + ax + b in projective coordinates (X : Y : Z),
/// x = X / Z and y = Y / Z, each coordinate <see cref="BrainpoolDomainParameters.LimbCount" />
/// limbs in Montgomery form, laid out X, Y, Z in one span: the complete addition law,
/// constant-time scalar multiplication, and the uncompressed encoding of SEC 1 section
/// 2.3.3 and 2.3.4 that TLS's key shares and X.509 keys carry.
/// </summary>
/// <remarks>
/// <para>
/// Addition is algorithm 1 of Renes, Costello and Batina, "Complete addition formulas for
/// prime order elliptic curves" (EUROCRYPT 2016), for any a: one formula, with no branch,
/// that is correct for doubling, for the point at infinity (0 : 1 : 0) and for a point
/// and its negative, on every curve of odd order, which the brainpool r1 curves are
/// (cofactor 1). So scalar multiplication never has a special case to branch on.
/// </para>
/// <para>
/// <see cref="MultiplyScalar" /> is constant-time in the scalar and the point: a fixed
/// 4-bit window over every scalar bit, four doublings and one addition per window whatever
/// its value, the table entry chosen by masks while reading all 16 entries, so no branch,
/// loop bound or table index depends on a secret. Its working memory is zeroed before it
/// returns.
/// </para>
/// </remarks>
internal static class BrainpoolPoint
{
    /// <summary>The bits a scalar window covers; the table holds 2^4 = 16 multiples.</summary>
    private const int WindowBits = 4;

    /// <summary>The number of precomputed multiples, 0P to 15P.</summary>
    private const int TableSize = 1 << WindowBits;

    /// <summary>Returns the working space in limbs <see cref="Add" /> needs for a curve of <paramref name="limbCount" /> limbs.</summary>
    public static int AddWorkLength(int limbCount) => (10 * limbCount) + 2;

    /// <summary>
    /// Sets <paramref name="result" /> to <paramref name="left" /> + <paramref name="right" />
    /// by the complete formulas (Renes, Costello and Batina, algorithm 1). <paramref name="result" />
    /// may alias either operand.
    /// </summary>
    /// <param name="domain">The curve.</param>
    /// <param name="result">Receives the sum, 3 * n limbs.</param>
    /// <param name="left">The first point, 3 * n limbs.</param>
    /// <param name="right">The second point, 3 * n limbs.</param>
    /// <param name="work">At least <see cref="AddWorkLength" /> limbs of working space.</param>
    public static void Add(BrainpoolDomainParameters domain, Span<uint> result, ReadOnlySpan<uint> left, ReadOnlySpan<uint> right, Span<uint> work)
    {
        int n = domain.LimbCount;
        MontgomeryModulus f = domain.Field;
        ReadOnlySpan<uint> x1 = left[..n], y1 = left.Slice(n, n), z1 = left.Slice(2 * n, n);
        ReadOnlySpan<uint> x2 = right[..n], y2 = right.Slice(n, n), z2 = right.Slice(2 * n, n);
        Span<uint> t0 = work[..n], t1 = work.Slice(n, n), t2 = work.Slice(2 * n, n);
        Span<uint> t3 = work.Slice(3 * n, n), t4 = work.Slice(4 * n, n), t5 = work.Slice(5 * n, n);
        Span<uint> x3 = work.Slice(6 * n, n), y3 = work.Slice(7 * n, n), z3 = work.Slice(8 * n, n);
        Span<uint> s = work.Slice(9 * n, n + 2);
        f.Multiply(t0, x1, x2, s);
        f.Multiply(t1, y1, y2, s);
        f.Multiply(t2, z1, z2, s);
        f.Add(t3, x1, y1, s);
        f.Add(t4, x2, y2, s);
        f.Multiply(t3, t3, t4, s);
        f.Add(t4, t0, t1, s);
        f.Subtract(t3, t3, t4);
        f.Add(t4, x1, z1, s);
        f.Add(t5, x2, z2, s);
        f.Multiply(t4, t4, t5, s);
        f.Add(t5, t0, t2, s);
        f.Subtract(t4, t4, t5);
        f.Add(t5, y1, z1, s);
        f.Add(x3, y2, z2, s);
        f.Multiply(t5, t5, x3, s);
        f.Add(x3, t1, t2, s);
        f.Subtract(t5, t5, x3);
        f.Multiply(z3, domain.A, t4, s);
        f.Multiply(x3, domain.ThreeB, t2, s);
        f.Add(z3, x3, z3, s);
        f.Subtract(x3, t1, z3);
        f.Add(z3, t1, z3, s);
        f.Multiply(y3, x3, z3, s);
        f.Add(t1, t0, t0, s);
        f.Add(t1, t1, t0, s);
        f.Multiply(t2, domain.A, t2, s);
        f.Multiply(t4, domain.ThreeB, t4, s);
        f.Add(t1, t1, t2, s);
        f.Subtract(t2, t0, t2);
        f.Multiply(t2, domain.A, t2, s);
        f.Add(t4, t4, t2, s);
        f.Multiply(t0, t1, t4, s);
        f.Add(y3, y3, t0, s);
        f.Multiply(t0, t5, t4, s);
        f.Multiply(x3, t3, x3, s);
        f.Subtract(x3, x3, t0);
        f.Multiply(t0, t3, t1, s);
        f.Multiply(z3, t5, z3, s);
        f.Add(z3, z3, t0, s);
        work.Slice(6 * n, 3 * n).CopyTo(result);
    }

    /// <summary>
    /// Sets <paramref name="result" /> to <paramref name="scalar" /> * <paramref name="point" />,
    /// constant-time in both (the remarks say how). The scalar is big-endian and any
    /// length; only its length shapes the running time.
    /// </summary>
    /// <param name="domain">The curve.</param>
    /// <param name="scalar">The big-endian scalar.</param>
    /// <param name="point">The point, 3 * n limbs.</param>
    /// <param name="result">Receives the product, 3 * n limbs; may alias <paramref name="point" />.</param>
    public static void MultiplyScalar(BrainpoolDomainParameters domain, ReadOnlySpan<byte> scalar, ReadOnlySpan<uint> point, Span<uint> result)
    {
        int size = 3 * domain.LimbCount;
        uint[] memory = new uint[((TableSize + 2) * size) + AddWorkLength(domain.LimbCount)];
        Span<uint> table = memory.AsSpan(0, TableSize * size);
        Span<uint> accumulator = memory.AsSpan(TableSize * size, size);
        Span<uint> selected = memory.AsSpan((TableSize + 1) * size, size);
        Span<uint> work = memory.AsSpan((TableSize + 2) * size);
        try
        {
            FillTable(domain, point, table, work);
            table[..size].CopyTo(accumulator);
            foreach (byte scalarByte in scalar)
            {
                ApplyWindow(domain, accumulator, table, (uint)scalarByte >> WindowBits, selected, work);
                ApplyWindow(domain, accumulator, table, scalarByte & 0xFu, selected, work);
            }

            accumulator.CopyTo(result);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(memory.AsSpan()));
        }
    }

    /// <summary>
    /// Reads an uncompressed point 0x04 || x || y (SEC 1 section 2.3.4) into
    /// <paramref name="point" /> as (x : y : 1) and returns whether it is a point of the
    /// curve: the right length and form, both coordinates below p, and y^2 = x^3 + ax + b.
    /// The point at infinity (the single byte 0x00) and the compressed forms are
    /// <see langword="false" />. The encoding is public, so the checks may stop early.
    /// </summary>
    /// <param name="domain">The curve.</param>
    /// <param name="encoded">The encoding a peer sent.</param>
    /// <param name="point">Receives the point, 3 * n limbs; its contents are unspecified on <see langword="false" />.</param>
    /// <returns><see langword="true" /> when the encoding is a point of the curve.</returns>
    public static bool TryDecode(BrainpoolDomainParameters domain, ReadOnlySpan<byte> encoded, Span<uint> point)
    {
        int n = domain.LimbCount;
        int length = domain.Length;
        if (encoded.Length != 1 + (2 * length) || encoded[0] != 0x04)
        {
            return false;
        }

        Span<uint> x = point[..n], y = point.Slice(n, n);
        MontgomeryModulus.ToLimbs(encoded.Slice(1, length), x);
        MontgomeryModulus.ToLimbs(encoded[(1 + length)..], y);
        if (!domain.Field.IsBelowModulus(x) || !domain.Field.IsBelowModulus(y))
        {
            return false;
        }

        uint[] scratch = new uint[n + 2];
        domain.Field.ToMontgomeryForm(x, x, scratch);
        domain.Field.ToMontgomeryForm(y, y, scratch);
        domain.One.CopyTo(point.Slice(2 * n, n));
        return IsOnCurve(domain, x, y, scratch);
    }

    /// <summary>
    /// Writes <paramref name="point" /> in affine form: x and y in ordinary form into
    /// <paramref name="x" /> and <paramref name="y" />, n limbs each. Returns whether it is
    /// not the point at infinity, whose x and y come out as 0; the answer is computed
    /// without a branch and the inversion is a fixed exponentiation, so a secret point
    /// costs the same time whatever it is.
    /// </summary>
    public static bool ToAffine(BrainpoolDomainParameters domain, ReadOnlySpan<uint> point, Span<uint> x, Span<uint> y)
    {
        int n = domain.LimbCount;
        uint[] memory = new uint[(2 * n) + 2];
        Span<uint> inverse = memory.AsSpan(0, n);
        Span<uint> scratch = memory.AsSpan(n);
        try
        {
            ReadOnlySpan<uint> z = point.Slice(2 * n, n);
            domain.InvertField(z, inverse);
            domain.Field.Multiply(x, point[..n], inverse, scratch);
            domain.Field.Multiply(y, point.Slice(n, n), inverse, scratch);
            return !BrainpoolDomainParameters.IsZero(z);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(memory.AsSpan()));
        }
    }

    /// <summary>Writes the affine point (<paramref name="x" />, <paramref name="y" />), ordinary form, as 0x04 || x || y into <paramref name="destination" />, 1 + 2 * <see cref="BrainpoolDomainParameters.Length" /> bytes.</summary>
    public static void EncodeUncompressed(BrainpoolDomainParameters domain, ReadOnlySpan<uint> x, ReadOnlySpan<uint> y, Span<byte> destination)
    {
        destination[0] = 0x04;
        MontgomeryModulus.FromLimbs(x, destination.Slice(1, domain.Length));
        MontgomeryModulus.FromLimbs(y, destination[(1 + domain.Length)..]);
    }

    /// <summary>Whether y^2 = x^3 + ax + b, all in Montgomery form.</summary>
    private static bool IsOnCurve(BrainpoolDomainParameters domain, ReadOnlySpan<uint> x, ReadOnlySpan<uint> y, Span<uint> scratch)
    {
        int n = domain.LimbCount;
        MontgomeryModulus f = domain.Field;
        uint[] memory = new uint[2 * n];
        Span<uint> left = memory.AsSpan(0, n);
        Span<uint> right = memory.AsSpan(n, n);
        f.Multiply(left, y, y, scratch);
        f.Multiply(right, x, x, scratch);
        f.Add(right, right, domain.A, scratch);
        f.Multiply(right, right, x, scratch);
        f.Add(right, right, domain.B, scratch);
        return left.SequenceEqual(right);
    }

    /// <summary>Fills the table with 0P (the point at infinity, (0 : 1 : 0)) to 15P.</summary>
    private static void FillTable(BrainpoolDomainParameters domain, ReadOnlySpan<uint> point, Span<uint> table, Span<uint> work)
    {
        int n = domain.LimbCount;
        int size = 3 * n;
        table[..size].Clear();
        domain.One.CopyTo(table.Slice(n, n));
        point.CopyTo(table.Slice(size, size));
        for (int entry = 2; entry < TableSize; entry++)
        {
            Add(domain, table.Slice(entry * size, size), table.Slice((entry - 1) * size, size), point, work);
        }
    }

    /// <summary>Doubles the accumulator four times, then adds table entry <paramref name="window" />, even when it is the point at infinity.</summary>
    private static void ApplyWindow(BrainpoolDomainParameters domain, Span<uint> accumulator, ReadOnlySpan<uint> table, uint window, Span<uint> selected, Span<uint> work)
    {
        for (int doubling = 0; doubling < WindowBits; doubling++)
        {
            Add(domain, accumulator, accumulator, accumulator, work);
        }

        SelectEntry(table, window, selected);
        Add(domain, accumulator, accumulator, selected, work);
    }

    /// <summary>Sets <paramref name="destination" /> to the one entry of <paramref name="table" /> <paramref name="window" /> names, reading every entry.</summary>
    private static void SelectEntry(ReadOnlySpan<uint> table, uint window, Span<uint> destination)
    {
        int size = destination.Length;
        destination.Clear();
        for (int entry = 0; entry < TableSize; entry++)
        {
            uint mask = ConstantTime.EqualMask((uint)entry, window);
            ReadOnlySpan<uint> candidate = table.Slice(entry * size, size);
            for (int limb = 0; limb < size; limb++)
            {
                destination[limb] |= candidate[limb] & mask;
            }
        }
    }
}
