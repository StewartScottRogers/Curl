namespace Curl.Cryptography;

/// <summary>
/// Sorts unsigned 32-bit integers with a fixed network of compare-and-swap steps
/// (djbsort's <c>crypto_sort_uint32</c>, as NTRU Prime's reference ships it): which pairs
/// are compared depends only on the length, and each exchange is a mask, so sorting a
/// secret list is constant-time in its contents.
/// </summary>
internal static class SortingNetwork
{
    /// <summary>Sorts <paramref name="values" /> into ascending order in place.</summary>
    public static void Sort(Span<uint> values)
    {
        int length = values.Length;
        int top = 1;
        while (top < length - top)
        {
            top += top;
        }

        for (int stride = top; stride > 0; stride >>= 1)
        {
            for (int index = 0; index < length - stride; index++)
            {
                if ((index & stride) == 0)
                {
                    MinMax(ref values[index], ref values[index + stride]);
                }
            }

            MergeStride(values, top, stride);
        }
    }

    /// <summary>The inner merge passes of one stride: strides top down to above <paramref name="stride" />.</summary>
    private static void MergeStride(Span<uint> values, int top, int stride)
    {
        for (int outer = top; outer > stride; outer >>= 1)
        {
            for (int index = 0; index < values.Length - outer; index++)
            {
                if ((index & stride) == 0)
                {
                    MinMax(ref values[index + stride], ref values[index + outer]);
                }
            }
        }
    }

    /// <summary>Puts the smaller of the two values in <paramref name="low" />, by mask.</summary>
    private static void MinMax(ref uint low, ref uint high)
    {
        uint x = low;
        uint y = high;
        uint difference = x ^ y;
        uint c = y - x;
        c ^= difference & (c ^ y ^ 0x80000000u);
        c >>= 31;
        c = 0u - c;
        c &= difference;
        low = x ^ c;
        high = y ^ c;
    }
}
