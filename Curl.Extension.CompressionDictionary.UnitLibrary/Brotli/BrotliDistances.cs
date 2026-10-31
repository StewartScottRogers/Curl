namespace Curl.Extension.CompressionDictionary.Brotli;

/// <summary>Turns distance codes into distances through the ring of the last four distances (RFC 7932 section 4).</summary>
internal sealed class BrotliDistances
{
    private static readonly int[] RingIndexBack = [0, 1, 2, 3, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1];

    private static readonly int[] RingDelta = [0, 0, 0, 0, -1, 1, -2, 2, -3, 3, -1, 1, -2, 2, -3, 3];

    private readonly int[] _ring = [16, 15, 11, 4];
    private int _next = 4;

    /// <summary>Resolves a distance code under NPOSTFIX and NDIRECT, reading its extra bits.</summary>
    public int Resolve(int code, int postfixBits, int directCount, BrotliBitReader reader)
    {
        if (code < 16)
        {
            return FromRing(code);
        }

        if (code < 16 + directCount)
        {
            return code - 15;
        }

        var adjusted = code - directCount - 16;
        var extraBitCount = 1 + (adjusted >> (postfixBits + 1));
        var offset = ((2 + ((adjusted >> postfixBits) & 1)) << extraBitCount) - 4;
        var low = adjusted & ((1 << postfixBits) - 1);
        return ((offset + reader.ReadBits(extraBitCount)) << postfixBits) + low + directCount + 1;
    }

    /// <summary>Pushes a distance a backward copy used onto the ring, unless code 0 (the last distance) gave it.</summary>
    public void RememberUnlessLast(int code, int distance)
    {
        if (code != 0)
        {
            _ring[_next & 3] = distance;
            _next++;
        }
    }

    private int FromRing(int code)
    {
        var distance = _ring[(_next - 1 - RingIndexBack[code]) & 3] + RingDelta[code];
        if (distance <= 0)
        {
            throw new BrotliFormatException("A Brotli distance code gives a distance below 1 (RFC 7932 section 4).");
        }

        return distance;
    }
}
