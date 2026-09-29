using System.Buffers.Binary;
using System.Numerics;

namespace Curl.Zstandard;

/// <summary>
/// The XXH64 non-cryptographic hash (xxHash specification, <c>doc/xxhash_spec.md</c>),
/// which RFC 8878 uses with seed 0 for a frame's <c>Content_Checksum</c>: the checksum is
/// the low 32 bits of the XXH64 of the frame's decoded content.
/// </summary>
/// <remarks>
/// Public so its own published test vectors pin it (ADR-0185), not because another
/// library needs it. <see cref="XxHash64Accumulator" /> is the same hash over input that
/// arrives in pieces.
/// </remarks>
public static class XxHash64
{
    /// <summary>The length in bytes of one stripe: four 8-byte lanes.</summary>
    internal const int StripeLength = 32;

    internal const ulong Prime1 = 0x9E3779B185EBCA87;

    internal const ulong Prime2 = 0xC2B2AE3D27D4EB4F;

    internal const ulong Prime3 = 0x165667B19E3779F9;

    internal const ulong Prime4 = 0x85EBCA77C2B2AE63;

    internal const ulong Prime5 = 0x27D4EB2F165667C5;

    /// <summary>Returns the XXH64 of <paramref name="data" /> under <paramref name="seed" />.</summary>
    public static ulong Hash(ReadOnlySpan<byte> data, ulong seed = 0)
    {
        var lanes = new XxHash64Lanes(seed);
        var rest = data;
        while (rest.Length >= StripeLength)
        {
            lanes.ConsumeStripe(rest[..StripeLength]);
            rest = rest[StripeLength..];
        }

        return Finish(data.Length >= StripeLength ? lanes.Converge() : seed + Prime5, (ulong)data.Length, rest);
    }

    /// <summary>
    /// Adds the total length to <paramref name="accumulator" />, folds in the fewer than
    /// 32 bytes of <paramref name="tail" />, and avalanches the result.
    /// </summary>
    internal static ulong Finish(ulong accumulator, ulong totalLength, ReadOnlySpan<byte> tail)
    {
        var hash = accumulator + totalLength;
        while (tail.Length >= 8)
        {
            hash ^= Round(0, BinaryPrimitives.ReadUInt64LittleEndian(tail));
            hash = (BitOperations.RotateLeft(hash, 27) * Prime1) + Prime4;
            tail = tail[8..];
        }

        if (tail.Length >= 4)
        {
            hash ^= BinaryPrimitives.ReadUInt32LittleEndian(tail) * Prime1;
            hash = (BitOperations.RotateLeft(hash, 23) * Prime2) + Prime3;
            tail = tail[4..];
        }

        foreach (var value in tail)
        {
            hash ^= value * Prime5;
            hash = BitOperations.RotateLeft(hash, 11) * Prime1;
        }

        return Avalanche(hash);
    }

    /// <summary>Mixes one 8-byte lane into one accumulator.</summary>
    internal static ulong Round(ulong accumulator, ulong lane) =>
        BitOperations.RotateLeft(accumulator + (lane * Prime2), 31) * Prime1;

    private static ulong Avalanche(ulong hash)
    {
        hash ^= hash >> 33;
        hash *= Prime2;
        hash ^= hash >> 29;
        hash *= Prime3;
        hash ^= hash >> 32;
        return hash;
    }
}
