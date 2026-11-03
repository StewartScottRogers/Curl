namespace Curl.Conformance.Extended.Kafka.Compression.Entropy.Fse;

/// <summary>
/// Writes a zstd FSE table description (RFC 8878 section 4.1.1): the accuracy log less 5 in four bits, then each
/// symbol's count plus one in a field one bit narrower when the value is small enough, a run of zero counts after a
/// zero in two-bit steps (3 meaning "three more, and another step"), stopping once the counts written add up. The
/// bits run from the lowest bit of the first byte up, padded to a whole byte.
/// </summary>
public static class KafkaReferenceFseTableDescriptionWriter
{
    private const int AccuracyLogFieldBits = 4;

    private const int ZeroRunFieldBits = 2;

    private const int ZeroRunContinues = 3;

    /// <summary>Writes the description of <paramref name="counts"/>.</summary>
    /// <param name="counts">The normalised counts, one per symbol.</param>
    /// <param name="accuracyLog">Their accuracy log.</param>
    /// <returns>The description's bytes.</returns>
    public static byte[] Write(IReadOnlyList<short> counts, int accuracyLog)
    {
        KafkaReferenceFseNormalizedCounts.CheckValid(counts, accuracyLog);
        List<bool> bits = [];
        Append(bits, accuracyLog - KafkaReferenceFseNormalizedCounts.MinimumAccuracyLog, AccuracyLogFieldBits);
        int threshold = 1 << accuracyLog;
        int remaining = threshold + 1;
        int width = accuracyLog + 1;
        int symbol = 0;
        while (remaining > 1)
        {
            int count = counts[symbol++];
            AppendCount(bits, count, width, threshold, remaining);
            remaining -= Math.Abs(count);
            symbol = count == 0 ? AppendZeroRun(bits, counts, symbol) : symbol;
            while (remaining < threshold)
            {
                width--;
                threshold >>= 1;
            }
        }

        byte[] description = new byte[(bits.Count + 7) / 8];
        for (int index = 0; index < bits.Count; index++)
        {
            description[index / 8] |= (byte)(bits[index] ? 1 << (index % 8) : 0);
        }

        return description;
    }

    // A value below the maximum fits one bit narrower; one at or above the threshold is moved up past the maximum.
    private static void AppendCount(List<bool> bits, int count, int width, int threshold, int remaining)
    {
        int maximum = (2 * threshold) - 1 - remaining;
        int value = count + 1;
        value += value >= threshold ? maximum : 0;
        Append(bits, value, value < maximum ? width - 1 : width);
    }

    // Writes how many zero counts follow the one just written; gives the symbol after them.
    private static int AppendZeroRun(List<bool> bits, IReadOnlyList<short> counts, int symbol)
    {
        int start = symbol;
        while (counts[symbol] == 0)
        {
            symbol++;
        }

        int run = symbol - start;
        for (; run >= ZeroRunContinues; run -= ZeroRunContinues)
        {
            Append(bits, ZeroRunContinues, ZeroRunFieldBits);
        }

        Append(bits, run, ZeroRunFieldBits);
        return symbol;
    }

    private static void Append(List<bool> bits, int value, int width)
    {
        for (int bit = 0; bit < width; bit++)
        {
            bits.Add(((value >> bit) & 1) != 0);
        }
    }
}
