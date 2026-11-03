namespace Curl.Conformance.Extended.Kafka.Compression.Entropy.Fse;

/// <summary>
/// zstd FSE normalised counts (RFC 8878 section 4.1.1): one count per symbol adding up to two to the power of the
/// accuracy log, -1 standing for "less than one" and counting as one. Makes them from symbol frequencies and checks them.
/// </summary>
public static class KafkaReferenceFseNormalizedCounts
{
    /// <summary>The smallest accuracy log a table description can carry.</summary>
    public const int MinimumAccuracyLog = 5;

    /// <summary>The largest accuracy log zstd allows any FSE table (literals and match lengths, section 4.1.1).</summary>
    public const int MaximumAccuracyLog = 9;

    /// <summary>
    /// Gives each symbol with a non-zero frequency a count of at least one, in proportion to its frequency, then adds or
    /// takes the difference from the largest counts so they add up to two to the power of <paramref name="accuracyLog"/>.
    /// </summary>
    /// <param name="frequencies">How often each symbol occurs, indexed by symbol.</param>
    /// <param name="accuracyLog">The accuracy log, <see cref="MinimumAccuracyLog"/> to <see cref="MaximumAccuracyLog"/>.</param>
    /// <returns>The normalised counts, one per symbol.</returns>
    public static short[] FromFrequencies(IReadOnlyList<int> frequencies, int accuracyLog)
    {
        ArgumentNullException.ThrowIfNull(frequencies);
        CheckAccuracyLog(accuracyLog);
        int size = 1 << accuracyLog;
        long total = Total(frequencies, size);
        short[] counts = new short[frequencies.Count];
        int sum = 0;
        for (int symbol = 0; symbol < counts.Length; symbol++)
        {
            counts[symbol] = Proportional(frequencies[symbol], size, total);
            sum += counts[symbol];
        }

        for (; sum > size; sum--)
        {
            counts[Largest(counts)]--;
        }

        for (; sum < size; sum++)
        {
            counts[Largest(counts)]++;
        }

        return counts;
    }

    /// <summary>Throws unless <paramref name="counts"/> add up to two to the power of <paramref name="accuracyLog"/>, each -1 as one.</summary>
    /// <param name="counts">The normalised counts.</param>
    /// <param name="accuracyLog">The accuracy log.</param>
    public static void CheckValid(IReadOnlyList<short> counts, int accuracyLog)
    {
        ArgumentNullException.ThrowIfNull(counts);
        CheckAccuracyLog(accuracyLog);
        int total = 0;
        foreach (short count in counts)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(count, (short)-1, nameof(counts));
            total += Math.Abs(count);
        }

        if (total != 1 << accuracyLog)
        {
            throw new ArgumentException("FSE counts add up to " + total + ", not " + (1 << accuracyLog), nameof(counts));
        }
    }

    private static void CheckAccuracyLog(int accuracyLog)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(accuracyLog, MinimumAccuracyLog);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(accuracyLog, MaximumAccuracyLog);
    }

    // The frequencies' total; throws unless each is non-negative and 1 to size of them are present.
    private static long Total(IReadOnlyList<int> frequencies, int size)
    {
        long total = 0;
        int present = 0;
        foreach (int frequency in frequencies)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(frequency, nameof(frequencies));
            total += frequency;
            present += frequency > 0 ? 1 : 0;
        }

        return present == 0 || present > size
            ? throw new ArgumentException("FSE needs 1 to " + size + " symbols present, not " + present, nameof(frequencies))
            : total;
    }

    // A present symbol's share of the states, rounded down but at least one; an absent symbol's is zero.
    private static short Proportional(int frequency, int size, long total) =>
        frequency == 0 ? (short)0 : (short)Math.Max(1, frequency * (long)size / total);

    private static int Largest(short[] counts)
    {
        int largest = 0;
        for (int symbol = 1; symbol < counts.Length; symbol++)
        {
            largest = counts[symbol] > counts[largest] ? symbol : largest;
        }

        return largest;
    }
}
