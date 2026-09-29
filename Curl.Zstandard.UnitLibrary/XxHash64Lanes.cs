using System.Buffers.Binary;
using System.Numerics;

namespace Curl.Zstandard;

/// <summary>
/// XXH64's four striped accumulators: each 32-byte stripe feeds one 8-byte lane to each,
/// and <see cref="Converge" /> merges them once the input has at least one stripe.
/// </summary>
internal struct XxHash64Lanes
{
    private ulong first;

    private ulong second;

    private ulong third;

    private ulong fourth;

    /// <summary>Starts the four accumulators from <paramref name="seed" />.</summary>
    public XxHash64Lanes(ulong seed)
    {
        first = seed + XxHash64.Prime1 + XxHash64.Prime2;
        second = seed + XxHash64.Prime2;
        third = seed;
        fourth = seed - XxHash64.Prime1;
    }

    /// <summary>Consumes one 32-byte <paramref name="stripe" />.</summary>
    public void ConsumeStripe(ReadOnlySpan<byte> stripe)
    {
        first = XxHash64.Round(first, BinaryPrimitives.ReadUInt64LittleEndian(stripe));
        second = XxHash64.Round(second, BinaryPrimitives.ReadUInt64LittleEndian(stripe[8..]));
        third = XxHash64.Round(third, BinaryPrimitives.ReadUInt64LittleEndian(stripe[16..]));
        fourth = XxHash64.Round(fourth, BinaryPrimitives.ReadUInt64LittleEndian(stripe[24..]));
    }

    /// <summary>Merges the four accumulators into the one the tail is folded into.</summary>
    public readonly ulong Converge()
    {
        var hash = BitOperations.RotateLeft(first, 1) + BitOperations.RotateLeft(second, 7)
            + BitOperations.RotateLeft(third, 12) + BitOperations.RotateLeft(fourth, 18);
        hash = MergeAccumulator(hash, first);
        hash = MergeAccumulator(hash, second);
        hash = MergeAccumulator(hash, third);
        return MergeAccumulator(hash, fourth);
    }

    private static ulong MergeAccumulator(ulong hash, ulong accumulator) =>
        ((hash ^ XxHash64.Round(0, accumulator)) * XxHash64.Prime1) + XxHash64.Prime4;
}
