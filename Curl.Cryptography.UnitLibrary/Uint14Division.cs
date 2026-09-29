namespace Curl.Cryptography;

/// <summary>
/// Division of a secret 32-bit value by a public modulus below 2^14 without the CPU's
/// divide instruction, whose running time depends on its operands: two multiplications
/// by a precomputed reciprocal and a masked correction, as NTRU Prime's reference
/// <c>uint32.c</c> and <c>int32.c</c> do (round 3, 2020). Constant-time in the dividend;
/// the modulus must be public.
/// </summary>
internal static class Uint14Division
{
    /// <summary>
    /// Returns <paramref name="dividend" /> mod <paramref name="modulus" /> and sets
    /// <paramref name="quotient" /> to the quotient (<c>uint32_divmod_uint14</c>).
    /// <paramref name="modulus" /> must be 1 to 16383.
    /// </summary>
    public static ushort DivideWithRemainder(uint dividend, ushort modulus, out uint quotient)
    {
        uint reciprocal = 0x80000000u / modulus;
        uint part = (uint)(((ulong)dividend * reciprocal) >> 31);
        dividend -= part * modulus;
        quotient = part;
        part = (uint)(((ulong)dividend * reciprocal) >> 31);
        dividend -= part * modulus;
        quotient += part;
        dividend -= modulus;
        quotient += 1;
        uint mask = 0u - (dividend >> 31);
        dividend += mask & modulus;
        quotient += mask;
        return (ushort)dividend;
    }

    /// <summary>Returns <paramref name="dividend" /> mod <paramref name="modulus" /> (<c>uint32_mod_uint14</c>).</summary>
    public static ushort Remainder(uint dividend, ushort modulus) => DivideWithRemainder(dividend, modulus, out _);

    /// <summary>
    /// Returns the non-negative remainder of a signed <paramref name="dividend" />, 0 to
    /// <paramref name="modulus" /> - 1 (<c>int32_mod_uint14</c>).
    /// </summary>
    public static ushort Remainder(int dividend, ushort modulus)
    {
        ushort remainder = Remainder(0x80000000u + (uint)dividend, modulus);
        ushort offset = Remainder(0x80000000u, modulus);
        remainder = (ushort)(remainder - offset);
        uint mask = 0u - (uint)(remainder >> 15);
        return (ushort)(remainder + (mask & modulus));
    }
}
