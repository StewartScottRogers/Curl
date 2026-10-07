using System.Runtime.CompilerServices;

namespace Curl.Cryptography;

/// <summary>
/// A signed 128-bit sum of limb products, held as two 64-bit halves in two's complement so
/// every operation is a few 64-bit instructions the JIT inlines. <see cref="Int128" />'s
/// operators become real calls once a field multiplication grows past the JIT's inline
/// budget, which cost <see cref="Field25519" /> and <see cref="Field448" /> most of their
/// time (BL-1525). Every member is constant-time: carries are computed by bit arithmetic,
/// never by a comparison or a branch.
/// </summary>
internal struct Accumulator128
{
    private ulong low;

    private ulong high;

    /// <summary>The low 64 bits of the sum.</summary>
    public readonly ulong Low => low;

    /// <summary>Adds the whole signed 128-bit product of <paramref name="left" /> and <paramref name="right" />.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void MultiplyAdd(long left, long right)
    {
        ulong productHigh = Math.BigMul((ulong)left, (ulong)right, out ulong productLow);
        productHigh -= (ulong)((left >> 63) & right) + (ulong)((right >> 63) & left);
        AddHalves(productLow, productHigh);
    }

    /// <summary>Adds the signed 64-bit <paramref name="value" />.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(long value) => AddHalves((ulong)value, (ulong)(value >> 63));

    /// <summary>Adds another sum.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(Accumulator128 other) => AddHalves(other.low, other.high);

    /// <summary>
    /// Returns the sum shifted right arithmetically by <paramref name="bits" /> (1 to 63), as a
    /// 64-bit value; the caller keeps the sum below 2^(63 + bits) in magnitude.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly long ShiftRight(int bits) => (long)((low >> bits) | (high << (64 - bits)));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AddHalves(ulong addendLow, ulong addendHigh)
    {
        ulong sum = low + addendLow;
        ulong carry = ((low & addendLow) | ((low | addendLow) & ~sum)) >> 63;
        low = sum;
        high += addendHigh + carry;
    }
}
