using System.Buffers.Binary;

namespace Curl.Conformance.Extended.Kafka.Compression;

/// <summary>
/// Finds, for the Snappy and LZ4 encoders, the latest earlier occurrence of the four bytes at a position and how far it
/// matches: every position looked at is remembered, so a match is the nearest one seen, at least four bytes long.
/// </summary>
/// <param name="data">The bytes being encoded.</param>
public sealed class KafkaReferenceMatchFinder(byte[] data)
{
    /// <summary>The fewest bytes a match covers.</summary>
    public const int MinimumLength = 4;

    private readonly Dictionary<int, int> latest = [];

    /// <summary>
    /// The match at <paramref name="position"/>, remembering that position: its offset back and its length, or 0 and 0
    /// when there is no earlier occurrence, it is before <paramref name="windowStart"/>, or it is more than
    /// <paramref name="maximumOffset"/> back.
    /// </summary>
    /// <param name="position">Where the match starts; at least four bytes must follow it.</param>
    /// <param name="windowStart">The earliest position a match may copy from.</param>
    /// <param name="maximumOffset">The largest offset the format writes.</param>
    /// <param name="maximumLength">The longest match the format writes in one element.</param>
    /// <param name="end">The position a match must not reach past.</param>
    /// <returns>The offset and length.</returns>
    public (int Offset, int Length) Find(int position, int windowStart, int maximumOffset, int maximumLength, int end)
    {
        int key = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(position));
        bool seen = latest.TryGetValue(key, out int earlier);
        latest[key] = position;
        if (!seen || earlier < windowStart || position - earlier > maximumOffset)
        {
            return (0, 0);
        }

        return (position - earlier, MatchLength(earlier, position, Math.Min(maximumLength, end - position)));
    }

    private int MatchLength(int earlier, int position, int longest)
    {
        int length = MinimumLength;
        while (length < longest && data[earlier + length] == data[position + length])
        {
            length++;
        }

        return length;
    }
}
