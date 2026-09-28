namespace Curl.Cryptography;

/// <summary>
/// Branch-free building blocks shared by the hand-built primitives (ADR-0118, "Constant
/// time and zeroing"): a secret bit becomes a mask, and a mask selects or swaps values, so
/// no branch, loop bound or index depends on the secret. Every member is constant-time in
/// its secret inputs; only the lengths of the spans it is given shape its running time.
/// </summary>
internal static class ConstantTime
{
    /// <summary>
    /// Turns a bit into a mask: <c>1</c> gives all ones, <c>0</c> gives zero. Only the
    /// lowest bit of <paramref name="bit" /> is read.
    /// </summary>
    public static uint MaskFromBit(uint bit) => 0u - (bit & 1u);

    /// <summary>
    /// Returns <paramref name="whenSet" /> where <paramref name="mask" /> is all ones and
    /// <paramref name="whenClear" /> where it is zero, without a branch.
    /// </summary>
    public static uint Select(uint mask, uint whenSet, uint whenClear) =>
        (whenSet & mask) | (whenClear & ~mask);

    /// <summary>
    /// Swaps the contents of <paramref name="left" /> and <paramref name="right" /> when
    /// the lowest bit of <paramref name="bit" /> is <c>1</c> and leaves both unchanged when
    /// it is <c>0</c>, touching every element either way (the Montgomery ladder's
    /// <c>cswap</c>, RFC 7748 section 5).
    /// </summary>
    /// <exception cref="ArgumentException">The spans differ in length.</exception>
    public static void ConditionalSwap(Span<uint> left, Span<uint> right, uint bit)
    {
        if (left.Length != right.Length)
        {
            throw new ArgumentException("Both spans must have the same length.", nameof(right));
        }

        uint mask = MaskFromBit(bit);
        for (int index = 0; index < left.Length; index++)
        {
            uint difference = mask & (left[index] ^ right[index]);
            left[index] ^= difference;
            right[index] ^= difference;
        }
    }

    /// <summary>
    /// Returns whether every byte of <paramref name="bytes" /> is zero, reading all of them
    /// whatever they hold (the all-zero shared-secret check of RFC 7748 section 6.1).
    /// </summary>
    public static bool IsAllZero(ReadOnlySpan<byte> bytes)
    {
        uint accumulator = 0;
        foreach (byte value in bytes)
        {
            accumulator |= value;
        }

        return accumulator == 0;
    }
}
